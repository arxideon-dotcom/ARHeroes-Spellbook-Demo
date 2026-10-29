"""Word timings of the voice-over files, from a local speech-recognition pass (faster-whisper small.en on this
machine; the audio is not sent anywhere). Writes processing/trailer/voice_words.json, which make_audio.py uses
to cut the recordings only in their own pauses and to time the subtitles. The recognised words are kept as they
came out (e.g. "Icestorm", "night"); the subtitle text itself is the script in make_audio.py.

Usage: python voice_timing.py
"""
import json
import subprocess
from pathlib import Path

import imageio_ffmpeg
import numpy as np
from faster_whisper import WhisperModel

ROOT = Path(__file__).resolve().parents[2]
PRIV = ROOT / "processing" / "trailer_private"
FILES = ["voice_intro.mp3", "voice_main.mp3"]


def main():
    model = WhisperModel("small.en", device="cpu", compute_type="int8")
    out = {}
    for f in FILES:
        raw = subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(), "-v", "error", "-i", str(PRIV / f),
                              "-f", "f32le", "-ac", "1", "-ar", "16000", "-"], capture_output=True).stdout
        a = np.frombuffer(raw, np.float32).copy()
        segs, _ = model.transcribe(a, word_timestamps=True, beam_size=5)
        words = [{"w": w.word.strip(), "s": round(w.start, 3), "e": round(w.end, 3), "p": round(w.probability, 2)}
                 for s in segs for w in s.words]
        out[f] = {"duration": round(len(a) / 16000, 3), "words": words}
        print(f, len(words), "words", out[f]["duration"], "s")
    (Path(__file__).parent / "voice_words.json").write_bytes(json.dumps(out, indent=1).encode("utf-8"))


if __name__ == "__main__":
    main()
