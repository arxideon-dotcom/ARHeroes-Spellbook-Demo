"""Key the approved-candidate key images with the project's green key
(process_spell.process_image) and build the check sheets.

Full despill: see full_despill().

Edge fade: for art whose mist reaches the canvas border (the model kept
extending the breath / tail to the edge), alpha is multiplied by a
smoothstep ramp on the named sides only, so the mist dissipates instead of
ending in a straight cut. The opaque subject is never inside the ramp.

Outputs (processing/ice_spells/):
  keyed/<name>_key.png            transparent, original canvas size
  check/<name>_backgrounds.png    parchment / light / dark
  check/key_images_vs_ice_storm.png  all three next to Ice Storm frame 0,
                                     content scaled to the same height box
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

HERE = Path(__file__).parent
ROOT = HERE.parent.parent
sys.path.insert(0, str(HERE.parent))
from process_spell import process_image  # noqa: E402

# name -> (raw file, sides to fade, ramp width as a fraction of the canvas)
SPELLS = {
    "frost_breath": ("raw/frost_breath_key_v2.png", "rb", 0.16),
    "ice_comet": ("raw/ice_comet_key_v2.png", "lt", 0.16),
    "absolute_zero": ("raw/absolute_zero_key_v3.png", "", 0.0),
}


def edge_fade(img: Image.Image, sides: str, frac: float) -> Image.Image:
    if not sides:
        return img
    a = np.asarray(img).astype(np.float32)
    h, w = a.shape[:2]
    ramp = max(1.0, frac * min(w, h))
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    dist = {"l": x, "r": w - 1 - x, "t": y, "b": h - 1 - y}
    m = np.ones((h, w), np.float32)
    for s in sides:
        t = np.clip(dist[s] / ramp, 0, 1)
        m *= t * t * (3 - 2 * t)
    a[..., 3] *= m
    return Image.fromarray(a.round().astype(np.uint8), "RGBA")


def full_despill(img: Image.Image) -> Image.Image:
    """Translucent mist was painted mixed with the green key (mint haze), which
    the edge-only despill leaves alone. For this ice palette any G above
    max(R, B) is spill: remove it and lower alpha by the same amount (scaled
    like the key's 110 upper threshold), so the haze stays white-blue and
    translucent. White, blue and lilac pixels have G <= max(R, B): untouched."""
    a = np.asarray(img).astype(np.float32)
    rb = np.maximum(a[..., 0], a[..., 2])
    spill = np.clip(a[..., 1] - rb, 0, None)
    a[..., 1] -= spill
    a[..., 3] *= np.clip(1 - spill / 110.0, 0, 1)
    return Image.fromarray(a.round().astype(np.uint8), "RGBA")


def parchment(size):
    spread = Image.open(ROOT / "Assets/Spellbook/Art/Spellbook_Spread_v8.png").convert("RGB")
    W, H = spread.size
    patch = spread.crop((int(W * 0.60), int(H * 0.30), int(W * 0.80), int(H * 0.55)))
    return patch.resize(size, Image.LANCZOS)


def on(bg: Image.Image, fg: Image.Image) -> Image.Image:
    out = bg.convert("RGBA").copy()
    out.alpha_composite(fg)
    return out.convert("RGB")


def fit_content(img: Image.Image, box_h: int) -> Image.Image:
    c = img.crop(img.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox())
    s = box_h / max(c.width, c.height)
    return c.resize((round(c.width * s), round(c.height * s)), Image.LANCZOS)


def main():
    (HERE / "keyed").mkdir(exist_ok=True)
    (HERE / "check").mkdir(exist_ok=True)
    keyed = {}
    only = sys.argv[1:]  # optional: re-key just these names, approved ones stay as they are
    for name, (raw, sides, frac) in SPELLS.items():
        if only and name not in only:
            keyed[name] = Image.open(HERE / "keyed" / f"{name}_key.png").convert("RGBA")
            continue
        img = edge_fade(full_despill(process_image(HERE / raw, key="green")), sides, frac)
        img.save(HERE / "keyed" / f"{name}_key.png")
        keyed[name] = img
        n = 700
        small = img.resize((n, n), Image.LANCZOS)
        bgs = [parchment((n, n)), Image.new("RGB", (n, n), (236, 232, 222)), Image.new("RGB", (n, n), (24, 22, 26))]
        sheet = Image.new("RGB", (n * 3, n))
        for i, bg in enumerate(bgs):
            sheet.paste(on(bg, small), (i * n, 0))
        sheet.save(HERE / "check" / f"{name}_backgrounds.png")

    storm = Image.open(ROOT / "Assets/Spellbook/Frames/IceStorm_000.png").convert("RGBA")
    box = 620
    tiles = [("Ice Storm (existing, frame 0)", fit_content(storm, box))] + \
            [(n, fit_content(keyed[n], box)) for n in SPELLS]
    cell = box + 40
    sheet = parchment((cell * 4, cell)).convert("RGBA")
    for i, (_, t) in enumerate(tiles):
        sheet.alpha_composite(t, (i * cell + (cell - t.width) // 2, (cell - t.height) // 2))
    sheet.convert("RGB").save(HERE / "check" / "key_images_vs_ice_storm.png")
    print("ok")


if __name__ == "__main__":
    main()
