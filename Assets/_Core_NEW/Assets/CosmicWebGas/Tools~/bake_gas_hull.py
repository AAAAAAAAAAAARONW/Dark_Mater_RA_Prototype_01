"""
The hull CosmicWebPyro_NEW draws its gas from: one smooth closed skin round all of a web mesh's
gas, made from the mesh's soft field (a WebSoftEdges_*_NEW texture, from
CosmicWebNebula/Tools~/bake_soft_edges.py), where the field crosses --iso.

The gas is marched along each pixel's ray from the far side of whatever hull it is drawn on.
On the web's own meshes that went wrong in two ways: they are marching-cubes skins, terraced
and full of tunnels and bubbles inside the thick of the gas, so every fold of them started a
ray of its own in the middle of the gas and the gas broke up into shards; and pushed out to
make room for the billows, the terraces folded into rings. This skin has none of that: it is
smooth, it sits round the gas with room to spare (the field it is cut from is blurred wider
than the gas), and it is a few hundred thousand triangles instead of millions.

Surface nets: one vertex in each cell of the field's grid the skin passes through (the middle
of where it crosses the cell's edges), one quad across each edge it crosses, turned outwards;
normals from the field's slope.

Written as a Unity Mesh (.asset), in the web mesh's own space, so it sits on the threads of
whichever web object holds it. --preview also writes raw .pos/.nrm/.idx files.

From Assets/_Core_NEW/Assets (the box is the field's, as bake_soft_edges.py printed it):

    python CosmicWebGas/Tools~/bake_gas_hull.py CosmicWebNebula/WebSoftEdges_BIG_NEW.asset CosmicWebGas/WebGasHull_BIG_NEW.asset \\
        --min -0.059563 -0.049881 -0.043521 --size 0.133 0.093 0.086 --iso 0.1
    python CosmicWebGas/Tools~/bake_gas_hull.py CosmicWebNebula/WebSoftEdges_Coreyellow_NEW.asset CosmicWebGas/WebGasHull_Coreyellow_NEW.asset \\
        --min -0.058793 -0.048011 -0.041868 --size 0.132 0.091 0.084 --iso 0.08

The gas itself is read from a finer field of the same mesh (bake_soft_edges.py --voxel 0.0007
--sigma 0.0011, WebGasField_*_NEW): this wider one is only for the hull.
"""
import argparse
import re

import numpy as np

ap = argparse.ArgumentParser()
ap.add_argument('field', help='the soft field, a Unity Texture3D asset (R8)')
ap.add_argument('out', help='the Mesh asset to write')
ap.add_argument('--min', type=float, nargs=3, required=True, help='the field box\'s lowest corner (the material\'s _SoftMin)')
ap.add_argument('--size', type=float, nargs=3, required=True, help='the field box\'s size (_SoftSize)')
ap.add_argument('--iso', type=float, default=0.1, help='the field value the skin follows: lower, roomier')
ap.add_argument('--preview', help='also write <this>.pos/.nrm/.idx (float32, float32, uint32)')
args = ap.parse_args()

t = open(args.field).read()
w, h, d = (int(re.search(k + r': (\d+)', t).group(1)) for k in ('m_Width', 'm_Height', 'm_Depth'))
F = np.frombuffer(bytes.fromhex(re.search(r'_typelessdata: ([0-9a-f]+)', t).group(1)), np.uint8)
F = F.reshape(d, h, w).transpose(2, 1, 0).astype(np.float32) / 255.0      # [x, y, z]
lo, size = np.array(args.min), np.array(args.size)
cell = size / np.array(F.shape)
iso = args.iso
inside = F > iso
n = np.array(F.shape)

# Where the skin crosses each grid edge, summed into the four cells round that edge.
acc = np.zeros(tuple(n - 1) + (3,)); cnt = np.zeros(tuple(n - 1))
quads, outward = [], []
for axis in range(3):
    a = [slice(None)] * 3; b = [slice(None)] * 3
    a[axis] = slice(0, -1); b[axis] = slice(1, None)
    F0, F1 = F[tuple(a)], F[tuple(b)]
    hit = np.argwhere(inside[tuple(a)] != inside[tuple(b)])
    f0, f1 = F0[tuple(hit.T)], F1[tuple(hit.T)]
    at = hit.astype(np.float64); at[:, axis] += (iso - f0) / (f1 - f0)
    u, v = [k for k in range(3) if k != axis]
    ring = []
    for du, dv in ((-1, -1), (0, -1), (0, 0), (-1, 0)):
        c = hit.copy(); c[:, u] += du; c[:, v] += dv
        ring.append(c)
        ok = np.all((c >= 0) & (c < n - 1), axis=1)
        np.add.at(acc, tuple(c[ok].T), at[ok]); np.add.at(cnt, tuple(c[ok].T), 1)
    ring = np.stack(ring, 1)                                     # quad's four cells, round the edge
    ok = np.all((ring >= 0) & (ring < (n - 1)), axis=(1, 2))
    quads.append(ring[ok])
    sign = np.where(inside[tuple(a)][tuple(hit[ok].T)], 1.0, -1.0)   # inside at the low end: out is +axis
    o = np.zeros((ok.sum(), 3)); o[:, axis] = sign
    outward.append(o)
quads = np.concatenate(quads); outward = np.concatenate(outward)

# One vertex per crossed cell, in the mesh's space.
used = cnt > 0
index = -np.ones(cnt.shape, np.int64); index[used] = np.arange(used.sum())
grid = acc[used] / cnt[used][:, None]
pos = lo + (grid + 0.5) * cell

