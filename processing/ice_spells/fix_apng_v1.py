"""Local cleanup of the three provided APNG animations (no new images generated).

Inputs : processing/ice_spells/apng/*.apng (copies; the originals in Downloads are never touched)
Outputs: processing/ice_spells/fix_v1/<Spell>/NNN.png, <file>_fix_v1.apng, fix_v1.json

Per-pixel operations, applied in this order (all on straight, non-premultiplied RGBA):

1. Frost Breath haze, frames 28-35 only.
   Measured: in those frames the dark semi-transparent haze has max(RGB) ~= 0.9 * alpha
   (p10..p90 0.7..1.1, positive correlation), i.e. its colour was multiplied by alpha a second time.
   In frames 0-27 the same ratio is spread 0.4..3.9 with no correlation. This layer is a shadow copy
   of the dragon (a second head silhouette in frames 31-35), so it is faded out rather than
   brightened: restoring its colour turned it into a pale second head. Weighted softly by
   semi-transparency, that ratio, darkness, and distance from opaque pixels (dragon scales and their
   antialiased edges are protected). The real breath mist has the normal colour signature and stays.
   No single brightness threshold is used.

2. Green key residue, all three clips.
   Measured: greener-than-both pixels (G - max(R, B) > 20) sit on the 0-2 px outer contour
   (47-85 % of contour pixels) and almost never 32+ px inside (<= 1.3 %), and mostly at low alpha.
   So the residue is tied to transparency. Correction = only the excess of G over max(R, B) beyond a
   small turquoise allowance, weighted by transparency (opaque pixels unchanged). The excess is moved
   partly into B (mint -> ice cyan) instead of being removed, so the colour keeps its brightness.
   Blue, cyan and lilac pixels have no excess and are not changed.

3. Edge fade (Frost Breath, Absolute Zero): content that runs into the canvas edge fades out over a
   band, only along the parts of the edge it actually touches (contact profile per frame, max over
   +/-2 frames so the fade does not flicker). Alpha only: nothing is blurred, scaled or moved.

4. Loop seam (Frost Breath, Absolute Zero): last frames fade out, a short transparent pause,
   then frame 0 fades in as a still before the loop restarts on the untouched frame 0.
   No reverse playback and no cross-blend of two different frames.
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageSequence

ROOT = Path(__file__).resolve().parent
SRC = ROOT / "apng"
OUT = ROOT / "fix_v1"

SPELLS = [
    dict(prefix="FrostBreath", file="icedragon-v1.apng",
         haze=range(28, 36), despill=True,
         edges={"right": 120, "bottom": 80, "top": 70, "left": 40},
         fade_out={32: 0.82, 33: 0.6, 34: 0.36, 35: 0.14},
         pause=0.22, fade_in=[0.25, 0.5, 0.75]),
    dict(prefix="IceComet", file="icecomet-v1.apng",
         haze=(), despill=True, edges={}, fade_out={}, pause=0.0, fade_in=[]),
    dict(prefix="AbsoluteZero", file="frozenknight-shatter.apng",
         haze=(), despill=True,
         edges={"right": 90, "left": 90, "top": 90, "bottom": 50},
         fade_out={35: 0.8, 36: 0.6, 37: 0.4, 38: 0.22, 39: 0.08},
         pause=0.36, fade_in=[0.2, 0.4, 0.6, 0.8]),
]


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def load(path):
    im = Image.open(path)
    frames, delays = [], []
    for f in ImageSequence.Iterator(im):
        frames.append(np.asarray(f.convert("RGBA")).astype(np.float32))
        delays.append(f.info["duration"] / 1000.0)
    return frames, delays


def dilate(mask, n):
    m = mask.copy()
    for _ in range(n):
        p = np.pad(m, 1, constant_values=False)
        m = m | p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
    return m


def fix_haze(a):
    rgb, al = a[..., :3], a[..., 3]
    mx = rgb.max(-1)
    ratio = mx / np.maximum(al, 1.0)
    semi = smoothstep(8, 20, al) * (1 - smoothstep(200, 235, al))
    close = 1 - smoothstep(0.15, 0.4, np.abs(ratio - 0.9))
    dark = 1 - smoothstep(120, 170, mx)
    protect = dilate(al >= 245, 2)
    w = semi * close * dark * (~protect)
    # контурные линии той же тени (рога, челюсть) темнее и выпадают из окна отношения;
    # рядом с найденной тенью гасятся и они — только тёмные полупрозрачные, яркий туман не трогается
    near = dilate(w > 0.3, 5)
    lines = semi * (1 - smoothstep(110, 160, mx)) * near * (~protect)
    w = np.maximum(w, lines)
    restored = np.clip(rgb * 255.0 / np.maximum(al, 1.0)[..., None], 0, 255)
    out = a.copy()
    out[..., :3] = rgb + w[..., None] * (restored - rgb)
    out[..., 3] = al * (1 - 0.92 * w)
    return out, w


def despill(a):
    r, g, b, al = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    excess = g - np.maximum(r, b)
    weight = 1 - smoothstep(90, 235, al)          # transparent -> 1, opaque -> 0
    s = np.maximum(excess - 6.0, 0.0) * weight     # 6 = turquoise allowance
    out = a.copy()
    out[..., 1] = g - 0.55 * s
    out[..., 2] = np.minimum(255.0, b + 0.45 * s)
    return out, s


def max_filter_1d(v, radius):
    p = np.pad(v, radius, mode="edge")
    return np.lib.stride_tricks.sliding_window_view(p, 2 * radius + 1).max(-1)


def box_1d(v, radius):
    p = np.pad(v, radius, mode="edge")
    c = np.cumsum(np.insert(p, 0, 0.0))
    return (c[2 * radius + 1:] - c[:-2 * radius - 1]) / (2 * radius + 1)


def edge_contact(al, side):
    strip = {"right": al[:, -4:].max(1), "left": al[:, :4].max(1),
             "top": al[:4, :].max(0), "bottom": al[-4:, :].max(0)}[side]
    return strip / 255.0


def edge_multipliers(alphas, edges):
    """Per-frame alpha multiplier: fade towards each edge, only where content touches it."""
    T = len(alphas)
    H, W = alphas[0].shape
    contacts = {s: np.stack([edge_contact(a, s) for a in alphas]) for s in edges}
    mults = []
    for t in range(T):
        m = np.ones((H, W), np.float32)
        for side, band in edges.items():
            c = contacts[side][max(0, t - 2):t + 3].max(0)
            c = np.clip(box_1d(max_filter_1d(c, 50), 30) * 1.5, 0, 1)
            if side in ("right", "left"):
                d = np.arange(W, dtype=np.float32)
                d = (W - 1 - d) if side == "right" else d
                ramp = smoothstep(0, band, d)[None, :]
                m *= 1 - c[:, None] * (1 - ramp)
            else:
                d = np.arange(H, dtype=np.float32)
                d = (H - 1 - d) if side == "bottom" else d
                ramp = smoothstep(0, band, d)[:, None]
                m *= 1 - c[None, :] * (1 - ramp)
        mults.append(m)
    return mults


def process(spec):
    frames, delays = load(SRC / spec["file"])
    stats = []
    out = []
    for i, a in enumerate(frames):
        st = {"frame": i}
        if i in spec["haze"]:
            a, w = fix_haze(a)
            st["haze_px"] = int((w > 0.05).sum())
        if spec["despill"]:
            a, s = despill(a)
            st["despill_px"] = int((s > 1).sum())
        out.append(a)
        stats.append(st)
    if spec["edges"]:
        mults = edge_multipliers([a[..., 3] for a in out], spec["edges"])
        for a, m in zip(out, mults):
            a[..., 3] *= m
    for i, k in spec["fade_out"].items():
        out[i][..., 3] *= k
    seq = [(a, d, "frame %d" % i) for i, (a, d) in enumerate(zip(out, delays))]
    if spec["pause"] > 0:
        seq.append((np.zeros_like(out[0]), spec["pause"], "transparent pause"))
    for k in spec["fade_in"]:
        f0 = out[0].copy()
        f0[..., 3] *= k
        seq.append((f0, delays[0], "frame 0 at alpha x%.2f" % k))
    return seq, delays, stats


def main():
    OUT.mkdir(exist_ok=True)
    report = {"source_dir": str(SRC), "clips": []}
    for spec in SPELLS:
        seq, src_delays, stats = process(spec)
        d = OUT / spec["prefix"]
        d.mkdir(exist_ok=True)
        for old in d.glob("*.png"):
            old.unlink()
        imgs = []
        for i, (a, _, _) in enumerate(seq):
            im = Image.fromarray(np.clip(a.round(), 0, 255).astype(np.uint8), "RGBA")
            im.save(d / ("%s_%03d.png" % (spec["prefix"], i)))
            imgs.append(im)
        apng = OUT / spec["file"].replace(".apng", "_fix_v1.apng")
        imgs[0].save(apng, save_all=True, append_images=imgs[1:], loop=0,
                     duration=[round(x * 1000) for _, x, _ in seq], disposal=0, blend=0)
        report["clips"].append({
            "prefix": spec["prefix"], "source": spec["file"], "output": apng.name,
            "frames": len(seq), "source_frames": len(src_delays),
            "delays": [round(x, 3) for _, x, _ in seq],
            "duration": round(sum(x for _, x, _ in seq), 3),
            "source_duration": round(sum(src_delays), 3),
            "sequence": [n for _, _, n in seq],
            "params": {k: (list(v) if isinstance(v, range) else v) for k, v in spec.items() if k not in ("prefix", "file")},
            "stats": stats,
        })
        print(spec["prefix"], len(seq), "frames", report["clips"][-1]["duration"], "s ->", apng.name)
    (OUT / "fix_v1.json").write_bytes(json.dumps(report, indent=1).encode("utf-8"))


if __name__ == "__main__":
    sys.exit(main())
