"""An original ambient piece for the credits of Journey of Light, synthesised from nothing but
sines and noise: a slow pad over four chords, a low root, sparse bell notes, a breath of high
noise; it swells where the credits leave the Solar System (about 1:45-2:05) and settles.
Seeded: the same piece every run.   python3 theme.py out.wav"""
import sys, math, wave
import numpy as np

SR = 44100
BAR = 8.0
LENGTH = 192.0
N = int(SR * LENGTH)
rng = np.random.default_rng(1977)
L = np.zeros(N); R = np.zeros(N)

def hz(midi): return 440.0 * 2 ** ((midi - 69) / 12)

def add(sig, start, pan, gain):
    i = int(start * SR)
    if i < 0: sig = sig[-i:]; i = 0
    if i >= N: return
    sig = sig[:N - i]
    L[i:i + len(sig)] += sig * gain * math.cos(pan * math.pi / 2)
    R[i:i + len(sig)] += sig * gain * math.sin(pan * math.pi / 2)

def pad_note(midi, start, dur, gain, bright):
    """Two voices a few cents apart, a handful of harmonics rolling off; slow in, slow out."""
    n = int((dur + 5.0) * SR); t = np.arange(n) / SR
    env = np.clip(t / 3.2, 0, 1) ** 1.5 * np.clip((dur + 5.0 - t) / 5.0, 0, 1) ** 1.5
    for cents, pan in ((-5, 0.25), (5, 0.75)):
        f = hz(midi) * 2 ** (cents / 1200)
        s = np.zeros(n)
        for h in range(1, 7):
            if f * h > 5000: break
            lfo = 1 + 0.25 * np.sin(2 * math.pi * (0.07 + 0.03 * h) * t + rng.uniform(0, 6.28))
            s += np.sin(2 * math.pi * f * h * t + rng.uniform(0, 6.28)) * lfo / h ** (2.4 - 0.9 * bright)
        add(s * env, start, pan, gain)

def bell(midi, start, gain, pan):
    n = int(6.0 * SR); t = np.arange(n) / SR; f = hz(midi)
    s = np.sin(2 * math.pi * f * t) * np.exp(-t / 1.6) + 0.35 * np.sin(2 * math.pi * f * 2.0 * t) * np.exp(-t / 0.9) \
        + 0.12 * np.sin(2 * math.pi * f * 3.01 * t) * np.exp(-t / 0.45)
    s *= np.clip(t / 0.006, 0, 1)
    add(s, start, pan, gain)

# The chords: D minor, B flat, F, C - and a second way round, through G minor.
A = [[38, 50, 57, 60, 64, 65], [34, 46, 53, 57, 62, 65], [41, 48, 57, 60, 64, 67], [36, 48, 55, 60, 62, 64]]
B = [[43, 50, 58, 62, 65, 69], [34, 46, 53, 58, 62, 65], [41, 53, 57, 60, 65, 67], [36, 48, 55, 60, 64, 67]]
bars = int(LENGTH / BAR)

def swell(t):   # the lift as the camera leaves the system, about 1:38 to 2:10
    return math.exp(-((t - 114.0) / 15.0) ** 2)

for b in range(bars):
    t0 = b * BAR
    chord = (A if (b // 4) % 2 == 0 else B)[b % 4]
    lift = swell(t0 + BAR / 2)
    enter = min(1.0, (b + 1) / 3.0)          # the pad thickens over the first bars
    for k, m in enumerate(chord):
        if k >= 2 + int(4 * enter): continue
        pad_note(m, t0 - 1.5, BAR, 0.05 * (1.0 if k else 1.2) * (1 + 0.5 * lift), 0.5 + 0.45 * lift)
    if lift > 0.25:                           # an octave above, in the swell
        for m in chord[2:]: pad_note(m + 12, t0 - 1.5, BAR, 0.022 * lift, 0.5)
    # the root, low
    n = int((BAR + 3) * SR); t = np.arange(n) / SR
    env = np.clip(t / 2.0, 0, 1) * np.clip((BAR + 3 - t) / 3.0, 0, 1)
    f0 = hz(chord[0])
    add((np.sin(2 * math.pi * f0 * t) + 0.3 * np.sin(2 * math.pi * 2 * f0 * t)) * env, t0 - 0.5, 0.5, 0.06 * min(1.0, b / 2.0) * (1 + 0.4 * lift))

# Bells: the chord's own notes and the fifth above, few at first, more in the swell, then few.
scale = [62, 65, 67, 69, 72, 74, 77, 79, 81]
t = 9.0
while t < LENGTH - 14:
    b = int(t / BAR); chord = (A if (b // 4) % 2 == 0 else B)[b % 4]
    lift = swell(t)
    pool = [m + 12 for m in chord[2:]] + [m for m in scale if (m % 12) in {c % 12 for c in chord}]
    m = pool[rng.integers(len(pool))]
    bell(m, t, 0.075 * (0.6 + 0.8 * rng.uniform()) * (1 + 0.6 * lift), rng.uniform(0.2, 0.8))
    if rng.uniform() < 0.3 + 0.4 * lift: bell(m + (7 if rng.uniform() < 0.5 else 12), t + 0.5, 0.04, rng.uniform(0.2, 0.8))
    t += rng.choice([2.0, 3.0, 4.0, 4.0, 6.0]) * (1.0 - 0.45 * lift)

# A breath of high noise, like the sky.
noise = rng.standard_normal(N)
spec = np.fft.rfft(noise); f = np.fft.rfftfreq(N, 1 / SR)
spec *= np.exp(-((np.log(np.maximum(f, 1)) - math.log(6500)) / 0.35) ** 2)
air = np.fft.irfft(spec, N); air /= np.abs(air).max()
tt = np.arange(N) / SR
air *= 0.012 * (0.6 + 0.4 * np.sin(2 * math.pi * tt / 23.0)) * np.clip(tt / 10, 0, 1)
L += air; R += np.roll(air, 331)

# A hall: each side through its own decaying noise.
def hall(x, seed):
    n = int(4.2 * SR); r = np.random.default_rng(seed)
    ir = r.standard_normal(n) * np.exp(-np.arange(n) / (1.25 * SR)); ir[:int(0.012 * SR)] = 0
    ir /= math.sqrt((ir ** 2).sum())
    size = 1 << int(math.ceil(math.log2(len(x) + n)))
    return np.fft.irfft(np.fft.rfft(x, size) * np.fft.rfft(ir, size), size)[:len(x)]
wetL, wetR = hall(L, 5), hall(R, 6)
L = 0.62 * L + 0.5 * wetL + 0.12 * wetR; R = 0.62 * R + 0.5 * wetR + 0.12 * wetL

# In over four seconds, out over ten; level; a soft ceiling.
fade = np.clip(tt / 4.0, 0, 1) * np.clip((LENGTH - tt) / 10.0, 0, 1) ** 1.5
L *= fade; R *= fade
peak = max(np.abs(L).max(), np.abs(R).max())
L, R = np.tanh(L / peak * 1.15) * 0.82, np.tanh(R / peak * 1.15) * 0.82
out = (np.stack([L, R], 1) * 32767).astype('<i2')
with wave.open(sys.argv[1], 'wb') as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes(out.tobytes())
rms = math.sqrt((L ** 2).mean()); print('wrote', sys.argv[1], 'seconds', LENGTH, 'peak', round(float(np.abs(L).max()), 3), 'rms', round(rms, 3))
