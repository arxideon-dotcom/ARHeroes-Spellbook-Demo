#!/usr/bin/env python3
"""ARHeroes-Spellbook-Demo — page surface of each top sheet (Oct 2026).

One source of truth for the geometry of the two pages. Every page element in Unity (frames,
medallions, banners, text, cartouche, schematics, tabs and the animation) is laid out in flat
local page coordinates and then mapped through this surface, so they all follow the paper together.

The surface of a sheet is a ruled surface between its measured top and bottom edges:
  - u runs left -> right across the sheet, v runs top -> bottom;
  - the outer edge is slanted by the page perspective (the edges are measured, not guessed);
  - near the spine the sheet curves down into the gutter: x is compressed there (h(t) below);
  - rows are foreshortened by perspective: the top edge is shorter than the bottom edge, so v is
    mapped projectively with the two edge widths.

Local page coordinates are pixels with the origin at the centre of the sheet and y up; one local
px is about one page-canvas px (900 px = 0.625 world units) in the middle of the page.

Usage:
  python make_page_surface.py --out ../Assets/Spellbook/Art/PageSurfaces.json [--preview grid.png]

Requires: pip install pillow numpy
"""

import argparse
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
SPREAD = ROOT / "Assets/Spellbook/Art/Spellbook_Spread_v8.png"
SPRITE_W, SPRITE_H, PPU = 1680, 964, 720.0
UNITS_PER_CANVAS_PX = 0.625 / 900
SAMPLES = 33
SPINE_COMPRESSION = 0.4   # slope of x at the spine relative to mid-page: 1 - 0.4 = 0.6

# Top sheet edges measured on the v8 spread (spread px). Corners: outer/spine x top and bottom.
PAGES = {
    "Left": {"top": [(265, 62), (400, 55), (550, 25), (640, 15), (750, 26), (790, 60)],
             "bottom": [(128, 828), (300, 808), (500, 800), (650, 810), (790, 850)],
             "x_top": (265, 790), "x_bottom": (128, 790), "spine_at_u1": True,
             "canvas_centre_world": (-0.4472, 0.0625)},
    "Right": {"top": [(890, 60), (980, 15), (1100, 22), (1220, 46), (1340, 67), (1405, 62)],
              "bottom": [(880, 850), (1000, 810), (1150, 800), (1350, 808), (1552, 832)],
              "x_top": (890, 1405), "x_bottom": (880, 1552), "spine_at_u1": False,
              "canvas_centre_world": (0.4278, 0.0625)},
}


def h(t):
    """Distance from the spine (0..1) -> compressed distance: slope 0.6 at the spine, ~1 elsewhere."""
    return t - SPINE_COMPRESSION * t * (1 - t) ** 4


def fit(points):
    xs, ys = zip(*points)
    return np.poly1d(np.polyfit(xs, ys, 3))


def build(pg):
    top_f, bot_f = fit(pg["top"]), fit(pg["bottom"])
    us = np.linspace(0, 1, SAMPLES)
    def xs(x0, x1):
        g = 1 - h(1 - us) if pg["spine_at_u1"] else h(us)
        return x0 + (x1 - x0) * g
    tx, bx = xs(*pg["x_top"]), xs(*pg["x_bottom"])
    top = np.stack([tx, top_f(tx)], 1)
    bottom = np.stack([bx, bot_f(bx)], 1)
    w0 = pg["x_top"][1] - pg["x_top"][0]
    w1 = pg["x_bottom"][1] - pg["x_bottom"][0]
    return top, bottom, w0, w1


def sample(arr, u):
    n = len(arr) - 1
    f = u * n
    i = int(np.clip(math.floor(f), 0, n - 1))
    return arr[i] + (arr[i + 1] - arr[i]) * (f - i)      # linear, extrapolates past the ends


def surface_point(top, bottom, w0, w1, u, v):
    vp = v * w0 / (w1 - v * (w1 - w0))                  # projective rows: top edge is farther
    return (1 - vp) * sample(top, u) + vp * sample(bottom, u)


def invert(top, bottom, w0, w1, target):
    u, v = 0.5, 0.5
    for _ in range(40):
        p = surface_point(top, bottom, w0, w1, u, v)
        e = 1e-4
        du = (surface_point(top, bottom, w0, w1, u + e, v) - p) / e
        dv = (surface_point(top, bottom, w0, w1, u, v + e) - p) / e
        J = np.array([du, dv]).T
        step = np.linalg.solve(J, target - p)
        u, v = u + step[0], v + step[1]
    return u, v


def main():
    ap = argparse.ArgumentParser(description="Compute the page surfaces.")
    ap.add_argument("--out", required=True)
    ap.add_argument("--preview", default="")
    args = ap.parse_args()
    pages = []
    for name, pg in PAGES.items():
        top, bottom, w0, w1 = build(pg)
        cw = pg["canvas_centre_world"]
        centre = np.array([cw[0] * PPU + SPRITE_W / 2, SPRITE_H / 2 - cw[1] * PPU])
        u0, v0 = invert(top, bottom, w0, w1, centre)
        # local size: mean edge length, in canvas px (1 spread px = 2 canvas px)
        width = (w0 + w1)                     # = 2 * mean width
        left_len = np.linalg.norm(bottom[0] - top[0]); right_len = np.linalg.norm(bottom[-1] - top[-1])
        height = left_len + right_len         # = 2 * mean height
        # Local origin = sheet centre (u = v = 0.5), y up
        pages.append({"name": name, "top": [{"x": float(a), "y": float(b)} for a, b in top],
                      "bottom": [{"x": float(a), "y": float(b)} for a, b in bottom],
                      "w0": float(w0), "w1": float(w1), "center": {"x": float(centre[0]), "y": float(centre[1])},
                      "width": float(width), "height": float(height)})
        print(f"{name}: local sheet {width:.0f} x {height:.0f} px (origin at the sheet centre)")
    Path(args.out).write_text(json.dumps({"pages": pages}, indent=1), encoding="utf-8")
    print("wrote", args.out)

    if args.preview:
        sp = Image.open(SPREAD).convert("RGBA")
        bg = Image.new("RGBA", sp.size, (20, 16, 12, 255)); bg.alpha_composite(sp)
        d = ImageDraw.Draw(bg)
        for p in pages:
            top = np.array([[q["x"], q["y"]] for q in p["top"]]); bottom = np.array([[q["x"], q["y"]] for q in p["bottom"]])
            for i in range(13):
                u = i / 12
                d.line([tuple(surface_point(top, bottom, p["w0"], p["w1"], u, v)) for v in np.linspace(0, 1, 40)], fill=(0, 220, 255, 255), width=1)
            for j in range(17):
                v = j / 16
                d.line([tuple(surface_point(top, bottom, p["w0"], p["w1"], u, v)) for u in np.linspace(0, 1, 40)], fill=(0, 220, 255, 255), width=1)
        bg.convert("RGB").save(args.preview)


if __name__ == "__main__":
    main()
