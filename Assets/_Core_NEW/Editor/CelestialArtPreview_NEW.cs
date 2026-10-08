using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class CelestialArtPreview_NEW
{
    // Isolated GPU previews: never opens, saves or replaces the user's scene.
    const string Art = "Assets/_Core_NEW/Assets/Celestial/";
    static Camera camera;
    static string output;
    static Scene previewScene;
    [MenuItem("Tools/Journey NEW/Celestial Art Preview")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Celestial Art Preview is available outside Play Mode.");
            return;
        }
        try
        {
            output = Path.GetFullPath("Logs/CelestialPreview");
            Directory.CreateDirectory(output);
            previewScene = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("Review camera") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(cameraObject,previewScene);
            camera = cameraObject.AddComponent<Camera>();
            camera.scene = previewScene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.003f,.005f,.01f,1);
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 500;
            camera.allowHDR = true;

            var earth = Sphere("Earth", "Earth_Polished", Vector3.zero, 2);
            earth.transform.rotation = Quaternion.Euler(0,32.965f,0)*Quaternion.Euler(0,90,23.44f);
            ChildSphere(earth.transform,"Atmosphere","Earth_Atmosphere",1.018f);
            ChildSphere(earth.transform,"Clouds","Earth_Clouds",1.004f);
            Capture("earth-sunlit",new Vector3(-2.8f,1,-3.0f),Vector3.zero,40);
            Capture("earth-approach",new Vector3(0,.2f,-4),Vector3.zero,36);
            UnityEngine.Object.DestroyImmediate(earth);

            string[] planets={"Mercury","Venus","Earth","Mars","Jupiter","Saturn","Uranus","Neptune","Moon"};
            float[] tilt={.034f,177.4f,23.44f,25.19f,3.13f,26.73f,97.77f,28.32f,23.44f};
            var bodies = new GameObject[planets.Length];
            for(int i=0;i<planets.Length;i++)
            {
                var b=Sphere(planets[i],planets[i]+"_Polished",new Vector3((i%3-1)*2.7f, (1-i/3)*2.65f,0),1.9f);
                b.transform.rotation=Quaternion.Euler(0,32.965f,0)*Quaternion.Euler(0,90,tilt[i]);
                // A consistent three-quarter light makes the material differences easy to compare.
                Vector3 direction=b.transform.InverseTransformDirection(new Vector3(-1,.5f,-1).normalized);
                var lighting=new MaterialPropertyBlock();
                lighting.SetVector("_SunDirection",new Vector4(direction.x,direction.y,direction.z,0));
                b.GetComponent<Renderer>().SetPropertyBlock(lighting);
                bodies[i]=b;
                if(planets[i]=="Earth")
                {
                    ChildSphere(b.transform,"Atmosphere","Earth_Atmosphere",1.018f);
                    ChildSphere(b.transform,"Clouds","Earth_Clouds",1.004f);
                }
            }
            Capture("planet-materials",new Vector3(0,0,-15),Vector3.zero,36);
            foreach(var b in bodies) UnityEngine.Object.DestroyImmediate(b);

            var galaxy=GameObject.CreatePrimitive(PrimitiveType.Cube);
            galaxy.hideFlags=HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(galaxy,previewScene);
            UnityEngine.Object.DestroyImmediate(galaxy.GetComponent<Collider>());
            galaxy.name="Milky Way volume";
            galaxy.transform.localScale=Vector3.one*80;
            galaxy.transform.rotation=Quaternion.Euler(-25,0,0);
            galaxy.GetComponent<Renderer>().sharedMaterial=Material("MilkyWay_SpiralVolume");
            var stars=new GameObject("Resolved Arm Stars") { hideFlags=HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(stars,previewScene);
            stars.transform.SetParent(galaxy.transform,false);
            stars.AddComponent<MeshFilter>();
            stars.AddComponent<MeshRenderer>().sharedMaterial=Material("MilkyWay_ResolvedStars");
            var generator=stars.AddComponent<MilkyWayStars_NEW>();
            generator.phase=.55f;
            generator.Rebuild();
            var mesh=stars.GetComponent<MeshFilter>().sharedMesh;
            if(mesh==null || mesh.vertexCount!=48000 || generator.armCount!=4) throw new Exception("Four-arm star mesh generation failed");
            Capture("milkyway-face",new Vector3(0,90,-55),Vector3.zero,48);
            Capture("milkyway-flight",new Vector3(-9.893f,4.5f,-85),Vector3.zero,48);
            Capture("milkyway-close",new Vector3(-15,20,-45),new Vector3(-3,0,0),55);
            // Same serialized material parameter used by WorldSwitcher_NEW.
            foreach(var renderer in galaxy.GetComponentsInChildren<Renderer>())
            {
                var block=new MaterialPropertyBlock(); block.SetColor("_Color",Color.clear); renderer.SetPropertyBlock(block,0);
            }
            Capture("milkyway-fade-zero",new Vector3(0,90,-55),Vector3.zero,48);
            foreach(string asset in AssetDatabase.FindAssets("t:Shader",new[]{"Assets/_Core_NEW/Resources"}))
            {
                var shader=AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(asset));
                if(ShaderUtil.ShaderHasError(shader)) throw new Exception("Shader failed: "+shader.name);
            }
            File.WriteAllText(Path.Combine(output,"validation.txt"),"PASS: shaders compiled; deterministic 12,000-star / four-arm mesh built; material, atmosphere, galaxy and zero-fade captures rendered.\n");
            Debug.Log("CELESTIAL PREVIEW PASSED: "+output);

        }
        catch(Exception e)
        {
            Debug.LogException(e);
            if(output!=null) File.WriteAllText(Path.Combine(output,"validation.txt"),e.ToString());

        }
        finally
        {
            if(previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }
    static Material Material(string name)
    {
        var m=AssetDatabase.LoadAssetAtPath<Material>(Art+name+".mat");
        if(m==null) throw new Exception("Missing material "+name);
        return m;
    }
    static GameObject Sphere(string name,string material,Vector3 at,float diameter)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.hideFlags=HideFlags.HideAndDontSave;
        SceneManager.MoveGameObjectToScene(go,previewScene);
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name=name; go.transform.position=at; go.transform.localScale=Vector3.one*diameter;
        go.GetComponent<Renderer>().sharedMaterial=Material(material);
        go.AddComponent<PlanetSphere_NEW>();
        return go;
    }
    static void ChildSphere(Transform parent,string name,string material,float scale)
    {
        var go=Sphere(name,material,Vector3.zero,1);
        go.transform.SetParent(parent,false);go.transform.localPosition=Vector3.zero;go.transform.localScale=Vector3.one*scale;
    }
    static void Capture(string name,Vector3 eye,Vector3 target,float fov)
    {
        camera.transform.position=eye;camera.transform.LookAt(target);camera.fieldOfView=fov;
        var rt=new RenderTexture(1600,1200,24,RenderTextureFormat.ARGB32);
        var previous=RenderTexture.active;
        Texture2D image=null;
        try
        {
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=previous;camera.targetTexture=null;
            if(image!=null) UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
