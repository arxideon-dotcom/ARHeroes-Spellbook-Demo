#!/usr/bin/env python3
"""ARHeroes-Spellbook-Demo — page frames, book shadow and table for the spread (Oct 2026).

Draws one overlay the size of the v8 spread (2x resolution) with a thin double ink frame inside
each page. The frame follows the measured shape of the top sheet: curved top and bottom edges,
outer edges slanted by the page perspective. Muse ornaments are placed on it: expressive corners,
small runes on the side edges, the header ornament that holds the school title, and gaps with
small diamonds for the page numbers. Also builds the contact shadow under the book and the
muted wooden table behind it.

Usage:
  python make_page_frames.py --raw muse_raw/ --out ../Assets/Spellbook/UI

Requires: pip install pillow numpy
"""

import argparse
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter

from make_ui_sprites import INK, ink_only, key, trim

ROOT = Path(__file__).resolve().parent.parent
SPREAD = ROOT / "Assets/Spellbook/Art/Spellbook_Spread_v8.png"
SCALE = 2                     # overlay pixels per spread pixel
INSET = 28                    # frame inset from the sheet edge, spread px

# Top sheet edges measured on the v8 spread (spread px): x -> y along the top and bottom edges,
# and the outer edges as two points each (top, bottom). The spine side stays clear of the rings.
PAGES = {
    "L": {"top": [(265, 62), (400, 55), (550, 25), (640, 15), (750, 26), (790, 60)],
          "bottom": [(130, 825), (300, 808), (500, 800), (650, 810), (790, 850)],
          "outer": [(265, 62), (128, 828)], "spine_x": 768, "outer_sign": -1},
    "R": {"top": [(892, 60), (980, 15), (1100, 22), (1220, 46), (1340, 67), (1405, 62)],
          "bottom": [(880, 850), (1000, 810), (1150, 800), (1350, 808), (1552, 832)],
          "outer": [(1405, 62), (1552, 832)], "spine_x": 908, "outer_sign": 1},
}


def smooth(points):
    xs, ys = zip(*points)
    return np.poly1d(np.polyfit(xs, ys, 3))


def line_x(p0, p1, y):
    (x0, y0), (x1, y1) = p0, p1
    return x0 + (x1 - x0) * (y - y0) / (y1 - y0)


def frame_path(pg, inset):
    """Closed polygon (spread px) of the frame `inset` px inside the sheet."""
    top, bot = smooth(pg["top"]), smooth(pg["bottom"])
    s = pg["outer_sign"]
    outer = [(x - s * inset, y) for x, y in pg["outer"]]
    spine = pg["spine_x"] - s * (inset - INSET)
    # outer line at a given y, moved inward; spine line vertical
    def ox(y): return line_x(outer[0], outer[1], y)
    # solve corners: top/bottom curve offset by inset meets outer / spine
    def meet_top(xf, x):
        for _ in range(8): x = xf(top(x) + inset)
        return x, top(x) + inset
    def meet_bot(xf, x):
        for _ in range(8): x = xf(bot(x) - inset)
        return x, bot(x) - inset
    # start from the measured outer corners: the cubic fits are only valid along the page
    tl_o, bl_o = meet_top(ox, outer[0][0]), meet_bot(ox, outer[1][0])
    tl_s, bl_s = (spine, top(spine) + inset), (spine, bot(spine) - inset)
    xa, xb = sorted((tl_o[0], tl_s[0]))
    top_pts = [(x, top(x) + inset) for x in np.linspace(xa, xb, 80)]
    xa, xb = sorted((bl_o[0], bl_s[0]))
    bot_pts = [(x, bot(x) - inset) for x in np.linspace(xa, xb, 80)]
    corners = {"top_outer": tl_o, "bottom_outer": bl_o, "top_spine": tl_s, "bottom_spine": bl_s}
    return top_pts, bot_pts, corners


def draw_poly(d, pts, width, alpha, gap=None):
    """Polyline in overlay px; `gap` = (x0, x1) spread px left open (page number, header)."""
    seg = []
    for x, y in pts:
        if gap and gap[0] < x < gap[1]:
            if len(seg) > 1: d.line(seg, fill=INK + (alpha,), width=width, joint="curve")
            seg = []
            continue
        seg.append((x * SCALE, y * SCALE))
    if len(seg) > 1: d.line(seg, fill=INK + (alpha,), width=width, joint="curve")


