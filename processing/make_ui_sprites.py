#!/usr/bin/env python3
"""ARHeroes-Spellbook-Demo — UI sprites for the spell pages (Oct 2026).

Page decoration (frames, banners, tabs, glyphs, stat icons, flourishes,
divider) is generated with Muse from processing/ui_prompts.json on a flat
magenta background. This script turns those raw images into the sprites
Unity uses:

  - keys magenta out with process_spell.py and trims to the content
  - centres the round frame on a square canvas and measures its empty hole
  - sets A-Z clockwise around the blank Muse disc in Cormorant Garamond
    (Muse cannot spell a full alphabet), then fits the disc into that hole,
    so both share one canvas and scale
  - ink drawings keep only their line shape: tab glyphs become a white
    alpha mask (tinted in Unity: gold when active, faded ink otherwise),
    marginalia are recoloured to one flat ink colour
  - draws the two trivial shapes itself: the circle mask (sized to the
    frame's hole) and the soft selection glow

Usage:
  python make_ui_sprites.py --raw muse_raw/ --out ../Assets/Spellbook/UI

Requires: pip install pillow numpy
"""

import argparse
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

from process_spell import process_image

FRAME_SIZE = 512
MAX_SIDE = {"UI_Banner": 768, "UI_BannerDim": 768, "UI_Divider": 768}
TAB_GLYPHS = {"UI_InkSnowflake", "UI_InkGlyph0", "UI_InkGlyph1", "UI_InkGlyph2", "UI_InkGlyph3"}
INK_LINES = {"UI_MarginCurl", "UI_MarginCompass"}
SQUARE = TAB_GLYPHS | {"UI_StatMana", "UI_StatPower", "UI_StatDuration", "UI_StatRange", "UI_MarginCompass"}
INK = (58, 34, 18)
LETTER_FONT = Path(__file__).resolve().parent.parent / "Assets/Spellbook/Fonts/CormorantGaramond-Bold.ttf"
ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
BLUE_ART = {"UI_StatMana"}  # genuinely blue: no magenta suppression
GOLD_L = (255, 230, 160)


def suppress_magenta(img: Image.Image) -> Image.Image:
    """Thin ink lines come out pinkish from the magenta key: blue may not exceed green.

    Safe for bronze, gold, parchment and sepia ink (all have B <= G); never use it on blue art.
    """
    a = np.array(img).astype(np.int32)
    a[..., 2] = np.minimum(a[..., 2], a[..., 1])
    return Image.fromarray(a.astype(np.uint8), "RGBA")


def key(src: Path) -> Image.Image:
    img = process_image(src)
    return img if src.stem in BLUE_ART else suppress_magenta(img)


def ink_only(img: Image.Image, rgb, widen: int, gain: float) -> Image.Image:
    """Keep the line shape, drop the colour: flat rgb, alpha optionally widened and strengthened."""
    a = img.split()[3]
    if widen > 1:
        a = a.filter(ImageFilter.MaxFilter(widen)).filter(ImageFilter.GaussianBlur(1))
    a = a.point(lambda v: min(255, int(v * gain)))
    out = Image.new("RGBA", img.size, tuple(rgb) + (0,))
    out.putalpha(a)
    return out


def letter_ring(disc_img: Image.Image) -> Image.Image:
    """A-Z clockwise from the top, letter tops facing outward, in the empty band of the disc."""
    out = disc_img.copy()
    R = out.width / 2
    font = ImageFont.truetype(str(LETTER_FONT), round(R * 0.21))
    ring = R * 0.73
    for i, ch in enumerate(ALPHABET):
        theta = math.radians(-90 + i * 360 / len(ALPHABET))
        tile = Image.new("RGBA", (round(R * 0.4), round(R * 0.4)), (0, 0, 0, 0))
        ImageDraw.Draw(tile).text((tile.width / 2, tile.height / 2), ch, font=font, fill=INK + (235,), anchor="mm")
        tile = tile.rotate(-(math.degrees(theta) + 90), resample=Image.BICUBIC)
        x = R + ring * math.cos(theta) - tile.width / 2
        y = R + ring * math.sin(theta) - tile.height / 2
        out.alpha_composite(tile, (round(x), round(y)))
    return out


def trim(img: Image.Image, margin: int = 6) -> Image.Image:
    a = np.array(img)[..., 3]
    ys, xs = np.where(a > 8)
    if xs.size == 0:
        return img
    box = (max(0, xs.min() - margin), max(0, ys.min() - margin),
           min(img.width, xs.max() + 1 + margin), min(img.height, ys.max() + 1 + margin))
    return img.crop(box)


