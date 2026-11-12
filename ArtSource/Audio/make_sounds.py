"""Synthesizes the prototype sound effects (no external assets). Run: python3 make_sounds.py <outdir>"""
import math, random, struct, sys, wave
SR = 44100
def write(path, samples):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s / peak * 0.9)) * 32767)) for s in samples))
def lowpass(x, a):
    y, out = 0.0, []
    for s in x: y += a * (s - y); out.append(y)
    return out
def gunshot(seed):
    random.seed(seed); n = int(SR * 0.55)
    noise = [random.uniform(-1, 1) for _ in range(n)]
    crack = [noise[i] * math.exp(-i / (SR * 0.006)) for i in range(n)]                    # sharp transient
    body = lowpass(noise, 0.18); body = [body[i] * math.exp(-i / (SR * 0.045)) * 2.2 for i in range(n)]
    thump = [math.sin(2 * math.pi * (95 - 40 * i / n) * i / SR) * math.exp(-i / (SR * 0.07)) * 0.9 for i in range(n)]
    tail = lowpass(noise, 0.04); tail = [tail[i] * math.exp(-i / (SR * 0.22)) * 3.0 for i in range(n)]
    mech = [0.0] * n
    for i in range(int(SR * 0.012), int(SR * 0.02)): mech[i] = random.uniform(-1, 1) * 0.25             # bolt click
    return [crack[i] * 0.9 + body[i] + thump[i] + tail[i] * 0.6 + mech[i] for i in range(n)]
def target_ping(base):
    n = int(SR * 0.9); random.seed(int(base))
    partials = [(base, 1.0, 0.35), (base * 2.76, 0.5, 0.18), (base * 5.40, 0.3, 0.09), (base * 8.93, 0.15, 0.05)]
    out = []
    for i in range(n):
        t = i / SR
        s = sum(a * math.sin(2 * math.pi * f * t) * math.exp(-t / d) for f, a, d in partials)
        s += random.uniform(-1, 1) * math.exp(-t / 0.004) * 0.6                                       # impact click
        out.append(s)
    return out
def impact_thud():
    random.seed(9); n = int(SR * 0.18); nz = lowpass([random.uniform(-1, 1) for _ in range(n)], 0.25)
    return [nz[i] * math.exp(-i / (SR * 0.03)) for i in range(n)]
def pickup():
    n = int(SR * 0.25); random.seed(3); out = []
    for i in range(n):
        t = i / SR
        out.append(random.uniform(-1, 1) * (math.exp(-t / 0.01) + 0.8 * math.exp(-max(0, t - 0.11) / 0.012) * (t > 0.11)))
    return out
out = sys.argv[1] if len(sys.argv) > 1 else "."
for k in range(3): write(f"{out}/Gunshot_{k + 1}.wav", gunshot(k + 1))
write(f"{out}/TargetHit.wav", target_ping(1250))
write(f"{out}/TargetHit_Low.wav", target_ping(820))
write(f"{out}/Impact.wav", impact_thud())
write(f"{out}/Pickup.wav", pickup())
print("sounds written to", out)
