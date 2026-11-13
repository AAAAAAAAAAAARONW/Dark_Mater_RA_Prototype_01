"""
The score from the tutorial to the end of the journey, for JourneyScore_NEW: made, like the
credits' theme, from nothing but sines, in its key (D minor), which it leads into. Three
layers for each place (the tutorial, and each layer of the journey), which the game mixes by
what is happening:

  Bed_<place>.ogg    the place at rest: a seamless 48 s loop, four 12 s bars, two colours of
                     one chord turning slowly, a few bells
  Lift_<place>.ogg   the same bars risen - the octave above, a low octave, the chord running in
                     bells - in step with the bed, brought up as the music swells
  Crest_<name>.ogg   the phrase a climax arrives on: the chord struck full, its bells running up

There is no noise in any of it: no whoosh, no boom. The swell is the music's own.

    python3 journey_score.py <out dir> [names...]        (needs numpy, ffmpeg)
"""
import sys, math, os, re, wave, subprocess
import numpy as np

SR = 44100
OUT = sys.argv[1] if len(sys.argv) > 1 else '.'
ONLY = set(sys.argv[2:])


def hz(m): return 440.0 * 2 ** ((m - 69) / 12)
def sm(a, b, x): t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)


class Track:
    """Two channels to add sound into; a loop wraps what runs off its end back to its start."""
    def __init__(self, seconds, loop=False):
        self.n = int(seconds * SR); self.loop = loop
        self.L = np.zeros(self.n); self.R = np.zeros(self.n)

    def add(self, sig, start, pan=0.5, gain=1.0):
        i = int(round(start * SR))
        gl, gr = gain * math.cos(pan * math.pi / 2), gain * math.sin(pan * math.pi / 2)
        if self.loop:
            idx = (i + np.arange(len(sig))) % self.n
            np.add.at(self.L, idx, sig * gl); np.add.at(self.R, idx, sig * gr)
        else:
            if i < 0: sig = sig[-i:]; i = 0
            sig = sig[:max(0, self.n - i)]
            self.L[i:i + len(sig)] += sig * gl; self.R[i:i + len(sig)] += sig * gr

    def hall(self, wet=0.5, seconds=4.2, decay=1.25, seed=5):
        def one(x, sd):
            n = int(seconds * SR); r = np.random.default_rng(sd)
            ir = r.standard_normal(n) * np.exp(-np.arange(n) / (decay * SR)); ir[:int(0.012 * SR)] = 0
            ir /= math.sqrt((ir ** 2).sum())
            if self.loop:      # round the loop: the tail of the end is under the start
                reps = int(math.ceil(n / len(x))) + 1
                irw = np.zeros(len(x))
                np.add.at(irw, np.arange(n) % len(x), ir)
                return np.fft.irfft(np.fft.rfft(x) * np.fft.rfft(irw), len(x))
            size = 1 << int(math.ceil(math.log2(len(x) + n)))
            return np.fft.irfft(np.fft.rfft(x, size) * np.fft.rfft(ir, size), size)[:len(x)]
        wl, wr = one(self.L, seed), one(self.R, seed + 1)
        self.L = (1 - wet * 0.75) * self.L + wet * wl + wet * 0.24 * wr
        self.R = (1 - wet * 0.75) * self.R + wet * wr + wet * 0.24 * wl

    def write(self, name, peak=0.8, fade_in=0.0, fade_out=0.0):
        t = np.arange(self.n) / SR
        env = np.ones(self.n)
        if fade_in > 0: env *= np.clip(t / fade_in, 0, 1)
        if fade_out > 0: env *= np.clip((self.n / SR - t) / fade_out, 0, 1) ** 1.5
        L, R = self.L * env, self.R * env
        top = max(np.abs(L).max(), np.abs(R).max(), 1e-9)
        L, R = np.tanh(L / top * 1.15) * peak / math.tanh(1.15), np.tanh(R / top * 1.15) * peak / math.tanh(1.15)
        wav = os.path.join(OUT, name + '.wav')
        with wave.open(wav, 'wb') as w:
            w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
            w.writeframes((np.stack([L, R], 1) * 32767).astype('<i2').tobytes())
        subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-i', wav, '-c:a', 'libvorbis', '-q:a', '4',
                        os.path.join(OUT, name + '.ogg')], check=True)
        os.remove(wav)
        print('%-22s %6.1f s  rms %.3f' % (name, self.n / SR, math.sqrt((L ** 2).mean())))


