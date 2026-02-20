using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 进入/离开 TriggerBox 的单相机切换控制：
/// - 进入：切到 Micro
/// - 离开：切回 Macro
/// - 两种情况下都执行一次镜头动作：低头 -> 保持 3 秒 -> 回正
/// </summary>
[RequireComponent(typeof(Collider))]
public class MicroToMacroTriggerSequence : MonoBehaviour
{
    [Header("References")]
    [SerializeField] MacroMicroTransitionController transitionController;
    [SerializeField] DarkMatterPlayerController playerController;
    [SerializeField] CameraFlareFlash flareFlash;
    Collider _triggerCollider;

    [Header("Filter")]
    [SerializeField] bool requireTag = false;
    [SerializeField] string targetTag = "Player";
    [SerializeField] bool usePollingFallback = true;
    [SerializeField] float pollingInterval = 0.1f;
    [SerializeField] bool autoAddKinematicRigidbody = true;

    [Header("Blend Timing")]
    [Tooltip("进入/离开时 Macro<->Micro 的渐变时长")]
    [SerializeField] float transitionDuration = 3f;

    [Header("Zoom & Speed (Enter/Exit)")]
    [Tooltip("进入触发区后的镜头距离倍率（<1 更近，建议 0.6~0.9）")]
    [SerializeField] float enterZoomMultiplier = 0.75f;
    [Tooltip("离开触发区后的镜头距离倍率（通常 1 恢复默认）")]
    [SerializeField] float exitZoomMultiplier = 1f;
    [SerializeField] float zoomBlendDuration = 0.45f;
    [Tooltip("进入触发区后的速度倍率（<1 减速）")]
    [SerializeField] float enterSpeedMultiplier = 0.5f;
    [Tooltip("离开触发区后的速度倍率（通常 1 恢复默认）")]
    [SerializeField] float exitSpeedMultiplier = 1f;
    [SerializeField] float speedBlendDuration = 0.6f;

    [Header("Flare Before Switch")]
    [SerializeField] bool useFlareBeforeSwitch = true;
    [SerializeField] Color flareColor = Color.white;
    [Range(0f, 1f)]
    [SerializeField] float flarePeakAlpha = 0.75f;
    [SerializeField] float flareDuration = 0.22f;
    [Tooltip("闪光开始后等待多久再启动 Layer 过渡")]
    [SerializeField] float flareLeadTime = 0.08f;

    [Header("Yellow Filament Transparency")]
    [SerializeField] bool driveYellowFilamentTransparency = true;
    [SerializeField] bool autoFindYellowFilamentRenderers = true;
    [SerializeField] Renderer[] yellowFilamentRenderers;
    [SerializeField] string yellowFilamentShaderName = "Custom/YellowFilamentNebulaVolume";
    [SerializeField] float filamentOpaqueValue = 1f;
    [SerializeField] float filamentHiddenValue = 0f;
    [SerializeField] float filamentBlendDuration = 3f;

    [Header("Camera Look-Down Sequence")]
    [Tooltip("进入 Trigger 时俯视角度（顶视 TOP-DOWN，建议 85~90°）；与 Zoom In 同时进行")]
    [Range(0f, 90f)]
    [SerializeField] float enterTopDownPitchOffset = 85f;
    [Tooltip("离开 Trigger 时低头偏移角（仅用于离开）")]
    [SerializeField] float lookDownPitchOffset = 25f;
    [Tooltip("进入时缓慢低头到 Top-Down 的时长（建议 1~2 秒）")]
    [SerializeField] float enterLookDownDuration = 1.5f;
    [SerializeField] float lookDownBlendDuration = 0.35f;
    [Tooltip("低头保持时长（按需求固定 3 秒）")]
    [SerializeField] float lookDownHoldDuration = 3f;
    [Tooltip("抬头回正时长（建议比低头更长，避免突然）")]
    [SerializeField] float lookUpBlendDuration = 1.2f;
    [Tooltip("抬头前的短暂停顿，增强“缓抬头”感")]
    [SerializeField] float lookUpStartDelay = 0.12f;
    [SerializeField] bool lockLookInputDuringLookDown = true;

    [SerializeField] bool debugLog = true;
    [SerializeField] bool debugVerbose;

    bool _isInside;
    bool _hasInitializedState;
    Coroutine _stateRoutine;
    Coroutine _zoomRoutine;
    Coroutine _speedRoutine;
    Coroutine _filamentRoutine;
    float _nextPollingTime;
    readonly List<Material> _filamentMaterials = new List<Material>();
    float _currentFilamentTransparency = 1f;

    void Reset()
    {
        _triggerCollider = GetComponent<Collider>();
        if (_triggerCollider != null) _triggerCollider.isTrigger = true;
    }

