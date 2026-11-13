"""
The score from the tutorial to the end of the journey, for JourneyScore_NEW: made, like the
credits' theme, from nothing but sines and noise, in its key (D minor), which it leads into.

  beds   Bed_<place>.ogg   a seamless loop for each place: held harmony with no beat, two
                           colours of one chord turning slowly, so it can be left at any moment
  cues   Cue_<name>.ogg    one for each transition, made to that transition's own timing, so
                           its climax lands on the picture's when the game starts it with the
                           picture

A dive's timing is read from the journey scene (its LayerDive_NEW rows), so after a dive is
re-timed, running this again puts the music back on it.

    python3 journey_score.py <Vizlab_Journey.unity> <out dir>     (needs numpy, ffmpeg)
"""
import sys, math, os, re, wave, subprocess
import numpy as np

SR = 44100
OUT = sys.argv[2] if len(sys.argv) > 2 else '.'
SCENE = sys.argv[1] if len(sys.argv) > 1 else None
ONLY = set(sys.argv[3:])


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

def swell_bell(track, midi, end, rise, gain, pan=0.5):
    """A bell played backwards: it grows to its strike, at `end`."""
    n = int(rise * SR); t = np.arange(n) / SR; f = hz(midi)
    s = (np.sin(2 * math.pi * f * t) + 0.3 * np.sin(2 * math.pi * 2 * f * t)) * np.exp(-(rise - t) / (rise * 0.33))
    track.add(s * np.clip((rise - t) / 0.02, 0, 1), end - rise, pan, gain)

def glide(track, f0, f1, start, dur, gain, curve=1.0, pan=0.5, harm=3):
    """A tone sliding from f0 to f1 (Hz), growing as it goes."""
    n = int(dur * SR); t = np.arange(n) / SR; k = (t / dur) ** curve
    f = f0 * (f1 / f0) ** k
    ph = 2 * math.pi * np.cumsum(f) / SR
    s = sum(np.sin(ph * h) / h ** 1.6 for h in range(1, harm + 1))
    env = (t / dur) ** 1.8 * np.clip((dur - t) / 0.08, 0, 1)
    track.add(s * env, start, pan, gain)

def wind(track, start, dur, f0, f1, gain, shape='rise', width=0.5, seed=1):
    """Noise through a band that slides from f0 to f1: a rush of air."""
    n = int(dur * SR); r = np.random.default_rng(seed)
    out = np.zeros((2, n)); t = np.arange(n) / SR; k = t / dur
    hop = 2048; win = np.hanning(hop * 2)
    for c in range(2):
        x = r.standard_normal(n + hop * 2); y = np.zeros(n + hop * 2)
        for i in range(0, n, hop):
            seg = x[i:i + hop * 2] * win
            fc = f0 * (f1 / f0) ** min(1.0, (i + hop) / n)
            fr = np.fft.rfftfreq(hop * 2, 1 / SR)
            sp = np.fft.rfft(seg) * np.exp(-((np.log(np.maximum(fr, 1)) - math.log(fc)) / width) ** 2)
            y[i:i + hop * 2] += np.fft.irfft(sp, hop * 2) * win
        out[c] = y[:n]
    env = {'rise': k ** 2.2 * np.clip((1 - k) / 0.02, 0, 1), 'fall': (1 - k) ** 2.0 * np.clip(k / 0.01, 0, 1),
           'hump': np.sin(math.pi * k) ** 1.5}[shape]
    out /= np.abs(out).max() + 1e-9
    track.add(out[0] * env, start, 0.0, gain); track.add(out[1] * env, start, 1.0, gain)

def boom(track, start, f0, f1, gain, decay=1.6):
    """A low hit: a sine falling in pitch."""
    n = int(decay * 4 * SR); t = np.arange(n) / SR
    f = f1 + (f0 - f1) * np.exp(-t / 0.12)
    s = np.sin(2 * math.pi * np.cumsum(f) / SR) * np.exp(-t / decay) * np.clip(t / 0.004, 0, 1)
    track.add(s, start, 0.5, gain)

