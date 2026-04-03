using UnityEngine;

///
/// <summary>
///  Example listener that reads event broadcast from layer statemanager
/// </summary>
/// 

public class LayerManagerListenerExample : MonoBehaviour
{
    [SerializeField] LayerStateManager stateManager;

    LayerStateSnapshot _last; // layer snapshot local cache

    void Awake()
    {
        if (stateManager == null)
            stateManager = FindObjectOfType<LayerStateManager>();
        if (stateManager != null)
            _last = stateManager.Snapshot;
    }

    void OnEnable()
    {
        if (stateManager == null) return;
        stateManager.OnStateChanged += HandleStateChanged;
    }

    void OnDisable()
    {
        if (stateManager == null) return;
        stateManager.OnStateChanged -= HandleStateChanged;
    }
    
    void HandleStateChanged(LayerStateSnapshot snapshot)
    {
        _last = snapshot;
        Debug.Log("this is listener, heard statechange: " + snapshot.currentDefinition.layerId);

        // Placeholder behavior, read the new layer profile
        var def = snapshot.currentDefinition;
        Debug.Log(
            $"[LayerListener] layerId={def.layerId} " +
            $"universeLayer={def.universeLayer} " +
            $"enterSpeed={def.enterSpeedMultiplier} exitSpeed={def.exitSpeedMultiplier} speedBlend={def.speedBlendDuration} " +
            $"transitionDuration={def.transitionDuration} " +
            $"flare: use={def.useFlareBeforeSwitch} lead={def.flareLeadTime} dur={def.flareDuration} peakA={def.flarePeakAlpha} " +
            $"filament: drive={def.driveYellowFilamentTransparency} opaque={def.filamentOpaqueValue} hidden={def.filamentHiddenValue} blend={def.filamentBlendDuration} " +
            $"lookDown: enter={def.enterLookDownDuration} blend={def.lookDownBlendDuration} hold={def.lookDownHoldDuration} " +
            $"lookUp: blend={def.lookUpBlendDuration} startDelay={def.lookUpStartDelay} " +
            $"HUD microMax={def.microSliderMaxLightYearsWhenInside} " +
            $"phase={snapshot.phase} p01={snapshot.normalizedProgress01:0.00}"
        );
    }
}