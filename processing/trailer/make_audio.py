"""Trailer soundtrack: music + the provided voice-over, mixed to one 180.0 s file; plus the subtitle timings.
Processing only — the copies in processing/trailer_private/ stay unchanged.

Music  the-menu-knows-your-name.wav (204.8 s): the first 180 s, no splice. Fade-in 1.5 s, fade-out over the last
       3.5 s (a natural dip of the track around 2:56). One fixed gain to a quiet bed.
Voice  voice_intro.mp3 (16.4 s): phrases 1-4. voice_main.mp3 (58.6 s): the whole text again, from "Welcome!";
       only its phrases 5-16 are used, so the intro is not doubled. Every phrase is cut in the middle of the pauses
       around it (word timings: voice_words.json from voice_timing.py), never inside a word, no speed change, and
       placed so that its first word lands at the scene time below. 15 ms fades at the cut points (in silence).
Duck   under every phrase the music goes down 10 dB: ramp down 0.6-0.25 s before the first word, back up 0.3-1.2 s
       after the last word (raised-cosine ramps).

Writes Assets/Spellbook/Trailer/Private/Resources/TrailerAudio.wav (git-ignored),
processing/trailer/voiceover.srt and processing/trailer/voice_lines.json (start, end, text of each subtitle).
"""
import json
import re
import subprocess
import wave
from pathlib import Path

import imageio_ffmpeg
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
PRIV = ROOT / "processing" / "trailer_private"
OUT = ROOT / "Assets" / "Spellbook" / "Trailer" / "Private" / "Resources" / "TrailerAudio.wav"
SR, LENGTH = 48000, 180.0
MUSIC_RMS_DB, VOICE_RMS_DB, DUCK_DB = -27.0, -25.5, -10.0
SUB_HOLD = 0.35

# (file, first words as recognised, trailer time of the first word, subtitle text = the script)
PHRASES = [
    ("voice_intro.mp3", "i'm creating", 2.50, "I’m creating a fantasy game on my own."),
    ("voice_intro.mp3", "i know", 4.92, "I know the world I want to build, but I don’t have a dedicated art or animation team."),
    ("voice_intro.mp3", "ready", 21.50, "Ready-made assets can give me a starting point, but I wanted something that felt like part of my own world."),
    ("voice_intro.mp3", "so i", 35.60, "So I started building a living spellbook with Muse."),
    ("voice_main.mp3", "with muse i", 39.60, "With Muse, I developed the artwork for the book, its decoration, and its spells."),
    ("voice_main.mp3", "each element", 52.00, "Each element was generated, selected, and cleaned before it went onto the page."),
    ("voice_main.mp3", "i mapped", 77.00, "I mapped every layer to the curvature of the illustrated pages,"),
    ("voice_main.mp3", "then added", 86.00, "then added typography, ornaments, and interaction."),
    ("voice_main.mp3", "muse also", 104.50, "Muse also created the animated spell sequences."),
    ("voice_main.mp3", "icestorm|ice storm", 111.00, "Ice Storm brings the page into motion."),
    ("voice_main.mp3", "frostbreath|frost breath", 121.00, "Frost Breath reveals an icy dragon."),
    ("voice_main.mp3", "ice comet", 129.50, "Ice Comet trails frozen energy."),
    ("voice_main.mp3", "absolute zero", 138.00, "Absolute Zero freezes and shatters a knight."),
    ("voice_main.mp3", "this is", 146.50, "This is the working Unity application, running in real time."),
    ("voice_main.mp3", "ai made", 165.50, "AI made this visual production achievable without a dedicated art team."),
    ("voice_main.mp3", "i kept", 172.00, "I kept control of the world and its presentation."),
]


def norm(w):
    return re.sub(r"[^a-z']", "", w.lower().replace("’", "'"))


def locate(words, key, from_index):
    """Index of the word where the phrase starts (first match after from_index)."""
    for alt in key.split("|"):
        toks = alt.split()
        for i in range(from_index, len(words) - len(toks) + 1):
            if all(norm(words[i + k]["w"]).startswith(t) for k, t in enumerate(toks)):
                return i
    raise ValueError("phrase not found: " + key)


def phrase_spans(timing):
    """For each phrase: (file, first word start, last word end, cut from, cut to) in that file's time."""
    by_file = {}
    for idx, (f, key, _, _) in enumerate(PHRASES):
        by_file.setdefault(f, []).append(idx)
    spans = [None] * len(PHRASES)
    for f, idxs in by_file.items():
        words = timing[f]["words"]
        starts, pos = [], 0
        for idx in idxs:
            i = locate(words, PHRASES[idx][1], pos)
            starts.append(i)
            pos = i + 1
        for k, idx in enumerate(idxs):
            i = starts[k]
            j = (starts[k + 1] - 1) if k + 1 < len(starts) else len(words) - 1
            if k + 1 == len(starts) and f == "voice_intro.mp3":
                j = len(words) - 1
            s, e = words[i]["s"], words[j]["e"]
            before = words[i - 1]["e"] if i > 0 else 0.0
            after = words[j + 1]["s"] if j + 1 < len(words) else timing[f]["duration"]
            cut_a = (before + s) / 2 if i > 0 else 0.0
            cut_b = (e + after) / 2 if j + 1 < len(words) else timing[f]["duration"]
            # at least 0.1 s of silence kept around the words (unless the file itself starts or ends there)
            assert (i == 0 or s - cut_a >= 0.1) and (j + 1 == len(words) or cut_b - e >= 0.1),                 "cut too close to a word: " + PHRASES[idx][3]
            spans[idx] = (f, s, e, cut_a, cut_b)
    return spans


