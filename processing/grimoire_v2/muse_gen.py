"""Generate the grimoire v2 decoration with muse-image-1.0 (Meta Model API).

Items in prompts.json with "ref" go to POST /v1/images/edits with the reference crop(s) from ref/
as style input (multipart, every input is an `image` part); the rest go to /v1/images/generations.
Each result is saved as raw/<name>.png plus raw/<name>.json (model, endpoint, size, prompt, inputs,
usage, time). Existing results are skipped unless named on the command line.

The key comes from META_MODEL_API_KEY (process env, then the Windows User scope) and is never printed
or written. On a billing, quota or rate-limit error all further requests stop.

Usage: python muse_gen.py [names...]
"""
import base64
import json
import os
import sys
import time
import urllib.error
import urllib.request
import uuid
from pathlib import Path

HERE = Path(__file__).resolve().parent
MODEL = "muse-image-1.0"
BASE = "https://api.meta.ai/v1/images/"


def api_key():
    key = os.environ.get("META_MODEL_API_KEY")
    if not key and os.name == "nt":
        import winreg
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, "Environment") as k:
            key = winreg.QueryValueEx(k, "META_MODEL_API_KEY")[0]
    if not key:
        sys.exit("META_MODEL_API_KEY is not set")
    return key


def request(endpoint, prompt, size, refs, key):
    fields = {"model": MODEL, "prompt": prompt, "size": size, "n": "1",
              "response_format": "b64_json", "output_format": "png"}
    if refs:
        b = uuid.uuid4().hex
        parts = [f'--{b}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode() for k, v in fields.items()]
        for i, p in enumerate(refs):
            parts.append((f'--{b}\r\nContent-Disposition: form-data; name="image"; filename="ref{i}.png"\r\n'
                          'Content-Type: image/png\r\n\r\n').encode() + p.read_bytes() + b"\r\n")
        parts.append(f"--{b}--\r\n".encode())
        data, ctype = b"".join(parts), f"multipart/form-data; boundary={b}"
    else:
        fields["n"] = 1
        data, ctype = json.dumps(fields).encode(), "application/json"
    req = urllib.request.Request(BASE + endpoint, data=data, method="POST",
                                 headers={"Authorization": "Bearer " + key, "Content-Type": ctype})
    with urllib.request.urlopen(req, timeout=300) as r:
        return json.load(r)


def main():
    spec = json.loads((HERE / "prompts.json").read_text(encoding="utf-8"))
    items = spec["items"]
    names = sys.argv[1:] or list(items)
    key = api_key()
    (HERE / "raw").mkdir(exist_ok=True)
    for name in names:
        item = items[name]
        out = HERE / "raw" / (name + ".png")
        if out.exists() and not sys.argv[1:]:
            print(name, "exists, skipped", flush=True)
            continue
        refs = [HERE / "ref" / r for r in item.get("ref", [])]
        if item.get("glow"):
            prompt = item["prompt"] + " " + spec["_glow"]
        elif refs:
            prompt = spec["_ref"] + " " + item["prompt"] + " " + spec["_ink"]
        else:
            prompt = item["prompt"] + " " + spec["_ink"]
        endpoint = "edits" if refs else "generations"
        t0 = time.time()
        try:
            d = request(endpoint, prompt, item["size"], refs, key)
        except urllib.error.HTTPError as e:
            body = e.read()[:400].decode("utf-8", "replace")
            print(name, "HTTP", e.code, body, flush=True)
            if e.code in (402, 403, 429) or any(w in body.lower() for w in ("billing", "credit", "quota", "payment", "balance")):
                print("STOP: billing/quota/rate-limit error, no further requests", flush=True)
                sys.exit(2)
            continue
        except Exception as e:
            print(name, "ERR", type(e).__name__, str(e)[:200], flush=True)
            continue
        out.write_bytes(base64.b64decode(d["data"][0]["b64_json"]))
        meta = {"model": MODEL, "endpoint": BASE + endpoint, "size": item["size"], "inputs": [r.name for r in refs],
                "prompt": prompt, "usage": d.get("usage"), "seconds": round(time.time() - t0, 1),
                "time": time.strftime("%Y-%m-%d %H:%M:%S")}
        out.with_suffix(".json").write_bytes(json.dumps(meta, indent=1).encode("utf-8"))
        print(name, "ok", endpoint, meta["seconds"], "s", flush=True)


if __name__ == "__main__":
    main()
