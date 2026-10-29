"""Splits the provided decor art into page elements (no new images are generated).

Sources (copies of the user's files; the originals in Downloads are not touched):
  processing/decor_provided/medallion_composition_src.png  - ink etching, 4 holes, orbits, emblem, cities
  processing/decor_provided/title_frame_src.png            - ink ribbon frame for the ICE MAGIC title

The composition cannot be placed whole: its holes are 234/254 px apart horizontally and ~290 px
vertically with r ~62, while the medallions are 500 x 570 page px apart with an art window r 162.
Scaling the holes to the windows (x2.61) would make the sheet 1850 px wide. So each hole becomes its
own tile (its orbits, beads and stars, clipped to its quadrant and without the shared centre emblem),
placed on its medallion's actual centre; the emblem, arms, cities and stars are separate elements.

Writes Assets/Spellbook/UI/UI_Decor*.png, UI_TitleRibbon.png and processing/decor_provided/decor_v2.json
(pixel geometry the scene builder uses).
"""
import json
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "processing" / "decor_provided"
UI = ROOT / "Assets" / "Spellbook" / "UI"

# Measured on the source (processing/decor_provided, max inscribed transparent circle per hole)
HOLES = [(259.9, 229.7), (494.0, 229.1), (250.5, 522.9), (503.9, 515.6)]   # TL, TR, BL, BR
HOLE_R = 62.0
CENTER = (377.0, 375.0)      # sun-moon emblem
EMBLEM_R = 92.0
TILE_R = 108.0


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def save(arr, name):
    Image.fromarray(np.clip(arr.round(), 0, 255).astype(np.uint8), "RGBA").save(UI / (name + ".png"))


def crop(a, x0, y0, x1, y1):
    return a[int(y0):int(y1), int(x0):int(x1)].copy(), (int(x0), int(y0))


