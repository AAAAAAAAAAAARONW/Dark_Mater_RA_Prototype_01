using UnityEngine;

/// <summary>
/// Trigger volume that switches LymanAlphaAbsorptionController between
/// TriggerBox and FakeForest modes when the player enters.
///
/// Use cases:
///   - Switch to FakeForest when entering the dense Lyman-alpha forest zone
///     (11B–10B ly) for an immediate saturated-forest look.
///   - Switch back to TriggerBox when leaving the dense zone.
///   - Place a FakeForest trigger at scene start for playtesting
///     before real trigger boxes are placed.
///
/// SETUP:
/// 1. Add a Collider to this GameObject, set isTrigger = true.
/// 2. Assign LymanAlphaAbsorptionController reference.
/// 3. Set Target Mode to the mode you want to switch TO on enter.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LymanAlphaModeTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] LymanAlphaAbsorptionController absorptionController;

    [Header("Mode Switch")]
    [Tooltip("The mode to switch to when the player enters this trigger.")]
    [SerializeField] LymanAlphaAbsorptionController.AbsorptionMode targetMode
        = LymanAlphaAbsorptionController.AbsorptionMode.FakeForest;

    [Tooltip("If true, switch back to the opposite mode when the player exits.")]
    [SerializeField] bool revertOnExit = true;

    [Tooltip("If true, this trigger can only fire once per play session.")]
    [SerializeField] bool fireOnce = false;

    [Header("Filter")]
    [SerializeField] string targetTag = "Player";

    [Header("Debug")]
    [SerializeField] bool debugLog = true;

    bool _fired = false;
    LymanAlphaAbsorptionController.AbsorptionMode _previousMode;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (!col.isTrigger) col.isTrigger = true;

        if (absorptionController == null)
            absorptionController = FindObjectOfType<LymanAlphaAbsorptionController>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (fireOnce && _fired) return;
        if (!other.CompareTag(targetTag)) return;
        if (absorptionController == null) return;

        _previousMode = absorptionController.CurrentMode;
        absorptionController.SetMode(targetMode);
        _fired = true;

        if (debugLog)
            Debug.Log($"[LyaModeTrigger] '{name}' entered — switched to {targetMode}.");
    }

    void OnTriggerExit(Collider other)
    {
        if (!revertOnExit) return;
        if (!other.CompareTag(targetTag)) return;
        if (absorptionController == null) return;

        absorptionController.SetMode(_previousMode);

        if (debugLog)
            Debug.Log($"[LyaModeTrigger] '{name}' exited — reverted to {_previousMode}.");
    }
}
