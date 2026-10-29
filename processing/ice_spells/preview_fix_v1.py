"""Before/after previews for fix_v1 on the book's parchment (stills + MP4 loops).

Writes processing/ice_spells/fix_v1/preview/. Needs imageio-ffmpeg for the MP4 files.
"""
import subprocess
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageSequence

ROOT = Path(__file__).resolve().parent
SRC = ROOT / "apng"
FIX = ROOT / "fix_v1"
OUT = FIX / "preview"
SPREAD = ROOT.parents[1] / "Assets/Spellbook/Art/Spellbook_Spread_v8.png"
FONT = ImageFont.truetype(r"C:\Windows\Fonts\arial.ttf", 22)
FILES = {"FrostBreath": "icedragon-v1", "IceComet": "icecomet-v1", "AbsoluteZero": "frozenknight-shatter"}


def frames_of(path):
    im = Image.open(path)
    out = []
    for f in ImageSequence.Iterator(im):
        out.append((f.convert("RGBA").copy(), f.info["duration"] / 1000.0))
    return out


def parchment(size):
    # кусок чистого пергамента правой страницы разворота
    p = Image.open(SPREAD).convert("RGBA").crop((900, 80, 1400, 580))
    return p.resize(size, Image.LANCZOS)


def on_parch(fr, bg):
    return Image.alpha_composite(bg, fr).convert("RGB")


def labeled(img, text, h=None):
    if h:
        img = img.resize((round(img.size[0] * h / img.size[1]), h), Image.LANCZOS)
    out = Image.new("RGB", (img.size[0], img.size[1] + 32), (28, 28, 28))
    out.paste(img, (0, 32))
    ImageDraw.Draw(out).text((8, 4), text, font=FONT, fill=(240, 240, 240))
    return out


def row(panels, title):
    W = sum(p.size[0] for p in panels) + 8 * (len(panels) - 1)
    H = max(p.size[1] for p in panels)
    out = Image.new("RGB", (W, H + 36), (16, 16, 16))
    ImageDraw.Draw(out).text((8, 6), title, font=FONT, fill=(255, 215, 120))
    x = 0
    for p in panels:
        out.paste(p, (x, 36))
        x += p.size[0] + 8
    return out


def stack(rows):
    W = max(r.size[0] for r in rows)
    out = Image.new("RGB", (W, sum(r.size[1] for r in rows) + 10 * (len(rows) - 1)), (16, 16, 16))
    y = 0
    for r in rows:
        out.paste(r, (0, y))
        y += r.size[1] + 10
    return out


