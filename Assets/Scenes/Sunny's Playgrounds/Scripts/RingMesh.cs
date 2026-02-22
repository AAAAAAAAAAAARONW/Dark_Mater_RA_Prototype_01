using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RingMesh : MonoBehaviour
{
    [Min(0.001f)] public float innerRadius = 1f;
    [Min(0.001f)] public float outerRadius = 1.2f;
    [Range(3, 512)] public int segments = 128;

    // If true, creates both top and bottom faces (useful if camera can go below).
    public bool doubleSided = true;

    void OnValidate()
    {
        innerRadius = Mathf.Max(0.001f, innerRadius);
        outerRadius = Mathf.Max(innerRadius + 0.001f, outerRadius);
        segments = Mathf.Clamp(segments, 3, 512);
        Generate();
    }

    void Reset() => Generate();

    [ContextMenu("Generate")]
    public void Generate()
    {
        var mesh = new Mesh();
        mesh.name = "RingMesh";

        int vertCount = (segments + 1) * 2;
        var verts = new Vector3[vertCount];
        var normals = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];

        // Two vertices per segment step: inner, outer
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float ang = t * Mathf.PI * 2f;
            float ca = Mathf.Cos(ang);
            float sa = Mathf.Sin(ang);

            int idx = i * 2;

            verts[idx + 0] = new Vector3(ca * innerRadius, 0f, sa * innerRadius);
            verts[idx + 1] = new Vector3(ca * outerRadius, 0f, sa * outerRadius);

            normals[idx + 0] = Vector3.up;
            normals[idx + 1] = Vector3.up;

            // UVs: U wraps around, V goes from inner(0) to outer(1)
            uvs[idx + 0] = new Vector2(0f, t);
            uvs[idx + 1] = new Vector2(1f, t);
        }

        int triCount = segments * 2;          // 2 triangles per segment
        int indexCount = triCount * 3;
        var tris = new int[indexCount];

        int ti = 0;
        for (int i = 0; i < segments; i++)
        {
            int idx = i * 2;

            // Top face
            tris[ti++] = idx + 0;
            tris[ti++] = idx + 3;
            tris[ti++] = idx + 1;

            tris[ti++] = idx + 0;
            tris[ti++] = idx + 2;
            tris[ti++] = idx + 3;
        }

        mesh.vertices = verts;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        if (doubleSided)
        {
            // Duplicate triangles with reversed winding for bottom face
            var tris2 = new int[tris.Length * 2];
            tris.CopyTo(tris2, 0);

            int offset = tris.Length;
            for (int i = 0; i < tris.Length; i += 3)
            {
                tris2[offset + i + 0] = tris[i + 0];
                tris2[offset + i + 1] = tris[i + 2];
                tris2[offset + i + 2] = tris[i + 1];
            }

            // Duplicate normals as down for bottom face by duplicating vertices (simpler for correct lighting)
            // If using unlit rings, you can skip vertex duplication and just use a double-sided shader.
            // Here we keep it simple: rely on double-sided triangles only.
            mesh.triangles = tris2;
        }

        GetComponent<MeshFilter>().sharedMesh = mesh;
    }
}