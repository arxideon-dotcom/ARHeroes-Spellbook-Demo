#!/usr/bin/env python3
"""ARHeroes-Spellbook-Demo — procedural UI sprites for the spell pages (Oct 2026).

Draws every page decoration from code: ornamental round frames for the
mini-windows, the selection glow, a neutral rune fill for empty slots,
title banners, school tabs, stat icons, corner flourishes and dividers.
No external art is used. Everything is drawn 4x and downsampled.

Usage:
  python make_ui_sprites.py --out ../Assets/Spellbook/UI

Requires: pip install pillow
"""

import argparse
import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

SS = 4  # supersampling factor

INK = (58, 34, 18)
BRONZE_D = (88, 58, 28)
BRONZE = (150, 104, 48)
GOLD = (222, 178, 92)
GOLD_L = (255, 230, 160)
PAPER = (236, 214, 172)
PAPER_D = (196, 162, 112)


def canvas(w, h):
    img = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def finish(img, w, h):
    return img.resize((w, h), Image.LANCZOS)


def ring(d, cx, cy, r, width, fill):
    d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=fill, width=max(1, int(width)))


def polar(cx, cy, r, deg):
    a = math.radians(deg)
    return cx + r * math.cos(a), cy + r * math.sin(a)


def diamond(d, cx, cy, deg, r0, r1, half, fill, outline):
    tip = polar(cx, cy, r1, deg)
    base = polar(cx, cy, r0, deg)
    mid = polar(cx, cy, (r0 + r1) * 0.5, deg)
    left = polar(mid[0], mid[1], half, deg + 90)
    right = polar(mid[0], mid[1], half, deg - 90)
    pts = [tip, left, base, right]
    d.polygon(pts, fill=fill, outline=outline)
    d.line(pts + [tip], fill=outline, width=SS * 2)


def ornate_frame(size=512):
    """Bronze ring with inner rules, tick marks and four compass points. Centre stays clear."""
    img, d = canvas(size, size)
    S = size * SS
    c = S / 2
    # compass points first, the ring covers their bases
    # PIL draws an outline inward from the bbox, so ring(r, w) covers r-w .. r
    for deg in (-90, 0, 90, 180):
        diamond(d, c, c, deg, S * 0.38, S * 0.50, S * 0.060, BRONZE, INK)
        diamond(d, c, c, deg, S * 0.42, S * 0.48, S * 0.024, GOLD, INK)
    for deg in (-45, 45, 135, 225):
        diamond(d, c, c, deg, S * 0.40, S * 0.475, S * 0.030, BRONZE_D, INK)
    # main band: ink edge, dark bronze, bronze body, gold rims
    ring(d, c, c, S * 0.440, S * 0.080, INK)
    ring(d, c, c, S * 0.432, S * 0.064, BRONZE_D)
    ring(d, c, c, S * 0.424, S * 0.046, BRONZE)
    ring(d, c, c, S * 0.432, S * 0.008, GOLD)
    ring(d, c, c, S * 0.380, S * 0.006, GOLD)
    # beads along the band
    for k in range(24):
        if k % 6 == 0:
            continue  # under the compass points
        x, y = polar(c, c, S * 0.401, k * 15 - 90)
        rr = S * 0.009
        d.ellipse((x - rr, y - rr, x + rr, y + rr), fill=GOLD_L, outline=INK, width=SS)
    return finish(img, size, size)