def load_music():
    w = wave.open(str(PRIV / "the-menu-knows-your-name.wav"))
    assert w.getframerate() == SR and w.getsampwidth() == 2
    return np.frombuffer(w.readframes(w.getnframes()), np.int16).reshape(-1, w.getnchannels()).astype(np.float32) / 32768


def load_voice(f):
    raw = subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(), "-v", "error", "-i", str(PRIV / f),
                          "-f", "f32le", "-ac", "1", "-ar", str(SR), "-"], capture_output=True).stdout
    return np.frombuffer(raw, np.float32).copy()


def ramp(n, up=True):
    r = np.sin(np.linspace(0, np.pi / 2, n, dtype=np.float32)) ** 2
    return r if up else r[::-1]


def tc(s):
    ms = int(round(s * 1000))
    return "%02d:%02d:%02d,%03d" % (ms // 3600000, ms // 60000 % 60, ms // 1000 % 60, ms % 1000)


def main():
    timing = json.loads((HERE / "voice_words.json").read_text(encoding="utf-8"))
    spans = phrase_spans(timing)
    n = int(LENGTH * SR)

    music = load_music()[:n].copy()
    fi, fo = int(1.5 * SR), int(3.5 * SR)
    music[:fi] *= ramp(fi)[:, None]
    music[-fo:] *= ramp(fo, False)[:, None]
    music *= 10 ** (MUSIC_RMS_DB / 20) / np.sqrt((music ** 2).mean())

    voices = {f: load_voice(f) for f in {sp[0] for sp in spans}}
    # one gain per file, from the speech actually used, so both recordings sit at the same level;
    # capped so that no voice peak goes above -4 dBFS
    for f, v in voices.items():
        used = np.concatenate([v[int(s * SR):int(e * SR)] for (ff, s, e, _, _) in spans if ff == f])
        g = min(10 ** (VOICE_RMS_DB / 20) / np.sqrt((used ** 2).mean()), 10 ** (-4.0 / 20) / np.abs(used).max())
        print("%s: gain %.1f dB, speech RMS %.1f dBFS, peak %.1f dBFS" % (f, 20 * np.log10(g),
              20 * np.log10(g * np.sqrt((used ** 2).mean())), 20 * np.log10(g * np.abs(used).max())))
        voices[f] = v * g

    track = np.zeros(n, np.float32)
    lines, edge = [], int(0.015 * SR)
    t = np.arange(n, dtype=np.float32) / SR
    duck = np.zeros(n, np.float32)
    end_prev = -1.0
    for (f, s, e, ca, cb), (_, _, at, text) in zip(spans, PHRASES):
        offset = at - s
        clip = voices[f][int(ca * SR):int(cb * SR)].copy()
        clip[:edge] *= ramp(edge); clip[-edge:] *= ramp(edge, False)
        i0 = int(round((ca + offset) * SR))
        assert i0 >= 0 and i0 + len(clip) <= n, "phrase outside the trailer: " + text
        assert ca + offset >= end_prev, "phrases overlap: " + text
        track[i0:i0 + len(clip)] += clip
        end_prev = cb + offset
        ws, we = at, at + (e - s)
        lines.append({"start": round(ws, 2), "end": round(we + SUB_HOLD, 2), "text": text, "spoken": [round(ws, 2), round(we, 2)]})
        down = np.clip((t - (ws - 0.6)) / 0.35, 0, 1)
        up = np.clip(((we + 1.2) - t) / 0.9, 0, 1)
        duck = np.maximum(duck, np.sin(np.minimum(down, up) * np.pi / 2) ** 2)
    assert end_prev <= LENGTH - 3.5, "the last phrase runs into the final fade"

    gain = 1 - (1 - 10 ** (DUCK_DB / 20)) * duck
    mix = music * gain[:, None] + track[:, None]
    peak = np.abs(mix).max()
    assert peak < 0.95, "clipping"
    OUT.parent.mkdir(parents=True, exist_ok=True)
    w = wave.open(str(OUT), "wb")
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((mix * 32767).astype(np.int16).tobytes())
    w.close()

    (HERE / "voice_lines.json").write_bytes(json.dumps(lines, indent=1, ensure_ascii=False).encode("utf-8"))
    srt = "".join("%d\n%s --> %s\n%s\n\n" % (k + 1, tc(l["start"]), tc(l["end"]), l["text"]) for k, l in enumerate(lines))
    (HERE / "voiceover.srt").write_bytes(srt.encode("utf-8"))
    speech = sum(l["spoken"][1] - l["spoken"][0] for l in lines)
    for l in lines:
        print("%6.2f-%6.2f  %s" % (l["spoken"][0], l["spoken"][1], l["text"]))
    print("%d phrases, %.1f s of speech; music RMS %.1f dBFS, ducked %.0f dB; voice RMS %.1f dBFS; mix peak %.1f dBFS" % (
        len(lines), speech, MUSIC_RMS_DB, DUCK_DB, VOICE_RMS_DB, 20 * np.log10(peak)))


if __name__ == "__main__":
    main()
