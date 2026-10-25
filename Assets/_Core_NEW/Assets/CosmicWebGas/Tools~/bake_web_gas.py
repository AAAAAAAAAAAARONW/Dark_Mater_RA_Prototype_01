"""
Bakes the puffs CosmicWebGas_NEW draws for one of the cosmic web's meshes.

    python3 bake_web_gas.py Assets/Models/BIG.fbx Assets/_Core_NEW/Assets/CosmicWebGas/WebGas_BIG_NEW.bytes --count 14000

The puffs are scattered over the mesh's surface (more where there is more surface), in the
mesh's own space as Unity imports it (the file's centimetres to metres, X mirrored), so they
sit where the threads are in whichever web object draws them. Two kinds: most are small and
bright, the thread's own glowing core; a few (--halo) are big and faint, the glow round it.
Each gets:

  size        core about 0.45, halo about 1.3 (of the material's _Size); bigger in the thick
              of a thread, smaller towards its edge, with some spread
  hue         how dense the gas is round it, as a density map of the cosmic web is coloured:
              0 a faint thread out in the voids, 0.5 an ordinary one, 1 where the threads
              crowd into a node or a main filament (the mesh's surface area over a wide
              blur, on a log scale), with a little slow noise
  brightness  falling towards the rim of a sheet, the tip of a thread and round a stray
              fragment, so the gas thins out there instead of stopping

"Thick" is the mesh's surface area round each point, blurred over a few world units, against
the most there is nearby (as in a thin sheet and a thick one alike, the middle reads 1).

Written as little-endian float32 records of six: x, y, z, size, hue, brightness. Needs only
numpy; reads ASCII FBX (the web's are). Re-run it if the mesh changes.
"""
import re, argparse
import numpy as np

ap = argparse.ArgumentParser()
ap.add_argument('fbx'); ap.add_argument('out')
ap.add_argument('--count', type=int, default=100000)
ap.add_argument('--halo', type=float, default=0.04, help='the share of puffs that are big faint halo rather than small bright core')
ap.add_argument('--seed', type=int, default=1)
ap.add_argument('--voxel', type=float, default=0.001, help='grid step for "thick", in the mesh space')
ap.add_argument('--sigma', type=float, default=0.0013, help='blur for "thick", in the mesh space')
args = ap.parse_args()
rng = np.random.default_rng(args.seed)

def grab(t, key):
    m = re.search(key + r': \*(\d+) \{\s*a: ', t)
    start = m.end(); end = t.index('}', start)
    return np.array(t[start:end].replace('\n', '').split(','), dtype=np.float64)

t = open(args.fbx, encoding='latin1').read()
v = grab(t, 'Vertices').reshape(-1, 3) * 0.01
v[:, 0] = -v[:, 0]
p = grab(t, 'PolygonVertexIndex').astype(np.int64)
ends = np.where(p < 0)[0]; p = np.where(p < 0, -p - 1, p)
starts = np.concatenate([[0], ends[:-1] + 1]); counts = ends - starts + 1
tris = []
for k in np.unique(counts):
    sel = starts[counts == k]
    for j in range(1, k - 1):
        tris.append(np.stack([p[sel], p[sel + j], p[sel + j + 1]], 1))
f = np.concatenate(tris)
A, B, C = v[f[:, 0]], v[f[:, 1]], v[f[:, 2]]
area = 0.5 * np.linalg.norm(np.cross(B - A, C - A), axis=1)

# "Thick": surface area round each point, blurred, against the most nearby.
voxel, sigma = args.voxel, args.sigma
pad = 3.0 * sigma
lo = v.min(0) - pad; hi = v.max(0) + pad
dims = np.ceil((hi - lo) / voxel).astype(int)
cen = ((A + B + C) / 3 - lo) / voxel - 0.5
i0 = np.floor(cen).astype(int); fr = cen - i0
field = np.zeros(dims)
for dx in (0, 1):
    for dy in (0, 1):
        for dz in (0, 1):
            w = (fr[:, 0] if dx else 1 - fr[:, 0]) * (fr[:, 1] if dy else 1 - fr[:, 1]) * (fr[:, 2] if dz else 1 - fr[:, 2])
            idx = np.clip(i0 + [dx, dy, dz], 0, dims - 1)
            np.add.at(field, (idx[:, 0], idx[:, 1], idx[:, 2]), area * w)
