#!/usr/bin/env python3
"""ARHeroes-Spellbook-Demo — grimoire ornaments for the spread (Oct 2026).

Turns the Muse ink drawings (processing/ui_prompts.json, section "grimoire") into page sprites:
  - ink ornaments are recoloured to one sepia ink; heavy knots are lightened LOCALLY (only where the
    stroke is thick), so thin connecting lines keep their full width and stay continuous
  - the bottom border rule is split into a left and a right half, so a page number sits in a small gap
  - hatched sketches become sepia ink whose strength follows the darkness of the drawing
  - medallion art keeps its own whites (snow, highlights, light paper): only the outer white
    background connected to the disc rim is removed, then the art is warmed slightly to the book
  - the active medallion gets a narrow golden rim glow and a small sparkle

Usage:
  python make_grimoire_ornaments.py --raw muse_raw/ --out ../Assets/Spellbook/UI

Requires: pip install pillow numpy
"""

import argparse
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

from make_ui_sprites import FRAME_SIZE, hole_radius, key, medal, pad_square, trim

INK = (46, 26, 13)
LIGHT_INK = (120, 88, 58)


def ink(img, rgb=INK, gain=1.3):
    """Line shape only, one flat ink colour."""
    a = img.split()[3].point(lambda v: min(255, int(v * gain)))
    out = Image.new("RGBA", img.size, tuple(rgb) + (0,))
    out.putalpha(a)
    return out


def lighten_knots(img, k):
    """Slim only the thick parts of the strokes.

    Thick regions = what survives an erosion of size k, grown back (a morphological opening).
    Inside them the eroded mask is used (lighter knots, same sharp edge); everywhere else, i.e. the
    thin connecting lines, the original mask is kept untouched.
    """
    a = img.split()[3]
    eroded = a.filter(ImageFilter.MinFilter(k))
    thick = eroded.point(lambda v: 255 if v > 128 else 0).filter(ImageFilter.MaxFilter(k + 2)).filter(ImageFilter.GaussianBlur(1))
    A, E, T = (np.asarray(x).astype(np.float32) / 255 for x in (a, eroded, thick))
    out = img.copy()
    out.putalpha(Image.fromarray(((E * T + A * (1 - T)) * 255).astype(np.uint8)))
    return out


def sepia_sketch(img):
    """Hatched sketch -> sepia ink on transparent: strength from darkness, magenta tint dropped."""
    a = np.array(img.convert("RGBA")).astype(np.float32)
    dark = np.clip((230 - a[..., :3].min(2)) / 170, 0, 1) * a[..., 3] / 255
    out = np.zeros_like(a); out[..., :3] = INK; out[..., 3] = dark * 255
    return Image.fromarray(out.astype(np.uint8), "RGBA")


def paper_tone(img):
    """Remove only the outer white background (connected to the disc rim); keep inner whites."""
    a = np.array(img.convert("RGBA")).astype(np.float32)
    h, w = a.shape[:2]
    near_white = ((a[..., :3].min(2) > 222) & (a[..., 3] > 0)).astype(np.uint8) * 255
    m = Image.fromarray(near_white, "L")
    cx, cy, r = w / 2, h / 2, min(w, h) / 2 - 4
    for k in range(72):
        x = int(cx + r * math.cos(k * math.pi / 36)); y = int(cy + r * math.sin(k * math.pi / 36))
        if m.getpixel((x, y)) == 255: ImageDraw.floodfill(m, (x, y), 128)
    outer = (np.array(m) == 128).astype(np.float32)
    outer = np.array(Image.fromarray((outer * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2))) / 255
    a[..., 3] *= 1 - outer
    warm = np.array([236, 214, 172], np.float32) / 255
    a[..., :3] = a[..., :3] * (0.82 + 0.18 * warm)
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")


