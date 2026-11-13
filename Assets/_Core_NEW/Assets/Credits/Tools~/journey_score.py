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
    'Tutorial':    ([38, 45, 52, 57, 62, 64], [38, 43, 50, 57, 62, 67], 0.40, 9, 0.5, 0.008, 0.0, 0.75),   # D sus2: open, waiting
    'Quasar':      ([38, 45, 53, 57, 64, 69], [38, 46, 53, 58, 65, 69], 0.85, 14, 1.2, 0.020, 2.6, 1.00),  # D minor: fierce
    'Macro':       ([34, 46, 53, 58, 62, 64, 69], [34, 48, 55, 60, 64, 67], 0.60, 10, 0.8, 0.016, 0.0, 0.9),  # B flat, lydian: vast
    'Micro':       ([43, 50, 58, 62, 65, 69], [43, 53, 58, 62, 65, 70], 0.45, 8, 0.7, 0.008, 0.0, 0.85),   # G minor 9: near, warm
    'CosmicWeb':   ([41, 48, 55, 57, 60, 65, 67], [41, 48, 55, 60, 64, 67], 0.60, 10, 0.8, 0.016, 0.0, 0.9),  # F add 9: wide, hopeful
    'MilkyWay':    ([45, 52, 55, 60, 62, 67], [45, 53, 57, 60, 65, 69], 0.50, 9, 0.7, 0.010, 0.0, 0.85),   # A minor 11: nearly home
    'SolarSystem': ([46, 53, 57, 60, 62, 65], [48, 55, 58, 62, 65, 69], 0.55, 12, 0.8, 0.010, 0.0, 0.9),   # B flat maj 9 / C: about to arrive
    'Earth':       ([41, 48, 53, 57, 60, 67], [38, 50, 53, 57, 62, 65], 0.35, 7, 0.9, 0.006, 0.0, 0.8),    # F, with D minor under it: home
}

def bed(name):
    a, b, bright, bells, sub, breath, trem, level = PLACES[name]
    seg = 12.0; T = Track(seg * 4, loop=True)
    for i, chord in enumerate((a, b, a, b)):
        t0 = i * seg
        for k, m in enumerate(chord):
            pad(T, m, t0 - 1.5, seg, 0.05 * (1.25 if k == 0 else 1.0), bright, trem=trem if k >= 3 else 0.0)
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
    """In step with the bed (same four 12 s bars, same chords): the octave above, a low octave
    under the root, and the chord running in bells. Silent in the game until the music rises."""
    a, b, bright, bells, sub, breath, trem, level = PLACES[name]
    seg = 12.0; T = Track(seg * 4, loop=True)
    for i, chord in enumerate((a, b, a, b)):
        t0 = i * seg
        for k, m in enumerate(chord[1:]):
            pad(T, m + 12, t0 - 1.0, seg, 0.045, min(1.0, bright + 0.3), attack=2.0, release=4.0, trem=3.1 if k >= 2 else 0.0)
        for m in chord[-2:]:
            pad(T, m + 24, t0 - 1.0, seg, 0.018, 0.4, attack=3.0, release=4.0)
        low = chord[0] - 12 if chord[0] - 12 >= 26 else chord[0]
        n = int((seg + 3) * SR); t = np.arange(n) / SR
        env = np.clip(t / 1.5, 0, 1) * np.clip((seg + 3 - t) / 3.0, 0, 1)
        f0 = hz(low)
        T.add((np.sin(2 * math.pi * f0 * t) + 0.5 * np.sin(4 * math.pi * f0 * t) + 0.2 * np.sin(6 * math.pi * f0 * t)) * env, t0 - 0.5, 0.5, 0.075)
        # The chord running: up and back, a note every three quarters of a second.
        tones = [m + 12 for m in chord[1:]] + [chord[1] + 24, chord[2] + 24]
        run = tones + tones[-2:0:-1]
        for j in range(16):
            m = run[j % len(run)]
            bell(T, m, t0 + j * 0.75, 0.075 if j % 4 == 0 else 0.05, 0.5 + 0.38 * math.sin(j * 1.9 + i), 1.1)
    T.hall(0.5, seed=31)
    T.write('Lift_' + name, peak=0.55)


# ── The phrase a climax arrives on ────────────────────────────────────────────────────
def crest(name, chord, tail=6.0, run=None):
    """The chord struck full - low root, every voice, its bells - and the bells running up
    out of it. Pitched throughout: sines only."""
    T = Track(tail + 6.5)
    t0 = 0.03
    for m, g in ((chord[0] - 12, 0.30), (chord[0], 0.22)):
        if m < 26: m += 12
        n = int(5.0 * SR); t = np.arange(n) / SR; f = hz(m)
        T.add((np.sin(2 * math.pi * f * t) + 0.4 * np.sin(4 * math.pi * f * t)) * np.clip(t / 0.04, 0, 1) * np.exp(-t / 1.6), t0, 0.5, g)
    for k, m in enumerate(chord):
        pad(T, m, t0, 1.6, 0.06, 0.8, attack=0.18, release=tail)
        if k: pad(T, m + 12, t0, 1.2, 0.035, 0.7, attack=0.25, release=tail * 0.8)
    for k, m in enumerate(chord[1:]):
        bell(T, m + 12, t0 + 0.014 * k, 0.17, 0.12 + 0.13 * k, 2.6)
    up = run or ([m + 12 for m in chord[1:]] + [m + 24 for m in chord[1:4]])
    for k, m in enumerate(up):
        bell(T, m, t0 + 0.35 + 0.15 * k, 0.11 - 0.004 * k, 0.2 + 0.6 * (k % 5) / 4, 1.8)
    end = t0 + 0.35 + 0.15 * len(up)
    for k, m in enumerate([up[-1], up[-3], up[-2]]):
        bell(T, m, end + 0.5 + 0.7 * k, 0.06, 0.3 + 0.2 * k, 2.2)
    T.hall(0.55, seconds=5.0, decay=1.7, seed=40 + len(name))
    T.write('Crest_' + name, peak=0.8, fade_out=2.5)


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    want = lambda n: not ONLY or n in ONLY
    for name in PLACES:
        if want('Bed_' + name): bed(name)
        if want('Lift_' + name): lift(name)
        if name != 'Tutorial' and want('Crest_' + name):
            # Into the Solar System the run is the planets' orbits, drawn one by one from the star.
            crest(name, PLACES[name][0], run=[62, 65, 69, 72, 74, 77, 81, 84] if name == 'SolarSystem' else None)
    if want('Crest_Emit'): crest('Emit', [38, 45, 50, 57, 62, 66, 69])                    # D major: the light is let go
    if want('Crest_Welcome'): crest('Welcome', [38, 50, 57, 62, 66, 69, 74], tail=8.0)     # the same, higher: the title
    if want('Crest_Home'): crest('Home', [41, 48, 53, 57, 60, 65, 67, 72], tail=10.0)      # F: home; the credits begin a third below