    void Awake()
    {
        _triggerCollider = GetComponent<Collider>();
        if (_triggerCollider != null && !_triggerCollider.isTrigger)
        {
            _triggerCollider.isTrigger = true;
            Log("Force set collider.isTrigger = true");
        }
        if (autoAddKinematicRigidbody && GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            Log("Auto added kinematic Rigidbody for trigger reliability.");
        }
        BuildFilamentMaterialCache();
        Log($"Awake complete. Collider={(_triggerCollider != null ? _triggerCollider.GetType().Name : "null")}, isTrigger={(_triggerCollider != null && _triggerCollider.isTrigger)}");
    }

    void OnValidate()
    {
        transitionDuration = Mathf.Max(0.05f, transitionDuration);
        enterZoomMultiplier = Mathf.Max(0.05f, enterZoomMultiplier);
        exitZoomMultiplier = Mathf.Max(0.05f, exitZoomMultiplier);
        zoomBlendDuration = Mathf.Max(0.01f, zoomBlendDuration);
        enterSpeedMultiplier = Mathf.Max(0f, enterSpeedMultiplier);
        exitSpeedMultiplier = Mathf.Max(0f, exitSpeedMultiplier);
        speedBlendDuration = Mathf.Max(0.01f, speedBlendDuration);
        flareDuration = Mathf.Max(0.01f, flareDuration);
        flareLeadTime = Mathf.Max(0f, flareLeadTime);
        filamentOpaqueValue = Mathf.Clamp01(filamentOpaqueValue);
        filamentHiddenValue = Mathf.Clamp01(filamentHiddenValue);
        filamentBlendDuration = Mathf.Max(0.01f, filamentBlendDuration);
        enterTopDownPitchOffset = Mathf.Clamp(enterTopDownPitchOffset, 0f, 90f);
        enterLookDownDuration = Mathf.Max(0.2f, enterLookDownDuration);
        lookDownBlendDuration = Mathf.Max(0.01f, lookDownBlendDuration);
        lookDownHoldDuration = Mathf.Max(0f, lookDownHoldDuration);
        lookUpBlendDuration = Mathf.Max(0.01f, lookUpBlendDuration);
        lookUpStartDelay = Mathf.Max(0f, lookUpStartDelay);
        pollingInterval = Mathf.Max(0.02f, pollingInterval);
    }

    void OnTriggerEnter(Collider other)
    {
        if (debugVerbose) Log($"OnTriggerEnter from {other.name}");
        if (IsValidSource(other.gameObject))
            SetInsideState(true, "OnTriggerEnter");
    }

    void OnTriggerStay(Collider other)
    {
        if (debugVerbose) Log($"OnTriggerStay from {other.name}");
        if (IsValidSource(other.gameObject))
            SetInsideState(true, "OnTriggerStay");
    }

    void OnTriggerExit(Collider other)
    {
        if (debugVerbose) Log($"OnTriggerExit from {other.name}");
        if (IsValidSource(other.gameObject))
            SetInsideState(false, "OnTriggerExit");
    }

    void Update()
    {
        if (!usePollingFallback) return;
        if (Time.time < _nextPollingTime) return;
        _nextPollingTime = Time.time + pollingInterval;

        if (playerController == null)
            playerController = FindObjectOfType<DarkMatterPlayerController>();
        if (transitionController == null)
            transitionController = FindObjectOfType<MacroMicroTransitionController>();
        if (flareFlash == null && transitionController != null)
            flareFlash = transitionController.GetComponent<CameraFlareFlash>();
        if (playerController == null || transitionController == null || _triggerCollider == null) return;

        Vector3 p = playerController.transform.position;
        Vector3 closest = _triggerCollider.ClosestPoint(p);
        bool isInsideByClosest = (closest - p).sqrMagnitude < 0.0001f;
        bool isInsideByBounds = _triggerCollider.bounds.Contains(p);
        bool isInside = isInsideByClosest || isInsideByBounds;

        if (!_hasInitializedState)
        {
            _hasInitializedState = true;
            _isInside = isInside;
            // 若初始就在 trigger 内，也走完整进入动画（缓慢 Top-Down + Zoom In），不跳过
            if (isInside)
                SetInsideState(true, "InitPolling");
            else
                ApplyStateImmediate(false, "InitPolling");
        }

        if (isInside != _isInside)
        {
            if (debugVerbose)
                Log($"Polling state changed -> inside={isInside}, insideByClosest={isInsideByClosest}, insideByBounds={isInsideByBounds}");
            SetInsideState(isInside, "Polling");
        }
    }

