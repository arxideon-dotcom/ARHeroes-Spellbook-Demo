#!/usr/bin/env python3
"""ARHeroes-Spellbook-Demo — UI sprites for the spell pages (Oct 2026).

Page decoration (frames, medallion art, banners, tabs, glyphs, stat icons,
marginalia, buttons) is generated with Muse from processing/ui_prompts.json
on a flat magenta background. This script turns those raw images into the
sprites Unity uses:

  - keys magenta out with process_spell.py and trims to the content
  - centres the round frame on a square canvas and measures its empty hole
  - fits the medallion illustrations into that hole as discs, so frame and
    art share one canvas and scale
  - ink drawings keep only their line shape: tab glyphs become a white
    alpha mask (tinted in Unity: gold when active, ink otherwise),
    marginalia are recoloured to one flat ink colour
  - draws the trivial shapes itself: the circle mask (sized to the frame's
    hole), the selection ring glow and a soft round glow

Usage:
  python make_ui_sprites.py --raw muse_raw/ --out ../Assets/Spellbook/UI

Requires: pip install pillow numpy
"""

import argparse
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

from process_spell import process_image

FRAME_SIZE = 512
MAX_SIDE = {"UI_Banner": 768, "UI_BannerDim": 768, "UI_Divider": 768, "UI_ArtShards": 1024,
            "UI_Schematic": 768, "UI_MarginNotes": 768}
TAB_GLYPHS = {"UI_InkSnowflake", "UI_InkGlyph0", "UI_InkGlyph1", "UI_InkGlyph2", "UI_InkGlyph3"}
INK_LINES = {"UI_MarginCurl", "UI_MarginCompass", "UI_Schematic", "UI_MarginNotes", "UI_SketchCrystals"}
SQUARE = TAB_GLYPHS | {"UI_StatMana", "UI_StatPower", "UI_StatDuration", "UI_StatRange", "UI_MarginCompass"}
# Medallion illustrations, cut to the frame's hole. The storm medallion sits on a pale ice backdrop.
MEDALS = {"UI_Medal1": "UI_Medal1", "UI_Medal2": "UI_Medal2", "UI_Medal3": "UI_Medal3", "UI_MedalStorm": "UI_MedalStormInk"}
INK = (58, 34, 18)
# Genuinely blue art: no magenta suppression
BLUE_ART = {"UI_StatMana", "UI_Medal1", "UI_Medal2", "UI_Medal3", "UI_MedalStormInk", "UI_ArtShards"}
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


def ring_glow(size: int, r: float) -> Image.Image:
    """Soft golden halo peaking at radius r (px)."""
    yy, xx = np.mgrid[0:size, 0:size]
    d = np.hypot(xx - size / 2, yy - size / 2) / size
    a = (np.clip(np.exp(-((d - r / size) / 0.045) ** 2), 0, 1) * 255).astype(np.uint8)
    rgb = np.zeros((size, size, 3), np.uint8)
    rgb[...] = (GOLD_L[0], GOLD_L[1], 150)
    return Image.fromarray(np.dstack([rgb, a]), "RGBA")


def soft_glow(size: int) -> Image.Image:
    """White radial glow (tinted in Unity): behind the active tab and the spell art."""
    yy, xx = np.mgrid[0:size, 0:size]
    d = np.hypot(xx - size / 2, yy - size / 2) / (size / 2)
    a = (np.clip(1 - d, 0, 1) ** 1.6 * 255).astype(np.uint8)
    return Image.fromarray(np.dstack([np.full((size, size, 3), 255, np.uint8), a]), "RGBA")


def medal(img: Image.Image, r_hole: float, backdrop=None) -> Image.Image:
    """Illustration as a disc that exactly fills the frame's hole, on the frame's canvas."""
    d = round(2 * (r_hole + 3))
    art = pad_square(trim(img, 0))
    if backdrop is not None:
        # A tall subject on a pale ice disc: the storm keeps its full height inside the circle
        bg = Image.new("RGBA", (art.width, art.height), backdrop + (255,))
        bg.alpha_composite(art)
        art = bg
    art = art.resize((d, d), Image.LANCZOS)
    m = disc(d, d / 2 - 0.5).split()[3]
    art.putalpha(Image.fromarray(np.minimum(np.array(art.split()[3]), np.array(m))))
    canvas = Image.new("RGBA", (FRAME_SIZE, FRAME_SIZE), (0, 0, 0, 0))
    canvas.paste(art, ((FRAME_SIZE - d) // 2, (FRAME_SIZE - d) // 2))
    return canvas


def main() -> int:
    ap = argparse.ArgumentParser(description="Build the spell-page UI sprites from Muse output.")
    ap.add_argument("--raw", required=True, help="Folder with the raw Muse PNGs (UI_*.png on magenta).")
    ap.add_argument("--out", required=True, help="Output folder (Assets/Spellbook/UI).")
    args = ap.parse_args()
    raw, out = Path(args.raw), Path(args.out)
    out.mkdir(parents=True, exist_ok=True)

    # The frame defines the shared canvas for the mask, the medallion art and the glow
    frame = fit(pad_square(trim(key(raw / "UI_FrameRing.png"))), FRAME_SIZE)
    frame = frame.resize((FRAME_SIZE, FRAME_SIZE), Image.LANCZOS)
    r_hole = hole_radius(frame)
    frame.save(out / "UI_FrameRing.png", optimize=True)
    disc(FRAME_SIZE, r_hole + 3).save(out / "UI_CircleMask.png", optimize=True)
    ring_glow(FRAME_SIZE, r_hole + FRAME_SIZE * 0.05).save(out / "UI_FrameGlow.png", optimize=True)
    soft_glow(256).save(out / "UI_SoftGlow.png", optimize=True)
    print(f"  frame hole radius {r_hole:.0f}px of {FRAME_SIZE} ({r_hole / FRAME_SIZE:.3f})")

    for name, src in MEDALS.items():
        backdrop = (214, 232, 244) if name == "UI_MedalStorm" else None
        medal(key(raw / f"{src}.png"), r_hole, backdrop).save(out / f"{name}.png", optimize=True)

    # Only the sprites listed in ui_prompts.json; raw/ may also hold rejected variants
    names = [k for k in json.loads((Path(__file__).parent / "ui_prompts.json").read_text(encoding="utf-8"))
             if not k.startswith("_")]
    for name in names:
        if name == "UI_FrameRing" or name in MEDALS.values():
            continue
        img = key(raw / f"{name}.png")
        if name in TAB_GLYPHS:
            img = ink_only(img, (255, 255, 255), widen=15, gain=1.5)
        elif name in INK_LINES:
            img = ink_only(img, INK, widen=3, gain=1.5)
        img = trim(img)
        if name in SQUARE:
            img = pad_square(img)
        fit(img, MAX_SIDE.get(name, 512)).save(out / f"{name}.png", optimize=True)
    print(f"Wrote UI sprites to {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
