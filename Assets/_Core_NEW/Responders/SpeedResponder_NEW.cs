using System.Collections;
using UnityEngine;

/// <summary>
/// Tweens the player's external speed multiplier when its channel fires.
///
/// Replaces SpeedLayerResponder. The old version delayed the tween by
/// LayerDefinitionTest.transitionDuration so the speed ramp would land at the same
/// moment as the world swap — a good intent implemented as an implicit contract
/// between two scripts that happened to read the same field. Nothing in the code
/// said they had to match, and the camera's own wait had drifted out of sync with
/// the real blend times, so on four of five transitions the speed change fired while
/// the player could still see the world.
///
/// Here both ride the same anchor, so they are aligned by construction and
/// transitionDuration is not needed at all.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("SPEED", "#4A94F2")]
public class SpeedResponder_NEW : LayerResponder_NEW
{
    [Header("Target")]
    [SerializeField] PlayerRig_NEW player;

    Coroutine _tween;

    protected override void Awake()
    {
        base.Awake();
        if (player == null) player = FindObjectOfType<PlayerRig_NEW>();
        if (player == null)
            Debug.LogError("[SpeedResponder_NEW] No PlayerRig_NEW found. Speed will not change.", this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (_tween != null) { StopCoroutine(_tween); _tween = null; }
    }

    protected override TimedChannel_NEW SelectChannel(LayerProfile_NEW profile) => profile.timing.speed;

    protected override void Apply(LayerProfile_NEW profile)
    {
        if (player == null) return;

        // Stop the previous tween on this component. The old version started a nested
        // coroutine on the player, which StopCoroutine could not reach, so two tweens
        // could end up fighting over the same multiplier.
        if (_tween != null) StopCoroutine(_tween);
        _tween = StartCoroutine(TweenTo(profile.zoneSpeedMultiplier, profile.speedBlendDuration));
    }

    IEnumerator TweenTo(float target, float duration)
    {
        float from = player.ExternalSpeedMultiplier;
        float to = Mathf.Max(0f, target);
        float d = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < d)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / d);
            float eased = t * t * (3f - 2f * t);   // same SmoothStep curve as the original
            player.SetExternalSpeedMultiplier(Mathf.Lerp(from, to, eased));
            yield return null;
        }

        player.SetExternalSpeedMultiplier(to);
        _tween = null;
    }
}