def crash(track, start, gain, decay=1.4, centre=5000, seed=3):
    n = int(decay * 4 * SR); r = np.random.default_rng(seed)
    for c, pan in ((0, 0.0), (1, 1.0)):
        x = r.standard_normal(n); sp = np.fft.rfft(x); f = np.fft.rfftfreq(n, 1 / SR)
        sp *= np.exp(-((np.log(np.maximum(f, 1)) - math.log(centre)) / 0.7) ** 2)
        y = np.fft.irfft(sp, n); y /= np.abs(y).max()
        t = np.arange(n) / SR
        track.add(y * np.exp(-t / decay) * np.clip(t / 0.003, 0, 1), start, pan, gain)

def air(track, gain, centre=6500, seed=9, period=23.0):
    n = track.n; r = np.random.default_rng(seed)
    sp = np.fft.rfft(r.standard_normal(n)); f = np.fft.rfftfreq(n, 1 / SR)
    sp *= np.exp(-((np.log(np.maximum(f, 1)) - math.log(centre)) / 0.35) ** 2)
    a = np.fft.irfft(sp, n); a /= np.abs(a).max()
    t = np.arange(n) / SR
    cycles = max(1, round(n / SR / period))
    a *= gain * (0.6 + 0.4 * np.sin(2 * math.pi * cycles * t / (n / SR)))
    track.L += a; track.R += np.roll(a, 331)


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
    air(T, breath, seed=sum(map(ord, name)), period=24.0)
    T.hall(0.5)
    T.write('Bed_' + name, peak=0.5 * level)


# ── The transitions ───────────────────────────────────────────────────────────────────
def dive_in(name, to, D, H, E):
    """Into a smaller world: everything rises and narrows to the peak at D, then the new place opens."""
    chord = PLACES[to][0]
    T = Track(D + H + E + 6.0)
    wind(T, 0, D, 300, 7000, 0.55, 'rise', seed=11)
    glide(T, hz(chord[0] + 12) / 2, hz(chord[0] + 12), 0, D, 0.20, curve=1.6)
    glide(T, hz(chord[2] + 12) / 2, hz(chord[2] + 12), D * 0.25, D * 0.75, 0.12, curve=1.6, pan=0.3)
    for k, m in enumerate(chord[2:]): swell_bell(T, m + 12, D, min(D, 2.4) - 0.2 * k, 0.09, 0.2 + 0.15 * k)
    p = D
    boom(T, p, 150, 46, 0.9, 1.3)
    crash(T, p, 0.30, 1.3, 6000, seed=4)
    for k, m in enumerate(chord[1:]): bell(T, m + 12, p + 0.012 * k, 0.16, 0.15 + 0.14 * k, 2.2)
    bell(T, chord[-1] + 24, p + 0.1, 0.07, 0.6, 1.8)
    for k, m in enumerate(chord): pad(T, m, p - 0.3, H + E * 0.6, 0.045, 0.7, attack=0.6, release=E * 0.6 + 2.5)
    T.hall(0.5, seed=21)
    T.write('Cue_' + name, peak=0.85, fade_out=1.5)

def dive_out(name, to, D, H, E):
    """Out to a larger world: a breath in, falling, then at D the bottom drops and it all opens wide."""
    chord = PLACES[to][0]
    T = Track(D + H + E + 7.0)
    wind(T, 0, D, 6500, 260, 0.5, 'rise', seed=12)
    glide(T, hz(chord[0] + 24), hz(chord[0] + 12), 0, D, 0.16, curve=0.7)
    glide(T, hz(chord[0] + 12), hz(chord[0]), D * 0.3, D * 0.7, 0.14, curve=0.8, pan=0.7)
    for k, m in enumerate(chord[1:4]): swell_bell(T, m, D, min(D, 2.8) - 0.25 * k, 0.10, 0.3 + 0.2 * k)
    p = D
    boom(T, p, 110, 34, 1.0, 2.2)
    crash(T, p, 0.22, 2.2, 3200, seed=5)
    wind(T, p, E + 2.0, 900, 180, 0.22, 'fall', width=0.8, seed=13)
    for k, m in enumerate(chord): pad(T, m - (12 if k > 1 else 0) + 12, p - 0.4, H + E, 0.055, 0.6, attack=0.9, release=4.0)
    # The world left falls away: its bells run down.
    top = [chord[-1] + 12, chord[-2] + 12, chord[-3] + 12, chord[2] + 12, chord[1] + 12]
    for k, m in enumerate(top): bell(T, m, p + 0.35 + 0.42 * k * (E / 5.0), 0.12 - 0.012 * k, 0.75 - 0.12 * k, 2.2)
    T.hall(0.55, seconds=5.0, decay=1.6, seed=22)
    T.write('Cue_' + name, peak=0.85, fade_out=2.0)

