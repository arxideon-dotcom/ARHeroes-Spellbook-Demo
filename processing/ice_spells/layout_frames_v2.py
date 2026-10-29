"""Sizes each spell animation for the right page by its main visible figure (not by the PNG canvas).

Reference: Ice Storm. Its visible area (union of alpha > 16 over all 36 frames, as the scene shows it:
581x658 frames at 936 px page height, centre y 271) is the art region A on the right sheet. The
existing self-test already proves A clears the spine rings, stays inside the sheet frame and above
the title banner.

Per clip (frames from fix_v1: edge fades, despill, Absolute Zero fade/pause/appear):
  core  = bounding box of the main figure: pixels opaque (alpha > 96) in >= 35 % of the frames
          (Frost Breath: union of per-frame 98 %-alpha-mass boxes, since the breath moves).
          Rare flying particles are not in the core, so they do not shrink the figure.
  scale = fill * min(A.w / core.w, A.h / core.h), one factor for both axes, chosen per clip.
  place = core centred in A.
  fade  = anything that would leave A fades out over RAMP page px inside A's edge (alpha only).
Frames are cropped to the part inside A and written to Assets/Spellbook/Frames; SpellClips.json gets
the art rectangle (page px) per clip. Ice Storm frames are not touched.
"""
import json
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
FIX = ROOT / "processing" / "ice_spells" / "fix_v1"
FRAMES = ROOT / "Assets" / "Spellbook" / "Frames"
CLIPS = ROOT / "Assets" / "Spellbook" / "Art" / "SpellClips.json"

# Ice Storm on the page (SpellbookSceneBuilder: ArtHeight 936, frame 581x658, centre y = 718+21-468)
STORM_H, STORM_W_PX, STORM_H_PX, STORM_CY = 936.0, 581, 658, 271.0
RAMP = 40.0

SPELLS = [
    dict(prefix="FrostBreath", title="Frost Breath", source="icedragon-v1_fix_v1.apng", core="mass", fill=0.97,
         description="An icy dragon exhales a torrent of freezing breath."),
    dict(prefix="IceComet", title="Ice Comet", source="icecomet-v1_fix_v1.apng", core="occupancy", fill=0.97,
         description="A frozen comet streaks through the air, trailing ice shards."),
    dict(prefix="AbsoluteZero", title="Absolute Zero", source="frozenknight-shatter_fix_v1.apng", core="occupancy", fill=0.9,
         description="Extreme cold freezes a knight until the figure shatters."),
]


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def storm_region():
    s = STORM_H / STORM_H_PX
    x0 = y0 = 10 ** 9
    x1 = y1 = -1
    for f in sorted(FRAMES.glob("IceStorm_*.png")):
        al = np.asarray(Image.open(f).convert("RGBA"))[..., 3]
        ys, xs = np.nonzero(al > 16)
        x0, y0, x1, y1 = min(x0, xs.min()), min(y0, ys.min()), max(x1, xs.max() + 1), max(y1, ys.max() + 1)
    left, top = -STORM_W_PX * s / 2, STORM_CY + STORM_H / 2
    return (left + x0 * s, top - y1 * s, left + x1 * s, top - y0 * s)   # page px, y up


def core_box(al, mode):
    if mode == "occupancy":
        occ = (al > 96).mean(0)
        ys, xs = np.nonzero(occ >= 0.35)
        return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
    u = [10 ** 9, 10 ** 9, -1, -1]
    for a in al:
        if a.sum() < 1:
            continue
        cx = np.cumsum(a.sum(0)) / a.sum()
        cy = np.cumsum(a.sum(1)) / a.sum()
        b = (np.searchsorted(cx, 0.01), np.searchsorted(cy, 0.01), np.searchsorted(cx, 0.99) + 1, np.searchsorted(cy, 0.99) + 1)
        u = [min(u[0], b[0]), min(u[1], b[1]), max(u[2], b[2]), max(u[3], b[3])]
    return tuple(u)


