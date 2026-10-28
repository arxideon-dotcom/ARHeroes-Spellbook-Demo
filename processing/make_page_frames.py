#!/usr/bin/env python3
"""ARHeroes-Spellbook-Demo — page frames, surface grid, book shadow and table (Oct 2026).

The page frame of each sheet is drawn FLAT, in the local page coordinates of
Assets/Spellbook/Art/PageSurfaces.json (origin at the sheet centre, y up). In Unity it is one
Image covering the whole sheet and goes through the same PageWarp surface as every other page
element, so the frame follows the paper exactly like the medallions and text do.

Frame: thin double ink rule inset from the sheet edge, Muse corner ornaments, small runes at the
middle of the side rules, a gap with runes for the page number, and on the left page the header
ornament (left scrolls, snowflake, right scrolls) around the school title.

Also writes a flat uniform grid per page (technical preview of the surface), the contact shadow
under the book and the muted wooden table behind it.

Usage:
  python make_page_frames.py --raw muse_raw/ --out ../Assets/Spellbook/UI

Requires: pip install pillow numpy
"""

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter

from make_ui_sprites import INK, ink_only, key, trim

ROOT = Path(__file__).resolve().parent.parent
SPREAD = ROOT / "Assets/Spellbook/Art/Spellbook_Spread_v8.png"
SURFACES = ROOT / "Assets/Spellbook/Art/PageSurfaces.json"

# Page layout shared with SpellbookSceneBuilder (local px)
FRAME_INSET = 45          # outer rule, from the sheet edge
RULE_GAP = 11             # outer -> inner rule
TITLE_Y = 650             # school title centre, left page
TITLE_HALF = 175          # half width kept clear for the title
PAGE_NUMBER_GAP = 60      # half width of the gap in the bottom rule
GRID_STEP = 66


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


class Flat:
    """A flat local-page canvas: local (x, y) with origin at the sheet centre, y up."""

    def __init__(self, width, height):
        self.w, self.h = round(width), round(height)
        self.img = Image.new("RGBA", (self.w, self.h), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.img)

    def px(self, x, y):
        return x + self.w / 2, self.h / 2 - y

    def line(self, pts, width, alpha, color=INK):
        self.d.line([self.px(x, y) for x, y in pts], fill=tuple(color) + (alpha,), width=width)

    def paste(self, art, x, y, w, flip_x=False, flip_y=False, alpha=1.0):
        im = art.resize((max(1, round(w)), max(1, round(w * art.height / art.width))), Image.LANCZOS)
        if flip_x: im = im.transpose(Image.FLIP_LEFT_RIGHT)
        if flip_y: im = im.transpose(Image.FLIP_TOP_BOTTOM)
        if alpha < 1: im.putalpha(im.split()[3].point(lambda v: int(v * alpha)))
        cx, cy = self.px(x, y)
        self.img.alpha_composite(im, (round(cx - im.width / 2), round(cy - im.height / 2)))


def page_frame(page, corner, header, runes, left_page):
    f = Flat(page["width"], page["height"])
    hx, hy = f.w / 2, f.h / 2
    for k, (width, alpha) in enumerate(((4, 210), (2, 150))):
        i = FRAME_INSET + k * RULE_GAP
        x0, x1, y0, y1 = -hx + i, hx - i, -hy + i, hy - i
        top_gap = TITLE_HALF + 30 if left_page else 0
        if top_gap:
            f.line([(x0, y1), (-top_gap, y1)], width, alpha); f.line([(top_gap, y1), (x1, y1)], width, alpha)
        else:
            f.line([(x0, y1), (x1, y1)], width, alpha)
        f.line([(x0, y0), (-PAGE_NUMBER_GAP, y0)], width, alpha); f.line([(PAGE_NUMBER_GAP, y0), (x1, y0)], width, alpha)
        for x in (x0, x1):   # side rules with a gap for the rune
            f.line([(x, y1), (x, 22)], width, alpha); f.line([(x, -22), (x, y0)], width, alpha)
    i = FRAME_INSET + RULE_GAP / 2
    cw = 150
    for sx in (-1, 1):
        for sy in (-1, 1):
            f.paste(corner, sx * (hx - i - cw / 2 + 18), sy * (hy - i - cw / 2 + 18), cw, flip_x=sx > 0, flip_y=sy < 0, alpha=0.95)
    f.paste(runes[0 if left_page else 3], -hx + i, 0, 26, alpha=0.9)
    f.paste(runes[1 if left_page else 4], hx - i, 0, 26, alpha=0.9)
    for k, sx in enumerate((-1, 1)):
        f.paste(runes[(2 + 3 * k) % len(runes)], sx * (PAGE_NUMBER_GAP + 34), -hy + i, 22, alpha=0.85)
    if left_page:
        left_c, centre, right_c = split_header(header)
        sw = 132
        f.paste(left_c, -TITLE_HALF - sw / 2 + 10, TITLE_Y - 8, sw, alpha=0.9)
        f.paste(right_c, TITLE_HALF + sw / 2 - 10, TITLE_Y - 8, sw, alpha=0.9)
        f.paste(centre, 0, hy - i - 2, 70, alpha=0.9)
    return f.img