def blur(a, s):
    for axis in range(3):
        n = a.shape[axis]; m = 1 << int(np.ceil(np.log2(n + 8 * s)))
        x = np.arange(m); x = np.minimum(x, m - x); k = np.exp(-0.5 * (x / s) ** 2); k /= k.sum()
        F = np.fft.rfft(a, n=m, axis=axis); K = np.fft.rfft(k)
        shape = [1] * 3; shape[axis] = len(K)
        a = np.fft.irfft(F * K.reshape(shape), n=m, axis=axis).take(np.arange(n), axis=axis)
    return a
def maxfilter(a, r):
    for axis in range(3):
        n = a.shape[axis]; w = [(0, 0)] * 3; w[axis] = (r, r)
        q = np.pad(a, w, mode='constant')
        a = np.stack([q.take(np.arange(i, i + n), axis=axis) for i in range(2 * r + 1)]).max(0)
    return a
sv = sigma / voxel
b = blur(field, sv)
M = blur(maxfilter(b, int(np.ceil(2.0 * sv))), 1.0)
def at(F, pts):
    g = (pts - lo) / voxel - 0.5
    gi = np.clip(np.floor(g).astype(int), 0, dims - 2); gf = g - gi
    out = 0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                w = (gf[:, 0] if dx else 1 - gf[:, 0]) * (gf[:, 1] if dy else 1 - gf[:, 1]) * (gf[:, 2] if dz else 1 - gf[:, 2])
                out = out + w * F[gi[:, 0] + dx, gi[:, 1] + dy, gi[:, 2] + dz]
    return out

# The puffs: on the surface, more where there is more of it.
n = args.count
tri = rng.choice(len(f), size=n, p=area / area.sum())
r1 = np.sqrt(rng.random(n)); r2 = rng.random(n)
pts = A[tri] * (1 - r1)[:, None] + B[tri] * (r1 * (1 - r2))[:, None] + C[tri] * (r1 * r2)[:, None]
bp = at(b, pts); thick = np.clip(bp / np.maximum(at(M, pts), 1e-30), 0, 1)
amount = np.clip(bp / np.percentile(bp, 20), 0, 1)

# A slow noise through the web for the colour: value noise on a coarse lattice, smoothed.
def slow_noise(x, cell, seed):
    g = x / cell; i = np.floor(g).astype(int); u = g - i; u = u * u * (3 - 2 * u)
    def val(ix):
        key = ix[:, 0] * 73856093 ^ ix[:, 1] * 19349663 ^ ix[:, 2] * 83492791
        return (np.sin(key * 0.000123 + seed) * 43758.5453) % 1.0
    out = 0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                w = (u[:, 0] if dx else 1 - u[:, 0]) * (u[:, 1] if dy else 1 - u[:, 1]) * (u[:, 2] if dz else 1 - u[:, 2])
                out = out + w * val(i + [dx, dy, dz])
    return out
env = np.log(np.maximum(at(blur(field, 4.0 * sv), pts), 1e-30))
lo_e, hi_e = np.percentile(env, [5, 98])
dens = np.clip((env - lo_e) / (hi_e - lo_e), 0, 1)
wobble = 0.6 * slow_noise(pts, 0.012, 3) + 0.4 * slow_noise(pts, 0.005, 7)
wobble = (wobble - wobble.mean()) / max(wobble.std(), 1e-6)
hue = np.clip(0.85 * dens ** 1.6 + 0.15 * thick + 0.06 * wobble, 0, 1)

halo = rng.random(n) < args.halo
size = np.where(halo, 1.3, 0.45) * (0.75 + 0.5 * rng.random(n)) * (0.65 + 0.55 * thick)
bright = np.where(halo, 0.15, 1.0) * (0.25 + 0.75 * thick) * amount * (0.6 + 0.8 * rng.random(n))
rec = np.stack([pts[:, 0], pts[:, 1], pts[:, 2], size, hue, bright], 1).astype('<f4')
rec.tofile(args.out)
print(f'{args.out}: {n} puffs ({halo.sum()} halo); size p10/p50/p90 {np.percentile(size, [10, 50, 90]).round(2)}, '
      f'hue {np.percentile(hue, [10, 50, 90]).round(2)}, brightness {np.percentile(bright, [10, 50, 90]).round(2)}')