    bool IsValidSource(GameObject sourceObject)
    {
        if (sourceObject == null) return false;
        if (requireTag && !sourceObject.CompareTag(targetTag))
        {
            if (debugVerbose) Log($"Skip: tag mismatch. sourceTag={sourceObject.tag}, need={targetTag}");
            return false;
        }

        if (playerController == null)
            playerController = sourceObject.GetComponentInParent<DarkMatterPlayerController>();
        if (transitionController == null)
            transitionController = FindObjectOfType<MacroMicroTransitionController>();
        if (flareFlash == null && transitionController != null)
            flareFlash = transitionController.GetComponent<CameraFlareFlash>();

        if (playerController == null || transitionController == null)
        {
            Debug.LogWarning("[MicroToMacroTriggerSequence] Missing references: playerController or transitionController.");
            return false;
        }
        return true;
    }

    void SetInsideState(bool inside, string source)
    {
        if (!_hasInitializedState)
            _hasInitializedState = true;
        if (_isInside == inside) return;
        _isInside = inside;

        if (transitionController == null)
            transitionController = FindObjectOfType<MacroMicroTransitionController>();
        if (playerController == null)
            playerController = FindObjectOfType<DarkMatterPlayerController>();
        if (transitionController == null || playerController == null)
        {
            Debug.LogWarning("[MicroToMacroTriggerSequence] Cannot run transition: transitionController or playerController is null.");
            return;
        }

        if (_zoomRoutine != null) StopCoroutine(_zoomRoutine);
        if (_speedRoutine != null) StopCoroutine(_speedRoutine);
        float targetZoom = inside ? enterZoomMultiplier : exitZoomMultiplier;
        float targetSpeed = inside ? enterSpeedMultiplier : exitSpeedMultiplier;
        float zoomDur = inside ? enterLookDownDuration : zoomBlendDuration; // 进入时与缓慢低头同速
        _zoomRoutine = StartCoroutine(playerController.TweenExternalOrbitDistanceMultiplier(targetZoom, zoomDur));
        _speedRoutine = StartCoroutine(playerController.TweenExternalSpeedMultiplier(targetSpeed, speedBlendDuration));

        if (_stateRoutine != null) StopCoroutine(_stateRoutine);
        _stateRoutine = StartCoroutine(PlayStateSequence(inside));

        Log($"State -> {(inside ? "INSIDE/Micro" : "OUTSIDE/Macro")} via {source}, transition={transitionDuration:0.00}s, zoom={targetZoom:0.00}, speed={targetSpeed:0.00}, flare={(useFlareBeforeSwitch ? "on" : "off")}, switchAfterLookDown=true");
    }

    void ApplyStateImmediate(bool inside, string source)
    {
        if (transitionController == null)
            transitionController = FindObjectOfType<MacroMicroTransitionController>();
        if (transitionController == null || playerController == null) return;

        transitionController.RebuildCaches();
        transitionController.SetTransitionImmediate(inside ? 1f : 0f);
        playerController.SetExternalOrbitDistanceMultiplier(inside ? enterZoomMultiplier : exitZoomMultiplier);
        playerController.SetExternalSpeedMultiplier(inside ? enterSpeedMultiplier : exitSpeedMultiplier);
        if (_stateRoutine != null)
        {
            StopCoroutine(_stateRoutine);
            _stateRoutine = null;
        }
        StartCoroutine(playerController.TweenCinematicPitchOffset(0f, 0.01f));
        ApplyFilamentTransparencyImmediate(inside ? filamentHiddenValue : filamentOpaqueValue);
        Log($"Initial state -> {(inside ? "INSIDE/Micro" : "OUTSIDE/Macro")} via {source}");
    }

