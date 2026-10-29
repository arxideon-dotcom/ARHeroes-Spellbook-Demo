"""Shrink an image onto a solid #00FF00 canvas of the same size so a cut-off
part (breath tip, comet tail) gets room to be completed by an edit request.
Usage: python pad_canvas.py <in.png> <out.png> <scale> <anchor_x 0..1> <anchor_y 0..1>"""
import sys
from PIL import Image
src, out, s, ax, ay = sys.argv[1], sys.argv[2], float(sys.argv[3]), float(sys.argv[4]), float(sys.argv[5])
im = Image.open(src).convert("RGB")
w, h = im.size
small = im.resize((round(w * s), round(h * s)), Image.LANCZOS)
canvas = Image.new("RGB", (w, h), (0, 255, 0))
canvas.paste(small, (round((w - small.width) * ax), round((h - small.height) * ay)))
canvas.save(out)