def main():
    A = storm_region()
    aw, ah = A[2] - A[0], A[3] - A[1]
    acx, acy = (A[0] + A[2]) / 2, (A[1] + A[3]) / 2
    fix = json.loads((FIX / "fix_v1.json").read_text(encoding="utf-8"))
    out = {"region": [round(v, 1) for v in A], "ramp": RAMP, "clips": []}
    print("region A (page px, x0 y0 x1 y1):", [round(v, 1) for v in A])
    for spec in SPELLS:
        meta = next(c for c in fix["clips"] if c["prefix"] == spec["prefix"])
        paths = [FIX / spec["prefix"] / ("%s_%03d.png" % (spec["prefix"], i)) for i in range(meta["frames"])]
        frames = [np.asarray(Image.open(p).convert("RGBA")).astype(np.float32) for p in paths]
        H, W = frames[0].shape[:2]
        al = np.stack([f[..., 3] for f in frames])
        c = core_box(al, spec["core"])
        cw, ch = c[2] - c[0], c[3] - c[1]
        s = spec["fill"] * min(aw / cw, ah / ch)
        # source px (u right, v down) -> page px (x right, y up): core centre lands on A's centre
        ox = acx - ((c[0] + c[2]) / 2) * s
        oy = acy + ((c[1] + c[3]) / 2) * s
        u = (np.arange(W) + 0.5) * s + ox
        v = oy - (np.arange(H) + 0.5) * s
        mx = smoothstep(A[0], A[0] + RAMP, u) * (1 - smoothstep(A[2] - RAMP, A[2], u))
        my = smoothstep(A[1], A[1] + RAMP, v) * (1 - smoothstep(A[3] - RAMP, A[3], v))
        m = my[:, None] * mx[None, :]
        # обрезка до части холста внутри A (одинаковая для всех кадров клипа)
        cols = np.nonzero(mx > 0)[0]
        rows = np.nonzero(my > 0)[0]
        u0, u1, v0, v1 = cols.min(), cols.max() + 1, rows.min(), rows.max() + 1
        for old in FRAMES.glob(spec["prefix"] + "_*.png"):
            idx = int(old.stem.split("_")[-1])
            if idx >= len(frames):
                old.unlink()
                meta_file = old.with_suffix(".png.meta")
                if meta_file.exists():
                    meta_file.unlink()
        for i, f in enumerate(frames):
            g = f.copy()
            g[..., 3] *= m
            g = g[v0:v1, u0:u1]
            Image.fromarray(np.clip(g.round(), 0, 255).astype(np.uint8), "RGBA").save(FRAMES / ("%s_%03d.png" % (spec["prefix"], i)))
        size = ((u1 - u0) * s, (v1 - v0) * s)
        center = (ox + (u0 + u1) / 2 * s, oy - (v0 + v1) / 2 * s)
        core_page = (ox + c[0] * s, oy - c[3] * s, ox + c[2] * s, oy - c[1] * s)
        out["clips"].append({
            "prefix": spec["prefix"], "title": spec["title"], "description": spec["description"],
            "source": "processing/ice_spells/fix_v1/" + spec["source"],
            "frames": len(frames), "delays": meta["delays"], "duration": meta["duration"],
            "canvas": [W, H], "crop": [int(u0), int(v0), int(u1 - u0), int(v1 - v0)],
            "scale": round(s, 4), "core": [int(v) for v in c],
            "artCenter": [round(center[0], 2), round(center[1], 2)], "artSize": [round(size[0], 2), round(size[1], 2)],
        })
        print("%-13s core %s -> %.0fx%.0f page px (A %.0fx%.0f), scale %.3f, crop %s, art centre %s size %s" % (
            spec["prefix"], c, cw * s, ch * s, aw, ah, s, (u0, v0, u1, v1),
            [round(v) for v in center], [round(v) for v in size]))
    CLIPS.write_bytes(json.dumps(out, indent=1).encode("utf-8"))


if __name__ == "__main__":
    main()