def main():
    a = np.asarray(Image.open(SRC / "medallion_composition_src.png").convert("RGBA")).astype(np.float32)
    H, W = a.shape[:2]
    Y, X = np.mgrid[0:H, 0:W].astype(np.float32)
    geo = {"source": "medallion_composition_src.png", "hole_r": HOLE_R, "elements": {}}

    def emit(name, m, box):
        x0, y0, x1, y1 = [int(v) for v in box]
        out = a[y0:y1, x0:x1].copy()
        out[..., 3] *= m[y0:y1, x0:x1]
        save(out, name)
        geo["elements"][name] = {"box": [x0, y0, x1, y1]}

    d_center = np.hypot(X - CENTER[0], Y - CENTER[1])
    no_emblem = smoothstep(EMBLEM_R, EMBLEM_R + 6, d_center)
    # Большой рунный круг (линии на r ~274 и ~305 от центра, руны между ними) не повторяется кусками
    inside_ring = 1 - smoothstep(256, 266, d_center)
    outside_ring = smoothstep(326, 336, d_center)

    # 1. Орбиты вокруг каждого отверстия: только свой квадрант, без центральной эмблемы
    for i, (cx, cy) in enumerate(HOLES):
        d = np.hypot(X - cx, Y - cy)
        m = 1 - smoothstep(TILE_R - 16, TILE_R, d)
        m *= smoothstep(360, 354, X) if cx < CENTER[0] else smoothstep(394, 400, X)
        m *= smoothstep(361, 355, Y) if cy < CENTER[1] else smoothstep(389, 395, Y)
        m *= no_emblem * inside_ring
        emit("UI_DecorOrbit%d" % i, m, (cx - TILE_R, cy - TILE_R, cx + TILE_R, cy + TILE_R))
        geo["elements"]["UI_DecorOrbit%d" % i]["hole"] = [cx, cy]

    # 2. Эмблема солнца и луны в центре креста
    m = 1 - smoothstep(EMBLEM_R - 10, EMBLEM_R, d_center)
    emit("UI_DecorEmblem", m, (CENTER[0] - EMBLEM_R, CENTER[1] - EMBLEM_R, CENTER[0] + EMBLEM_R, CENTER[1] + EMBLEM_R))

    # 3. Лучи креста вверх и вниз от эмблемы (узкая полоса по оси, мягкие концы)
    band = 1 - smoothstep(10, 15, np.abs(X - CENTER[0]))
    emit("UI_DecorArmUp", band * smoothstep(140, 156, Y) * (1 - smoothstep(276, 290, Y)), (CENTER[0] - 15, 140, CENTER[0] + 15, 290))
    emit("UI_DecorArmDown", band * smoothstep(462, 476, Y) * (1 - smoothstep(586, 600, Y)), (CENTER[0] - 15, 462, CENTER[0] + 15, 600))

    # 4. Города: всё, что снаружи рунного круга, в нижних углах
    city_l = outside_ring * smoothstep(500, 512, Y) * (1 - smoothstep(352, 362, X))
    emit("UI_DecorCityL", city_l, (10, 500, 362, 795))
    city_r = outside_ring * smoothstep(500, 512, Y) * smoothstep(432, 442, X)
    emit("UI_DecorCityR", city_r, (432, 500, 708, 795))

    # 5. Чаша с пламенем
    m = smoothstep(338, 346, X) * (1 - smoothstep(416, 424, X)) * smoothstep(604, 612, Y)
    emit("UI_DecorChalice", m, (338, 604, 424, 792))

    # 6. Звезда-компас и столбик планет
    d = np.hypot(X - 130, Y - 118)
    emit("UI_DecorCompass", 1 - smoothstep(54, 64, d), (66, 54, 194, 182))
    m = smoothstep(72, 78, X) * (1 - smoothstep(122, 128, X)) * smoothstep(165, 172, Y) * (1 - smoothstep(333, 340, Y))
    emit("UI_DecorPlanets", m, (72, 165, 128, 340))

    # 7. Лента заголовка: видимая часть, чёрные чернила -> коричневые чернила страницы
    t = np.asarray(Image.open(SRC / "title_frame_src.png").convert("RGBA")).astype(np.float32)
    al = t[..., 3]
    ys, xs = np.nonzero(al > 8)
    x0, y0, x1, y1 = xs.min() - 4, ys.min() - 4, xs.max() + 5, ys.max() + 5
    t = t[y0:y1, x0:x1]
    ink = np.array([52.0, 30.0, 15.0])
    t[..., :3] = ink + t[..., :3] * np.array([0.95, 0.85, 0.75])
    save(t, "UI_TitleRibbon")
    # Свободное поле ленты: строки центральной колонки между верхней и нижней кромкой
    col = t[:, t.shape[1] // 2, 3]
    edges = np.nonzero(col > 120)[0]
    top_edge = edges[edges < t.shape[0] // 2].max() if (edges < t.shape[0] // 2).any() else 0
    bottom_edge = edges[edges > t.shape[0] // 2].min() if (edges > t.shape[0] // 2).any() else t.shape[0]
    # Ширина поля: между внутренними завитками на средней строке поля
    mid = (top_edge + bottom_edge) // 2
    row = t[mid, :, 3]
    inner = np.nonzero(row > 120)[0]
    left_in = inner[inner < t.shape[1] // 2].max() if (inner < t.shape[1] // 2).any() else 0
    right_in = inner[inner > t.shape[1] // 2].min() if (inner > t.shape[1] // 2).any() else t.shape[1]
    geo["ribbon"] = {"size": [int(t.shape[1]), int(t.shape[0])], "band_top": int(top_edge),
                     "band_bottom": int(bottom_edge), "band_left": int(left_in), "band_right": int(right_in)}

    (SRC / "decor_v2.json").write_bytes(json.dumps(geo, indent=1).encode("utf-8"))
    print(json.dumps(geo["ribbon"]), {k: v["box"] for k, v in geo["elements"].items()})


if __name__ == "__main__":
    main()
