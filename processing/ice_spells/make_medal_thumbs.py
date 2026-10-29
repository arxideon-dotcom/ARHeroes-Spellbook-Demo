"""Static medallion thumbnails cut from one frame of each provided animation.

Same treatment as the Ice Storm medallion (make_grimoire_ornaments.py): the
frame is trimmed to its visible pixels, padded to a square and fitted as a
disc into the shared frame ring's hole (as make_ui_sprites.medal) on the
ring's 512 px canvas. No backdrop, no recolouring.

Usage: python make_medal_thumbs.py
"""
import sys
from pathlib import Path

from PIL import Image

HERE = Path(__file__).parent
ROOT = HERE.parent.parent
sys.path.insert(0, str(HERE.parent))
import numpy as np  # noqa: E402
from make_ui_sprites import FRAME_SIZE, disc, hole_radius, pad_square, trim  # noqa: E402

UI = ROOT / "Assets/Spellbook/UI"
FRAMES = ROOT / "Assets/Spellbook/Frames"
# Sprite name -> (frame chosen for a readable effect, extra margin around the square)
# The comet is diagonal: without a margin the disc cuts its core off in the corner
THUMBS = {
    "UI_MedalFrostBreath": ("FrostBreath_010.png", 1.0),   # breath fully out of the jaws
    "UI_MedalIceComet": ("IceComet_015.png", 1.2),          # bright core, curled tail
    "UI_MedalAbsoluteZero": ("AbsoluteZero_026.png", 1.0),  # cracks and ice bursting from the standing knight
}


def thumb(img: Image.Image, r_hole: float, margin: float) -> Image.Image:
    """make_ui_sprites.medal() with a margin: same disc size and mask on the ring canvas,
    the art is scaled down by `margin` inside the disc (medal() trims any padding away)."""
    d = round(2 * (r_hole + 3))
    sq = pad_square(trim(img, 0))
    inner = round(d / margin)
    art = Image.new("RGBA", (d, d), (0, 0, 0, 0))
    art.alpha_composite(sq.resize((inner, inner), Image.LANCZOS), ((d - inner) // 2, (d - inner) // 2))
    m = disc(d, d / 2 - 0.5).split()[3]
    art.putalpha(Image.fromarray(np.minimum(np.array(art.split()[3]), np.array(m))))
    canvas = Image.new("RGBA", (FRAME_SIZE, FRAME_SIZE), (0, 0, 0, 0))
    canvas.paste(art, ((FRAME_SIZE - d) // 2, (FRAME_SIZE - d) // 2))
    return canvas


def main():
    r_hole = hole_radius(Image.open(UI / "UI_FrameRing.png").convert("RGBA"))
    for name, (frame, margin) in THUMBS.items():
        thumb(Image.open(FRAMES / frame).convert("RGBA"), r_hole, margin).save(UI / f"{name}.png", optimize=True)
        print("wrote", name, "from", frame)


if __name__ == "__main__":
    main()