def dive_solar(D, H, E, settle):
    """The long one, into the Sun's system: the galaxy spirals up round one star (to D), the star
    alone, its flare (at D + H + 0.7), the orbits drawn, the system growing into place (the landing
    at D + H + E), and the settle."""
    chord = PLACES['SolarSystem'][0]; scale = [57, 60, 62, 65, 67, 69, 72, 74, 77, 79, 81, 84]
    flare = D + H + 0.7; land = D + H + E
    T = Track(land + settle + 7.0)
    wind(T, 0, D + 0.4, 260, 8000, 0.45, 'rise', seed=14)
    glide(T, hz(46), hz(58), 0, D, 0.18, curve=1.4)
    glide(T, hz(53), hz(65), D * 0.2, D * 0.8, 0.12, curve=1.4, pan=0.3)
    # The spiral: notes climbing, closer and closer together.
    t, i = 0.3, 0
    while t < D - 0.15:
        k = t / D
        bell(T, scale[i % len(scale)] + (12 if i >= len(scale) else 0), t, 0.05 + 0.07 * k, 0.5 + 0.4 * math.sin(i * 1.3), 0.9)
        t += 0.62 * (1 - k) ** 1.15 + 0.07; i += 1
    # The star alone: one high note held.
    n = int((flare - D + 0.6) * SR); tt = np.arange(n) / SR
    T.add(np.sin(2 * math.pi * hz(86) * tt) * np.clip(tt / 0.3, 0, 1) * np.clip((n / SR - tt) / 0.5, 0, 1), D - 0.1, 0.5, 0.05)
    for k, m in enumerate(chord[2:]): swell_bell(T, m + 12, flare, 1.2 - 0.1 * k, 0.10, 0.2 + 0.2 * k)
    # The flare.
    crash(T, flare, 0.26, 1.8, 7500, seed=6)
    boom(T, flare, 90, 46, 0.5, 1.6)
    for k, m in enumerate(chord): bell(T, m + 24 if k > 2 else m + 12, flare + 0.01 * k, 0.17, 0.15 + 0.13 * k, 2.6)
    # The orbits, innermost first: a run up, one note an orbit, as they are drawn.
    run = [62, 65, 69, 72, 74, 77, 81, 84]
    for k, m in enumerate(run): bell(T, m, flare + 0.3 + 0.14 * k, 0.11, 0.2 + 0.08 * k, 1.5)
    # The system grows into its place.
    for k, m in enumerate(chord): pad(T, m, flare - 0.2, land - flare + settle * 0.5, 0.05, 0.65, attack=1.2, release=settle + 3.0)
    for k, m in enumerate(chord[1:4]): pad(T, m + 12, flare + 1.0, land - flare - 1.0, 0.025, 0.5, attack=3.0, release=4.0)
    wind(T, flare, land - flare, 1200, 400, 0.10, 'hump', width=0.9, seed=15)
    boom(T, land, 70, 41, 0.35, 2.0)
    bell(T, chord[0] + 24, land, 0.12, 0.5, 3.0); bell(T, chord[3] + 12, land + 0.02, 0.10, 0.3, 3.0); bell(T, chord[5] + 12, land + 0.04, 0.09, 0.7, 3.0)
    T.hall(0.5, seconds=5.0, decay=1.5, seed=23)
    T.write('Cue_Dive_SolarSystem', peak=0.85, fade_out=2.5)

