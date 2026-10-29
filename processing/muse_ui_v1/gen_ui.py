"""Generate every entry of processing/ui_prompts.json with muse-image-1.0 (skips existing files).
Usage: python gen_ui.py [names...]   (key from META_MODEL_API_KEY only)"""
import base64, json, os, sys, urllib.error, urllib.request
from pathlib import Path
here = Path(__file__).parent
spec = json.loads(Path(r"C:\Users\Mefisto\ARHeroes-Spellbook-Demo\processing\ui_prompts.json").read_text(encoding="utf-8"))
style = spec.pop("_style")
names = sys.argv[1:] or list(spec)
for name in names:
    out = here / "raw" / f"{name}{os.environ.get('SUFFIX', '')}.png"
    if out.exists() and not sys.argv[1:]:
        continue
    item = spec[name]
    body = json.dumps({"model": "muse-image-1.0", "prompt": item["prompt"] + " " + item.get("style", style), "size": item["size"],
                       "n": 1, "response_format": "b64_json", "output_format": "png"}).encode()
    req = urllib.request.Request("https://api.meta.ai/v1/images/generations", data=body, method="POST",
                                 headers={"Authorization": "Bearer " + os.environ["META_MODEL_API_KEY"], "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=200) as r:
            d = json.load(r)
        out.write_bytes(base64.b64decode(d["data"][0]["b64_json"]))
        print(name, "ok", flush=True)
    except urllib.error.HTTPError as e:
        print(name, "HTTP", e.code, e.read()[:300].decode("utf-8", "replace"), flush=True)
    except Exception as e:
        print(name, "ERR", type(e).__name__, str(e)[:120], flush=True)
