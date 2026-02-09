using UnityEngine;

/// <summary>
/// Trigger volume that causes the photon trail to show "partial absorption" (striped/fragmented gaps)
/// when the player passes through. Add a Collider (set to Is Trigger) and optionally a Rigidbody (kinematic).
/// PhotonTrailController on the player reads these params and drives the trail shader.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AbsorptionVolume : MonoBehaviour
{
    [Header("Absorption")]
    [Tooltip("How much the trail is absorbed (0 = none, 1 = full banding visible)")]
    [Range(0f, 1f)]
    [SerializeField] float absorptionStrength = 0.7f;

    [Header("Stripe / band pattern")]
    [Tooltip("Frequency of stripes along the trail (along UV.y)")]
    [SerializeField] float stripeFrequency = 8f;
    [Tooltip("Sharpness of stripe edges (higher = harder bands)")]
    [Range(0.1f, 10f)]
    [SerializeField] float stripeSharpness = 3f;

    [Header("Noise (fragmentation)")]
    [Tooltip("Scale of noise for irregular gaps")]
    [SerializeField] float noiseScale = 4f;
    [Tooltip("Speed of noise animation")]
    [SerializeField] float noiseSpeed = 1f;

    [Header("Optional tint")]
    [Tooltip("Optional color shift where absorption happens (e.g. blue scattering)")]
    [SerializeField] bool useTintShift;
    [SerializeField] Color tintShift = new Color(0.3f, 0.5f, 1f, 0.2f);

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (!col.isTrigger)
            col.isTrigger = true;
    }

    public float AbsorptionStrength => absorptionStrength;
    public float StripeFrequency => stripeFrequency;
    public float StripeSharpness => stripeSharpness;
    public float NoiseScale => noiseScale;
    public float NoiseSpeed => noiseSpeed;
    public bool UseTintShift => useTintShift;
    public Color TintShift => tintShift;
}
