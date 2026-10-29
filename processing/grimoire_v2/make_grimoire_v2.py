"""Turns the Muse generations in raw/ into Unity sprites (processing only: keying, cleanup, crops, export).

Ink items (on flat magenta): keyed with processing/process_spell.py, then recoloured by brightness onto
one sepia scale (dark ink -> pale parchment), which removes the violet tint the magenta key leaves in
thin strokes; genuine pale ice-blue accents (G and B above R) keep their colour. Then trimmed.
Glow items (on black): alpha = brightness, colour = brightness-normalised.

Writes Assets/Spellbook/UI/UI_V2_*.png and grimoire_v2/layout.json (measured geometry for the scene).
Previous sprites stay in place (and in processing/backup_pre_grimoire_v2) for restore.
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
from process_spell import process_image  # noqa: E402

UI = HERE.parents[1] / "Assets" / "Spellbook" / "UI"
MED = 512
UNDER_R = 176            # radius of the window mask on the 512 canvas (UI_CircleMask)
RIM_R = 213              # radius of the selection glow on the 512 canvas (old UI_RimGlow)
SEPIA_DARK = np.array([40.0, 22.0, 10.0])
SEPIA_LIGHT = np.array([238.0, 218.0, 178.0])


def key_ink(name):
    img = np.asarray(process_image(HERE / "raw" / (name + ".png"), "magenta")).astype(np.float32)
    r, g, b, a = img[..., 0], img[..., 1], img[..., 2], img[..., 3]
    # Тушь: цвет по яркости на сепийной шкале (тёмная тушь -> светлый пергамент), так от пурпурного фона
    # не остаётся фиолетовой каймы; настоящие бледно-голубые акценты (G и B выше R) сохраняются
    ice = (b > r + 20) & (g > r + 5)
    lum = 0.30 * r + 0.59 * g + 0.11 * b
    t = np.clip(lum / 235.0, 0, 1)[..., None]
    sepia = SEPIA_DARK * (1 - t) + SEPIA_LIGHT * t
    img[..., :3] = np.where(ice[..., None], img[..., :3], sepia)
    # Светлые заливки (бумага внутри рисунка) не рисуются поверх страницы: прозрачность по тёмности,
    # так сквозь них виден пергамент книги, а штрих и штриховка остаются
    dark = np.clip((246.0 - lum) / 80.0, 0, 1)
    a = np.where(ice, a, a * dark)
    img[..., 3] = np.where(a < 10, 0, a)
    return img


def trim(img, pad=6):
    ys, xs = np.nonzero(img[..., 3] > 8)
    x0, y0 = max(0, xs.min() - pad), max(0, ys.min() - pad)
    x1, y1 = min(img.shape[1], xs.max() + pad + 1), min(img.shape[0], ys.max() + pad + 1)
    return img[y0:y1, x0:x1]


def save(img, name):
    Image.fromarray(np.clip(img.round(), 0, 255).astype(np.uint8), "RGBA").save(UI / (name + ".png"), optimize=True)


def fit_disc(img, radius, size=MED):
    """Drawing's bounding circle -> radius on a size x size canvas, centred."""
    ys, xs = np.nonzero(img[..., 3] > 16)
    cx, cy = (xs.min() + xs.max()) / 2, (ys.min() + ys.max()) / 2
    r0 = max(xs.max() - xs.min(), ys.max() - ys.min()) / 2
    s = radius / r0
    im = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGBA")
    im = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    canvas.alpha_composite(im, (round(size / 2 - cx * s), round(size / 2 - cy * s)))
    return np.asarray(canvas).astype(np.float32), s


def glow(name):
    rgb = np.asarray(Image.open(HERE / "raw" / (name + ".png")).convert("RGB")).astype(np.float32)
    lum = rgb.max(-1)
    a = np.clip((lum - 6) / 249, 0, 1)
    col = np.clip(rgb / np.maximum(lum, 1)[..., None] * 255, 0, 255)
    return np.dstack([col, a * 255])


# Подпись медальона в координатах листа относительно центра медальона (SpellbookSceneBuilder:
# лента на -250, центр поля ленты + подъём подписи) и размер чистого поля вокруг неё; орбита стоит
# в масштабе 0.40 px листа на px текстуры (OrbitScale)
LABEL_OFFSET = (3.3, -247.0)
LABEL_HALF = (152.0, 31.0)
LABEL_FEATHER = 10.0
ORBIT_SCALE = 0.40


def clear_label(img):
    """Soft rounded-rect hole in the orbit drawing under the medallion label; nothing is drawn instead."""
    h, w = img.shape[:2]
    Y, X = np.mgrid[0:h, 0:w].astype(np.float32)
    px = (X + 0.5 - w / 2) * ORBIT_SCALE - LABEL_OFFSET[0]
    py = (h / 2 - Y - 0.5) * ORBIT_SCALE - LABEL_OFFSET[1]
    r = LABEL_HALF[1]
    qx = np.maximum(np.abs(px) - (LABEL_HALF[0] - r), 0)
    d = np.hypot(qx, np.abs(py)) - r              # signed distance to a capsule around the text
    out = img.copy()
    out[..., 3] *= np.clip(d / LABEL_FEATHER, 0, 1)
    return out