def paste(canvas, img, cx, cy, w, angle=0.0, flip_x=False, flip_y=False, alpha=1.0):
    im = img.resize((max(1, round(w * SCALE)), max(1, round(w * SCALE * img.height / img.width))), Image.LANCZOS)
    if flip_x: im = im.transpose(Image.FLIP_LEFT_RIGHT)
    if flip_y: im = im.transpose(Image.FLIP_TOP_BOTTOM)
    if angle: im = im.rotate(angle, expand=True, resample=Image.BICUBIC)
    if alpha < 1: im.putalpha(im.split()[3].point(lambda v: int(v * alpha)))
    canvas.alpha_composite(im, (round(cx * SCALE - im.width / 2), round(cy * SCALE - im.height / 2)))


def split_glyphs(strip):
    """Rune strip -> separate glyph images (columns with ink)."""
    a = np.array(strip.split()[3]) > 40
    cols = a.any(axis=0)
    glyphs, x = [], 0
    while x < len(cols):
        if cols[x]:
            x0 = x
            while x < len(cols) and cols[x]: x += 1
            if x - x0 > 8: glyphs.append(trim(strip.crop((x0, 0, x, strip.height)), 2))
        x += 1
    return glyphs


def split_header(header):
    """Header ornament -> (left scrolls, centre glyph, right scrolls), split at the empty columns."""
    a = np.array(header.split()[3]) > 40
    # a thin horizontal rule runs through every column: count a column only where there is more than the rule
    cols = a.sum(axis=0) > max(6, header.height * 0.04)
    runs, x = [], 0
    while x < len(cols):
        if cols[x]:
            x0 = x
            while x < len(cols) and cols[x]: x += 1
            runs.append((x0, x))
        x += 1
    runs.sort(key=lambda r: r[1] - r[0], reverse=True)
    big = sorted(runs[:2])
    rest = [r for r in runs[2:] if big[0][1] < r[0] < big[1][0]]
    left = trim(header.crop((big[0][0], 0, big[0][1], header.height)), 2)
    right = trim(header.crop((big[1][0], 0, big[1][1], header.height)), 2)
    if rest:
        c0 = min(r[0] for r in rest); c1 = max(r[1] for r in rest)
        centre = trim(header.crop((c0, 0, c1, header.height)), 2)
    else:
        centre = Image.new("RGBA", (4, 4), (0, 0, 0, 0))
    return left, centre, right


