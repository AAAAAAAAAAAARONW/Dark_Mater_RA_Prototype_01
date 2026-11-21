using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Tools > Journey NEW > Magellan > Build Telescope from STL: reads LCO's model of the Magellan
/// 6.5 m telescope (Models/Magellan_LCO/STL), puts its parts together, and saves the meshes, the
/// materials and Magellan_NEW.prefab beside it, with MagellanLightPath_NEW set up on it.
///
/// THE STLs are laid out for 3D printing, each part in its own place and turn, in millimetres at
/// full size, right-handed. Each part is moved into the telescope here by what was measured from
/// it (Models/Magellan_LCO/README.md says how). The struts are drawn here, not taken from the STL.
///
/// THE MESHES. Corners shared by faces within smoothAngle of each other are smoothed together, so
/// the curved parts are round and the edges stay sharp; the mirrors' faces are a second submesh,
/// for the mirror material. Saved over the old ones, so what refers to them keeps working.
///
/// THE PREFAB. Floor at its root's origin, the elevation axis along Z. "Tube" holds what tilts
/// (mirrors, cell, struts, discs), pivoted on the elevation axis; the base and supports stay put.
/// The instrument is a stand-in box at the focus. Materials already there are kept as tuned.
///
/// The model is LCO's: the credits name them (CreditsRoll_NEW, Acknowledgements).
/// </summary>
public static class MagellanBuilder_NEW
{
    const string MenuPath = "Tools/Journey NEW/Magellan/Build Telescope from STL";
    const string Root = "Assets/_Core_NEW/Models/Magellan_LCO";
    const string StlFolder = Root + "/STL";
    const string MeshFolder = Root + "/Meshes";
    const string MaterialFolder = Root + "/Materials";
    const string PrefabPath = Root + "/Magellan_NEW.prefab";

    const float SmoothAngle = 35f;

    // Measured from the STLs, metres, in the primary's own frame (Primario.stl is upright).
    const float AxisY = 4.48f;              // elevation axis, through the centre section
    const float PrimaryVertexY = 3.705f;    // the mirror's vertex: its sag fits f/1.25
    const float FocalLength = 8.125f;
    const float PrimaryRadius = 3.251f;
    const float FinalRatio = 11f / 1.25f;   // f/11 over f/1.25: the secondary's magnification
    const float Spacing = 6.875f;           // primary vertex to secondary vertex (f/11 Cassegrain)
    const float TrunnionZ = 4.341f;         // the centre section's ends, where the discs go
    const float DiscRadius = 3.048f;
    const float FloorLift = 1.416f;         // from the primary's frame up to the base's underside

    struct Part
    {
        public string name, file;
        public Func<Vector3, Vector3> place;        // STL millimetres -> metres, primary frame
        public Func<Vector3, Vector3, bool> mirror; // (centroid mm, normal) in the STL's own frame
        public Func<Vector3, Vector3, bool> keep;   // the same; null keeps every face
        public bool tilts;
        public bool dark;
    }

