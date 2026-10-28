#!/usr/bin/env python3
"""ARHeroes-Spellbook-Demo — surface grid, book shadow and table (Oct 2026).

Writes a flat uniform grid per page in the local page coordinates of
Assets/Spellbook/Art/PageSurfaces.json (technical preview of the page surface; in Unity it goes
through the same PageWarp as every page element), the contact shadow under the book and the muted
wooden table behind it. The page ornaments themselves come from make_grimoire_ornaments.py.

Usage:
  python make_page_frames.py --raw muse_raw/ --out ../Assets/Spellbook/UI

Requires: pip install pillow numpy
"""

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter


ROOT = Path(__file__).resolve().parent.parent
SPREAD = ROOT / "Assets/Spellbook/Art/Spellbook_Spread_v8.png"
SURFACES = ROOT / "Assets/Spellbook/Art/PageSurfaces.json"

GRID_STEP = 66
INK = (58, 34, 18)


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

    for name, page in pages.items():
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
    print(f"Wrote surface grids, shadow and table to {out}")


if __name__ == "__main__":
    main()