RNG = np.random.default_rng(2026)

def pad(track, midi, start, dur, gain, bright=0.5, attack=3.2, release=5.0, trem=0.0):
    """A held note: two voices a few cents apart, a handful of harmonics; slow in, slow out."""
    n = int((dur + release) * SR); t = np.arange(n) / SR
    env = np.clip(t / attack, 0, 1) ** 1.5 * np.clip((dur + release - t) / release, 0, 1) ** 1.5
    if trem > 0: env = env * (1 - 0.35 + 0.35 * np.sin(2 * math.pi * trem * t + RNG.uniform(0, 6.28)))
    for cents, pan in ((-5, 0.25), (5, 0.75)):
        f = hz(midi) * 2 ** (cents / 1200); s = np.zeros(n)
        for h in range(1, 7):
            if f * h > 5200: break
            lfo = 1 + 0.25 * np.sin(2 * math.pi * (0.07 + 0.03 * h) * t + RNG.uniform(0, 6.28))
            s += np.sin(2 * math.pi * f * h * t + RNG.uniform(0, 6.28)) * lfo / h ** (2.4 - 0.9 * bright)
        track.add(s * env, start, pan, gain)

def bell(track, midi, start, gain, pan=0.5, decay=1.6):
    n = int(decay * 4.0 * SR); t = np.arange(n) / SR; f = hz(midi)
    s = np.sin(2 * math.pi * f * t) * np.exp(-t / decay) + 0.35 * np.sin(2 * math.pi * f * 2.0 * t) * np.exp(-t / (decay * 0.56)) \
        + 0.12 * np.sin(2 * math.pi * f * 3.01 * t) * np.exp(-t / (decay * 0.28))
    track.add(s * np.clip(t / 0.006, 0, 1), start, pan, gain)

# ── The places: each a chord in D minor's scale, in two colours ──────────────────────────
#    name: (colour A, colour B, brightness, bells a loop, sub, air, tremolo, level)
PLACES = {
    'Tutorial':    ([38, 45, 54, 57, 62, 64], [38, 43, 50, 55, 59, 62, 66], 0.40, 5, 0.4, 0.008, 0.0, 0.8),   # D major, add 9: bright, curious
    'Quasar':      ([38, 45, 53, 57, 64, 69], [38, 46, 53, 58, 65, 70], 0.85, 14, 1.2, 0.020, 2.6, 1.00),   # D minor: fierce
    'Macro':       ([41, 48, 56, 60, 67, 72], [41, 49, 56, 61, 65, 68], 0.60, 10, 0.9, 0.016, 0.0, 0.9),    # F minor: vast, dark
    'Micro':       ([44, 51, 56, 60, 63, 70], [44, 53, 56, 60, 63, 68], 0.45, 8, 0.7, 0.008, 0.0, 0.85),    # A flat major: near, warm
    'CosmicWeb':   ([35, 47, 54, 59, 62, 66, 73], [35, 47, 55, 59, 62, 67, 74], 0.60, 10, 0.9, 0.016, 0.0, 0.9),  # B minor: wide, cold
    'MilkyWay':    ([38, 45, 54, 57, 62, 66, 69], [38, 47, 55, 59, 62, 67], 0.55, 9, 0.8, 0.010, 0.0, 0.9),  # D major: home's galaxy
    'SolarSystem': ([40, 47, 56, 59, 64, 68, 71], [40, 49, 57, 61, 64, 69], 0.60, 12, 0.8, 0.010, 0.0, 0.9), # E major: a step higher still
    'Earth':       ([38, 50, 54, 57, 62, 64], [38, 47, 55, 59, 62, 64], 0.35, 7, 0.9, 0.006, 0.0, 0.8),      # D major, low and still: home
}
# Each place is in a key of its own, so every arrival is a change of key: D minor, up a third
# to F minor, its relative A flat, up a third to B minor, its relative D major, up a step to E,
# and down onto D to land.

# The lighter places move: plucked notes on a quick, even pulse (120 to the minute), each with
# a step of its own. The others hold still.
#   skip   four in a bar, off the beat: the tutorial - curious, a little playful
#   waltz  three in a bar, low note then two above: the first galaxy - close, turning
#   stride four in a bar, pushing on: the Milky Way - nearly home
#   lilt   six in a bar, two swings: Earth - light, at rest
STEPS = {'Tutorial': 'skip', 'Micro': 'waltz', 'MilkyWay': 'stride', 'Earth': 'lilt'}