def outro(pull=5.0, fade=3.5, title=2.2):
    """The tutorial ends: the camera pulls out (to `pull`), the frame goes to black, the title comes up."""
    T = Track(pull + fade + title + 12.0)
    d = [38, 45, 52, 57, 62, 64]; bb = [34, 46, 53, 58, 62, 65]; top = [38, 50, 57, 62, 66, 69]   # D sus2, B flat, then D major: welcome
    for k, m in enumerate(d): pad(T, m, 0, pull, 0.05, 0.3 + 0.4 * 1, attack=pull * 0.9, release=3.0)
    glide(T, hz(50), hz(62), 0, pull, 0.10, curve=1.2)
    wind(T, 0, pull + 1.0, 5000, 400, 0.20, 'hump', width=0.8, seed=16)
    for k, m in enumerate(bb): pad(T, m, pull - 0.5, fade, 0.055, 0.6, attack=1.2, release=3.0)
    at = pull + fade + 0.6
    for k, m in enumerate(top[2:]): swell_bell(T, m + 12, at, 1.6 - 0.15 * k, 0.09, 0.2 + 0.2 * k)
    boom(T, at, 80, 37, 0.45, 2.0)
    for k, m in enumerate(top): pad(T, m, at - 0.4, title + 3.0, 0.055, 0.7, attack=0.8, release=7.0)
    for k, m in enumerate(top[1:]): bell(T, m + 12, at + 0.015 * k, 0.15, 0.15 + 0.15 * k, 2.8)
    for k, m in enumerate([74, 78, 81, 86]): bell(T, m, at + 0.9 + 0.5 * k, 0.07, 0.3 + 0.12 * k, 2.0)
    T.hall(0.55, seconds=5.0, decay=1.6, seed=24)
    T.write('Cue_Outro', peak=0.8, fade_out=3.0)

def earth_approach(rise=17.0):
    """Earth grows to fill the screen: a long rise that holds at its top until the light goes in."""
    T = Track(rise + 9.0)
    a = [45, 52, 57, 62, 64, 69]    # A sus4: wanting D
    for k, m in enumerate(a): pad(T, m, 0, rise + 3.0, 0.05, 0.75, attack=rise * 0.95, release=5.0, trem=0.0 if k < 3 else 3.2)
    glide(T, hz(45), hz(57), rise * 0.2, rise * 0.8, 0.12, curve=1.3)
    glide(T, hz(57), hz(69), rise * 0.5, rise * 0.5, 0.08, curve=1.3, pan=0.7)
    wind(T, 0, rise + 2.5, 300, 5000, 0.30, 'rise', width=0.7, seed=17)
    t, i = rise * 0.3, 0
    while t < rise:        # a pulse, quickening
        k = t / rise
        boom(T, t, 70, 45, 0.10 + 0.22 * k, 0.5)
        t += 1.6 * (1 - k) + 0.42; i += 1
    T.hall(0.5, seed=25)
    T.write('Cue_EarthApproach', peak=0.7, fade_out=5.0)

def earth_arrive(descent=14.0, summit=16.0):
    """The light goes into Earth: the hit, the rush of air, and the long fall to the ground."""
    T = Track(descent + summit * 0.5 + 8.0)
    f = [41, 48, 53, 57, 60, 67]; dm = [38, 50, 53, 57, 62, 65]
    boom(T, 0, 120, 36, 1.0, 2.4)
    crash(T, 0, 0.22, 2.0, 2800, seed=7)
    wind(T, 0, descent, 3200, 220, 0.34, 'fall', width=0.8, seed=18)
    for k, m in enumerate(dm): pad(T, m, -0.3, descent * 0.5, 0.055, 0.6, attack=0.7, release=5.0)
    for k, m in enumerate(f): pad(T, m, descent * 0.45, descent * 0.55 + summit * 0.3, 0.05, 0.45, attack=4.0, release=7.0)
    fall = [81, 79, 77, 74, 72, 69, 67, 65, 62, 60]     # down, slower and slower: the ground comes up and settles
    t = 0.8
    for k, m in enumerate(fall):
        bell(T, m, t, 0.12 - 0.006 * k, 0.5 + 0.35 * math.sin(k * 1.7), 2.4)
        t += 0.75 + 0.22 * k
    T.hall(0.55, seconds=5.0, decay=1.7, seed=26)
    T.write('Cue_EarthArrive', peak=0.85, fade_out=5.0)

def dome():
    """The light goes into the telescope's dome: a low door closing, one warm bell."""
    T = Track(8.0)
    boom(T, 0, 85, 38, 0.8, 1.5)
    bell(T, 50, 0.02, 0.25, 0.5, 3.2); bell(T, 57, 0.05, 0.14, 0.35, 2.6); bell(T, 62, 0.4, 0.08, 0.65, 2.4)
    T.hall(0.55, seconds=5.0, decay=1.8, seed=27)
    T.write('Cue_Dome', peak=0.6, fade_out=2.0)

