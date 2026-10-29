"""Encode a staged-mode take (f_00000.jpg ... at exactly 30 fps, Time.captureFramerate) into MP4 1920x1080,
with the English subtitles burned in by the player and voiceover.srt kept as a sidecar file.

Frame capture does not record sound and runs slower than real time, so the trailer music is muxed here: the
music file starts at trailer time 0, and a take recorded from <start> seconds takes the same slice, so picture
and music stay in sync.

Usage: python encode_take.py <take dir> <out.mp4> [<music.wav> <take start s>]   (needs: pip install imageio-ffmpeg)
"""
import subprocess
import sys
from pathlib import Path

import imageio_ffmpeg


def main():
    take, out = Path(sys.argv[1]), Path(sys.argv[2])
    n = len(list(take.glob("f_*.jpg")))
    cmd = [imageio_ffmpeg.get_ffmpeg_exe(), "-y", "-framerate", "30", "-i", str(take / "f_%05d.jpg")]
    if len(sys.argv) > 4:
        cmd += ["-ss", sys.argv[4], "-t", "%.3f" % (n / 30.0), "-i", sys.argv[3], "-map", "0:v", "-map", "1:a",
                "-c:a", "aac", "-b:a", "192k"]
        # an excerpt of the trailer: short fades where the soundtrack is cut, so it does not start or stop abruptly
        fades = []
        if float(sys.argv[4]) > 0:
            fades.append("afade=t=in:d=0.4")
        if float(sys.argv[4]) + n / 30.0 < 179.9:
            fades.append("afade=t=out:st=%.3f:d=0.6" % (n / 30.0 - 0.6))
        if fades:
            cmd += ["-af", ",".join(fades)]
    cmd += ["-vf", "scale=1920:1080:flags=lanczos", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "18",
            "-preset", "slow", "-movflags", "+faststart", "-t", "%.3f" % (n / 30.0), str(out)]
    r = subprocess.run(cmd, capture_output=True, text=True)
    print("frames", n, "->", "%.2f s" % (n / 30.0), "exit", r.returncode, out)
    if r.returncode:
        print(r.stderr[-800:])


if __name__ == "__main__":
    main()
