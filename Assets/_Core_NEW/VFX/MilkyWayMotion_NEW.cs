using UnityEngine;

/// <summary>Rotate the visual galaxy, leaving the sibling dive target on its flight path.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
[DefaultExecutionOrder(-250)]
public sealed class MilkyWayMotion_NEW : MonoBehaviour
{
    public enum Quality { Low, Balanced, High }
    public Quality quality = Quality.Balanced;
    [Tooltip("Art-directed time compression, in degrees per second. Zero freezes the galaxy.")]
    [Range(-3,3)] public float degreesPerSecond = 0.45f;
    [Tooltip("Stop the rotation when WorldSwitcher hides this galaxy.")]
    public bool pauseWhenHidden = true;

    MeshRenderer volume;
    MilkyWayStars_NEW stars;
    Material original, runtime;
    Quaternion initialRotation;
    Quality appliedQuality;
    float angle;
    bool ready;

    public static int SampleCount(Quality value)
    {
        return value == Quality.Low ? 16 : value == Quality.High ? 48 : 32;
    }
    public static int StarCount(Quality value)
    {
        return value == Quality.Low ? 4000 : value == Quality.High ? 12000 : 8000;
    }

    void OnEnable() { if (Application.isPlaying) Initialize(); }
    void Update() { if (Application.isPlaying) Simulate(Time.deltaTime); }
    void OnDisable() { Release(); }
    void OnDestroy() { Release(); }

    // Also used by the isolated editor benchmark without entering Play Mode.
    public void Initialize()
    {
        if (ready) return;
        volume = GetComponent<MeshRenderer>();
        original = volume.sharedMaterial;
        if (original == null) return;
        stars = GetComponentInChildren<MilkyWayStars_NEW>(true);
        initialRotation = transform.localRotation;
        angle = 0;
        runtime = new Material(original) { name = original.name + " (runtime)", hideFlags = HideFlags.HideAndDontSave };
        if (!SystemInfo.supports3DTextures || runtime.GetTexture("_Volume") == null)
        {
            runtime.DisableKeyword("MILKYWAY_BAKED_VOLUME");
            Debug.LogWarning("Milky Way density cache is unavailable. Using the more expensive procedural fallback.",this);
        }
        volume.sharedMaterial = runtime;
        ready = true;
        ApplyQuality();
    }

    public void Simulate(float deltaTime)
    {
        if (!ready) Initialize();
        if (!ready) return;
        if (quality != appliedQuality) ApplyQuality();
        if (pauseWhenHidden && !volume.enabled) return;
        // One transform update; the volume and stars rotate together. No mesh rebuild.
        angle = Mathf.Repeat(angle + degreesPerSecond * Mathf.Max(0,deltaTime),360);
        transform.localRotation = initialRotation * Quaternion.AngleAxis(angle,Vector3.up);
    }

    public void ApplyQuality()
    {
        if (!ready) return;
        appliedQuality = quality;
        int steps = SampleCount(quality);
        if (!runtime.IsKeywordEnabled("MILKYWAY_BAKED_VOLUME")) steps = Mathf.Min(steps,24);
        runtime.SetFloat("_Steps",steps);
        if (stars != null) stars.SetVisibleStarCount(StarCount(quality));
    }

    public void RestartMotion()
    {
        angle = 0;
        if (ready) transform.localRotation = initialRotation;
    }

    void Release()
    {
        if (!ready) return;
        if (volume != null && volume.sharedMaterial == runtime) volume.sharedMaterial = original;
        transform.localRotation = initialRotation;
        if (stars != null) stars.SetVisibleStarCount(stars.count);
        if (runtime != null)
        {
            if (Application.isPlaying) Destroy(runtime); else DestroyImmediate(runtime);
        }
        runtime = null;
        ready = false;
    }
}
