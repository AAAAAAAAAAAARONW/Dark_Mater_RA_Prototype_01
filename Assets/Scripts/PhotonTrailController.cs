using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// On the photon/player: detects overlapping AbsorptionVolumes, combines their absorption
/// (SUM or MAX), and feeds parameters to the TrailRenderer material via MaterialPropertyBlock.
/// Requires a TrailRenderer (e.g. on a child "Trail") using the PhotonTrail shader.
/// Uses overlap check so it works with CharacterController (no Rigidbody required on player).
/// </summary>
public class PhotonTrailController : MonoBehaviour
{
    [Header("Trail")]
    [Tooltip("TrailRenderer to drive (e.g. child 'Trail'). If null, uses first in children.")]
    [SerializeField] TrailRenderer trail;

    [Header("Combination")]
    [Tooltip("Combine multiple overlapping volumes: Max = strongest wins, Sum = stack (clamped to 1)")]
    [SerializeField] bool combineAsSum;
    [Header("Test")]
    [Tooltip("Force absorption for testing (ignores volumes). Disable for normal operation.")]
    [SerializeField] bool testMode = false;
    [Tooltip("Test absorption value (0-1). Only used when testMode is enabled.")]
    [Range(0f, 1f)]
    [SerializeField] float testAbsorb = 0.5f;

    [Header("Absorption Smoothing")]
    [Tooltip("How fast absorption ramps up when entering a volume (per second)")]
    [SerializeField] float absorbRiseSpeed = 0.6f;
    [Tooltip("How fast stripe/noise params blend in (per second)")]
    [SerializeField] float paramBlendSpeed = 2f;

    [Header("Overlap")]
    [Tooltip("Radius used to detect overlapping AbsorptionVolumes at player position")]
    [SerializeField] float overlapRadius = 2f;
    [Tooltip("Debug: show overlap sphere in Scene view")]
    [SerializeField] bool debugDrawOverlap = true;
    [Tooltip("Debug: print absorption values to console")]
    [SerializeField] bool debugLog = false;

    readonly List<AbsorptionVolume> _volumes = new List<AbsorptionVolume>();
    Collider[] _overlapBuffer = new Collider[32];
    MaterialPropertyBlock _block;
    TrailRenderer _trailRenderer;
    Material _trailMaterialInstance; // Instance material for direct modification
    float _currentAbsorb;
    float _currentStripeFreq = 8f;
    float _currentStripeSharp = 3f;
    float _currentNoiseScale = 4f;
    float _currentNoiseSpeed = 1f;
    Color _currentTintShift = Color.clear;
    bool _currentUseTint;

    /// <summary>Current absorption value (0..1), ramping up and persistent.</summary>
    public float CurrentAbsorb => _currentAbsorb;

    static readonly int AbsorbId = Shader.PropertyToID("_Absorb");
    static readonly int StripeFrequencyId = Shader.PropertyToID("_StripeFrequency");
    static readonly int StripeSharpnessId = Shader.PropertyToID("_StripeSharpness");
    static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
    static readonly int NoiseSpeedId = Shader.PropertyToID("_NoiseSpeed");
    static readonly int TintShiftId = Shader.PropertyToID("_TintShift");
    static readonly int UseTintShiftId = Shader.PropertyToID("_UseTintShift");

    void Awake()
    {
        if (trail == null)
            trail = GetComponentInChildren<TrailRenderer>();
        if (trail != null)
        {
            _trailRenderer = trail;
            // Create material instance to ensure we can modify it (only at runtime)
            if (Application.isPlaying && _trailRenderer.sharedMaterial != null)
            {
                _trailMaterialInstance = new Material(_trailRenderer.sharedMaterial);
                _trailRenderer.material = _trailMaterialInstance;
                // Initialize absorption to 0 (override any material default)
                _trailMaterialInstance.SetFloat(AbsorbId, 0f);
            }
        }
        _block = new MaterialPropertyBlock();
    }

    void OnValidate()
    {
        // Only find TrailRenderer in edit mode, don't create material instances
        if (trail == null)
            trail = GetComponentInChildren<TrailRenderer>();
        // Don't create material instances in edit mode - only in runtime
        // Material instance creation happens in Awake() and LateUpdate() during runtime
    }

    void OnDestroy()
    {
        // Clean up material instance
        if (_trailMaterialInstance != null)
        {
            Destroy(_trailMaterialInstance);
        }
    }