def main():
    ap = argparse.ArgumentParser(description="Build page frames, book shadow and table background.")
    ap.add_argument("--raw", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()
    raw, out = Path(args.raw), Path(args.out)
    spread = Image.open(SPREAD).convert("RGBA")
    W, H = spread.size

    corner = trim(ink_only(key(raw / "UI_FrameCorner.png"), INK, 3, 1.5))
    header = trim(ink_only(key(raw / "UI_HeaderOrnament.png"), INK, 3, 1.5))
    runes = split_glyphs(trim(ink_only(key(raw / "UI_RuneStrip.png"), INK, 3, 1.6)))
    print(f"  {len(runes)} rune glyphs")

    frames = Image.new("RGBA", (W * SCALE, H * SCALE), (0, 0, 0, 0))
    d = ImageDraw.Draw(frames)
    for name, pg in PAGES.items():
        s = pg["outer_sign"]
        mid_x = (pg["spine_x"] + pg["outer"][0][0]) / 2
        for inset, width, alpha in ((INSET, 3, 200), (INSET + 7, 2, 140)):
            top, bot, c = frame_path(pg, inset)
            header_gap = (mid_x - 150, mid_x + 150) if name == "L" else None
            draw_poly(d, top, width, alpha, header_gap)
            draw_poly(d, bot, width, alpha, (mid_x - 34, mid_x + 34))
            draw_poly(d, [c["top_outer"], c["bottom_outer"]] if True else [], width, alpha)
            # spine side: straight, with a gap at the middle for a rune
            ys = np.linspace(c["top_spine"][1], c["bottom_spine"][1], 60)
            draw_poly(d, [(c["top_spine"][0], y) for y in ys if abs(y - (c["top_spine"][1] + c["bottom_spine"][1]) / 2) > 22], width, alpha)
        top, bot, c = frame_path(pg, INSET + 3)
        # expressive corners: the corner art is drawn for the top-left corner
        for key_, (fx, fy) in {"top_outer": (s > 0, False), "bottom_outer": (s > 0, True),
                               "top_spine": (s < 0, False), "bottom_spine": (s < 0, True)}.items():
            x, y = c[key_]
            ang = math.degrees(math.atan2(*(np.subtract(pg["outer"][1], pg["outer"][0])[::-1]))) - 90
            tilt = (ang if "outer" in key_ else 0) * (1 if s < 0 else 1)
            ox = 40 if not fx else -40
            oy = 40 if not fy else -40
            paste(frames, corner, x + ox, y + oy, 92, angle=-tilt if "outer" in key_ else 0, flip_x=fx, flip_y=fy, alpha=0.95)
        # runes on the side edges and next to the page-number gap
        ys = [c["top_spine"][1], c["bottom_spine"][1]]
        sy = (ys[0] + ys[1]) / 2
        paste(frames, runes[0 if s < 0 else 3], c["top_spine"][0], sy, 16, alpha=0.9)
        ox0 = line_x(c["top_outer"], c["bottom_outer"], sy)
        paste(frames, runes[1 if s < 0 else 4], ox0, sy, 16, alpha=0.9)
        bot_y = smooth(pg["bottom"])(mid_x) - INSET - 3
        for k, dx in enumerate((-48, 48)):
            paste(frames, runes[(2 + k * 3) % len(runes)], mid_x + dx, bot_y, 12, alpha=0.85)
        if name == "L":
            ty = smooth(pg["top"])(mid_x) + INSET
            # The header ornament is split into its left scrolls, centre snowflake and right scrolls,
            # placed around the school title (the title is ~140 spread px wide at its Unity size)
            left_c, centre, right_c = split_header(header)
            title_half, cw = 72, 66
            paste(frames, left_c, mid_x - title_half - cw / 2 - 4, ty + 56, cw, alpha=0.9)
            paste(frames, right_c, mid_x + title_half + cw / 2 + 4, ty + 56, cw, alpha=0.9)
            paste(frames, centre, mid_x, ty + 22, 40, alpha=0.9)
    frames.save(out / "UI_PageFrames.png", optimize=True)

    # Contact shadow: the book's silhouette, blurred and a little lower, on a padded canvas
    pad = 80
    a = spread.split()[3]
    sh = Image.new("L", (W + 2 * pad, H + 2 * pad), 0)
    sh.paste(a, (pad, pad + 14))
    sh = sh.filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.GaussianBlur(26))
    shadow = Image.new("RGBA", sh.size, (8, 5, 3, 0)); shadow.putalpha(sh.point(lambda v: int(v * 0.85)))
    shadow.save(out / "UI_BookShadow.png", optimize=True)

    # Table: muted, darker, gentle warm light in the middle, vignette to the frame edges
    t = Image.open(raw / "UI_Table.png").convert("RGB")
    t = ImageEnhance.Color(t).enhance(0.75)
    arr = np.asarray(t).astype(np.float32) / 255
    h, w = arr.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    r = np.hypot((xx - w * 0.5) / (w * 0.55), (yy - h * 0.48) / (h * 0.62))
    lum = arr.mean(2, keepdims=True)
    arr = arr * 0.55 + lum * 0.0                                   # overall darker, light pool kept soft
    arr = np.minimum(arr, 0.42)                                    # tame the hot spot
    warm = np.clip(1 - r, 0, 1)[..., None] ** 1.5 * np.array([0.10, 0.06, 0.02])
    vignette = np.clip(1.0 - 0.75 * np.clip(r - 0.35, 0, 1) ** 1.2, 0.18, 1)[..., None]
    arr = np.clip((arr + warm) * vignette, 0, 1)
    Image.fromarray((arr * 255).astype(np.uint8)).save(out / "UI_Table.png", optimize=True)
    print("Wrote UI_PageFrames.png, UI_BookShadow.png, UI_Table.png")


if __name__ == "__main__":
    main()
