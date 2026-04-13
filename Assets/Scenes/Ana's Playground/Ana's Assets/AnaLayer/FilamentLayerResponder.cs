using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Listens to LayerStateManagerTest.OnLayerChanged and tweens the yellow
/// filament transparency to match the new layer's profile.
///
/// Renderer references live here (scene object) not in LayerDefinitionTest
/// (ScriptableObjects cannot hold scene object references).
///
/// Enter a non-default layer → tween to filamentHiddenValue.
/// Enter the default layer   → tween to filamentOpaqueValue.
///
/// Attach to any persistent GameObject (e.g. GameManager).
/// </summary>
public class FilamentLayerResponder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] LayerStateManagerTest stateManager;

    [Header("Filament Renderers")]
    [Tooltip("Assign yellow filament renderers manually, or enable auto-find.")]
    [SerializeField] bool autoFindRenderers = true;
    [SerializeField] Renderer[] manualRenderers;

    [Header("Debug")]
    [SerializeField] bool debugLog = true;

    readonly List<Material> _filamentMaterials = new List<Material>();
    float _currentTransparency = 1f;
    Coroutine _tweenRoutine;
    bool _cacheBuilt = false;

    void Awake()
    {
        if (stateManager == null)
            stateManager = FindObjectOfType<LayerStateManagerTest>();
    }

    void OnEnable()
    {
        if (stateManager != null)
            stateManager.OnLayerChanged += HandleLayerChanged;
    }

    void OnDisable()
    {
        if (stateManager != null)
            stateManager.OnLayerChanged -= HandleLayerChanged;
    }

    void HandleLayerChanged(LayerDefinitionTest previous, LayerDefinitionTest current)
    {
        if (current == null) return;
        if (!current.driveYellowFilamentTransparency) return;

        // Build cache on first use, using the shader name from the incoming profile
        if (!_cacheBuilt)
            BuildMaterialCache(current.yellowFilamentShaderName);

        if (_filamentMaterials.Count == 0)
        {
            Debug.LogWarning("[FilamentLayerResponder] No filament materials found.");
            return;
        }

        // Non-default layers hide the filament; default layer restores it
        float target = current.isDefaultLayer
            ? current.filamentOpaqueValue
            : current.filamentHiddenValue;

        if (_tweenRoutine != null)
            StopCoroutine(_tweenRoutine);

        _tweenRoutine = StartCoroutine(TweenTransparency(target, current.filamentBlendDuration));

        if (debugLog)
            Debug.Log($"[FilamentLayerResponder] Layer '{current.layerId}' — " +
                      $"tweening filament to {target} over {current.filamentBlendDuration}s.");
    }

    void BuildMaterialCache(string shaderName)
    {
        _filamentMaterials.Clear();

        Renderer[] renderers = (autoFindRenderers || manualRenderers == null || manualRenderers.Length == 0)
            ? FindObjectsOfType<Renderer>()
            : manualRenderers;

        var seen = new HashSet<Material>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            foreach (var mat in r.materials)
            {
                if (mat == null) continue;
                if (!mat.HasProperty("_Transparency")) continue;
                if (!string.IsNullOrEmpty(shaderName) &&
                    mat.shader != null &&
                    mat.shader.name != shaderName) continue;
                if (seen.Add(mat))
                    _filamentMaterials.Add(mat);
            }
        }

        _cacheBuilt = true;

        if (_filamentMaterials.Count > 0)
            _currentTransparency = _filamentMaterials[0].GetFloat("_Transparency");
        else if (debugLog)
            Debug.LogWarning("[FilamentLayerResponder] No materials matched shader name " +
                             $"'{shaderName}' with _Transparency property.");
    }

    IEnumerator TweenTransparency(float target, float duration)
    {
        float from = _currentTransparency;
        float to = Mathf.Clamp01(target);
        float elapsed = 0f;
        float d = Mathf.Max(0.01f, duration);

        while (elapsed < d)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / d);
            float eased = t * t * (3f - 2f * t); // smoothstep
            float value = Mathf.Lerp(from, to, eased);
            ApplyTransparency(value);
            yield return null;
        }

        ApplyTransparency(to);
        _tweenRoutine = null;
    }

    void ApplyTransparency(float value)
    {
        _currentTransparency = value;
        for (int i = 0; i < _filamentMaterials.Count; i++)
            _filamentMaterials[i].SetFloat("_Transparency", value);
    }
}