def main():
    OUT.mkdir(exist_ok=True)
    src = {k: frames_of(SRC / (v + ".apng")) for k, v in FILES.items()}
    fix = {k: frames_of(FIX / (v + "_fix_v1.apng")) for k, v in FILES.items()}
    bg = {k: parchment(src[k][0][0].size) for k in FILES}

    def pair(spell, i, j=None, title="", crop=None, h=520):
        a = on_parch(src[spell][i][0], bg[spell])
        b = on_parch(fix[spell][i if j is None else j][0], bg[spell])
        if crop:
            a, b = a.crop(crop), b.crop(crop)
        return row([labeled(a, "до: кадр %d" % i, h), labeled(b, "после: кадр %d" % (i if j is None else j), h)], title)

    # 1. Края и дымка
    stack([
        pair("FrostBreath", 10, title="Frost Breath, кадр 10: срез струи справа и частиц снизу"),
        pair("FrostBreath", 22, title="Frost Breath, кадр 22: верхний и правый край"),
        pair("FrostBreath", 29, title="Frost Breath, кадр 29: тёмная дымка"),
        pair("FrostBreath", 33, title="Frost Breath, кадр 33: дымка в конце цикла"),
    ]).save(OUT / "01_frost_breath_edges_haze.png")
    stack([
        pair("AbsoluteZero", 28, title="Absolute Zero, кадр 28: начало раскалывания"),
        pair("AbsoluteZero", 31, title="Absolute Zero, кадр 31: осколки у краёв"),
        pair("AbsoluteZero", 34, title="Absolute Zero, кадр 34"),
    ]).save(OUT / "02_absolute_zero_edges.png")

    # 2. Зелёная кайма крупно (x3)
    def zoom(spell, i, box, title):
        a = on_parch(src[spell][i][0], bg[spell]).crop(box)
        b = on_parch(fix[spell][i][0], bg[spell]).crop(box)
        s = 3
        a = a.resize((a.size[0] * s, a.size[1] * s), Image.NEAREST)
        b = b.resize((b.size[0] * s, b.size[1] * s), Image.NEAREST)
        return row([labeled(a, "до"), labeled(b, "после")], title)

    stack([
        zoom("IceComet", 10, (20, 60, 220, 260), "Ice Comet, кадр 10: край хвоста (x3)"),
        zoom("IceComet", 30, (380, 380, 580, 580), "Ice Comet, кадр 30: ядро и туман (x3)"),
    ]).save(OUT / "03_green_fringe_ice_comet.png")
    stack([
        zoom("AbsoluteZero", 20, (90, 420, 290, 620), "Absolute Zero, кадр 20: лёд у ног (x3)"),
        zoom("AbsoluteZero", 30, (420, 120, 620, 320), "Absolute Zero, кадр 30: осколки (x3)"),
        zoom("FrostBreath", 10, (330, 280, 530, 480), "Frost Breath, кадр 10: край потока (x3)"),
    ]).save(OUT / "04_green_fringe_az_fb.png")

    # 3. Почему для Frost Breath нет кроссфейда: смешение кадров 35 и 0 даёт двойную голову
    a35, a0 = src["FrostBreath"][35][0], src["FrostBreath"][0][0]
    mix = Image.blend(a35, a0, 0.5)
    stack([row([labeled(on_parch(a35, bg["FrostBreath"]), "кадр 35", 420),
                labeled(on_parch(mix, bg["FrostBreath"]), "кроссфейд 50 % (отклонено)", 420),
                labeled(on_parch(a0, bg["FrostBreath"]), "кадр 0", 420)],
               "Frost Breath: голова в кадрах 35 и 0 не совпадает (сдвиг ~10 px, IoU 0.74) — смешение даёт двойной контур")]
          ).save(OUT / "05_frost_breath_crossfade_rejected.png")

    # 4. MP4: до | после на пергаменте, три цикла, настоящие задержки кадров
    import imageio_ffmpeg
    ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    FPS, PH = 30, 600
    for spell in FILES:
        A, B = src[spell], fix[spell]
        la, lb = sum(d for _, d in A), sum(d for _, d in B)
        total = 3 * max(la, lb) + 0.3
        ca = [on_parch(f, bg[spell]).resize((PH, PH), Image.LANCZOS) for f, _ in A]
        cb = [on_parch(f, bg[spell]).resize((PH, PH), Image.LANCZOS) for f, _ in B]
        sa = np.cumsum([0] + [d for _, d in A])
        sb = np.cumsum([0] + [d for _, d in B])
        W, H = 2 * PH + 16, PH + 40
        W += W % 2
        out = OUT / ("%s_before_after.mp4" % spell)
        p = subprocess.Popen([ffmpeg, "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS),
                              "-i", "-", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "16", "-preset", "slow",
                              "-movflags", "+faststart", str(out)], stdin=subprocess.PIPE, stderr=subprocess.DEVNULL)
        for k in range(int(total * FPS)):
            t = k / FPS
            ia = int(np.searchsorted(sa, t % la, side="right") - 1)
            ib = int(np.searchsorted(sb, t % lb, side="right") - 1)
            fr = Image.new("RGB", (W, H), (20, 20, 20))
            fr.paste(ca[ia], (0, 40))
            fr.paste(cb[ib], (PH + 16, 40))
            d = ImageDraw.Draw(fr)
            d.text((8, 8), "до: кадр %d/%d, цикл %.2f с" % (ia, len(A) - 1, la), font=FONT, fill=(240, 240, 240))
            d.text((PH + 24, 8), "после: кадр %d/%d, цикл %.2f с" % (ib, len(B) - 1, lb), font=FONT, fill=(240, 240, 240))
            p.stdin.write(fr.tobytes())
        p.stdin.close()
        print(out.name, p.wait(), "%.1f s" % total)


if __name__ == "__main__":
    main()