    void LateUpdate()
    {
        // Only run at runtime
        if (!Application.isPlaying) return;
        
        if (_trailRenderer == null)
        {
            if (trail == null)
                trail = GetComponentInChildren<TrailRenderer>();
            if (trail != null)
                _trailRenderer = trail;
            if (_trailRenderer == null) return;
        }

        // Ensure material instance exists (only at runtime)
        if (Application.isPlaying && _trailMaterialInstance == null && _trailRenderer.sharedMaterial != null)
        {
            _trailMaterialInstance = new Material(_trailRenderer.sharedMaterial);
            _trailRenderer.material = _trailMaterialInstance;
            if (debugLog)
                Debug.Log("[PhotonTrail] Created material instance");
        }

        _volumes.Clear();
        // Use QueryTriggerInteraction.Collide to detect trigger colliders
        // Also try without QueryTriggerInteraction in case volumes aren't triggers
        int nHit = Physics.OverlapSphereNonAlloc(transform.position, overlapRadius, _overlapBuffer, -1, QueryTriggerInteraction.Collide);
        
        // Also check all AbsorptionVolumes in scene if overlap doesn't work
        if (nHit == 0 && !testMode)
        {
            // Fallback: check distance to all volumes
            AbsorptionVolume[] allVolumes = FindObjectsOfType<AbsorptionVolume>();
            foreach (var vol in allVolumes)
            {
                if (vol != null && vol.gameObject.activeInHierarchy)
                {
                    float dist = Vector3.Distance(transform.position, vol.transform.position);
                    // Check if player is inside volume's collider bounds
                    Collider col = vol.GetComponent<Collider>();
                    if (col != null && col.bounds.Contains(transform.position))
                    {
                        if (!_volumes.Contains(vol))
                            _volumes.Add(vol);
                    }
                    else if (dist < overlapRadius)
                    {
                        // Also check by distance as fallback
                        if (!_volumes.Contains(vol))
                            _volumes.Add(vol);
                    }
                }
            }
        }
        else
        {
            // Normal overlap detection
            for (int i = 0; i < nHit; i++)
            {
                var vol = _overlapBuffer[i].GetComponent<AbsorptionVolume>();
                if (vol != null && !_volumes.Contains(vol))
                    _volumes.Add(vol);
            }
        }
        
        if (nHit == _overlapBuffer.Length && nHit > 0)
        {
            var next = new Collider[_overlapBuffer.Length * 2];
            System.Array.Copy(_overlapBuffer, next, _overlapBuffer.Length);
            _overlapBuffer = next;
        }

        float absorb = 0f;
        float stripeFreq = _currentStripeFreq;
        float stripeSharp = _currentStripeSharp;
        float noiseScale = _currentNoiseScale;
        float noiseSpeed = _currentNoiseSpeed;
        Color tintShift = _currentTintShift;
        bool useTint = _currentUseTint;

        if (testMode)
        {
            // Test mode: force absorption for debugging
            absorb = testAbsorb;
            stripeFreq = 8f;
            stripeSharp = 3f;
            noiseScale = 4f;
            noiseSpeed = 1f;
        }
        else if (_volumes.Count > 0)
        {
            if (combineAsSum)
            {
                foreach (var v in _volumes)
                {
                    absorb += v.AbsorptionStrength;
                    stripeFreq += v.StripeFrequency;
                    stripeSharp += v.StripeSharpness;
                    noiseScale += v.NoiseScale;
                    noiseSpeed += v.NoiseSpeed;
                    if (v.UseTintShift)
                    {
                        useTint = true;
                        tintShift += v.TintShift;
                    }
                }
                absorb = Mathf.Clamp01(absorb);
                if (_volumes.Count > 1)
                {
                    int n = _volumes.Count;
                    stripeFreq /= n;
                    stripeSharp /= n;
                    noiseScale /= n;
                    noiseSpeed /= n;
                    if (useTint) tintShift /= n;
                }
            }
            else
            {
                foreach (var v in _volumes)
                {
                    if (v.AbsorptionStrength > absorb)
                    {
                        absorb = v.AbsorptionStrength;
                        stripeFreq = v.StripeFrequency;
                        stripeSharp = v.StripeSharpness;
                        noiseScale = v.NoiseScale;
                        noiseSpeed = v.NoiseSpeed;
                        useTint = v.UseTintShift;
                        tintShift = v.TintShift;
                    }
                }
            }
        }

        // Gradual absorption: ramp up only, never auto-decrease
        if (absorb > _currentAbsorb)
        {
            _currentAbsorb = Mathf.MoveTowards(_currentAbsorb, absorb, absorbRiseSpeed * Time.deltaTime);
            float blend = 1f - Mathf.Exp(-paramBlendSpeed * Time.deltaTime);
            _currentStripeFreq = Mathf.Lerp(_currentStripeFreq, stripeFreq, blend);
            _currentStripeSharp = Mathf.Lerp(_currentStripeSharp, stripeSharp, blend);
            _currentNoiseScale = Mathf.Lerp(_currentNoiseScale, noiseScale, blend);
            _currentNoiseSpeed = Mathf.Lerp(_currentNoiseSpeed, noiseSpeed, blend);
            if (useTint)
            {
                _currentUseTint = true;
                _currentTintShift = Color.Lerp(_currentTintShift, tintShift, blend);
            }
        }

        // Try both MaterialPropertyBlock and direct material modification
        // TrailRenderer sometimes doesn't respect PropertyBlock, so we modify material directly
        // Always set absorb to ensure it's 0 when no volumes (override any material default)
        if (_trailMaterialInstance != null)
        {
            // Direct material modification (more reliable for TrailRenderer)
            _trailMaterialInstance.SetFloat(AbsorbId, _currentAbsorb);
            _trailMaterialInstance.SetFloat(StripeFrequencyId, _currentStripeFreq);
            _trailMaterialInstance.SetFloat(StripeSharpnessId, _currentStripeSharp);
            _trailMaterialInstance.SetFloat(NoiseScaleId, _currentNoiseScale);
            _trailMaterialInstance.SetFloat(NoiseSpeedId, _currentNoiseSpeed);
            _trailMaterialInstance.SetFloat(UseTintShiftId, _currentUseTint ? 1f : 0f);
            _trailMaterialInstance.SetColor(TintShiftId, _currentTintShift);
        }
        else if (_trailRenderer != null)
        {
            // Fallback to PropertyBlock
            _trailRenderer.GetPropertyBlock(_block);
            _block.SetFloat(AbsorbId, _currentAbsorb);
            _block.SetFloat(StripeFrequencyId, _currentStripeFreq);
            _block.SetFloat(StripeSharpnessId, _currentStripeSharp);
            _block.SetFloat(NoiseScaleId, _currentNoiseScale);
            _block.SetFloat(NoiseSpeedId, _currentNoiseSpeed);
            _block.SetFloat(UseTintShiftId, _currentUseTint ? 1f : 0f);
            _block.SetColor(TintShiftId, _currentTintShift);
            _trailRenderer.SetPropertyBlock(_block);
        }
        
        // Also check if material uses correct shader (use sharedMaterial to avoid edit mode issues)
        if (_trailRenderer.sharedMaterial != null)
        {
            string shaderName = _trailRenderer.sharedMaterial.shader.name;
            if (!shaderName.Contains("PhotonTrail"))
            {
                if (debugLog && Application.isPlaying && Time.frameCount % 300 == 0) // Every 5 seconds
                {
                    Debug.LogWarning($"[PhotonTrail] TrailRenderer material shader is '{shaderName}', expected 'Custom/PhotonTrail'! Absorption won't work.");
                }
            }
        }

        // Debug output (only at runtime)
        if (Application.isPlaying && debugLog && Time.frameCount % 60 == 0) // Every 60 frames
        {
            string matName = _trailRenderer.sharedMaterial != null ? _trailRenderer.sharedMaterial.shader.name : "null";
            string volNames = "";
            foreach (var v in _volumes)
            {
                if (v != null) volNames += v.name + " ";
            }
            Debug.Log($"[PhotonTrail] Pos: {transform.position}, Volumes: {_volumes.Count} [{volNames}], Absorb: {_currentAbsorb:F2}, StripeFreq: {_currentStripeFreq:F2}, Material: {matName}, Instance: {(_trailMaterialInstance != null ? "yes" : "no")}");
        }
    }

    void OnDrawGizmosSelected()
    {
        if (debugDrawOverlap)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, overlapRadius);
        }
    }
}
