"""Prepare the requested recorded range sounds; requires the existing ffmpeg CLI.

Run from anywhere:
  python3 ArtSource/Audio/prepare_range_audio.py --out Assets/Audio \
      --preview Logs/rifle-audio-preview.wav

Video-derived gunshots have UNVERIFIED redistribution rights. See SOURCES.md.
This changes WAV contents only, never Unity .meta files or scene references.
"""
import argparse
import array
import hashlib
import math
from pathlib import Path
import struct
import subprocess
import sys
import wave

SR = 44100
SOURCE = Path(__file__).resolve().parent / "Recorded"
# Source-video seconds. Leave room for the initial transient and natural decay,
# ending before the next audible-event region. These are candidates for listening.
SHOTS = ((0.17, 0.75), (3.48, 4.16), (5.78, 6.49))


def decode(path):
    result = subprocess.run(
        ["ffmpeg", "-v", "error", "-i", str(path), "-f", "f32le", "-ac", "1", "-ar", str(SR), "pipe:1"],
        check=True, capture_output=True,
    )
    data = array.array("f")
    data.frombytes(result.stdout)
    if sys.byteorder != "little":
        data.byteswap()
    return data


def prepare(samples, peak, highpass):
    # Remove low-frequency handling noise/DC without synthesizing replacement audio.
    rc = 1 / (2 * math.pi * highpass)
    coefficient = rc / (rc + 1 / SR)
    previous = state = 0.0
    out = []
    for sample in samples:
        state = coefficient * (state + sample - previous)
        previous = sample
        out.append(state)
    maximum = max(map(abs, out), default=0)
    if maximum < 0.001:
        raise ValueError("Source excerpt is silent")
    first = next(i for i, sample in enumerate(out) if abs(sample) >= maximum * 0.015)
    out = out[max(0, first - int(SR * 0.001)):]
    # A 0.5 ms attack avoids an edit click; a short tail fade avoids a hard cut.
    attack, release = int(SR * 0.0005), int(SR * 0.08)
    for i in range(len(out)):
        out[i] *= min(1.0, i / attack, (len(out) - 1 - i) / release)
    gain = peak / max(map(abs, out))
    return [sample * gain for sample in out]


def write(path, samples):
    path.parent.mkdir(parents=True, exist_ok=True)
    if not samples or not all(math.isfinite(s) and abs(s) < 1 for s in samples):
        raise ValueError("Silent, non-finite or clipped output")
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(SR)
        output.writeframes(b"".join(struct.pack("<h", round(s * 32767)) for s in samples))


def burst(shots, hit):
    # Same 700 RPM and one-shot gains as RifleController. Include a target hit on every round.
    result = [0.0] * int(SR * 1.7)
    for shot in range(10):
        offset = round(shot * 60 / 700 * SR)
        for samples, gain in ((shots[shot % len(shots)], 0.9), (hit, 0.75)):
            for i, sample in enumerate(samples):
                if offset + i < len(result):
                    result[offset + i] += sample * gain
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()
    video = decode(SOURCE / "youtube-XUYfaH1Z4FQ.webm")
    shots = [prepare(video[int(start * SR):int(end * SR)], 0.72, 65) for start, end in SHOTS]
    hit = prepare(decode(SOURCE / "bing1.wav"), 0.32, 250)
    preview = burst(shots, hit)
    peak = max(map(abs, preview))
    if peak > 0.9:
        gain = 0.9 / peak
        shots = [[s * gain for s in shot] for shot in shots]
        hit = [s * gain for s in hit]
        preview = burst(shots, hit)
    for name, samples in [(f"Gunshot_{i + 1}", shot) for i, shot in enumerate(shots)] + [("TargetHit", hit)]:
        path = args.out / (name + ".wav")
        write(path, samples)
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        print(f"{name}: {len(samples) / SR:.3f}s, peak={max(map(abs, samples)):.3f}, sha256={digest}")
    if args.preview:
        write(args.preview, preview)
    print(f"700 RPM all-hits preview peak={max(map(abs, preview)):.3f}; listening quality NOT ASSESSED")


if __name__ == "__main__":
    main()
