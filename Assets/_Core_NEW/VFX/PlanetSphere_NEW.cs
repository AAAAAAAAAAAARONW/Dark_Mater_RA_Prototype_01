using UnityEngine;

/// <summary>Shared smooth sphere for close planet views and the thin atmosphere shells.</summary>
[ExecuteAlways, RequireComponent(typeof(MeshFilter))]
[DefaultExecutionOrder(-310)]
public sealed class PlanetSphere_NEW : MonoBehaviour
{
    static Mesh sphere;
    static int users;
    bool registered;
    Mesh previous;

    void OnEnable()
    {
        if (registered) return;
        var filter = GetComponent<MeshFilter>();
        previous = filter.sharedMesh;
        if (sphere == null) sphere = Build();
        filter.sharedMesh = sphere;
        users++;
        registered = true;
    }
    void OnDisable()
    {
        if (!registered) return;
        var filter = GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh == sphere) filter.sharedMesh = previous;
        registered = false;
        if (--users > 0 || sphere == null) return;
        if (Application.isPlaying) Destroy(sphere); else DestroyImmediate(sphere);
        sphere = null;
    }

    static Mesh Build()
    {
        const int longitude = 128, latitude = 64;
        int stride = longitude + 1;
        var vertices = new Vector3[stride * (latitude+1)];
        var normals = new Vector3[vertices.Length];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[longitude * latitude * 6];
        for (int y=0;y<=latitude;y++)
        for (int x=0;x<=longitude;x++)
        {
            float u=x/(float)longitude, v=y/(float)latitude;
            float theta=(1-v)*Mathf.PI, phi=u*Mathf.PI*2;
            // Equirectangular UVs, duplicated seam vertices and smooth radial normals.
            Vector3 n=new Vector3(-Mathf.Sin(phi)*Mathf.Sin(theta),Mathf.Cos(theta),-Mathf.Cos(phi)*Mathf.Sin(theta));
            int i=y*stride+x;
            normals[i]=n;vertices[i]=n*.5f;uv[i]=new Vector2(u,v);
        }
        int t=0;
        for (int y=0;y<latitude;y++)
        for (int x=0;x<longitude;x++)
        {
            int i=y*stride+x;
            triangles[t++]=i;triangles[t++]=i+1;triangles[t++]=i+stride;
            triangles[t++]=i+1;triangles[t++]=i+stride+1;triangles[t++]=i+stride;
        }
        var mesh=new Mesh { name="Planet sphere 128x64 (shared)", hideFlags=HideFlags.HideAndDontSave };
        mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=triangles;
        mesh.bounds=new Bounds(Vector3.zero,Vector3.one);
        return mesh;
    }
}
