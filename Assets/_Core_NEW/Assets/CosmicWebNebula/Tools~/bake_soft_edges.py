"""
Bakes a cosmic web mesh's soft-edge field for CosmicWebNebula_NEW (its _Soft texture).

    python3 bake_soft_edges.py Assets/Models/BIG.fbx Assets/_Core_NEW/Assets/CosmicWebNebula/WebSoftEdges_BIG_NEW.asset

Much of the web is thin sheets and threads: closed shells whose rim turns round in a hair's
width, so a surface shader alone stops the gas there in a hard line. The field says, for each
point round the mesh, how much of the shape is near it compared with the most there is nearby:
1 in the middle of a sheet or a thread, falling to a half and less towards its rim, its tip and
round a stray fragment. The shader fades the gas with it.

  1. The mesh's surface area, splatted into a grid in the mesh's own space (as Unity imports
     it: the file's centimetres to metres, X mirrored).
  2. Blurred over sigma (a few world units at the web's scale).
  3. Divided by its own maximum over about two sigmas round each point (so a thin sheet and a
     thick one read the same in their middles), times a gentle fade where there is very little
     surface at all.
Written as a Unity Texture3D (R8, linear, clamped), x fastest. Prints the box to put in the
material's _SoftMin and _SoftSize. Needs only numpy; reads ASCII FBX (the web's are).

Re-run it if the web's mesh changes; nothing else needs it.
"""
import re, sys, argparse
import numpy as np

ap = argparse.ArgumentParser()
ap.add_argument('fbx'); ap.add_argument('out')
ap.add_argument('--voxel', type=float, default=0.001, help='grid step, in the mesh space (the web is 3777 to 6000 times this in the world)')
ap.add_argument('--sigma', type=float, default=0.0013, help='blur, in the mesh space')
args = ap.parse_args()

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

voxel, sigma = args.voxel, args.sigma
pad = 3.0 * sigma
lo = v.min(0) - pad; hi = v.max(0) + pad
dims = np.ceil((hi - lo) / voxel).astype(int); hi = lo + dims * voxel

# 1. surface area, splatted
A, B, C = v[f[:, 0]], v[f[:, 1]], v[f[:, 2]]
area = 0.5 * np.linalg.norm(np.cross(B - A, C - A), axis=1)
cen = ((A + B + C) / 3 - lo) / voxel - 0.5
i0 = np.floor(cen).astype(int); fr = cen - i0
field = np.zeros(dims, dtype=np.float64)
for dx in (0, 1):
    for dy in (0, 1):
        for dz in (0, 1):
            w = (fr[:, 0] if dx else 1 - fr[:, 0]) * (fr[:, 1] if dy else 1 - fr[:, 1]) * (fr[:, 2] if dz else 1 - fr[:, 2])
            idx = np.clip(i0 + [dx, dy, dz], 0, dims - 1)
            np.add.at(field, (idx[:, 0], idx[:, 1], idx[:, 2]), area * w)

# 2. blurred
def blur(a, s):
    for axis in range(3):
        n = a.shape[axis]; m = 1 << int(np.ceil(np.log2(n + 8 * s)))
        x = np.arange(m); x = np.minimum(x, m - x); k = np.exp(-0.5 * (x / s) ** 2); k /= k.sum()
        F = np.fft.rfft(a, n=m, axis=axis); K = np.fft.rfft(k)
        shape = [1] * 3; shape[axis] = len(K)
        a = np.fft.irfft(F * K.reshape(shape), n=m, axis=axis).take(np.arange(n), axis=axis)
    return a
sv = sigma / voxel
b = blur(field, sv)

# 3. against the most nearby, and faded where there is hardly any surface
def maxfilter(a, r):
    for axis in range(3):
        n = a.shape[axis]; w = [(0, 0)] * 3; w[axis] = (r, r)
        q = np.pad(a, w, mode='constant')
        a = np.stack([q.take(np.arange(i, i + n), axis=axis) for i in range(2 * r + 1)]).max(0)
    return a
M = blur(maxfilter(b, int(np.ceil(2.0 * sv))), 1.0)
g = (v - lo) / voxel - 0.5
gi = np.clip(np.floor(g).astype(int), 0, dims - 2); gf = g - gi
def at_vertices(F):
    out = 0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                w = (gf[:, 0] if dx else 1 - gf[:, 0]) * (gf[:, 1] if dy else 1 - gf[:, 1]) * (gf[:, 2] if dz else 1 - gf[:, 2])
                out = out + w * F[gi[:, 0] + dx, gi[:, 1] + dy, gi[:, 2] + dz]
    return out
ref = np.percentile(at_vertices(b), 20)
soft = np.clip(b / np.maximum(M, 1e-30), 0, 1) * np.clip(b / ref, 0, 1)

data = np.round(soft.transpose(2, 1, 0) * 255).astype(np.uint8).tobytes()
name = re.sub(r'.*/', '', args.out).rsplit('.', 1)[0]
with open(args.out, 'w') as o:
    o.write(f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!117 &11700000
Texture3D:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_ImageContentsHash:
    serializedVersion: 2
    Hash: 00000000000000000000000000000000
  m_ForcedFallbackFormat: 4
  m_DownscaleFallback: 0
  serializedVersion: 3
  m_ColorSpace: 0
  m_Format: 5
  m_Width: {dims[0]}
  m_Height: {dims[1]}
  m_Depth: {dims[2]}
  m_MipCount: 1
  m_DataSize: {len(data)}
  m_TextureSettings:
    serializedVersion: 2
    m_FilterMode: 1
    m_Aniso: 0
    m_MipBias: 0
    m_WrapU: 1
    m_WrapV: 1
    m_WrapW: 1
  m_IsReadable: 0
  image data: {len(data)}
  _typelessdata: {data.hex()}
  m_StreamData:
    offset: 0
    size: 0
    path: 
""")
s = at_vertices(soft)
print(f'{name}: {dims[0]} x {dims[1]} x {dims[2]}; at the surface p5/p25/p50/p75 {np.percentile(s, [5, 25, 50, 75]).round(3)}')
print(f'  _SoftMin  ({lo[0]:.6f}, {lo[1]:.6f}, {lo[2]:.6f}, 0)')
print(f'  _SoftSize ({hi[0] - lo[0]:.6f}, {hi[1] - lo[1]:.6f}, {hi[2] - lo[2]:.6f}, 0)')