def pad_square(img: Image.Image) -> Image.Image:
    side = max(img.size)
    out = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    out.paste(img, ((side - img.width) // 2, (side - img.height) // 2))
    return out


def fit(img: Image.Image, max_side: int) -> Image.Image:
    s = max_side / max(img.size)
    return img.resize((max(1, round(img.width * s)), max(1, round(img.height * s))), Image.LANCZOS) if s < 1 else img


def hole_radius(frame: Image.Image) -> float:
    """Radius (px) of the empty centre: median distance to the first opaque pixel along 72 rays."""
    a = np.array(frame)[..., 3]
    c = frame.width / 2
    hits = []
    for k in range(72):
        ang = k * 2 * math.pi / 72
        for r in range(4, frame.width // 2):
            x, y = int(c + r * math.cos(ang)), int(c + r * math.sin(ang))
            if a[y, x] > 128:
                hits.append(r)
                break
    return float(np.median(hits))


def disc(size: int, r: float) -> Image.Image:
    ss = 4
    yy, xx = np.mgrid[0:size * ss, 0:size * ss]
    c = size * ss / 2
    inside = np.hypot(xx - c + 0.5, yy - c + 0.5) <= r * ss
    big = Image.fromarray(np.dstack([np.full(inside.shape + (3,), 255, np.uint8), (inside * 255).astype(np.uint8)]), "RGBA")
    return big.resize((size, size), Image.LANCZOS)


def glow(size: int, r: float) -> Image.Image:
    """Soft golden halo peaking just outside radius r (px)."""
    yy, xx = np.mgrid[0:size, 0:size]
    d = np.hypot(xx - size / 2, yy - size / 2) / size
    rn = r / size
    t = np.exp(-((d - rn) / 0.05) ** 2)
    a = (np.clip(t, 0, 1) * 255).astype(np.uint8)
    rgb = np.zeros((size, size, 3), np.uint8)
    rgb[...] = (GOLD_L[0], GOLD_L[1], 150)
    return Image.fromarray(np.dstack([rgb, a]), "RGBA")


def main() -> int:
    ap = argparse.ArgumentParser(description="Build the spell-page UI sprites from Muse output.")
    ap.add_argument("--raw", required=True, help="Folder with the raw Muse PNGs (UI_*.png on magenta).")
    ap.add_argument("--out", required=True, help="Output folder (Assets/Spellbook/UI).")
    args = ap.parse_args()
    raw, out = Path(args.raw), Path(args.out)
    out.mkdir(parents=True, exist_ok=True)

    # The frame defines the shared canvas for the mask, the rune disc and the glow
    frame = fit(pad_square(trim(key(raw / "UI_FrameRing.png"))), FRAME_SIZE)
    frame = frame.resize((FRAME_SIZE, FRAME_SIZE), Image.LANCZOS)
    r_hole = hole_radius(frame)
    frame.save(out / "UI_FrameRing.png", optimize=True)
    disc(FRAME_SIZE, r_hole + 3).save(out / "UI_CircleMask.png", optimize=True)
    # halo over the bronze band, just outside the hole
    glow(FRAME_SIZE, r_hole + FRAME_SIZE * 0.06).save(out / "UI_FrameGlow.png", optimize=True)

    letters = letter_ring(pad_square(trim(key(raw / "UI_LetterDisc.png"), 0)))
    d = round(2 * (r_hole + 3))
    disc_canvas = Image.new("RGBA", (FRAME_SIZE, FRAME_SIZE), (0, 0, 0, 0))
    disc_canvas.paste(letters.resize((d, d), Image.LANCZOS), ((FRAME_SIZE - d) // 2, (FRAME_SIZE - d) // 2))
    disc_canvas.save(out / "UI_LetterDisc.png", optimize=True)
    print(f"  frame hole radius {r_hole:.0f}px of {FRAME_SIZE} ({r_hole / FRAME_SIZE:.3f})")

    # Only the sprites listed in ui_prompts.json; raw/ may also hold rejected variants
    names = [k for k in json.loads((Path(__file__).parent / "ui_prompts.json").read_text(encoding="utf-8"))
             if not k.startswith("_")]
    for name in names:
        if name in ("UI_FrameRing", "UI_LetterDisc"):
            continue
        img = key(raw / f"{name}.png")
        if name in TAB_GLYPHS:
            img = ink_only(img, (255, 255, 255), widen=15, gain=1.5)
        elif name in INK_LINES:
            img = ink_only(img, INK, widen=3, gain=1.6)
        img = trim(img)
        if name in SQUARE:
            img = pad_square(img)
        fit(img, MAX_SIDE.get(name, 512)).save(out / f"{name}.png", optimize=True)
    print(f"Wrote UI sprites to {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
