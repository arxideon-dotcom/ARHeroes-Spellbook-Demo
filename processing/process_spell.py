#!/usr/bin/env python3
"""ARHeroes-Spellbook-Demo — hackathon processing step (Oct 2026).

Turns Muse-generated artwork on a solid magenta #FF00FF background into
transparent frames for the Unity demo.

Pipeline (the project's standing chroma rules):
  - magenta-ness = min(R, B) - G, smoothstep 30 -> 110
    (low threshold on purpose: a higher one eats dark robe/ink edges)
  - despill: pull R/B toward G on semi-transparent edge pixels
  - canvas size is NEVER changed: all frames stay aligned for the loop

Usage:
  Single image (a spell page illustration):
    python process_spell.py art_magenta.png --out out/
  Frame sequence (animation frames frame_000.png ... frame_039.png):
    python process_spell.py frames_magenta/ --out out/ --fps 12 --apng spell_loop.apng
  Optional loop-seam softening (dissolves the last N frames into the first N):
    python process_spell.py frames_magenta/ --out out/ --xfade 12

Requires: pip install pillow numpy
"""

import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image

SMOOTH_LOW = 30.0
SMOOTH_HIGH = 110.0


def magenta_alpha(rgb: np.ndarray) -> np.ndarray:
    """Alpha channel (0..1) from magenta-ness. Magenta -> 0, artwork -> 1."""
    r = rgb[..., 0].astype(np.float32)
    g = rgb[..., 1].astype(np.float32)
    b = rgb[..., 2].astype(np.float32)
    magenta = np.minimum(r, b) - g
    t = np.clip((magenta - SMOOTH_LOW) / (SMOOTH_HIGH - SMOOTH_LOW), 0.0, 1.0)
    smooth = t * t * (3.0 - 2.0 * t)  # smoothstep: 0 = artwork, 1 = magenta
    return 1.0 - smooth


def despill(rgba: np.ndarray, alpha: np.ndarray) -> np.ndarray:
    """Remove magenta fringe on semi-transparent edges.

    Where alpha is partial, blend R and B toward G proportionally to
    transparency; fully opaque pixels are untouched.
    """
    out = rgba.astype(np.float32)
    a = alpha  # 2D (H, W): per-channel math below must stay 2D
    g = out[..., 1]
    out[..., 0] = g + (out[..., 0] - g) * a
    out[..., 2] = g + (out[..., 2] - g) * a
    return np.clip(out, 0, 255).astype(np.uint8)


def process_image(src: Path) -> Image.Image:
    rgb = np.array(Image.open(src).convert("RGB"))
    alpha = magenta_alpha(rgb)
    rgba = np.dstack([rgb, (alpha * 255).astype(np.uint8)])
    rgba[..., :3] = despill(rgba[..., :3], alpha)
    transparent = float((alpha == 0).mean() * 100)
    print(f"  {src.name}: transparent={transparent:.1f}%")
    return Image.fromarray(rgba, "RGBA")


def crossfade_splice(frames: list, n: int) -> list:
    """Soften the loop seam: dissolve the last n frames toward the first n.

    Tail frame M-n+i becomes (1-w)*tail + w*head[i], w = (i+1)/n, so the
    tail fades into the head. The head is untouched and the sequence length
    never changes. Only used when the generated frames do not already loop
    cleanly.
    """
    m = len(frames)
    if n <= 0 or m < 2 * n:
        return frames
    arrs = [np.array(f).astype(np.float32) for f in frames]
    for i in range(n):
        w = (i + 1) / n
        arrs[m - n + i] = (1.0 - w) * arrs[m - n + i] + w * arrs[i]
    return [Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA") for a in arrs]


def main() -> int:
    ap = argparse.ArgumentParser(description="Magenta-key Muse art to transparent frames.")
    ap.add_argument("input", help="Magenta-background image file or directory of frames.")
    ap.add_argument("--out", default="out", help="Output directory for transparent PNGs.")
    ap.add_argument("--fps", type=float, default=12.0, help="APNG frame rate (default 12).")
    ap.add_argument("--apng", default="", help="Also build an APNG with this filename.")
    ap.add_argument("--xfade", type=int, default=0,
                    help="Soften the loop seam by dissolving the last N frames into "
                         "the first N (0 = off).")
    args = ap.parse_args()

    src = Path(args.input)
    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)

    if src.is_file():
        files = [src]
    elif src.is_dir():
        files = sorted(p for p in src.iterdir()
                       if p.suffix.lower() in (".png", ".jpg", ".jpeg", ".webp"))
        if not files:
            print(f"No images found in {src}", file=sys.stderr)
            return 1
    else:
        print(f"Not found: {src}", file=sys.stderr)
        return 1

    frames = [process_image(f) for f in files]
    # Splice before writing, so the Unity PNGs and the APNG get the same sequence
    frames = crossfade_splice(frames, args.xfade)
    for f, img in zip(files, frames):
        img.save(out_dir / f"{f.stem}_transparent.png")
    print(f"Wrote {len(frames)} transparent frame(s) to {out_dir}/")

    if args.apng and len(frames) > 1:
        ms = max(1, int(round(1000 / args.fps)))
        frames[0].save(out_dir / args.apng, save_all=True,
                       append_images=frames[1:], duration=ms, loop=0)
        print(f"Wrote APNG {out_dir / args.apng} ({len(frames)} frames, {args.fps} fps)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
