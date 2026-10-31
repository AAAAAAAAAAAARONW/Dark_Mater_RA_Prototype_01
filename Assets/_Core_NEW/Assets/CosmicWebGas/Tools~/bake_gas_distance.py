"""
Where CosmicWebPyro_NEW's gas is: how near each point round a web mesh is to the mesh's own
surface, as a Unity Texture3D (R8). 1 on the surface, falling to 0 at --reach from it.

The web's threads are thin: about a world unit across (twice the mesh's volume over its area,
at the web's scale), much less than any 3D texture small enough to ship can resolve. A field of
how much of the mesh is round each point, blurred (bake_soft_edges.py), turns them into fat
blobs at that resolution, and the network is lost in clouds. A distance to the surface does not
blur away: between two samples it is near linear, so the gas still runs along each thread,
where the thread is, a few world units across, and the network keeps its shape.

The distance is to the nearest of the mesh's vertices and triangle middles (they are under a
world unit apart, so it is good to a fraction of that); not signed: inside a thread is within
its own radius of the surface anyway.

Written as little-endian bytes, x fastest, as bake_soft_edges.py writes its fields. Prints the
box for the material's _SoftMin and _SoftSize. Needs only numpy; reads ASCII FBX.

    python bake_gas_distance.py Assets/Models/BIG.fbx Assets/_Core_NEW/Assets/CosmicWebGas/WebGasField_BIG_NEW.asset
"""
import argparse
import re

import numpy as np

ap = argparse.ArgumentParser()
ap.add_argument('fbx'); ap.add_argument('out')
ap.add_argument('--voxel', type=float, default=0.0007, help='grid step, in the mesh space (the web is 3777 to 6000 times this in the world)')
ap.add_argument('--reach', type=float, default=0.0025, help='the field falls from 1 on the surface to 0 this far from it, in the mesh space')
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
points = np.concatenate([v, (v[f[:, 0]] + v[f[:, 1]] + v[f[:, 2]]) / 3])

h, reach = args.voxel, args.reach
pad = reach + 2 * h
lo = v.min(0) - pad; hi = v.max(0) + pad
dims = np.ceil((hi - lo) / h).astype(int); hi = lo + dims * h

# Nearest sample within reach, for every voxel: each point pushes its distance into the voxels
# round it (voxel centres at lo + (i + 0.5) h).
g = (points - lo) / h - 0.5
base = np.round(g).astype(np.int64)
fx, fy, fz = (g - base).astype(np.float32).T                 # where in its voxel, -0.5 to 0.5
f2 = fx * fx + fy * fy + fz * fz
flat = (base[:, 0] * dims[1] + base[:, 1]) * dims[2] + base[:, 2]
K = int(np.ceil(reach / h)) + 1                               # the box has more room than this round every point
best = np.full(int(np.prod(dims)), np.float32((reach / h) ** 2), np.float32)
r = np.arange(-K, K + 1)
for ox in r:
    for oy in r:
        for oz in r:
            if ox * ox + oy * oy + oz * oz > (K + 0.5) ** 2:
                continue
            d2 = f2 - 2 * (fx * ox + fy * oy + fz * oz) + np.float32(ox * ox + oy * oy + oz * oz)
            np.minimum.at(best, flat + ((ox * dims[1] + oy) * dims[2] + oz), d2)
dist = np.sqrt(best).reshape(dims) * h
near = np.clip(1 - dist / reach, 0, 1)

data = np.round(near.transpose(2, 1, 0) * 255).astype(np.uint8).tobytes()
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
print(f'{name}: {dims[0]} x {dims[1]} x {dims[2]}, {len(points)} surface samples; within reach {np.mean(near > 0):.3f} of the box')
print(f'  _SoftMin  ({lo[0]:.6f}, {lo[1]:.6f}, {lo[2]:.6f}, 0)')
print(f'  _SoftSize ({hi[0] - lo[0]:.6f}, {hi[1] - lo[1]:.6f}, {hi[2] - lo[2]:.6f}, 0)')