def glow(size=512):
    """Soft golden halo ring for the selected window."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()
    c = size / 2
    for y in range(size):
        for x in range(size):
            r = math.hypot(x - c, y - c) / size
            t = math.exp(-((r - 0.40) / 0.06) ** 2)
            if r < 0.40:
                t = max(t, 0.35 * math.exp(-((r - 0.40) / 0.12) ** 2))
            a = int(255 * t)
            if a > 2:
                px[x, y] = (GOLD_L[0], GOLD_L[1], 150, a)
    return img


def disc(size=512, r_frac=0.365):
    img, d = canvas(size, size)
    S = size * SS
    c, r = S / 2, S * r_frac
    d.ellipse((c - r, c - r, c + r, c + r), fill=(255, 255, 255, 255))
    return finish(img, size, size)


def rune_fill(size=512, seed=7):
    """Neutral runic pattern for an empty slot: sepia wash, rings, glyph band, centre star."""
    rnd = random.Random(seed)
    img, d = canvas(size, size)
    S = size * SS
    c = S / 2
    r = S * 0.365
    d.ellipse((c - r, c - r, c + r, c + r), fill=PAPER_D + (70,))
    ink = INK + (150,)
    faint = INK + (90,)
    for rr, w in ((0.345, 3), (0.325, 2), (0.215, 3), (0.195, 2), (0.08, 2)):
        ring(d, c, c, S * rr, SS * w, ink if w == 3 else faint)
    # glyph band between 0.215 and 0.335: angular strokes, every glyph different
    n = 14
    for k in range(n):
        a = k * 360 / n
        gx, gy = polar(c, c, S * 0.27, a)
        h = S * 0.045
        pts = [(rnd.uniform(-0.5, 0.5), rnd.uniform(-1, 1)) for _ in range(rnd.randint(3, 4))]
        rot = math.radians(a + 90)
        def tr(p):
            x, y = p[0] * h, p[1] * h
            return gx + x * math.cos(rot) - y * math.sin(rot), gy + x * math.sin(rot) + y * math.cos(rot)
        d.line([tr((0, -1)), tr((0, 1))], fill=ink, width=SS * 3)
        d.line([tr(p) for p in pts], fill=ink, width=SS * 3)
    # centre: eight-point star
    for k in range(8):
        a = k * 45
        rr = S * (0.17 if k % 2 == 0 else 0.11)
        d.line([(c, c), polar(c, c, rr, a)], fill=faint, width=SS * 2)
    for k in range(4):
        diamond(d, c, c, k * 90 - 90, S * 0.02, S * 0.15, S * 0.025, INK + (60,), ink)
    return finish(img, size, size)


def banner(w=640, h=128, dim=False):
    """Parchment ribbon with folded tails."""
    img, d = canvas(w, h)
    W, H = w * SS, h * SS
    paper = PAPER if not dim else (206, 184, 146)
    shade = PAPER_D if not dim else (170, 142, 104)
    t = H * 0.28           # tail drop
    bx0, bx1 = W * 0.11, W * 0.89
    top, bot = H * 0.10, H * 0.72
    # tails behind the band
    lt = [(0, top + t), (bx0 + W * 0.04, top + t), (bx0 + W * 0.04, bot + t), (0, bot + t), (W * 0.045, (top + bot) / 2 + t)]
    rt = [(W, top + t), (bx1 - W * 0.04, top + t), (bx1 - W * 0.04, bot + t), (W, bot + t), (W - W * 0.045, (top + bot) / 2 + t)]
    for poly in (lt, rt):
        d.polygon(poly, fill=shade, outline=INK)
        d.line(poly + [poly[0]], fill=INK, width=SS * 3)
    # fold triangles
    d.polygon([(bx0, bot), (bx0 + W * 0.04, bot + t), (bx0 + W * 0.04, bot)], fill=BRONZE_D, outline=INK)
    d.polygon([(bx1, bot), (bx1 - W * 0.04, bot + t), (bx1 - W * 0.04, bot)], fill=BRONZE_D, outline=INK)
    # band with a slight arch
    pts_top = [(bx0 + (bx1 - bx0) * i / 20, top + H * 0.05 * math.sin(math.pi * i / 20) * -1 + H * 0.05) for i in range(21)]
    pts_bot = [(x, y + (bot - top)) for x, y in reversed(pts_top)]
    band = pts_top + pts_bot
    d.polygon(band, fill=paper)
    d.line(band + [band[0]], fill=INK, width=SS * 3)
    inner_t = [(x, y + H * 0.07) for x, y in pts_top[1:-1]]
    inner_b = [(x, y - H * 0.07) for x, y in pts_bot[1:-1]]
    d.line(inner_t, fill=INK + (120,), width=SS * 2)
    d.line(inner_b, fill=INK + (120,), width=SS * 2)
    return finish(img, w, h)


def tab(w=120, h=150, active=False):
    """School tab: parchment card with a rounded top, gold when active."""
    img, d = canvas(w, h)
    W, H = w * SS, h * SS
    m = W * 0.06
    rad = W * 0.18
    fill = (236, 196, 104) if active else (150, 120, 84)
    edge = BRONZE_D if active else (96, 72, 48)
    d.rounded_rectangle((m, m, W - m, H + rad), radius=rad, fill=fill, outline=INK, width=SS * 3)
    d.rounded_rectangle((m * 2.4, m * 2.4, W - m * 2.4, H + rad), radius=rad * 0.7, outline=edge, width=SS * 2)
    if active:
        g = Image.new("RGBA", img.size, (0, 0, 0, 0))
        gd = ImageDraw.Draw(g)
        gd.ellipse((W * 0.1, H * 0.15, W * 0.9, H * 0.85), fill=GOLD_L + (110,))
        g = g.filter(ImageFilter.GaussianBlur(W * 0.12))
        img = Image.alpha_composite(img, g)
    return finish(img, w, h)


def icon_snowflake(size=128, color=INK):
    img, d = canvas(size, size)
    S = size * SS
    c = S / 2
    for k in range(6):
        a = k * 60 - 90
        end = polar(c, c, S * 0.40, a)
        d.line([(c, c), end], fill=color, width=SS * 5)
        for f in (0.22, 0.32):
            p = polar(c, c, S * f, a)
            for s in (-40, 40):
                d.line([p, polar(p[0], p[1], S * 0.09, a + s)], fill=color, width=SS * 4)
    r = S * 0.05
    d.ellipse((c - r, c - r, c + r, c + r), fill=color)
    return finish(img, size, size)


def icon_glyph(size=128, kind=0, color=INK):
    """Neutral geometric glyphs for the dimmed tabs (not tied to any named school)."""
    img, d = canvas(size, size)
    S = size * SS
    c = S / 2
    w = SS * 5
    if kind == 0:     # circle with dot
        ring(d, c, c, S * 0.32, w, color)
        r = S * 0.07
        d.ellipse((c - r, c - r, c + r, c + r), fill=color)
    elif kind == 1:   # triangle with bar
        pts = [polar(c, c * 1.08, S * 0.36, a) for a in (-90, 30, 150)]
        d.line(pts + [pts[0]], fill=color, width=w)
        d.line([(c - S * 0.14, c * 1.12), (c + S * 0.14, c * 1.12)], fill=color, width=w)
    elif kind == 2:   # diamond with cross
        pts = [polar(c, c, S * 0.36, a) for a in (-90, 0, 90, 180)]
        d.line(pts + [pts[0]], fill=color, width=w)
        d.line([pts[0], pts[2]], fill=color, width=SS * 3)
        d.line([pts[1], pts[3]], fill=color, width=SS * 3)
    else:             # three stacked arcs
        for k, rr in enumerate((0.34, 0.24, 0.14)):
            d.arc((c - S * rr, c - S * rr + k * S * 0.05, c + S * rr, c + S * rr + k * S * 0.05), 200, 340, fill=color, width=w)
    return finish(img, size, size)


def icon_drop(size=128):
    img, d = canvas(size, size)
    S = size * SS
    c = S / 2
    pts = []
    for i in range(61):
        t = i / 60 * 2 * math.pi
        x = math.sin(t) * (1 - math.cos(t)) * 0.5
        y = -math.cos(t)
        pts.append((c + x * S * 0.36, c + S * 0.06 + y * S * 0.40 * -1))
    # teardrop: point up
    pts = []
    for i in range(121):
        t = math.pi * i / 120 * 2
        r = S * 0.26
        if i <= 60:
            a = math.pi * (i / 60)
            pts.append((c + r * math.cos(a + math.pi), c + S * 0.12 + r * math.sin(a)))
    top = (c, c - S * 0.40)
    poly = [top] + pts[::-1]
    poly = [top, (c - S * 0.26, c + S * 0.12)] + [(c + S * 0.26 * math.cos(math.pi - math.pi * k / 30), c + S * 0.12 + S * 0.26 * math.sin(math.pi - math.pi * k / 30) * -1 * -1) for k in range(31)] + [(c + S * 0.26, c + S * 0.12)]
    d.polygon(poly, fill=(46, 104, 196), outline=INK)
    d.line(poly + [poly[0]], fill=INK, width=SS * 4)
    d.ellipse((c - S * 0.13, c + S * 0.02, c - S * 0.03, c + S * 0.16), fill=(190, 225, 255))
    return finish(img, size, size)


def icon_compass(size=128):
    img, d = canvas(size, size)
    S = size * SS
    c = S / 2
    ring(d, c, c, S * 0.40, SS * 6, INK)
    ring(d, c, c, S * 0.385, SS * 3, BRONZE)
    for k in range(8):
        a = k * 45 - 90
        big = k % 2 == 0
        diamond(d, c, c, a, 0, S * (0.36 if big else 0.22), S * (0.07 if big else 0.045),
                BRONZE if big else GOLD, INK)
    r = S * 0.05
    d.ellipse((c - r, c - r, c + r, c + r), fill=GOLD, outline=INK, width=SS * 2)
    return finish(img, size, size)


def icon_hourglass(size=128):
    img, d = canvas(size, size)
    S = size * SS
    c = S / 2
    w, top, bot = S * 0.26, S * 0.14, S * 0.86
    d.polygon([(c - w, top), (c + w, top), (c + S * 0.03, c), (c - S * 0.03, c)], fill=(240, 222, 186), outline=INK)
    d.polygon([(c - S * 0.03, c), (c + S * 0.03, c), (c + w, bot), (c - w, bot)], fill=(240, 222, 186), outline=INK)
    d.polygon([(c - w * 0.75, bot - S * 0.02), (c + w * 0.75, bot - S * 0.02), (c, c + S * 0.12)], fill=(200, 150, 70))
    d.polygon([(c - w * 0.45, top + S * 0.16), (c + w * 0.45, top + S * 0.16), (c, c - S * 0.03)], fill=(200, 150, 70))
    for y in (top, bot):
        d.rounded_rectangle((c - S * 0.33, y - S * 0.045, c + S * 0.33, y + S * 0.045), radius=S * 0.02, fill=BRONZE, outline=INK, width=SS * 3)
    for x in (c - S * 0.28, c + S * 0.28):
        d.line([(x, top), (x, bot)], fill=BRONZE_D, width=SS * 5)
    d.line([(c - w, top), (c - S * 0.03, c), (c - w, bot)], fill=INK, width=SS * 3)
    d.line([(c + w, top), (c + S * 0.03, c), (c + w, bot)], fill=INK, width=SS * 3)
    return finish(img, size, size)


def icon_target(size=128):
    img, d = canvas(size, size)
    S = size * SS
    c = S / 2
    ring(d, c, c, S * 0.34, SS * 6, INK)
    ring(d, c, c, S * 0.33, SS * 3, BRONZE)
    ring(d, c, c, S * 0.17, SS * 4, INK)
    for a in (-90, 0, 90, 180):
        d.line([polar(c, c, S * 0.22, a), polar(c, c, S * 0.46, a)], fill=INK, width=SS * 6)
        d.line([polar(c, c, S * 0.23, a), polar(c, c, S * 0.45, a)], fill=BRONZE, width=SS * 2)
    r = S * 0.05
    d.ellipse((c - r, c - r, c + r, c + r), fill=(170, 40, 30), outline=INK, width=SS * 2)
    return finish(img, size, size)


def corner_flourish(size=256):
    """Ink flourish for the top-left page corner; flipped in Unity for the other corners."""
    img, d = canvas(size, size)
    S = size * SS
    ink = INK + (150,)
    w = SS * 3
    m = S * 0.06
    d.line([(m, m), (S * 0.85, m)], fill=ink, width=w)
    d.line([(m, m), (m, S * 0.85)], fill=ink, width=w)
    d.line([(m * 2.2, m * 2.2), (S * 0.55, m * 2.2)], fill=INK + (90,), width=SS * 2)
    d.line([(m * 2.2, m * 2.2), (m * 2.2, S * 0.55)], fill=INK + (90,), width=SS * 2)
    # curls
    for k, (cx, cy, r, a0, a1) in enumerate(((S * 0.30, S * 0.30, S * 0.14, 180, 450),
                                             (S * 0.30, S * 0.30, S * 0.07, 0, 270),
                                             (S * 0.62, S * 0.17, S * 0.08, 90, 330),
                                             (S * 0.17, S * 0.62, S * 0.08, 300, 540))):
        d.arc((cx - r, cy - r, cx + r, cy + r), a0, a1, fill=ink, width=w)
    for x, y in ((S * 0.85, m), (m, S * 0.85)):
        r = S * 0.018
        d.ellipse((x - r, y - r, x + r, y + r), fill=ink)
    diamond(Image.new("RGBA", (1, 1)) and d, m, m, 45, 0, S * 0.07, S * 0.03, INK + (150,), ink)
    return finish(img, size, size)


def divider(w=512, h=32):
    img, d = canvas(w, h)
    W, H = w * SS, h * SS
    c = H / 2
    for i in range(40):
        x0 = W * 0.04 + (W * 0.42) * i / 40
        x1 = W * 0.96 - (W * 0.42) * i / 40
        a = int(40 + 140 * i / 40)
        wd = max(SS, int(SS * (1 + 2 * i / 40)))
        d.line([(x0, c), (x0 + W * 0.011, c)], fill=INK + (a,), width=wd)
        d.line([(x1 - W * 0.011, c), (x1, c)], fill=INK + (a,), width=wd)
    diamond(d, W / 2, c, 0, -H * 0.35, H * 0.35, H * 0.22, GOLD, INK)
    return finish(img, w, h)


def main():
    ap = argparse.ArgumentParser(description="Draw the spell-page UI sprites.")
    ap.add_argument("--out", required=True, help="Output folder (Assets/Spellbook/UI).")
    out = Path(ap.parse_args().out)
    out.mkdir(parents=True, exist_ok=True)
    sprites = {
        "UI_FrameRing": ornate_frame(),
        "UI_FrameGlow": glow(),
        "UI_CircleMask": disc(),
        "UI_RuneFill": rune_fill(),
        "UI_Banner": banner(),
        "UI_BannerDim": banner(dim=True),
        "UI_TabActive": tab(active=True),
        "UI_Tab": tab(),
        "UI_IconSnowflake": icon_snowflake(),
        "UI_IconGlyph0": icon_glyph(kind=0),
        "UI_IconGlyph1": icon_glyph(kind=1),
        "UI_IconGlyph2": icon_glyph(kind=2),
        "UI_IconGlyph3": icon_glyph(kind=3),
        "UI_StatMana": icon_drop(),
        "UI_StatPower": icon_compass(),
        "UI_StatDuration": icon_hourglass(),
        "UI_StatRange": icon_target(),
        "UI_CornerFlourish": corner_flourish(),
        "UI_Divider": divider(),
    }
    for name, img in sprites.items():
        img.save(out / f"{name}.png", optimize=True)
    print(f"Wrote {len(sprites)} sprites to {out}")


if __name__ == "__main__":
    main()
