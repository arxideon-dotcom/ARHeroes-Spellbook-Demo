"""One muse-image-1.0 request to POST /v1/images/edits (OpenAI-style multipart).

Every input image is sent as an `image` part (the API takes a list for
multi-image composition / style reference). The key is read from
META_MODEL_API_KEY (process env, then the Windows User scope); it is never
printed or written anywhere.

Each call writes <out>.png plus <out>.json (model, size, inputs, prompt,
usage) so the hackathon provenance stays next to the result.

Usage:
  python muse_edit.py <prompt.txt> <out.png> <input.png> [<input2.png> ...]
  env MUSE_SIZE (default 1024x1024)
"""
import base64
import json
import os
import sys
import urllib.error
import urllib.request
import uuid
from pathlib import Path

MODEL = "muse-image-1.0"
URL = "https://api.meta.ai/v1/images/edits"


def api_key() -> str:
    key = os.environ.get("META_MODEL_API_KEY")
    if not key and os.name == "nt":
        import winreg
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, "Environment") as k:
            key = winreg.QueryValueEx(k, "META_MODEL_API_KEY")[0]
    if not key:
        sys.exit("META_MODEL_API_KEY is not set")
    return key


def main() -> None:
    prompt_file, out = Path(sys.argv[1]), Path(sys.argv[2])
    inputs = [Path(p) for p in sys.argv[3:]]
    prompt = " ".join(prompt_file.read_text(encoding="utf-8").split())
    size = os.environ.get("MUSE_SIZE", "1024x1024")
    fields = {"model": MODEL, "prompt": prompt, "size": size, "n": "1",
              "response_format": "b64_json", "output_format": "png"}
    b = uuid.uuid4().hex
    parts = [f'--{b}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode()
             for k, v in fields.items()]
    for i, p in enumerate(inputs):
        parts.append((f'--{b}\r\nContent-Disposition: form-data; name="image"; filename="input{i}.png"\r\n'
                      'Content-Type: image/png\r\n\r\n').encode() + p.read_bytes() + b"\r\n")
    parts.append(f"--{b}--\r\n".encode())
    req = urllib.request.Request(URL, data=b"".join(parts), method="POST", headers={
        "Authorization": "Bearer " + api_key(),
        "Content-Type": f"multipart/form-data; boundary={b}"})
    try:
        with urllib.request.urlopen(req, timeout=300) as r:
            d = json.load(r)
    except urllib.error.HTTPError as e:
        print("HTTP", e.code, e.read()[:600].decode("utf-8", "replace"))
        sys.exit(1)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(base64.b64decode(d["data"][0]["b64_json"]))
    meta = {"model": MODEL, "endpoint": URL, "size": size,
            "inputs": [p.name for p in inputs], "prompt_file": prompt_file.name,
            "prompt": prompt, "usage": d.get("usage")}
    out.with_suffix(".json").write_text(json.dumps(meta, indent=2), encoding="utf-8")
    print("saved", out)


if __name__ == "__main__":
    main()