def grid(page):
    f = Flat(page["width"], page["height"])
    hx, hy = f.w / 2, f.h / 2
    cyan = (0, 210, 255)
    for k in range(0, int(hx) + 1, GRID_STEP):
        for x in {k, -k}:
            f.line([(x, -hy), (x, hy)], 4 if k == 0 else 2, 230, cyan)
    for k in range(0, int(hy) + 1, GRID_STEP):
        for y in {k, -k}:
            f.line([(-hx, y), (hx, y)], 4 if k == 0 else 2, 230, cyan)
    f.d.rectangle((1, 1, f.w - 2, f.h - 2), outline=cyan + (255,), width=5)
    return f.img


def main():
    ap = argparse.ArgumentParser(description="Build page frames, surface grids, book shadow and table background.")
    ap.add_argument("--raw", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()
    raw, out = Path(args.raw), Path(args.out)
    pages = {p["name"]: p for p in json.loads(SURFACES.read_text(encoding="utf-8"))["pages"]}

    corner = trim(ink_only(key(raw / "UI_FrameCorner.png"), INK, 3, 1.5))
    header = trim(ink_only(key(raw / "UI_HeaderOrnament.png"), INK, 3, 1.5))
    runes = split_glyphs(trim(ink_only(key(raw / "UI_RuneStrip.png"), INK, 3, 1.6)))
    for name, page in pages.items():
        page_frame(page, corner, header, runes, name == "Left").save(out / f"UI_PageFrame_{name}.png", optimize=True)
        grid(page).save(out / f"UI_SurfaceGrid_{name}.png", optimize=True)

    # Contact shadow: the book's silhouette, blurred and a little lower, on a padded canvas
    spread = Image.open(SPREAD).convert("RGBA")
    W, H = spread.size
    pad = 80
    sh = Image.new("L", (W + 2 * pad, H + 2 * pad), 0)
    sh.paste(spread.split()[3], (pad, pad + 14))
    sh = sh.filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.GaussianBlur(26))
    shadow = Image.new("RGBA", sh.size, (8, 5, 3, 0)); shadow.putalpha(sh.point(lambda v: int(v * 0.85)))
    shadow.save(out / "UI_BookShadow.png", optimize=True)

    # Table: muted, darker, gentle warm light in the middle, vignette to the frame edges
    t = ImageEnhance.Color(Image.open(raw / "UI_Table.png").convert("RGB")).enhance(0.75)
    arr = np.asarray(t).astype(np.float32) / 255
    h, w = arr.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    r = np.hypot((xx - w * 0.5) / (w * 0.55), (yy - h * 0.48) / (h * 0.62))
    arr = np.minimum(arr * 0.55, 0.42)
    warm = np.clip(1 - r, 0, 1)[..., None] ** 1.5 * np.array([0.10, 0.06, 0.02])
    vignette = np.clip(1.0 - 0.75 * np.clip(r - 0.35, 0, 1) ** 1.2, 0.18, 1)[..., None]
    Image.fromarray((np.clip((arr + warm) * vignette, 0, 1) * 255).astype(np.uint8)).save(out / "UI_Table.png", optimize=True)
    print(f"Wrote page frames, surface grids, shadow and table to {out}")


if __name__ == "__main__":
    main()