# Normals: outward, down the field's slope at the vertex.
g = np.stack(np.gradient(F), -1)
gi = np.clip(np.round(grid).astype(int), 0, n - 1)
nrm = -g[tuple(gi.T)]
nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-9)

# Two triangles a quad, turned so that cross(b - a, c - a) points out (Unity's front face). The
# quad's turn comes from the edge it crosses (its four cells go round that edge one way: out
# along +x, -y, +z), so neighbouring quads always agree; of its two diagonals, the one that
# does not fold it.
q = index[tuple(quads.reshape(-1, 3).T)].reshape(-1, 4)
axis_out = np.array([[1, 0, 0], [0, -1, 0], [0, 0, 1]])[np.argmax(np.abs(outward), 1)]
reverse = (axis_out * outward).sum(1) < 0
q[reverse] = q[reverse][:, ::-1]
def facing(t):
    A, B, C = pos[t[:, 0]], pos[t[:, 1]], pos[t[:, 2]]
    n = np.cross(B - A, C - A)
    return (n * outward).sum(1) / np.maximum(np.linalg.norm(n, axis=1), 1e-30)
s1 = np.minimum(facing(q[:, [0, 1, 2]]), facing(q[:, [0, 2, 3]]))
s2 = np.minimum(facing(q[:, [0, 1, 3]]), facing(q[:, [1, 2, 3]]))
first = s1 >= s2
tri = np.concatenate([np.where(first[:, None], q[:, [0, 1, 2]], q[:, [0, 1, 3]]),
                      np.where(first[:, None], q[:, [0, 2, 3]], q[:, [1, 2, 3]])])
flip = np.concatenate([np.maximum(s1, s2), np.maximum(s1, s2)]) < 0
tri = tri[(tri[:, 0] != tri[:, 1]) & (tri[:, 1] != tri[:, 2]) & (tri[:, 0] != tri[:, 2])]

pos32, nrm32, idx32 = pos.astype('<f4'), nrm.astype('<f4'), tri.astype('<u4')
if args.preview:
    pos32.tofile(args.preview + '.pos'); nrm32.tofile(args.preview + '.nrm'); idx32.tofile(args.preview + '.idx')

vtx = np.concatenate([pos32, nrm32], 1).astype('<f4').tobytes()
ib = idx32.tobytes()
mn, mx = pos.min(0), pos.max(0)
centre, extent = (mn + mx) / 2, (mx - mn) / 2
name = re.sub(r'.*/', '', args.out).rsplit('.', 1)[0]
v3 = lambda p: f'{{x: {p[0]:.6f}, y: {p[1]:.6f}, z: {p[2]:.6f}}}'
empty = '    - stream: 0\n      offset: 0\n      format: 0\n      dimension: 0\n'
packed = lambda k: f'    {k}:\n      m_NumItems: 0\n      m_Range: 0\n      m_Start: 0\n      m_Data: \n      m_BitSize: 0\n'
bits = lambda k: f'    {k}:\n      m_NumItems: 0\n      m_Data: \n      m_BitSize: 0\n'
with open(args.out, 'w') as o:
    o.write(f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!43 &4300000
Mesh:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  serializedVersion: 10
  m_SubMeshes:
  - serializedVersion: 2
    firstByte: 0
    indexCount: {idx32.size}
    topology: 0
    baseVertex: 0
    firstVertex: 0
    vertexCount: {len(pos32)}
    localAABB:
      m_Center: {v3(centre)}
      m_Extent: {v3(extent)}
  m_Shapes:
    vertices: []
    shapes: []
    channels: []
    fullWeights: []
  m_BindPose: []
  m_BoneNameHashes:
  m_RootBoneNameHash: 0
  m_BonesAABB: []
  m_VariableBoneCountWeights:
    m_Data:
  m_MeshCompression: 0
  m_IsReadable: 0
  m_KeepVertices: 0
  m_KeepIndices: 0
  m_IndexFormat: 1
  m_IndexBuffer: {ib.hex()}
  m_VertexData:
    serializedVersion: 3
    m_VertexCount: {len(pos32)}
    m_Channels:
    - stream: 0
      offset: 0
      format: 0
      dimension: 3
    - stream: 0
      offset: 12
      format: 0
      dimension: 3
{empty * 12}    m_DataSize: {len(vtx)}
    _typelessdata: {vtx.hex()}
  m_CompressedMesh:
{packed('m_Vertices')}{packed('m_UV')}{packed('m_Normals')}{packed('m_Tangents')}{bits('m_Weights')}{bits('m_NormalSigns')}{bits('m_TangentSigns')}{packed('m_FloatColors')}{bits('m_BoneIndices')}{bits('m_Triangles')}    m_UVInfo: 0
  m_LocalAABB:
    m_Center: {v3(centre)}
    m_Extent: {v3(extent)}
  m_MeshUsageFlags: 0
  m_BakedConvexCollisionMesh:
  m_BakedTriangleCollisionMesh:
  m_MeshMetrics[0]: 1
  m_MeshMetrics[1]: 1
  m_MeshOptimizationFlags: 1
  m_StreamData:
    offset: 0
    size: 0
    path:
""")
print(f'{name}: {len(pos32)} vertices, {len(idx32)} triangles; still folded {flip.mean():.4f}; box {np.round(mn, 4)} .. {np.round(mx, 4)}')
