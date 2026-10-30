"""
The glowing hearts of the second cosmic web's clusters, for CosmicWebGas_NEW: a handful of big
soft puffs where the clusters' gas is densest, drawn added together (CosmicWebGasCoreGlow_NEW),
so each node shines and blooms instead of reading as a flat patch of colour.

Reads the clusters' own puffs (WebGas_Core_NEW.bytes, from bake_web_gas.py), keeps the dense
ones, groups them into knots (touching cells of a grid), and for each knot big enough writes
three puffs at its middle: a wide faint halo, a mid glow and a small hot heart. Their size is in
the mesh's own units (the material's _Size is 1), their "hue" picks the colour along the
material's ramp (halo 0.3, glow 0.65, heart 1), their brightness falls with the knot's size.

Written as little-endian float32 records of six, as bake_web_gas.py writes them: x, y, z, size,
hue, brightness.

It also prints the first six knots as the _Knot0-5 vectors of the threads' and the middle's
materials (CosmicWebGasThreads_NEW, CosmicWebGasMiddle_NEW): xyz the knot's middle, w how far
the gas round it runs pinker and brighter (its reach x --knot-reach, at least --knot-floor).
Copy them into both materials after a re-bake.

    python bake_core_glow.py WebGas_Core_NEW.bytes WebGas_CoreGlow_NEW.bytes
"""
import argparse
from collections import defaultdict

import numpy as np

ap = argparse.ArgumentParser()
ap.add_argument('puffs', help='the clusters\' puffs (WebGas_Core_NEW.bytes)')
ap.add_argument('out', help='the glow puffs to write')
ap.add_argument('--dense', type=float, default=0.4, help='puffs denser than this make up the knots')
ap.add_argument('--cell', type=float, default=0.002, help='grid step for grouping, in the mesh space')
ap.add_argument('--smallest', type=int, default=12, help='knots with fewer dense puffs than this get no glow')
ap.add_argument('--knot-reach', type=float, default=4.0, help='how far round a knot the threads run pinker, in knot reaches')
ap.add_argument('--knot-floor', type=float, default=0.004, help='the least of that, in the mesh space')
args = ap.parse_args()

rec = np.fromfile(args.puffs, dtype='<f4').reshape(-1, 6)
dense = rec[rec[:, 4] > args.dense, :3]

cells = defaultdict(list)
for i, k in enumerate(map(tuple, np.floor(dense / args.cell).astype(int))):
    cells[k].append(i)

knots, seen = [], set()
for start in cells:
    if start in seen:
        continue
    stack, members = [start], []
    seen.add(start)
    while stack:
        c = stack.pop()
        members += cells[c]
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    n = (c[0] + dx, c[1] + dy, c[2] + dz)
                    if n in cells and n not in seen:
                        seen.add(n)
                        stack.append(n)
    if len(members) >= args.smallest:
        knots.append(dense[members])

knots.sort(key=len, reverse=True)
biggest = len(knots[0])
out = []
for k in knots:
    middle = k.mean(axis=0)
    reach = max(float(np.percentile(np.linalg.norm(k - middle, axis=1), 80)), args.cell * 0.5)
    strength = (len(k) / biggest) ** 0.35
    for size, hue, bright in ((reach * 3.2, 0.3, 0.35), (reach * 1.6, 0.65, 0.7), (reach * 0.7, 1.0, 1.0)):
        out.append([*middle, size, hue, bright * strength])
    print(f'knot of {len(k)} at {np.round(middle, 4)}, reach {reach:.4f}')

np.asarray(out, dtype='<f4').tofile(args.out)
for i, k in enumerate(knots[:6]):
    middle = k.mean(axis=0)
    reach = max(float(np.percentile(np.linalg.norm(k - middle, axis=1), 80)), args.cell * 0.5)
    print(f'_Knot{i}: ({middle[0]:.5f}, {middle[1]:.5f}, {middle[2]:.5f}, {max(reach * args.knot_reach, args.knot_floor):.5f})')
print(f'{len(out)} glow puffs for {len(knots)} knots -> {args.out}')
