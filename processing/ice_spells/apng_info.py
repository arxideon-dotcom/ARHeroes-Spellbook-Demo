"""Print the APNG structure: canvas, acTL, every fcTL (size, offset, delay,
dispose_op, blend_op). Usage: python apng_info.py <file.apng> [...]"""
import struct
import sys
import zlib

DISPOSE = {0: "none", 1: "background", 2: "previous"}
BLEND = {0: "source", 1: "over"}


def chunks(data):
    pos = 8
    while pos < len(data):
        n, = struct.unpack(">I", data[pos:pos + 4])
        typ = data[pos + 4:pos + 8].decode("ascii")
        yield typ, data[pos + 8:pos + 8 + n]
        pos += 12 + n


for path in sys.argv[1:]:
    data = open(path, "rb").read()
    print("==", path, len(data), "bytes")
    frames, total = [], 0.0
    idat_before_fctl = False
    seen_fctl = False
    for typ, body in chunks(data):
        if typ == "IHDR":
            w, h, depth, ctype = struct.unpack(">IIBB", body[:10])
            print(f"canvas {w}x{h} depth {depth} colortype {ctype}")
        elif typ == "acTL":
            nf, plays = struct.unpack(">II", body)
            print(f"acTL frames={nf} plays={plays}")
        elif typ == "fcTL":
            seen_fctl = True
            seq, fw, fh, fx, fy, dn, dd, dop, bop = struct.unpack(">IIIIIHHBB", body)
            delay = dn / (dd or 100)
            total += delay
            frames.append((fw, fh, fx, fy, round(delay, 4), DISPOSE[dop], BLEND[bop]))
        elif typ == "IDAT" and not seen_fctl:
            idat_before_fctl = True
    print("default image is NOT part of the animation" if idat_before_fctl else "default image = frame 0")
    delays = sorted({f[4] for f in frames})
    print(f"fcTL count {len(frames)}, total {total:.3f} s, delays {delays}")
    print("dispose ops", sorted({f[5] for f in frames}), "blend ops", sorted({f[6] for f in frames}))
    partial = [i for i, f in enumerate(frames) if (f[0], f[1], f[2], f[3]) != (w, h, 0, 0)]
    print("partial-region frames:", len(partial), partial[:12])
    for i, f in enumerate(frames[:3] + frames[-2:]):
        pass
    print("first:", frames[0], "last:", frames[-1])
