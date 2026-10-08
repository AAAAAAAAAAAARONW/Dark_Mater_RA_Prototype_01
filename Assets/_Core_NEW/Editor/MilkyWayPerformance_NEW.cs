using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MilkyWayPerformance_NEW
{
    const string Output = "Logs/MilkyWayPerformance";
    const int Warmup = 20, Samples = 24;
    static Scene preview;
    static Camera camera;
    static GameObject galaxy;
    static MilkyWayMotion_NEW motion;
    static MilkyWayStars_NEW stars;
    static RenderTexture target;
    static Texture2D sync;

    [MenuItem("Tools/Journey NEW/Benchmark Dynamic Milky Way")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            UnityEngine.Debug.LogWarning("Run the isolated galaxy benchmark outside Play Mode.");
            return;
        }
        Directory.CreateDirectory(Output);
        var previousTarget = RenderTexture.active;
        try
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MilkyWayBake_NEW.MaterialPath);
            if (material == null || material.GetTexture("_Volume") == null || !material.IsKeywordEnabled("MILKYWAY_BAKED_VOLUME"))
                throw new InvalidOperationException("Bake Milky Way Density before benchmarking.");
            preview = EditorSceneManager.NewPreviewScene();
            var go = MakeObject("Benchmark camera");
            camera = go.AddComponent<Camera>();
            camera.scene = preview;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.003f,.005f,.01f,1);
            camera.nearClipPlane = .01f; camera.farClipPlane = 500;
            camera.fieldOfView = 48; camera.allowHDR = true; camera.allowMSAA = false;
            galaxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            galaxy.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(galaxy,preview);
            UnityEngine.Object.DestroyImmediate(galaxy.GetComponent<Collider>());
            galaxy.transform.localScale = Vector3.one*80;
            galaxy.transform.localRotation = Quaternion.Euler(-25,0,0);
            galaxy.GetComponent<Renderer>().sharedMaterial = material;
            var starObject = MakeObject("Benchmark stars");
            starObject.transform.SetParent(galaxy.transform,false);
            starObject.AddComponent<MeshFilter>();
            starObject.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Core_NEW/Assets/Celestial/MilkyWay_ResolvedStars.mat");
            stars = starObject.AddComponent<MilkyWayStars_NEW>();
            stars.Rebuild();
            motion = galaxy.AddComponent<MilkyWayMotion_NEW>();
            motion.Initialize();
            sync = new Texture2D(1,1,TextureFormat.RGB24,false);

            ValidateMotionAndQuality();
            // Allow first-use driver optimization and GPU clocks to settle before reporting.
            SetResolution(1920,1080); SetView(0); Configure(1);
            for (int i=0;i<120;i++) MeasureFrame();
            var csv = new StringBuilder("width,height,view,mode,steps,stars,median_ms,p95_ms,min_ms,max_ms\n");
            int[] widths = {1920,2560};
            string[] views = {"face","flight","inside"};
            string[] modes = {"empty","procedural80","low","balanced","high"};
            for (int resolution=0;resolution<widths.Length;resolution++)
            {
                SetResolution(widths[resolution],widths[resolution]*9/16);
                for (int view=0;view<views.Length;view++)
                {
                    SetView(view);
                    for (int mode=0;mode<modes.Length;mode++)
                    {
                        Configure(mode);
                        for (int i=0;i<Warmup;i++) MeasureFrame();
                        var timings = new double[Samples];
                        for (int i=0;i<Samples;i++) timings[i]=MeasureFrame();
                        Array.Sort(timings);
                        int steps = mode==0 ? 0 : mode==1 ? 80 : MilkyWayMotion_NEW.SampleCount((MilkyWayMotion_NEW.Quality)(mode-2));
                        int count = mode==0 ? 0 : mode==1 ? 12000 : MilkyWayMotion_NEW.StarCount((MilkyWayMotion_NEW.Quality)(mode-2));
                        csv.AppendFormat(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6:F3},{7:F3},{8:F3},{9:F3}\n",
                            target.width,target.height,views[view],modes[mode],steps,count,
                            (timings[Samples/2-1]+timings[Samples/2])*0.5,timings[(int)Math.Ceiling(Samples*.95)-1],timings[0],timings[Samples-1]);
                        File.WriteAllText(Output+"/timings.csv",csv.ToString());
                    }
                }
            }
            Configure(3);
            SetResolution(1280,960);
            SetView(0);
            Capture("balanced-face");
            SetView(1); Capture("balanced-flight");
            SetView(2); Capture("balanced-inside");
            SetView(0);
            Configure(1); Capture("procedural-reference");
            Configure(3);
            var baseRotation = galaxy.transform.localRotation;
            for (int i=0;i<72;i++)
            {
                // Six seconds of real-time motion at the scene's normal speed.
                motion.Simulate(1f/12);
                Capture("motion-"+i.ToString("D3"));
            }
            if (Quaternion.Angle(baseRotation,galaxy.transform.localRotation)<2.5f)
                throw new Exception("Rendered motion sequence did not advance.");
            foreach (var renderer in galaxy.GetComponentsInChildren<Renderer>())
            {
                var fade = new MaterialPropertyBlock();
                fade.SetColor("_Color",Color.clear);
                renderer.SetPropertyBlock(fade,0);
            }
            Capture("fade-zero");
            foreach (string guid in AssetDatabase.FindAssets("t:Shader",new[]{"Assets/_Core_NEW/Resources","Assets/_Core_NEW/Editor"}))
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                if (shader.name.Contains("Milky") && ShaderUtil.ShaderHasError(shader)) throw new Exception("Shader error: "+shader.name);
            }
            File.WriteAllText(Output+"/hardware.txt","GPU: "+SystemInfo.graphicsDeviceName+"\nVRAM MiB: "+SystemInfo.graphicsMemorySize+
                "\nAPI: "+SystemInfo.graphicsDeviceVersion+"\nUnity: "+Application.unityVersion+
                "\nCPU: "+SystemInfo.processorType+"\nColor space: "+QualitySettings.activeColorSpace+
                "\nMethod: Camera.Render + synchronous 1x1 ReadPixels, 120 initial warmups, then 20 warmups + 24 samples per case. Includes CPU submission, GPU completion wait, and readback. Not GPU timestamp data or full-game FPS. No post-processing, MSAA, or other scene geometry.\n");
            File.WriteAllText(Output+"/validation.txt","PASS: baked volume, four arms, all quality tiers, shared star mesh during motion, hidden pause, material/rotation restoration, six-second motion sequence, fade render, and shader compilation.\n");
            UnityEngine.Debug.Log("DYNAMIC MILKY WAY BENCHMARK PASSED: "+Path.GetFullPath(Output));
        }
        catch (Exception e)
        {
            File.WriteAllText(Output+"/validation.txt",e.ToString());
            UnityEngine.Debug.LogException(e);
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (camera != null) camera.targetTexture = null;
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
            if (sync != null) UnityEngine.Object.DestroyImmediate(sync);
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    static GameObject MakeObject(string name)
    {
        var go = new GameObject(name) { hideFlags=HideFlags.HideAndDontSave };
        SceneManager.MoveGameObjectToScene(go,preview);
        return go;
    }
    static void ValidateMotionAndQuality()
    {
        var renderer = galaxy.GetComponent<Renderer>();
        var mesh = stars.GetComponent<MeshFilter>().sharedMesh;
        var initial = galaxy.transform.localRotation;
        var local = stars.transform.localPosition;
        if (stars.armCount != 4) throw new Exception("Arm count changed.");
        motion.Simulate(60);
        if (Mathf.Abs(Quaternion.Angle(initial,galaxy.transform.localRotation)-27)>0.02f) throw new Exception("Incorrect angular motion.");
        if (mesh != stars.GetComponent<MeshFilter>().sharedMesh || stars.transform.localPosition != local) throw new Exception("Motion rebuilt or displaced stars.");
        var rotated = galaxy.transform.localRotation;
        renderer.enabled = false; motion.Simulate(10); renderer.enabled = true;
        if (Quaternion.Angle(rotated,galaxy.transform.localRotation)>0.001f) throw new Exception("Hidden galaxy did not pause.");
        for (int q=0;q<3;q++)
        {
            motion.quality = (MilkyWayMotion_NEW.Quality)q; motion.ApplyQuality();
            if (mesh.GetIndexCount(0)!=(uint)(MilkyWayMotion_NEW.StarCount(motion.quality)*6)) throw new Exception("Star budget was not applied.");
            if (renderer.sharedMaterial.GetFloat("_Steps")!=MilkyWayMotion_NEW.SampleCount(motion.quality)) throw new Exception("Volume budget was not applied.");
        }
        UnityEngine.Object.DestroyImmediate(motion);
        if (Quaternion.Angle(initial,galaxy.transform.localRotation)>0.001f || renderer.sharedMaterial != AssetDatabase.LoadAssetAtPath<Material>(MilkyWayBake_NEW.MaterialPath))
            throw new Exception("Motion component did not restore original state. Angle: "+Quaternion.Angle(initial,galaxy.transform.localRotation)+", material: "+renderer.sharedMaterial.name);
        motion = galaxy.AddComponent<MilkyWayMotion_NEW>(); motion.Initialize();
    }
    static void SetResolution(int width,int height)
    {
        camera.targetTexture = null;
        if (target != null) UnityEngine.Object.DestroyImmediate(target);
        target = new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
        target.Create(); camera.targetTexture = target;
    }
    static void SetView(int view)
    {
        camera.transform.position = view==0 ? new Vector3(0,90,-55) : view==1 ? new Vector3(-9.893f,4.5f,-85) : new Vector3(-12,3,-18);
        camera.transform.LookAt(Vector3.zero);
    }
    static void Configure(int mode)
    {
        motion.RestartMotion();
        var renderer = galaxy.GetComponent<Renderer>();
        var material = renderer.sharedMaterial;
        renderer.enabled = mode!=0;
        stars.GetComponent<Renderer>().enabled = mode!=0;
        material.EnableKeyword("MILKYWAY_BAKED_VOLUME");
        if (mode==1)
        {
            material.DisableKeyword("MILKYWAY_BAKED_VOLUME"); material.SetFloat("_Steps",80);
            stars.SetVisibleStarCount(12000);
        }
        else if (mode>=2)
        {
            motion.quality=(MilkyWayMotion_NEW.Quality)(mode-2); motion.ApplyQuality();
        }
    }
    static double MeasureFrame()
    {
        // Readback makes the queued render complete before stopping the wall clock.
        var timer = Stopwatch.StartNew();
        motion.Simulate(1f/60);
        camera.Render(); RenderTexture.active=target;
        sync.ReadPixels(new Rect(target.width/2,target.height/2,1,1),0,0,false);
        timer.Stop();
        return timer.Elapsed.TotalMilliseconds;
    }
    static void Capture(string name)
    {
        camera.Render(); RenderTexture.active=target;
        var image = new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        try
        {
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0,false); image.Apply(false,false);
            File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(image); }
    }
}