    IEnumerator PlayStateSequence(bool inside)
    {
        if (lockLookInputDuringLookDown)
            playerController.SetLookInputLocked(true);

        if (inside)
        {
            // 进入：先保持 Macro 可见；相机 TOP-DOWN + Zoom In（Zoom 已在 SetInsideState 里启动）
            transitionController.RebuildCaches();
            transitionController.SetTransitionImmediate(0f); // 先关闭 MicroLevel，仅显示 MacroLevel

            if (useFlareBeforeSwitch && flareFlash != null)
            {
                flareFlash.PlayFlash(flareDuration, flarePeakAlpha, flareColor);
                yield return new WaitForSeconds(flareLeadTime);
            }

            // 缓慢 Top-Down + Zoom In（Zoom 已由 SetInsideState 启动）
            Log("Enter: starting camera top-down + zoom in");
            yield return playerController.TweenCinematicPitchOffset(enterTopDownPitchOffset, enterLookDownDuration);

            // 相机达到 90°(Top-Down) 后再打开 Micro、关闭 Macro
            transitionController.SetTransitionImmediate(1f);

            if (_filamentRoutine != null) StopCoroutine(_filamentRoutine);
            _filamentRoutine = StartCoroutine(TweenFilamentTransparency(filamentHiddenValue, Mathf.Max(0.01f, filamentBlendDuration)));

            yield return new WaitForSeconds(lookDownHoldDuration);
            if (lookUpStartDelay > 0f)
                yield return new WaitForSeconds(lookUpStartDelay);

            // 保持 Micro 可见，仅做“慢抬头回正”
            transitionController.SetTransitionImmediate(1f);
            if (_filamentRoutine != null) StopCoroutine(_filamentRoutine);
            _filamentRoutine = StartCoroutine(TweenFilamentTransparency(filamentOpaqueValue, Mathf.Max(0.01f, filamentBlendDuration)));

            yield return playerController.TweenCinematicPitchOffset(0f, lookUpBlendDuration);
        }
        else
        {
            // 离开：低头 -> 切回 Macro -> 保持 -> 抬头
            yield return playerController.TweenCinematicPitchOffset(lookDownPitchOffset, lookDownBlendDuration);

            if (useFlareBeforeSwitch && flareFlash != null)
            {
                flareFlash.PlayFlash(flareDuration, flarePeakAlpha, flareColor);
                yield return new WaitForSeconds(flareLeadTime);
            }

            transitionController.RebuildCaches();
            transitionController.StartTransition(0f, transitionDuration); // 关 Micro，开 Macro

            if (_filamentRoutine != null) StopCoroutine(_filamentRoutine);
            _filamentRoutine = StartCoroutine(TweenFilamentTransparency(filamentOpaqueValue, Mathf.Max(0.01f, filamentBlendDuration)));

            yield return new WaitForSeconds(lookDownHoldDuration);
            if (lookUpStartDelay > 0f)
                yield return new WaitForSeconds(lookUpStartDelay);
            yield return playerController.TweenCinematicPitchOffset(0f, lookUpBlendDuration);
        }

        if (lockLookInputDuringLookDown)
            playerController.SetLookInputLocked(false);
        _stateRoutine = null;
    }

    void BuildFilamentMaterialCache()
    {
        _filamentMaterials.Clear();
        if (!driveYellowFilamentTransparency) return;

        var renderers = yellowFilamentRenderers;
        if ((renderers == null || renderers.Length == 0) && autoFindYellowFilamentRenderers)
            renderers = FindObjectsOfType<Renderer>(); // Unity 2019: no includeInactive overload; only active objects are found
        if (renderers == null) return;

        var unique = new HashSet<Material>();
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            var mats = r.materials;
            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                if (mat == null) continue;
                if (!mat.HasProperty("_Transparency")) continue;
                if (!string.IsNullOrEmpty(yellowFilamentShaderName) && mat.shader != null && mat.shader.name != yellowFilamentShaderName)
                    continue;
                if (unique.Add(mat))
                    _filamentMaterials.Add(mat);
            }
        }

        if (_filamentMaterials.Count > 0)
            _currentFilamentTransparency = _filamentMaterials[0].GetFloat("_Transparency");
        else if (debugLog)
            Debug.LogWarning("[MicroToMacroTriggerSequence] No Yellow Filament materials found for _Transparency animation.");
    }

    void ApplyFilamentTransparencyImmediate(float value)
    {
        if (!driveYellowFilamentTransparency) return;
        if (_filamentMaterials.Count == 0) BuildFilamentMaterialCache();
        float v = Mathf.Clamp01(value);
        for (int i = 0; i < _filamentMaterials.Count; i++)
            _filamentMaterials[i].SetFloat("_Transparency", v);
        _currentFilamentTransparency = v;
    }

    IEnumerator TweenFilamentTransparency(float target, float duration)
    {
        if (!driveYellowFilamentTransparency) yield break;
        if (_filamentMaterials.Count == 0) BuildFilamentMaterialCache();
        if (_filamentMaterials.Count == 0) yield break;

        float from = _currentFilamentTransparency;
        float to = Mathf.Clamp01(target);
        float d = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        while (elapsed < d)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / d);
            float eased = t * t * (3f - 2f * t);
            float v = Mathf.Lerp(from, to, eased);
            for (int i = 0; i < _filamentMaterials.Count; i++)
                _filamentMaterials[i].SetFloat("_Transparency", v);
            _currentFilamentTransparency = v;
            yield return null;
        }
        for (int i = 0; i < _filamentMaterials.Count; i++)
            _filamentMaterials[i].SetFloat("_Transparency", to);
        _currentFilamentTransparency = to;
        _filamentRoutine = null;
    }

    void Log(string message)
    {
        if (!debugLog) return;
        Debug.Log($"[MicroToMacroTriggerSequence:{name}] {message}");
    }
}
