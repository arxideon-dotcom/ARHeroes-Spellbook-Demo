"""One muse-image-1.0 image edit request (OpenAI-style multipart /v1/images/edits).
Key comes only from META_MODEL_API_KEY; it is never printed or written.
Usage: python edit.py <input.png> <prompt.txt> <out.png>"""
import base64
import json
import os
import sys
import urllib.error
import urllib.request
import uuid
from pathlib import Path

here = Path(__file__).parent
src, prompt_file, out_name = sys.argv[1], sys.argv[2], sys.argv[3]
prompt = " ".join((here / prompt_file).read_text(encoding="utf-8").split())
fields = {
    "model": "muse-image-1.0",
    "prompt": prompt,
    "size": os.environ.get("MUSE_SIZE", "1536x1024"),
    "n": "1",
    "response_format": "b64_json",
    "output_format": "png",
}
boundary = uuid.uuid4().hex
parts = []
for k, v in fields.items():
    parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode())
parts.append((f'--{boundary}\r\nContent-Disposition: form-data; name="image"; filename="input.png"\r\n'
              'Content-Type: image/png\r\n\r\n').encode() + (here / src).read_bytes() + b"\r\n")
parts.append(f"--{boundary}--\r\n".encode())
req = urllib.request.Request(
    "https://api.meta.ai/v1/images/edits", data=b"".join(parts), method="POST",
    headers={"Authorization": "Bearer " + os.environ["META_MODEL_API_KEY"],
             "Content-Type": f"multipart/form-data; boundary={boundary}"})
try:
    with urllib.request.urlopen(req, timeout=200) as r:
        d = json.load(r)
except urllib.error.HTTPError as e:
    print("HTTP", e.code, e.read()[:600].decode("utf-8", "replace"))
    sys.exit(1)
(here / out_name).write_bytes(base64.b64decode(d["data"][0]["b64_json"]))
print("saved", here / out_name, "prompt chars:", len(prompt))
