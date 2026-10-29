"""Background music for the trailer: one 180.0 s track from the provided WAV (processing only, the original copy
in processing/trailer_private/ is not modified).

The source is 169.8 s, so one musical passage is repeated: the script looks for two moments A < B, about
10-12 s apart, whose sound (short-time spectrum over 3 s) is most alike, and plays track[0:B] then track[A:],
joined with a 0.5 s equal-power crossfade. Then: fade-in 2 s, fade-out over the last 3.5 s, and one fixed gain to
a quiet background level (about -27 dBFS RMS, peaks well under 0 dBFS) to leave room for a future voice-over.

Writes Assets/Spellbook/Trailer/Private/Resources/TrailerMusic.wav (git-ignored) and prints the join and levels.
"""
import wave
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "processing" / "trailer_private" / "the-kingdom-revealed.wav"
OUT = ROOT / "Assets" / "Spellbook" / "Trailer" / "Private" / "Resources" / "TrailerMusic.wav"
LENGTH = 180.0
TARGET_RMS_DB = -27.0
FADE_IN, FADE_OUT, XFADE = 2.0, 3.5, 0.5


def load(path):
    w = wave.open(str(path))
    sr, ch = w.getframerate(), w.getnchannels()
    x = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).reshape(-1, ch).astype(np.float32) / 32768.0
    return x, sr


def features(mono, sr, hop=0.05, n=4096):
    h = int(sr * hop)
    win = np.hanning(n).astype(np.float32)
    frames = np.lib.stride_tricks.sliding_window_view(np.pad(mono, (0, n)), n)[::h]
    spec = np.abs(np.fft.rfft(frames * win, axis=1))[:, 1:1024]
    # log bands
    edges = np.unique(np.geomspace(1, spec.shape[1], 48).astype(int))
    bands = np.stack([spec[:, a:b].mean(1) for a, b in zip(edges[:-1], edges[1:])], 1)
    f = np.log1p(bands * 50)
    f -= f.mean(1, keepdims=True)
    f /= np.linalg.norm(f, axis=1, keepdims=True) + 1e-9
    return f, hop


def main():
    x, sr = load(SRC)
    dur = len(x) / sr
    need = LENGTH - dur
    mono = x.mean(1)
    f, hop = features(mono, sr)
    ctx = int(3.0 / hop)
    best = (-1.0, 0, 0)
    lo, hi = int((need + 0.3) / hop), int((need + 2.5) / hop)       # B - A between need+0.3 and need+2.5 s
    for a in range(int(30 / hop), int((dur - 30) / hop)):
        for d in range(lo, hi):
            b = a + d
            if b + ctx >= len(f):
                break
            s = float((f[a - ctx:a + ctx] * f[b - ctx:b + ctx]).sum(1).mean())
            if s > best[0]:
                best = (s, a, b)
    sim, a, b = best
    A, B = a * hop, b * hop
    ia, ib, nx = int(A * sr), int(B * sr), int(XFADE * sr)
    t = np.linspace(0, np.pi / 2, nx, dtype=np.float32)[:, None]
    head = x[:ib].copy()
    tail = x[ia:].copy()
    joined = np.concatenate([head[:-nx], head[-nx:] * np.cos(t) + tail[:nx] * np.sin(t), tail[nx:]])
    n = int(LENGTH * sr)
    if len(joined) < n:
        joined = np.concatenate([joined, np.zeros((n - len(joined), 2), np.float32)])
    y = joined[:n]
    env = np.ones(n, np.float32)
    fi, fo = int(FADE_IN * sr), int(FADE_OUT * sr)
    env[:fi] = np.sin(np.linspace(0, np.pi / 2, fi)) ** 2
    env[-fo:] = np.cos(np.linspace(0, np.pi / 2, fo)) ** 2
    y *= env[:, None]
    rms = np.sqrt((y ** 2).mean())
    gain = 10 ** (TARGET_RMS_DB / 20) / rms
    y *= gain
    peak = np.abs(y).max()
    assert peak < 0.98, "clipping"
    OUT.parent.mkdir(parents=True, exist_ok=True)
    w = wave.open(str(OUT), "wb")
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(sr)
    w.writeframes((np.clip(y, -1, 1) * 32767).astype(np.int16).tobytes())
    w.close()
    print("source %.2f s; repeat %.2f-%.2f s (similarity %.3f), join at %.2f s of the output; output %.2f s" % (dur, A, B, sim, B, n / sr))
    print("gain %.1f dB, RMS %.1f dBFS, peak %.1f dBFS" % (20 * np.log10(gain), 20 * np.log10(np.sqrt((y ** 2).mean())), 20 * np.log10(peak)))


if __name__ == "__main__":
    main()
