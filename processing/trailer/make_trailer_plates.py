"""Plates for the trailer sections built from saved project materials (processing only, nothing generated).

Process chain (0:35-1:00), one element — the right-page drawing:
  1 reference   grimoire_v2/ref/ref_right_page.png   (crop of the author's style reference image)
  2 Muse result grimoire_v2/raw/RightPage.png         (muse-image-1.0 /images/edits, see RightPage.json)
  3 processed   Assets/Spellbook/UI/UI_V2_RightPage.png on a checkerboard (transparent asset)
  4 in the book is shown live in Unity.
Developer work (0:55-1:15): Frost Breath frame 29 from the copied APNG vs the processed frame (fix_v1), same
  parchment and scale; Absolute Zero loop seam, original frames 35-39 -> 0 vs the processed fade/pause/appear.
Writes Assets/Spellbook/Trailer/T_*.png.
"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
G = ROOT / "processing" / "grimoire_v2"
OUT = ROOT / "Assets" / "Spellbook" / "Trailer"
sys.path.insert(0, str(G))
sys.path.insert(0, str(G.parent))
from make_grimoire_v2 import key_ink  # noqa: E402


def checker(size, c=24):
    w, h = size
    y, x = np.mgrid[0:h, 0:w]
    v = np.where(((x // c) + (y // c)) % 2 == 0, 228, 196).astype(np.uint8)
    return Image.fromarray(np.dstack([v, v, v, np.full_like(v, 255)]))


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    Image.open(G / "ref" / "ref_right_page.png").convert("RGB").save(OUT / "T_Process1_Reference.png")
    raw = Image.open(G / "raw" / "RightPage.png").convert("RGB")
    raw.resize((raw.width * 2 // 3, raw.height * 2 // 3), Image.LANCZOS).save(OUT / "T_Process2_Muse.png")
    proc = Image.open(ROOT / "Assets/Spellbook/UI/UI_V2_RightPage.png").convert("RGBA")
    proc = proc.resize((proc.width * 2 // 3, proc.height * 2 // 3), Image.LANCZOS)
    plate = checker(proc.size)
    plate.alpha_composite(proc)
    plate.convert("RGB").save(OUT / "T_Process3_Processed.png")

    spread = Image.open(ROOT / "Assets/Spellbook/Art/Spellbook_Spread_v8.png").convert("RGBA")
    # Process of one asset (0:50-1:15): the wide name ribbon — saved Muse result and the same image after keying,
    # same canvas and crop (same scale); the element itself then appears on the right page in Unity
    rawr = Image.open(G / "raw" / "NameRibbonWide.png").convert("RGBA")
    keyed = Image.fromarray(np.clip(key_ink("NameRibbonWide"), 0, 255).astype(np.uint8), "RGBA")
    box = (0, 250, rawr.width, rawr.height - 250)
    rawr.crop(box).convert("RGB").save(OUT / "T_Ribbon_Raw.png")
    chk = checker(rawr.size, 32); chk.alpha_composite(keyed)
    chk.crop(box).convert("RGB").save(OUT / "T_Ribbon_Processed.png")
    # Main shot of the generation block: the cleaned ribbon on calm parchment (the raw result is only a small inset)
    parchment = spread.crop((960, 140, 1360, 407)).resize(rawr.size, Image.LANCZOS)
    parchment.alpha_composite(keyed)
    parchment.crop(box).convert("RGB").save(OUT / "T_Ribbon_Parchment.png")
    # Cleanup of a Muse APNG (Frost Breath frame 29: dark ghost haze, cut edges, green fringe) — the copied
    # original APNG frame and the processed frame (processing/ice_spells/fix_v1), same canvas, same parchment
    from PIL import ImageSequence
    ICE = ROOT / "processing" / "ice_spells"
    src = [f.convert("RGBA").copy() for f in ImageSequence.Iterator(Image.open(ICE / "apng" / "icedragon-v1.apng"))]
    parch2 = spread.crop((900, 80, 1400, 580)).resize((700, 700), Image.LANCZOS)
    for name, fr in (("T_Clean_Before.png", src[29]), ("T_Clean_After.png", Image.open(ICE / "fix_v1/FrostBreath/FrostBreath_029.png").convert("RGBA"))):
        im = parch2.copy(); im.alpha_composite(fr); im.convert("RGB").save(OUT / name)

    # Loop seam of Absolute Zero: original APNG frames 35-39 then frame 0 (hard jump), and the processed
    # sequence: the same ending fading out, a transparent pause, frame 0 fading in, then frame 0
    az = [f.convert("RGBA").copy() for f in ImageSequence.Iterator(Image.open(ICE / "apng" / "frozenknight-shatter.apng"))]
    fixed = [Image.open(ICE / ("fix_v1/AbsoluteZero/AbsoluteZero_%03d.png" % i)).convert("RGBA") for i in range(45)]
    def strip(frames, name):
        T, gap = 300, 14
        out = Image.new("RGB", (len(frames) * T + (len(frames) - 1) * gap, T), (24, 20, 18))
        for k, f in enumerate(frames):
            cell = spread.crop((900, 80, 1400, 580)).resize((T, T), Image.LANCZOS)
            cell.alpha_composite(f.resize((T, T), Image.LANCZOS))
            out.paste(cell.convert("RGB"), (k * (T + gap), 0))
        out.save(OUT / name)
    # Moving comparison of the loop seam (shown after the first animation moment): the frames around the
    # restart, original APNG 30-39 -> 0-5 (0.12 s each) and processed 30-44 -> 0-5 (40 = 0.36 s pause)
    for old in OUT.glob("T_LoopOrig_*.png"): old.unlink()
    for old in OUT.glob("T_LoopFix_*.png"): old.unlink()
    for old in OUT.glob("T_Loop_*.png"): old.unlink()
    cellbg = spread.crop((900, 80, 1400, 580)).resize((560, 560), Image.LANCZOS)
    def flip(frames, prefix):
        for k, f in enumerate(frames):
            c = cellbg.copy(); c.alpha_composite(f.resize((560, 560), Image.LANCZOS))
            c.convert("RGB").save(OUT / ("%s_%02d.png" % (prefix, k)))
    flip([az[i] for i in list(range(30, 40)) + list(range(0, 6))], "T_LoopOrig")
    flip([fixed[i] for i in list(range(30, 45)) + list(range(0, 6))], "T_LoopFix")
    print("ok")


if __name__ == "__main__":
    main()
