"""Synthesised reload foley (no recordings, so no licence questions): magazine release, magazine out, cloth grab,
magazine in, palm slap, bolt release and pistol slide release, for rifle (heavier, lower) and pistol variants.
Each sound is built from short noise transients (clicks, scrapes) and decaying inharmonic partials (metal ring,
spring), mono 44.1 kHz 16-bit.

    python3 ArtSource/Audio/make_reload_sounds.py Assets/Audio
"""
import sys, os, wave
import numpy as np

SR = 44100
rng = np.random.default_rng(7)


def t_axis(sec): return np.arange(int(SR * sec)) / SR


def env(t, attack, decay):
    a = np.clip(t / max(attack, 1e-5), 0, 1)
    return a * np.exp(-np.maximum(t - attack, 0) / decay)


def bandnoise(n, lo, hi):
    """White noise band-passed in the frequency domain."""
    x = rng.standard_normal(n)
    X = np.fft.rfft(x); f = np.fft.rfftfreq(n, 1 / SR)
    X[(f < lo) | (f > hi)] = 0
    y = np.fft.irfft(X, n)
    return y / (np.abs(y).max() + 1e-9)


def click(sec, lo, hi, decay, gain=1.0):
    t = t_axis(sec)
    return bandnoise(len(t), lo, hi) * env(t, 0.0003, decay) * gain


def ring(sec, freqs, decays, gains):
    t = t_axis(sec); y = np.zeros_like(t)
    for f, d, g in zip(freqs, decays, gains):
        y += g * np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * env(t, 0.0005, d)
    return y


def scrape(sec, lo, hi, rate):
    """Friction: band noise with a fast irregular amplitude (ridges of the magazine sliding past the well)."""
    t = t_axis(sec)
    n = bandnoise(len(t), lo, hi)
    grit = 0.55 + 0.45 * np.sign(np.sin(2 * np.pi * rate * t + 3 * np.sin(2 * np.pi * 7 * t)))
    shape = np.sin(np.pi * np.clip(t / sec, 0, 1)) ** 0.7
    return n * grit * shape


def mix(*xs):
    """Sum of sounds of different lengths."""
    y = np.zeros(max(len(x) for x in xs))
    for x in xs: y[:len(x)] += x
    return y


def place(dst, src, at):
    i = int(at * SR); j = min(len(dst), i + len(src))
    dst[i:j] += src[:j - i]


def lowshelf_thump(sec, f0, decay):
    t = t_axis(sec)
    return np.sin(2 * np.pi * f0 * t * (1 - 0.35 * t / sec)) * env(t, 0.001, decay)


def finish(y, peak=0.8):
    y = y - np.mean(y)
    fade = int(0.004 * SR); y[-fade:] *= np.linspace(1, 0, fade)
    return y / (np.abs(y).max() + 1e-9) * peak


def write(path, y):
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(y, -1, 1) * 32767).astype(np.int16).tobytes())


def mag_out(k):          # k > 1: lower / heavier (rifle)
    y = np.zeros(int(SR * 0.42))
    place(y, mix(click(0.05, 2500 / k, 9000 / k, 0.004), ring(0.08, [2900 / k, 4700 / k], [0.012, 0.008], [0.4, 0.25])), 0.0)   # release button
    place(y, scrape(0.16 * k, 1200 / k, 6000 / k, 90 / k) * 0.35, 0.02)                                                   # slides out of the well
    place(y, mix(click(0.04, 800 / k, 4000 / k, 0.008, 0.5), lowshelf_thump(0.08, 160 / k, 0.02) * 0.5), 0.02 + 0.16 * k)      # clears the well
    return finish(y, 0.75)


def mag_in(k):
    y = np.zeros(int(SR * 0.45))
    place(y, scrape(0.09 * k, 1500 / k, 7000 / k, 120 / k) * 0.3, 0.0)                                                     # enters the well
    hit = 0.09 * k
    place(y, click(0.06, 1500 / k, 8000 / k, 0.006, 1.0), hit)                                                              # latch clack
    place(y, ring(0.18, [1850 / k, 3100 / k, 5300 / k], [0.03, 0.02, 0.01], [0.35, 0.3, 0.15]), hit)
    place(y, lowshelf_thump(0.12, 120 / k, 0.035) * 0.9, hit)                                                              # body of the gun
    place(y, click(0.03, 3000 / k, 9000 / k, 0.003, 0.35), hit + 0.012)                                                     # catch snaps in
    return finish(y, 0.85)


def slap(k):
    y = np.zeros(int(SR * 0.25))
    place(y, mix(click(0.05, 300, 3000, 0.012, 1.0), lowshelf_thump(0.1, 110 / k, 0.03) * 0.8), 0.0)       # palm on polymer
    place(y, ring(0.12, [2400 / k, 3900 / k], [0.02, 0.012], [0.15, 0.1]), 0.002)
    return finish(y, 0.7)


def bolt_release(k):
    y = np.zeros(int(SR * 0.5))
    place(y, click(0.03, 2000, 8000, 0.003, 0.4), 0.0)                                                  # catch pressed
    place(y, scrape(0.03, 2000 / k, 8000 / k, 300) * 0.25, 0.005)                                        # carrier runs forward
    hit = 0.035
    place(y, click(0.07, 900 / k, 7000 / k, 0.01, 1.0), hit)                                             # slams home
    place(y, ring(0.35, [1320 / k, 2210 / k, 3480 / k, 5150 / k], [0.06, 0.045, 0.025, 0.012], [0.4, 0.35, 0.25, 0.12]), hit)
    place(y, lowshelf_thump(0.15, 95 / k, 0.04), hit)
    spring = ring(0.25, [780 / k, 1170 / k], [0.05, 0.04], [0.12, 0.08]) * (1 + 0.6 * np.sin(2 * np.pi * 31 * t_axis(0.25)))
    place(y, spring, hit + 0.004)                                                                        # buffer spring
    return finish(y, 0.9)


def grab():
    """Cloth and gear rustle as the hand goes for a new magazine."""
    sec = 0.32; t = t_axis(sec)
    n = bandnoise(len(t), 400, 5000)
    am = np.convolve(np.abs(rng.standard_normal(len(t))), np.ones(400) / 400, mode="same")
    shape = np.sin(np.pi * t / sec) ** 1.5
    y = n * am * shape
    place(y, click(0.04, 600, 3500, 0.01, 0.4), 0.18)                                                    # magazine taken from the pouch
    return finish(y, 0.45)


out = sys.argv[1] if len(sys.argv) > 1 else "."
os.makedirs(out, exist_ok=True)
for name, y in (("Reload_RifleMagOut", mag_out(1.35)), ("Reload_RifleMagIn", mag_in(1.35)), ("Reload_RifleBoltRelease", bolt_release(1.25)),
                ("Reload_PistolMagOut", mag_out(0.9)), ("Reload_PistolMagIn", mag_in(0.9)), ("Reload_PistolSlideRelease", bolt_release(0.85)),
                ("Reload_Slap", slap(1.0)), ("Reload_Grab", grab())):
    write(os.path.join(out, name + ".wav"), y)
    print("wrote", name, f"{len(y) / SR:.2f}s")
