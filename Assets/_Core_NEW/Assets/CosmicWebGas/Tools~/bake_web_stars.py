"""
The glowing points of the cosmic web, for CosmicWebGas_NEW: the galaxies strung along its
threads and crowded into its nodes, as the reference shows them — a second thing beside the
gas, small sharp points of light, sparse along a thread, thick where threads meet, packed
into a cluster's heart.

Picks them from the web's own gas puffs (bake_web_gas.py's files: the threads', the middle's
and the clusters'), so they sit where the gas is: a puff is more likely picked where the gas
is denser (its "hue") and away from a thin rim (its brightness), and many more from the
clusters than from the threads. Each gets:

  size        about 1 (of the material's _Size), a few bigger
  hue         how far along the material's ramp its colour is: most a warm peach, those in
              dense gas orange to yellow, as hot as the place it sits
  brightness  mostly faint, a few bright (a power law), so the web reads as a dust of light
              with some points standing out and blooming

Written as little-endian float32 records of six, as bake_web_gas.py writes them: x, y, z,
size, hue, brightness.

    python bake_web_stars.py WebGas_BIG_NEW.bytes WebGas_Coreyellow_NEW.bytes WebGas_Core_NEW.bytes WebGas_Stars_NEW.bytes
"""
import argparse

import numpy as np

ap = argparse.ArgumentParser()
ap.add_argument('threads', help='the threads\' puffs (WebGas_BIG_NEW.bytes)')
ap.add_argument('middle', help='the middle\'s puffs (WebGas_Coreyellow_NEW.bytes)')
ap.add_argument('clusters', help='the clusters\' puffs (WebGas_Core_NEW.bytes)')
ap.add_argument('out', help='the points to write')
ap.add_argument('--counts', type=int, nargs=3, default=(6000, 2000, 2200), help='how many from the threads, the middle and the clusters')
ap.add_argument('--steep', type=float, default=2.0, help='how much more likely in dense gas (the puff\'s hue to this power)')
ap.add_argument('--bright', type=float, default=2.2, help='power law of the brightness: higher, fewer bright points')
ap.add_argument('--seed', type=int, default=7)
args = ap.parse_args()
rng = np.random.default_rng(args.seed)

out = []
for path, count, warm in zip((args.threads, args.middle, args.clusters), args.counts, (0.0, 0.15, 0.35)):
    rec = np.fromfile(path, dtype='<f4').reshape(-1, 6)
    hue, rim = np.clip(rec[:, 4], 0, 1), np.clip(rec[:, 5], 0, 1)
    w = (0.05 + hue) ** args.steep * rim
    pick = rng.choice(len(rec), size=min(count, len(rec)), replace=False, p=w / w.sum())
    r = rec[pick]
    n = len(r)
    size = np.where(rng.random(n) < 0.08, rng.uniform(1.6, 2.6, n), rng.uniform(0.7, 1.2, n))
    tone = np.clip(warm + 0.55 * np.clip(r[:, 4], 0, 1) + rng.normal(0, 0.12, n), 0, 1)
    bright = np.clip(rng.pareto(args.bright, n) * 0.35 + 0.15, 0.1, 3.0) * (0.6 + 0.4 * np.clip(r[:, 5], 0, 1))
    out.append(np.stack([r[:, 0], r[:, 1], r[:, 2], size, tone, bright], 1))
    print(f'{path}: {n} points, bright p50/p95/max {np.percentile(bright, [50, 95]).round(2)} {bright.max():.2f}')

out = np.concatenate(out).astype('<f4')
rng.shuffle(out)
out.tofile(args.out)
print(f'{len(out)} points -> {args.out}')
