"""One muse-image-1.0 request, same endpoint/params as test_magenta_pipeline.sh.
Key comes only from META_MODEL_API_KEY; it is never printed or written."""
import base64
import json
import os
import sys
import urllib.error
import urllib.request
from pathlib import Path

here = Path(__file__).parent
prompt = " ".join((here / (sys.argv[1] if len(sys.argv) > 1 else "prompt.txt")).read_text(encoding="utf-8").split())
body = json.dumps({
    "model": "muse-image-1.0",
    "prompt": prompt,
    "size": os.environ.get("MUSE_SIZE", "1024x1024"),
    "n": 1,
    "response_format": "b64_json",
    "output_format": "png",
}).encode("utf-8")
req = urllib.request.Request(
    "https://api.meta.ai/v1/images/generations", data=body, method="POST",
    headers={"Authorization": "Bearer " + os.environ["META_MODEL_API_KEY"],
             "Content-Type": "application/json"})
try:
    with urllib.request.urlopen(req, timeout=170) as r:
        d = json.load(r)
except urllib.error.HTTPError as e:
    print("HTTP", e.code, e.read()[:500].decode("utf-8", "replace"))
    sys.exit(1)
out = here / (sys.argv[2] if len(sys.argv) > 2 else "ice_storm_raw.png")
out.write_bytes(base64.b64decode(d["data"][0]["b64_json"]))
print("saved", out, "prompt chars:", len(prompt))
