"""Extract full composited frames from the provided APNG animations.

Composition follows the APNG spec for every fcTL: the frame region is
written at (x_offset, y_offset) with blend_op SOURCE (replace, alpha
included) or OVER (alpha-composite), then dispose_op NONE / BACKGROUND
(clear the region to transparent) / PREVIOUS (restore the canvas) is
applied before the next frame. The result is checked pixel for pixel
against Pillow's own APNG compositor.

All frames of one animation are cropped to the same rectangle: the union
of every frame's visible pixels plus a small transparent pad. The crop
removes only empty canvas, so frames stay aligned and proportions are not
changed. Delays are kept per frame exactly as stored in the APNG.

Writes:
  Assets/Spellbook/Frames/<Prefix>_NNN.png
  Assets/Spellbook/Art/SpellClips.json  (per spell: frames, per-frame delays,
                                        source file, canvas and crop)

Usage: python extract_apng.py
"""
import json
import struct
import zlib
from io import BytesIO
from pathlib import Path

import numpy as np
from PIL import Image

HERE = Path(__file__).parent
ROOT = HERE.parent.parent
FRAMES = ROOT / "Assets/Spellbook/Frames"
CLIPS = ROOT / "Assets/Spellbook/Art/SpellClips.json"
PAD = 4

# Prefix -> provided file (copied unchanged from Downloads into processing/ice_spells/apng)
SPELLS = [
    ("FrostBreath", "Frost Breath", "icedragon-v1.apng",
     "An icy dragon exhales a torrent of freezing breath."),
    ("IceComet", "Ice Comet", "icecomet-v1.apng",
     "A frozen comet streaks through the air, trailing ice shards."),
    ("AbsoluteZero", "Absolute Zero", "frozenknight-shatter.apng",
     "Extreme cold freezes a knight until the figure shatters."),
]


def chunks(data):
    pos = 8
    while pos < len(data):
        n, = struct.unpack(">I", data[pos:pos + 4])
        yield data[pos + 4:pos + 8].decode("ascii"), data[pos + 8:pos + 8 + n]
        pos += 12 + n


def png_from(ihdr, w, h, idat, extra):
    """A standalone PNG for one frame's data (IHDR with the frame size, shared PLTE/tRNS)."""
    def chunk(t, body):
        return struct.pack(">I", len(body)) + t + body + struct.pack(">I", zlib.crc32(t + body) & 0xFFFFFFFF)
    head = struct.pack(">II", w, h) + ihdr[8:]
    out = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", head)
    for t, body in extra:
        out += chunk(t.encode(), body)
    out += chunk(b"IDAT", idat) + chunk(b"IEND", b"")
    return Image.open(BytesIO(out)).convert("RGBA")


def composite(path: Path):
    data = path.read_bytes()
    ihdr, extra, fctl, frames = None, [], None, []
    pending = []  # (fctl, idat bytes)
    cur = None
    for typ, body in chunks(data):
        if typ == "IHDR":
            ihdr = body
        elif typ in ("PLTE", "tRNS"):
            extra.append((typ, body))
        elif typ == "fcTL":
            if cur is not None:
                pending.append(cur)
            seq, fw, fh, fx, fy, dn, dd, dop, bop = struct.unpack(">IIIIIHHBB", body)
            cur = [dict(w=fw, h=fh, x=fx, y=fy, num=dn, den=dd or 100, dispose=dop, blend=bop), b""]
        elif typ == "IDAT" and cur is not None:
            cur[1] += body
        elif typ == "fdAT":
            cur[1] += body[4:]
    pending.append(cur)

    W, H = struct.unpack(">II", ihdr[:8])
    canvas = np.zeros((H, W, 4), np.float32)
    for i, (f, idat) in enumerate(pending):
        if i == 0 and f["dispose"] == 2:
            f["dispose"] = 1  # spec: PREVIOUS on the first frame is treated as BACKGROUND
        before = canvas.copy()
        region = np.asarray(png_from(ihdr, f["w"], f["h"], idat, extra)).astype(np.float32)
        y0, x0 = f["y"], f["x"]
        dst = canvas[y0:y0 + f["h"], x0:x0 + f["w"]]
        if f["blend"] == 0:
            dst[...] = region
        else:
            sa, da = region[..., 3:4] / 255, dst[..., 3:4] / 255
            oa = sa + da * (1 - sa)
            rgb = (region[..., :3] * sa + dst[..., :3] * da * (1 - sa)) / np.maximum(oa, 1e-6)
            dst[...] = np.concatenate([rgb, oa * 255], -1)
        frames.append((np.clip(canvas.round(), 0, 255).astype(np.uint8), f["num"] / f["den"], f))
        if f["dispose"] == 1:
            canvas[y0:y0 + f["h"], x0:x0 + f["w"]] = 0
        elif f["dispose"] == 2:
            canvas = before
    return (W, H), frames


def pillow_frames(path: Path):
    im = Image.open(path)
    out = []
    for i in range(im.n_frames):
        im.seek(i)
        out.append(np.asarray(im.convert("RGBA")))
    return out


def main():
    FRAMES.mkdir(parents=True, exist_ok=True)
    clips = []
    for prefix, title, file, description in SPELLS:
        src = HERE / "apng" / file
        (W, H), frames = composite(src)
        ref = pillow_frames(src)
        # Fully transparent pixels may differ only in their (invisible) colour
        mism = 0
        for (a, _, _), b in zip(frames, ref):
            va, vb = a[..., 3] > 0, b[..., 3] > 0
            mism += int((va != vb).sum() + (np.abs(a[va].astype(int) - b[va].astype(int)) > 1).any(-1).sum())
        assert len(frames) == len(ref), (len(frames), len(ref))

        union = np.zeros((H, W), bool)
        for a, _, _ in frames:
            union |= a[..., 3] > 0
        ys, xs = np.nonzero(union)
        x0, y0 = max(0, xs.min() - PAD), max(0, ys.min() - PAD)
        x1, y1 = min(W, xs.max() + 1 + PAD), min(H, ys.max() + 1 + PAD)
        touches = [s for s, t in (("left", xs.min() == 0), ("top", ys.min() == 0),
                                  ("right", xs.max() == W - 1), ("bottom", ys.max() == H - 1)) if t]

        for old in FRAMES.glob(prefix + "_*.png"):
            old.unlink()
        for i, (a, _, _) in enumerate(frames):
            Image.fromarray(a[y0:y1, x0:x1], "RGBA").save(FRAMES / f"{prefix}_{i:03d}.png", optimize=False)

        delays = [round(d, 6) for _, d, _ in frames]
        partial_alpha = int(sum(((a[..., 3] > 0) & (a[..., 3] < 255)).sum() for a, _, _ in frames))
        clips.append(dict(prefix=prefix, title=title, description=description, source=file,
                          frames=len(frames), delays=delays, duration=round(sum(delays), 6),
                          canvas=[W, H], crop=[int(x0), int(y0), int(x1), int(y1)]))
        print(f"{prefix}: {len(frames)} frames, {sum(delays):.3f} s, delays {sorted(set(delays))}, "
              f"canvas {W}x{H}, crop {x0},{y0}-{x1},{y1} ({x1 - x0}x{y1 - y0}), "
              f"content touches canvas edge: {touches or 'no'}, semi-transparent px {partial_alpha}, "
              f"pixels differing from Pillow: {mism}")
    CLIPS.write_text(json.dumps({"clips": clips}, indent=2), encoding="utf-8")
    print("wrote", CLIPS)


if __name__ == "__main__":
    main()