def pluck(track, midi, start, gain, pan=0.5, decay=0.28):
    n = int(decay * 7 * SR); t = np.arange(n) / SR; f = hz(midi)
    s = np.sin(2 * math.pi * f * t) * np.exp(-t / decay) + 0.45 * np.sin(4 * math.pi * f * t) * np.exp(-t / (decay * 0.5)) \
        + 0.15 * np.sin(8 * math.pi * f * t) * np.exp(-t / (decay * 0.25))
    track.add(s * np.clip(t / 0.004, 0, 1), start, pan, gain)

def step(T, kind, chord, t0, seg, shade=1.0):
    tones = sorted(m + 12 for m in chord[1:]); low = chord[0] + 12; hi = tones + [tones[0] + 12, tones[1] + 12]
    e = 0.25
    if kind == 'skip':
        bar = [0, None, 2, 1, None, 3, 2, None]
        for b in range(int(seg / 2.0)):
            pluck(T, low, t0 + b * 2.0, 0.085 * shade, 0.5, 0.4)
            for j, k in enumerate(bar):
                if k is not None: pluck(T, hi[(k + (b % 2)) % len(hi)], t0 + b * 2.0 + j * e, (0.075 if j == 0 else 0.055) * shade, 0.3 + 0.4 * (j % 2))
            if b % 2 == 1: bell(T, hi[-1], t0 + b * 2.0 + 1.5, 0.045 * shade, 0.7, 0.9)
    elif kind == 'waltz':
        for b in range(int(seg / 1.5)):
            at = t0 + b * 1.5
            pluck(T, low, at, 0.09 * shade, 0.5, 0.5)
            for beat in (1, 2):
                pluck(T, hi[(b + beat) % 3], at + beat * 0.5, 0.055 * shade, 0.35); pluck(T, hi[(b + beat) % 3 + 2], at + beat * 0.5, 0.05 * shade, 0.65)
            if b % 2 == 0: bell(T, hi[(b // 2 * 2 + 3) % len(hi)] + 12, at, 0.05 * shade, 0.6, 1.2)
    elif kind == 'stride':
        bar = [0, 2, None, 3, 1, None, 2, 4]
        for b in range(int(seg / 2.0)):
            pluck(T, low, t0 + b * 2.0, 0.09 * shade, 0.5, 0.4); pluck(T, low + 7, t0 + b * 2.0 + 1.0, 0.06 * shade, 0.5, 0.35)
            for j, k in enumerate(bar):
                if k is not None: pluck(T, hi[(k + b) % len(hi)], t0 + b * 2.0 + j * e, (0.075 if j % 4 == 0 else 0.055) * shade, 0.3 + 0.4 * (j % 2))
            if b % 2 == 0: bell(T, hi[(b + 2) % len(hi)] + 12, t0 + b * 2.0 + 0.5, 0.05 * shade, 0.4, 1.3)
    elif kind == 'lilt':
        bar = [0, None, 2, 1, None, 3]
        for b in range(int(seg / 1.5)):
            at = t0 + b * 1.5
            pluck(T, low, at, 0.08 * shade, 0.5, 0.5)
            for j, k in enumerate(bar):
                if k is not None and j: pluck(T, hi[(k + b // 2) % len(hi)], at + j * e, 0.055 * shade, 0.3 + 0.4 * (j % 2), 0.32)
            if b % 4 == 2: bell(T, hi[(b // 4 + 4) % len(hi)] + 12, at + 0.75, 0.045 * shade, 0.6, 1.4)

def bed(name):
    a, b, bright, bells, sub, breath, trem, level = PLACES[name]
    seg = 12.0; T = Track(seg * 4, loop=True)
    for i, chord in enumerate((a, b, a, b)):
        t0 = i * seg
        for k, m in enumerate(chord):
            pad(T, m, t0 - 1.5, seg, 0.05 * (1.25 if k == 0 else 1.0) * (0.55 if name in STEPS else 1.0), bright, trem=trem if k >= 3 else 0.0)
        if name in STEPS: step(T, STEPS[name], chord, t0, seg)
        n = int((seg + 3) * SR); t = np.arange(n) / SR
        env = np.clip(t / 2.0, 0, 1) * np.clip((seg + 3 - t) / 3.0, 0, 1)
        f0 = hz(chord[0])
        T.add((np.sin(2 * math.pi * f0 * t) + 0.3 * np.sin(4 * math.pi * f0 * t)) * env, t0 - 0.5, 0.5, 0.06 * sub)
    r = np.random.default_rng(abs(hash(name)) % 9973 if False else sum(map(ord, name)))
    for _ in range(bells):
        t = r.uniform(0, seg * 4); chord = (a, b, a, b)[int(t // seg)]
        m = chord[r.integers(2, len(chord))] + 12
        bell(T, m, t, 0.06 * (0.6 + 0.8 * r.uniform()), r.uniform(0.2, 0.8))
        if r.uniform() < 0.35: bell(T, m + 7, t + 0.5, 0.035, r.uniform(0.2, 0.8))
    T.hall(0.5)
    T.write('Bed_' + name, peak=0.5 * level)


# ── The risen layer: the same bars, lifted ────────────────────────────────────────────
def lift(name):
    """In step with the bed (same four 12 s bars, same chords), and much more of it: the chord
    in a bright, brassy middle and the octave above, a heavy low octave, the chord running fast
    in bells, a slow pulse on the root. Silent in the game until the music rises."""
    a, b, bright, bells, sub, breath, trem, level = PLACES[name]
    seg = 12.0; T = Track(seg * 4, loop=True)
    for i, chord in enumerate((a, b, a, b)):
        t0 = i * seg
        for k, m in enumerate(chord[1:]):
            pad(T, m, t0 - 0.8, seg, 0.05, 1.25, attack=1.5, release=3.5)                       # brass
            pad(T, m + 12, t0 - 0.8, seg, 0.045, 1.0, attack=1.5, release=3.5, trem=4.5 if k >= 2 else 0.0)   # strings above
        for m in chord[-3:]:
            pad(T, m + 24, t0 - 0.8, seg, 0.02, 0.5, attack=2.5, release=3.5)
        low = chord[0] - 12 if chord[0] - 12 >= 26 else chord[0]
        n = int((seg + 3) * SR); t = np.arange(n) / SR
        env = np.clip(t / 1.2, 0, 1) * np.clip((seg + 3 - t) / 3.0, 0, 1)
        f0 = hz(low)
        T.add((np.sin(2 * math.pi * f0 * t) + 0.6 * np.sin(4 * math.pi * f0 * t) + 0.35 * np.sin(6 * math.pi * f0 * t)) * env, t0 - 0.4, 0.5, 0.13)
        # A slow pulse on the root, a pitched drum: two to the bar and a pick-up.
        for beat in ((0.0, 3.0, 6.0, 9.0, 10.5, 11.25) if name not in STEPS else (0.0, 1.5, 3.0, 4.5, 6.0, 7.5, 9.0, 10.5, 11.25)):
            n2 = int(1.6 * SR); t2 = np.arange(n2) / SR
            T.add((np.sin(2 * math.pi * f0 * t2) + 0.5 * np.sin(4 * math.pi * f0 * t2)) * np.exp(-t2 / 0.35) * np.clip(t2 / 0.008, 0, 1), t0 + beat, 0.5, 0.20 if beat in (0.0, 6.0) else 0.12)
        tones = [m + 12 for m in chord[1:]] + [chord[1] + 24, chord[2] + 24]
        run = tones + tones[-2:0:-1]
        every = 0.25 if name in STEPS else 0.375                                           # on the place's own pulse
        for j in range(int(seg / every)):                                                   # the chord running, fast
            bell(T, run[j % len(run)], t0 + j * every, 0.07 if j % 4 == 0 else 0.045, 0.5 + 0.4 * math.sin(j * 1.9 + i), 0.8)
    T.hall(0.45, seed=31)
    T.write('Lift_' + name, peak=0.8)


# ── The phrase a climax arrives on ────────────────────────────────────────────────────
LEAD = 1.2      # seconds from a crest's start to its strike; JourneyScore_NEW.CrestLead is the same

def strike(T, t0, chord, gain, tail, wide=True):
    low = chord[0] - 12 if chord[0] - 12 >= 26 else chord[0]
    for m, g in ((low, 0.34), (low + 12, 0.24)):
        n = int(6.0 * SR); t = np.arange(n) / SR; f = hz(m)
        T.add((np.sin(2 * math.pi * f * t) + 0.5 * np.sin(4 * math.pi * f * t) + 0.25 * np.sin(6 * math.pi * f * t)) * np.clip(t / 0.02, 0, 1) * np.exp(-t / 1.8), t0, 0.5, g * gain)
    for k, m in enumerate(chord):
        pad(T, m, t0, tail * 0.3, 0.07 * gain, 1.25, attack=0.08, release=tail)
        if k: pad(T, m + 12, t0, tail * 0.25, 0.05 * gain, 1.0, attack=0.12, release=tail * 0.8)
        if wide and k > 1: pad(T, m + 24, t0, tail * 0.2, 0.02 * gain, 0.6, attack=0.2, release=tail * 0.7)
    for k, m in enumerate(chord[1:]):
        bell(T, m + 12, t0 + 0.012 * k, 0.18 * gain, 0.1 + 0.14 * k, 2.8)

def crest(name, chord, tail=7.0, run=None, lead=True):
    """Two chords climbing to it - the flat sixth, the flat seventh, a drum quickening under
    them - and then the place's own chord struck with everything, its bells running up. Without
    the lead, the strike is at the start. Pitched throughout: sines only."""
    L = LEAD if lead else 0.0
    T = Track(L + tail + 6.5)
    root = chord[0]
    if lead:
        for i, (step, at) in enumerate(((-4, 0.0), (-2, L * 0.5))):
            r = root + step
            tri = [r, r + 7, r + 12, r + 16, r + 19, r + 24]
            for m in tri: pad(T, m, at, L * 0.5, 0.06, 1.2, attack=0.06, release=0.7)
            for k, m in enumerate(tri[2:]): bell(T, m + 12, at + 0.01 * k, 0.10 + 0.03 * i, 0.2 + 0.2 * k, 1.2)
        low = root - 12 if root - 12 >= 26 else root
        for at, g in ((0.0, 0.16), (L * 0.25, 0.10), (L * 0.5, 0.18), (L * 0.67, 0.12), (L * 0.8, 0.15), (L * 0.9, 0.18)):   # the drum, quickening
            n = int(1.2 * SR); t = np.arange(n) / SR; f = hz(low)
            T.add((np.sin(2 * math.pi * f * t) + 0.5 * np.sin(4 * math.pi * f * t)) * np.exp(-t / 0.22) * np.clip(t / 0.006, 0, 1), at, 0.5, g)
    strike(T, L, chord, 1.0, tail)
    up = run or ([m + 12 for m in chord[1:]] + [m + 24 for m in chord[1:4]])
    for k, m in enumerate(up):
        bell(T, m, L + 0.3 + 0.13 * k, 0.12 - 0.004 * k, 0.2 + 0.6 * (k % 5) / 4, 1.8)
    end = L + 0.3 + 0.13 * len(up)
    strike(T, end + 0.25, chord, 0.45, tail * 0.7, wide=False)        # and once more, softer: the echo of it
    T.hall(0.5, seconds=5.5, decay=1.8, seed=40 + len(name))
    T.write('Crest_' + name, peak=0.92, fade_out=2.5)


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    want = lambda n: not ONLY or n in ONLY
    for name in PLACES:
        if want('Bed_' + name): bed(name)
        if want('Lift_' + name): lift(name)
        if name != 'Tutorial' and want('Crest_' + name):
            # Into the Solar System the run is the planets' orbits, drawn one by one from the star.
            # Earth's is struck at once: the game cannot know the moment before it comes.
            crest(name, PLACES[name][0], run=[64, 68, 71, 76, 80, 83, 88, 92] if name == 'SolarSystem' else None, lead=name != 'Earth')
    if want('Crest_Emit'): crest('Emit', [38, 45, 50, 57, 62, 66, 69], lead=False)          # D major, at once: the light is let go
    if want('Crest_Welcome'): crest('Welcome', [38, 50, 57, 62, 66, 69, 74], tail=8.0)        # the same, higher: the title
    if want('Crest_Home'): crest('Home', [38, 45, 50, 54, 57, 62, 66, 69, 74], tail=11.0)     # D major, everything: home. The credits begin in its minor
