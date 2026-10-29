"""One muse-image-1.0 image edit request with several input images (multipart /v1/images/edits).
Key comes only from META_MODEL_API_KEY; it is never printed or written.
Usage: python edit2.py <a.png,b.png> <prompt.txt> <out.png>"""
import base64
import json
import os
import sys
import urllib.error
import urllib.request
import uuid
from pathlib import Path

here = Path(__file__).parent
srcs, prompt_file, out_name = sys.argv[1].split(","), sys.argv[2], sys.argv[3]
prompt = " ".join((here / prompt_file).read_text(encoding="utf-8").split())
fields = {
    "model": "muse-image-1.0",
    "prompt": prompt,
    "size": os.environ.get("MUSE_SIZE", "1536x1024"),
    "n": "1",
    "response_format": "b64_json",
    "output_format": "png",
}
CRLF = "\r\n"
boundary = uuid.uuid4().hex
parts = []
for k, v in fields.items():
    parts.append(f'--{boundary}{CRLF}Content-Disposition: form-data; name="{k}"{CRLF}{CRLF}{v}{CRLF}'.encode())
# the API wants indexed keys image[0], image[1], ...; "image[]" is rejected
for i, src in enumerate(srcs):
    head = (f'--{boundary}{CRLF}Content-Disposition: form-data; name="image[{i}]"; '
            f'filename="image{i + 1}.png"{CRLF}Content-Type: image/png{CRLF}{CRLF}')
    parts.append(head.encode() + (here / src).read_bytes() + CRLF.encode())
parts.append(f"--{boundary}--{CRLF}".encode())
req = urllib.request.Request(
    "https://api.meta.ai/v1/images/edits", data=b"".join(parts), method="POST",
    headers={"Authorization": "Bearer " + os.environ["META_MODEL_API_KEY"],
             "Content-Type": f"multipart/form-data; boundary={boundary}"})
try:
    with urllib.request.urlopen(req, timeout=300) as r:
        d = json.load(r)
except urllib.error.HTTPError as e:
    print("HTTP", e.code, e.read()[:600].decode("utf-8", "replace"))
    sys.exit(1)
(here / out_name).write_bytes(base64.b64decode(d["data"][0]["b64_json"]))
print("saved", here / out_name, "images sent:", len(srcs), "prompt chars:", len(prompt),
      "usage:", json.dumps(d.get("usage", {}))[:300])