def spectrum(grow=2.5, hold=6.0, fade=2.0):
    """The cut to the observatory, and the spectrum filling the screen: B flat, C, and home on F -
    the credits' theme then begins a third below, on D minor."""
    T = Track(grow + hold + fade + 8.0)
    bb = [34, 46, 53, 58, 62, 65]; c = [36, 48, 55, 60, 64, 67]; f = [41, 48, 53, 57, 60, 65, 67, 72]
    for k, m in enumerate(bb): pad(T, m, 0, grow * 0.5, 0.05, 0.55, attack=0.8, release=2.0)
    for k, m in enumerate(c): pad(T, m, grow * 0.45, grow * 0.5, 0.055, 0.65, attack=0.8, release=2.0)
    glide(T, hz(48), hz(60), 0, grow, 0.10, curve=1.2)
    for k, m in enumerate(f[3:]): swell_bell(T, m + 12, grow, 1.5 - 0.12 * k, 0.08, 0.15 + 0.17 * k)
    boom(T, grow, 85, 43, 0.6, 2.2)
    crash(T, grow, 0.14, 2.2, 6000, seed=8)
    for k, m in enumerate(f): pad(T, m, grow - 0.3, hold, 0.05, 0.7, attack=0.7, release=fade + 5.0)
    for k, m in enumerate(f[1:]): bell(T, m + 12, grow + 0.012 * k, 0.14, 0.12 + 0.11 * k, 3.0)
    r = np.random.default_rng(77)
    for k in range(9): bell(T, [72, 77, 79, 81, 84, 89][r.integers(6)], grow + 0.8 + k * 0.62 + r.uniform(0, 0.2), 0.06, r.uniform(0.2, 0.8), 2.2)
    T.hall(0.55, seconds=5.5, decay=1.8, seed=28)
    T.write('Cue_Spectrum', peak=0.8, fade_out=4.0)


def dives_from(scene):
    """toLayerId -> (direction, continuous, dive, hold, emerge, settle), from the scene's LayerDive_NEW rows."""
    defaults = {'Micro': (0, 0, 3, 0.2, 2.8, 3), 'CosmicWeb': (1, 0, 3, 0.2, 4.5, 3), 'MilkyWay': (0, 0, 3, 0.2, 2.8, 3),
                'SolarSystem': (0, 1, 5.5, 0.3, 6.5, 3), 'Macro': (1, 0, 6.5, 0.2, 5, 3)}
    if not scene or not os.path.exists(scene): return defaults
    s = open(scene, encoding='utf-8').read()
    found = {}
    for m in re.finditer(r'- enabled: \d+\n\s+toLayerId: (\w+)\n(.*?)(?=\n  - enabled: |\n--- )', s, flags=re.S):
        body = m.group(2)
        def num(key, d=0.0):
            g = re.search(r'\n\s+' + key + r': ([-\d.e]+)', '\n' + body); return float(g.group(1)) if g else d
        if 'diveSeconds' in body:
            found[m.group(1)] = (int(num('direction')), int(num('continuous')), num('diveSeconds', 3), num('peakHoldSeconds', 0.2),
                                 num('emergeSeconds', 2.8), num('endSettleSeconds', 3))
    return found or defaults


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    want = lambda n: not ONLY or n in ONLY
    for name in PLACES:
        if want('Bed_' + name): bed(name)
    rows = dives_from(SCENE)
    print('dives:', rows)
    for to, (direction, continuous, D, H, E, settle) in rows.items():
        if not want('Cue_Dive_' + to): continue
        if continuous: dive_solar(D, H, E, settle)
        elif direction == 1: dive_out('Dive_' + to, to, D, H, E)
        else: dive_in('Dive_' + to, to, D, H, E)
    if want('Cue_Outro'): outro()
    if want('Cue_EarthApproach'): earth_approach()
    if want('Cue_EarthArrive'): earth_arrive()
    if want('Cue_Dome'): dome()
    if want('Cue_Spectrum'): spectrum()