def rim_glow(size, r_frac, width):
    yy, xx = np.mgrid[0:size, 0:size]
    d = np.hypot(xx - size / 2, yy - size / 2) / size
    a = np.exp(-((d - r_frac) / width) ** 2)
    img = np.zeros((size, size, 4), np.uint8); img[..., :3] = (255, 205, 90); img[..., 3] = (a * 255).astype(np.uint8)
    return Image.fromarray(img, "RGBA")


def sparkle(size):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0)); d = ImageDraw.Draw(img); c = size / 2
    d.polygon([(c, 0), (c + size * 0.07, c), (c, size), (c - size * 0.07, c)], fill=(255, 236, 170, 255))
    d.polygon([(0, c), (c, c - size * 0.07), (size, c), (c, c + size * 0.07)], fill=(255, 236, 170, 255))
    return img.filter(ImageFilter.GaussianBlur(1))


def crop(img):
    return img.crop(img.split()[3].point(lambda v: 255 if v > 8 else 0).getbbox())


def build(raw: Path):
    """All grimoire sprites as {name: image}. Shared by the Unity export and the offline mockup."""
    o = {}
    k = lambda n: key(raw / f"{n}.png")
    border = crop(lighten_knots(ink(k("UI_OrnBorderRule")), 5))
    bw = border.width
    o["UI_OrnBorderL"] = crop(border.crop((0, 0, bw // 2, border.height)))
    o["UI_OrnBorderR"] = crop(border.crop((bw // 2, 0, bw, border.height)))
    o["UI_OrnChain"] = crop(lighten_knots(ink(k("UI_OrnChainVertical")), 5))
    o["UI_OrnTitle"] = crop(lighten_knots(ink(k("UI_OrnTitleWide")), 3))
    o["UI_OrnSigns"] = crop(ink(k("UI_OrnSignChain"), LIGHT_INK, 1.2))
    o["UI_OrnDivider"] = crop(ink(k("UI_OrnDividerFine"), INK, 1.5))
    o["UI_OrnTick"] = crop(ink(k("UI_OrnStatTick"), INK, 1.5))
    o["UI_OrnCorner"] = crop(lighten_knots(ink(k("UI_FrameCorner")), 3))
    o["UI_SketchCliffs"] = crop(sepia_sketch(k("UI_SketchCliffs")))
    o["UI_SketchShards"] = crop(sepia_sketch(k("UI_SketchShards")))
    return o


def main():
    ap = argparse.ArgumentParser(description="Build the grimoire ornament sprites.")
    ap.add_argument("--raw", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()
    raw, out = Path(args.raw), Path(args.out)
    for name, img in build(raw).items():
        s = 1200 / max(img.size)
        if s < 1: img = img.resize((round(img.width * s), round(img.height * s)), Image.LANCZOS)
        img.save(out / f"{name}.png", optimize=True)

    # Medallion art on the frame's canvas; the storm medallion now has no coloured backdrop
    frame = Image.open(out / "UI_FrameRing.png").convert("RGBA")
    r_hole = hole_radius(frame)
    for i in (1, 2, 3):
        medal(paper_tone(key(raw / f"UI_Medal{i}.png")), r_hole).save(out / f"UI_Medal{i}.png", optimize=True)
    storm = key(raw / "UI_MedalStormInk.png")
    tall = Image.new("RGBA", (900, 900), (0, 0, 0, 0))
    st = trim(storm, 0); st = st.resize((round(st.width * 760 / st.height), 760), Image.LANCZOS)
    tall.alpha_composite(st, ((900 - st.width) // 2, 80))
    medal(pad_square(tall), r_hole).save(out / "UI_MedalStorm.png", optimize=True)

    # Active medallion: narrow rim glow on the frame canvas (0.395 of the frame width) and a sparkle
    rim_glow(FRAME_SIZE, 0.395, 0.012).save(out / "UI_RimGlow.png", optimize=True)
    sparkle(64).save(out / "UI_Sparkle.png", optimize=True)
    print(f"Wrote grimoire ornaments and medallions to {out}")


if __name__ == "__main__":
    main()