def empty_radius(img):
    """Radius of the empty centre of a ring drawing (median over rays)."""
    al = img[..., 3]
    ys, xs = np.nonzero(al > 16)
    cx, cy = (xs.min() + xs.max()) / 2, (ys.min() + ys.max()) / 2
    rs = []
    for t in np.linspace(0, 2 * np.pi, 90, endpoint=False):
        r = 0
        while True:
            x, y = int(cx + r * np.cos(t)), int(cy + r * np.sin(t))
            if not (0 <= x < al.shape[1] and 0 <= y < al.shape[0]) or al[y, x] > 40:
                break
            r += 1
        rs.append(r)
    return float(np.percentile(rs, 25)), float(max(xs.max() - xs.min(), ys.max() - ys.min()) / 2)


def main():
    layout = {}
    for n in ("UnderStorm", "UnderFrostBreath", "UnderIceComet", "UnderAbsoluteZero"):
        img, s = fit_disc(key_ink(n), UNDER_R - 1)
        save(img, "UI_V2_" + n)
    ring = clear_label(trim(key_ink("OrbitRing")))
    r_in, r_out = empty_radius(ring)
    save(ring, "UI_V2_OrbitRing")
    layout["OrbitRing"] = {"size": [ring.shape[1], ring.shape[0]], "empty_r": r_in, "outer_r": r_out}
    for n in ("TitleRibbon", "CenterEmblem", "Connector", "CityLeft", "CityRight", "Chalice", "Compass", "Planets"):
        img = trim(key_ink(n))
        save(img, "UI_V2_" + n)
        layout[n] = {"size": [img.shape[1], img.shape[0]]}
    # Правая страница: весь холст (пропорции страницы), без обрезки — раскладка генерации сохраняется
    page = key_ink("RightPage")
    save(page, "UI_V2_RightPage")
    layout["RightPage"] = {"size": [page.shape[1], page.shape[0]]}
    rim, _ = fit_disc(glow("RimGlow"), RIM_R)
    save(rim, "UI_V2_RimGlow")
    for n in ("Sparkle", "SoftGlow"):
        save(trim(glow(n), pad=2), "UI_V2_" + n)
    # Правая страница режется на части в координатах её же генерации (1280x1920):
    # круг схемы (центр 720,580, r 445), группа у корешка и группа у внешнего поля (без круга)
    Y, X = np.mgrid[0:page.shape[0], 0:page.shape[1]].astype(np.float32)
    d = np.hypot(X - 720, Y - 580)
    circle = page.copy()
    circle[..., 3] *= np.clip((445 - d) / 12, 0, 1)
    save(circle[580 - 445:580 + 445, 720 - 445:720 + 445], "UI_V2_RightDiagram")
    outside = page.copy()
    outside[..., 3] *= np.clip((d - 448) / 10, 0, 1)
    save(trim(outside[400:1180, 20:262]), "UI_V2_RightNotesSpine")
    save(trim(outside[240:1340, 860:1280]), "UI_V2_RightNotesOuter")
    # Орбита верхнего ряда: верхняя четверть мягко гаснет, чтобы не идти за надписью заголовка
    top = ring.copy()
    yy = np.arange(top.shape[0], dtype=np.float32)[:, None] / top.shape[0]
    top[..., 3] *= np.clip((yy - 0.10) / 0.22, 0, 1)
    save(top, "UI_V2_OrbitRingTop")
    # Свободное поле ленты заголовка: строки, где на колонке 30 % ширины нет туши, между кромками
    rib = np.asarray(Image.open(UI / "UI_V2_TitleRibbon.png")).astype(np.float32)
    col = rib[:, int(rib.shape[1] * 0.3), 3]
    ink = np.nonzero(col > 90)[0]
    mid = rib.shape[0] // 2
    top_e, bot_e = ink[ink < mid].max(), ink[ink > mid].min()
    row = rib[(top_e + bot_e) // 2, :, 3]
    inner = np.nonzero(row > 90)[0]
    left_e = inner[inner < rib.shape[1] // 2].max()
    right_e = inner[inner > rib.shape[1] // 2].min()
    layout["TitleRibbon"].update({"band_top": int(top_e), "band_bottom": int(bot_e), "band_left": int(left_e), "band_right": int(right_e)})
    # Мелкие элементы на листе ужимаются в 5-12 раз, тонкий штрих при этом исчезает. Экспортируем их в
    # 3 px текстуры на px листа: штрих перед уменьшением утолщается пропорционально уменьшению
    for n, page_w in (("Chalice", 80), ("CityLeft", 280), ("CityRight", 280), ("Compass", 120), ("Planets", 44),
                      ("Connector", 27), ("CenterEmblem", 233), ("RightNotesSpine", 97)):
        im = Image.open(UI / ("UI_V2_%s.png" % n))
        w = page_w * 3
        f = im.width / w
        if f > 1.5:
            al = im.split()[3]
            k = int(round(f / 2)) * 2 + 1
            thick = al.filter(ImageFilter.MaxFilter(min(k, 9)))
            al = Image.fromarray(np.maximum(np.asarray(al), (np.asarray(thick) * 0.8).astype(np.uint8)))
            im.putalpha(al)
            im = im.resize((w, round(im.height / f)), Image.LANCZOS)
            im.save(UI / ("UI_V2_%s.png" % n), optimize=True)
        layout.setdefault(n, {})["export"] = [im.width, im.height]
    (HERE / "layout.json").write_bytes(json.dumps(layout, indent=1).encode("utf-8"))
    print(json.dumps(layout))


if __name__ == "__main__":
    main()
