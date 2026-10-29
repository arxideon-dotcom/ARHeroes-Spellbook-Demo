"""Name ribbons from ONE Muse generation (raw/NameRibbonWide.png): the wide one as is, the compact one
from the same ends with a shorter straight middle (cut at the centre, joined with a soft seam).
raw/NameRibbonCompact.png came out at the same length as the wide one, so it is kept only as a record.
Writes Assets/Spellbook/UI/UI_V2_NameRibbonWide.png, UI_V2_NameRibbonCompact.png and the measured face
boxes into layout.json (key "NameRibbon")."""
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter
from make_grimoire_v2 import key_ink, trim, save, UI, HERE


def face(img):
    """Empty face: rows/cols between the inner outlines, measured on the middle column / row."""
    al = img[..., 3]
    col = al[:, img.shape[1] // 2]
    ink = np.nonzero(col > 90)[0]
    mid = img.shape[0] // 2
    top, bot = ink[ink < mid].max(), ink[ink > mid].min()
    row = al[(top + bot) // 2]
    inner = np.nonzero(row > 90)[0]
    left = inner[inner < img.shape[1] // 2].max()
    right = inner[inner > img.shape[1] // 2].min()
    return int(left), int(top), int(right), int(bot)


def main():
    wide = trim(key_ink("NameRibbonWide"))
    save(wide, "UI_V2_NameRibbonWide")
    W = wide.shape[1]
    # компактная: вырезаем 5 % ширины из середины прямой части, шов — мягкий переход 24 px
    cut = int(W * 0.05)
    a0 = W // 2 - cut // 2
    left, right = wide[:, :a0 + 12], wide[:, a0 + cut - 12:]
    comp = np.zeros((wide.shape[0], left.shape[1] + right.shape[1] - 24, 4), np.float32)
    comp[:, :left.shape[1]] = left
    w = np.linspace(0, 1, 24)[None, :, None]
    x0 = left.shape[1] - 24
    comp[:, x0:x0 + 24] = left[:, -24:] * (1 - w) + right[:, :24] * w
    comp[:, x0 + 24:] = right[:, 24:]
    # На медальоне лента уменьшается примерно в 4 раза: штрих плотнее и чуть толще, чтобы контур не таял.
    # Только альфа и тон туши этого спрайта; страница и остальной декор не трогаются
    al = Image.fromarray(np.clip(comp[..., 3], 0, 255).astype(np.uint8))
    thick = np.asarray(al.filter(ImageFilter.MaxFilter(3))).astype(np.float32)
    a = comp[..., 3] / 255.0
    a = 1 - (1 - a) ** 2
    comp[..., 3] = np.clip(np.maximum(a * 255, thick * 0.75), 0, 255)
    comp[..., :3] = comp[..., :3] * 0.8
    save(comp, "UI_V2_NameRibbonCompact")
    lay = json.loads((HERE / "layout.json").read_text(encoding="utf-8"))
    lay["NameRibbon"] = {"wide": {"size": [W, wide.shape[0]], "face": face(wide)},
                         "compact": {"size": [comp.shape[1], comp.shape[0]], "face": face(comp)}}
    (HERE / "layout.json").write_bytes(json.dumps(lay, indent=1).encode("utf-8"))
    print(lay["NameRibbon"])


if __name__ == "__main__":
    main()