    [MenuItem(MenuPath)]
    internal static void Build()
    {
        if (!AssetDatabase.IsValidFolder(StlFolder))
        {
            Debug.LogWarning("Journey NEW: no " + StlFolder + "; put LCO's STL files there first.");
            return;
        }
        Folder(MeshFolder);
        Folder(MaterialFolder);

        Material structure = MaterialAt("Magellan_Structure", new Color(0.8f, 0.81f, 0.82f), 0.25f, 0.35f, false);
        Material baseDark = MaterialAt("Magellan_Base", new Color(0.17f, 0.18f, 0.19f), 0.1f, 0.25f, false);
        Material mirrorMat = MaterialAt("Magellan_Mirror", new Color(0.86f, 0.88f, 0.9f), 1f, 0.96f, true);
        Material instrumentMat = MaterialAt("Magellan_Instrument", new Color(0.12f, 0.13f, 0.15f), 0.4f, 0.4f, true);

        float secondaryVertexY = PrimaryVertexY + Spacing;
        float secondaryToFocus = FinalRatio * (PrimaryVertexY + FocalLength - secondaryVertexY);
        float focusZ = secondaryToFocus - (secondaryVertexY - AxisY);

        // The tertiary: a flat plate 87 mm thick on the end of a support tube 2.85 m long, square
        // to it. Turned to 45 degrees at the elevation axis the tube would run down through the
        // primary, so only the plate is used: its face turned to face up and out along +Z, its
        // middle on the axis.
        Vector3 tertiaryNormal = new Vector3(0.73f, 0.69f, 0f).normalized;
        Vector3 tertiaryFace = new Vector3(-644f, -1478f, 6683f) + tertiaryNormal * 43f;
        Quaternion tertiaryTurn = Quaternion.FromToRotation(tertiaryNormal, new Vector3(0f, 1f, 1f).normalized);
        float tertiaryFacePlane = Vector3.Dot(tertiaryFace, tertiaryNormal);

        var parts = new List<Part>
        {
            new Part
            {
                name = "Primary", file = "Primario.stl", tilts = true,
                place = p => p * 0.001f,
                mirror = (c, n) => n.y > 0.5f && c.y > 3600f && c.y < 4045f && new Vector2(c.x, c.z).magnitude < 3260f,
            },
            new Part
            {
                // On its side, the secondary's face at +X: stood up, face down, vertex at Spacing.
                name = "Secondary", file = "Secundario.stl", tilts = true,
                place = p => new Vector3(p.y * 0.001f, secondaryVertexY - (p.x * 0.001f - 3.104f), p.z * 0.001f - 3.858f),
                mirror = (c, n) => c.x > 3035f && n.x > 0.3f,
            },
            new Part
            {
                name = "Tertiary", file = "Espejo terciario - 01.stl", tilts = true,
                place = p => tertiaryTurn * ((p - tertiaryFace) * 0.001f) + new Vector3(0f, AxisY, 0f),
                mirror = (c, n) => Vector3.Dot(n, tertiaryNormal) > 0.95f && Vector3.Dot(c, tertiaryNormal) > tertiaryFacePlane - 20f,
                keep = (c, n) => Vector3.Dot(c, tertiaryNormal) > tertiaryFacePlane - 130f,
            },
            new Part
            {
                name = "Elevation Disc +Z", file = "Discos.stl", tilts = true,
                place = p => new Vector3(p.x * 0.001f, p.y * 0.001f + AxisY, p.z * 0.001f + 0.1f + TrunnionZ),
            },
            new Part
            {
                name = "Elevation Disc -Z", file = "Discos.stl", tilts = true,
                place = p => new Vector3(-p.x * 0.001f, p.y * 0.001f + AxisY, -(p.z * 0.001f + 0.1f + TrunnionZ)),
            },
            new Part
            {
                // Its arc under the disc: the arc's lowest point (2.222 m up it) at the disc's bottom.
                name = "Disc Support +Z", file = "Soporte discos EL.stl", tilts = false,
                place = p => new Vector3(p.x * 0.001f - 2.753f, p.y * 0.001f + (AxisY - DiscRadius - 2.222f), p.z * 0.001f + 2.798f + 4.711f),
            },
            new Part
            {
                name = "Disc Support -Z", file = "Soporte discos EL.stl", tilts = false,
                place = p => new Vector3(-(p.x * 0.001f - 2.753f), p.y * 0.001f + (AxisY - DiscRadius - 2.222f), -(p.z * 0.001f + 2.798f + 4.711f)),
            },
            new Part
            {
                // Flat, Z up, its long side along the elevation axis, under the supports.
                name = "Base", file = "Base magallanes.stl", tilts = false, dark = true,
                place = p => new Vector3(p.x * 0.001f, p.z * 0.001f - FloorLift, -p.y * 0.001f),
            },
        };

        var root = new GameObject("Magellan 6.5 m (LCO model)");
        try
        {
            var tube = new GameObject("Tube").transform;
            tube.SetParent(root.transform, false);
            tube.localPosition = new Vector3(0f, AxisY + FloorLift, 0f);

            var renderers = new Dictionary<string, MeshRenderer>();
            int done = 0;
            foreach (Part part in parts)
            {
                EditorUtility.DisplayProgressBar("Magellan", "Building " + part.name, done++ / (float)(parts.Count + 1));
                string stl = StlFolder + "/" + part.file;
                if (!File.Exists(stl))
                {
                    Debug.LogWarning("Journey NEW: no " + stl + "; " + part.name + " left out.");
                    continue;
                }
                List<Vector3> raw = ReadStl(stl);
                // Into the prefab's spaces: what tilts about the axis, the rest about the floor.
                Vector3 shift = part.tilts ? new Vector3(0f, -AxisY, 0f) : new Vector3(0f, FloorLift, 0f);
                Mesh mesh = MakeMesh(part.name, raw, p => part.place(p) + shift, part.mirror, part.keep);
                mesh = SaveMesh(mesh, MeshFolder + "/Magellan_" + part.name.Replace(' ', '_').Replace("+", "P").Replace("-", "M") + ".asset");

                var go = new GameObject(part.name);
                go.transform.SetParent(part.tilts ? tube : root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                Material body = part.dark ? baseDark : structure;
                mr.sharedMaterials = mesh.subMeshCount > 1 ? new[] { body, mirrorMat } : new[] { body };
                renderers[part.name] = mr;
            }

            EditorUtility.DisplayProgressBar("Magellan", "Struts", parts.Count / (float)(parts.Count + 1));
            Mesh struts = SaveMesh(MakeStruts(secondaryVertexY), MeshFolder + "/Magellan_Struts.asset");
            var strutGo = new GameObject("Struts");
            strutGo.transform.SetParent(tube, false);
            strutGo.AddComponent<MeshFilter>().sharedMesh = struts;
            strutGo.AddComponent<MeshRenderer>().sharedMaterial = structure;

            // The instrument at the focus, on the +Z Nasmyth side: a stand-in until there is one.
            var instrument = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instrument.name = "Instrument (stand-in)";
            UnityEngine.Object.DestroyImmediate(instrument.GetComponent<Collider>());
            instrument.transform.SetParent(root.transform, false);
            instrument.transform.localPosition = new Vector3(0f, AxisY + FloorLift, focusZ + 0.7f);
            instrument.transform.localScale = new Vector3(1.4f, 1.4f, 1.4f);
            var instrumentRenderer = instrument.GetComponent<MeshRenderer>();
            instrumentRenderer.sharedMaterial = instrumentMat;

            var path = root.AddComponent<MagellanLightPath_NEW>();
            var so = new SerializedObject(path);
            so.FindProperty("tube").objectReferenceValue = tube;
            so.FindProperty("primaryMirror").objectReferenceValue = Get(renderers, "Primary");
            so.FindProperty("secondaryMirror").objectReferenceValue = Get(renderers, "Secondary");
            so.FindProperty("tertiaryMirror").objectReferenceValue = Get(renderers, "Tertiary");
            so.FindProperty("mirrorSlot").intValue = 1;
            so.FindProperty("instrument").objectReferenceValue = instrumentRenderer;
            so.FindProperty("primaryVertexY").floatValue = PrimaryVertexY - AxisY;
            so.FindProperty("focalLength").floatValue = FocalLength;
            so.FindProperty("primaryRadius").floatValue = PrimaryRadius;
            so.FindProperty("secondaryVertexY").floatValue = secondaryVertexY - AxisY;
            so.FindProperty("secondaryToFocus").floatValue = secondaryToFocus;
            so.FindProperty("tertiaryCentre").vector3Value = Vector3.zero;
            so.FindProperty("focus").vector3Value = new Vector3(0f, 0f, focusZ);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("Journey NEW: built " + PrefabPath + " from LCO's Magellan model (focus " + focusZ.ToString("0.00") +
                      " m out along the elevation axis).");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            EditorUtility.ClearProgressBar();
        }
    }

    static Renderer Get(Dictionary<string, MeshRenderer> all, string name) =>
        all.TryGetValue(name, out MeshRenderer r) ? r : null;

    /// <summary>Every triangle's three corners, in the file's order and units. Binary or ASCII.</summary>
    static List<Vector3> ReadStl(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        var corners = new List<Vector3>();
        if (bytes.Length >= 84)
        {
            uint count = BitConverter.ToUInt32(bytes, 80);
            if (84 + (long)count * 50 == bytes.Length)
            {
                for (int i = 0; i < count; i++)
                {
                    int o = 84 + i * 50 + 12;
                    for (int k = 0; k < 3; k++, o += 12)
                        corners.Add(new Vector3(BitConverter.ToSingle(bytes, o), BitConverter.ToSingle(bytes, o + 4),
                                                BitConverter.ToSingle(bytes, o + 8)));
                }
                return corners;
            }
        }
        foreach (string line in File.ReadAllLines(path))
        {
            string t = line.Trim();
            if (!t.StartsWith("vertex ")) continue;
            string[] w = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            corners.Add(new Vector3(float.Parse(w[1], ci), float.Parse(w[2], ci), float.Parse(w[3], ci)));
        }
        return corners;
    }

    /// <summary>
    /// A mesh from STL corners: placed, mirrored into Unity's left-handed space (X flipped, the
    /// winding reversed), smoothed within SmoothAngle, welded, the mirror faces a second submesh.
    /// </summary>
    static Mesh MakeMesh(string name, List<Vector3> raw, Func<Vector3, Vector3> place, Func<Vector3, Vector3, bool> mirror,
                         Func<Vector3, Vector3, bool> keep)
    {
        if (keep != null)
        {
            var kept = new List<Vector3>(raw.Count);
            for (int f = 0; f + 2 < raw.Count; f += 3)
            {
                Vector3 a = raw[f], b = raw[f + 1], c = raw[f + 2];
                if (keep((a + b + c) / 3f, Vector3.Cross(b - a, c - a).normalized)) { kept.Add(a); kept.Add(b); kept.Add(c); }
            }
            raw = kept;
        }
        int faces = raw.Count / 3;
        var pos = new Vector3[faces * 3];
        var faceNormal = new Vector3[faces];
        var isMirror = new bool[faces];
        for (int f = 0; f < faces; f++)
        {
            Vector3 a = raw[f * 3], b = raw[f * 3 + 1], c = raw[f * 3 + 2];
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            isMirror[f] = mirror != null && mirror((a + b + c) / 3f, n);

            // Flip X, and swap two corners so the face still faces out.
            Vector3 pa = Flip(place(a)), pb = Flip(place(c)), pc = Flip(place(b));
            pos[f * 3] = pa; pos[f * 3 + 1] = pb; pos[f * 3 + 2] = pc;
            faceNormal[f] = Vector3.Cross(pb - pa, pc - pa);
        }

        // Faces round each corner position.
        var around = new Dictionary<Vector3Int, List<int>>();
        var keys = new Vector3Int[pos.Length];
        for (int i = 0; i < pos.Length; i++)
        {
            keys[i] = Key(pos[i]);
            if (!around.TryGetValue(keys[i], out List<int> list)) around[keys[i]] = list = new List<int>(6);
            list.Add(i / 3);
        }

        float cosLimit = Mathf.Cos(SmoothAngle * Mathf.Deg2Rad);
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var weld = new Dictionary<(Vector3Int, Vector3Int), int>();
        var body = new List<int>();
        var glass = new List<int>();
        for (int i = 0; i < pos.Length; i++)
        {
            int f = i / 3;
            Vector3 own = faceNormal[f].normalized;
            Vector3 sum = Vector3.zero;
            foreach (int g in around[keys[i]])
                if (isMirror[g] == isMirror[f] && Vector3.Dot(faceNormal[g].normalized, own) >= cosLimit) sum += faceNormal[g];
            Vector3 n = sum.sqrMagnitude > 0f ? sum.normalized : own;

            var wk = (keys[i], new Vector3Int(Mathf.RoundToInt(n.x * 1000f), Mathf.RoundToInt(n.y * 1000f), Mathf.RoundToInt(n.z * 1000f)));
            if (!weld.TryGetValue(wk, out int index))
            {
                index = vertices.Count;
                weld[wk] = index;
                vertices.Add(pos[i]);
                normals.Add(n);
            }
            (isMirror[f] ? glass : body).Add(index);
        }

        var mesh = new Mesh { name = "Magellan " + name };
        mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.subMeshCount = glass.Count > 0 ? 2 : 1;
        mesh.SetTriangles(body, 0);
        if (glass.Count > 0) mesh.SetTriangles(glass, 1);
        mesh.RecalculateBounds();
        return mesh;
    }

    static Vector3 Flip(Vector3 v) => new Vector3(-v.x, v.y, v.z);

    static Vector3Int Key(Vector3 v) =>
        new Vector3Int(Mathf.RoundToInt(v.x * 10000f), Mathf.RoundToInt(v.y * 10000f), Mathf.RoundToInt(v.z * 10000f));

    /// <summary>
    /// Eight struts, 0.22 m thick, in Serrurier pairs: from the top corners of the centre section
    /// up to the top ring's four lugs, each lug taking the two corners either side of it.
    /// In the tube's space.
    /// </summary>
    static Mesh MakeStruts(float secondaryVertexY)
    {
        const float corner = 3.2f, lug = 3.8f, radius = 0.11f;
        const int sides = 12;
        float bottom = 4.939f - AxisY;
        float top = secondaryVertexY + (3.104f - 1.342f) - AxisY;   // the top ring's underside

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();
        Vector3[] lugs = { new Vector3(lug, top, 0f), new Vector3(0f, top, lug), new Vector3(-lug, top, 0f), new Vector3(0f, top, -lug) };
        foreach (Vector3 l in lugs)
        {
            Vector3 side = Vector3.Cross(Vector3.up, new Vector3(l.x, 0f, l.z).normalized);
            Vector3 outward = new Vector3(l.x, 0f, l.z).normalized;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 c = outward * corner + side * corner * s;
                Tube(vertices, normals, triangles, new Vector3(c.x, bottom, c.z), l, radius, sides);
            }
        }

        var mesh = new Mesh { name = "Magellan Struts" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    static void Tube(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 from, Vector3 to, float radius, int sides)
    {
        Vector3 axis = (to - from).normalized;
        Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 w = Vector3.Cross(axis, u);
        int start = v.Count;
        for (int i = 0; i <= sides; i++)
        {
            float a = i / (float)sides * Mathf.PI * 2f;
            Vector3 d = u * Mathf.Cos(a) + w * Mathf.Sin(a);
            v.Add(from + d * radius); n.Add(d);
            v.Add(to + d * radius); n.Add(d);
        }
        for (int i = 0; i < sides; i++)
        {
            int a = start + i * 2, b = a + 1, c = a + 2, d = a + 3;
            t.Add(a); t.Add(c); t.Add(b);
            t.Add(b); t.Add(c); t.Add(d);
        }
    }

    /// <summary>Saves the mesh at path, over the one there if any (so references to it hold).</summary>
    static Mesh SaveMesh(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        existing.Clear();
        EditorUtility.CopySerialized(mesh, existing);
        existing.name = Path.GetFileNameWithoutExtension(path);
        UnityEngine.Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    /// <summary>The material of that name, made (Standard) if it is not there; one that is, is left as tuned.</summary>
    static Material MaterialAt(string name, Color colour, float metallic, float smoothness, bool emissive)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Standard")) { name = name };
        mat.color = colour;
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Glossiness", smoothness);
        if (emissive)
        {
            // Lit up by MagellanLightPath_NEW through a property block: the keyword has to be on.
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
