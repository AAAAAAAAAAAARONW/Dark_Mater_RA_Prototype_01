PhotonTrail – Partial absorption (striped gaps) for TrailRenderer
================================================================

1) AbsorptionVolume (trigger)
   - Add to a GameObject with a Collider (set Is Trigger = true).
   - Optional: add Rigidbody, set to Kinematic (not required for overlap detection).
   - Params: absorptionStrength, stripeFrequency, stripeSharpness, noiseScale, noiseSpeed, optional tintShift.
   - Place volumes in the scene where you want the trail to look "absorbed" (different media).

2) PhotonTrailController (on photon/player)
   - Add to the same GameObject as the player (e.g. with DarkMatterPlayerController).
   - Assign the TrailRenderer (e.g. child "Trail"); if null, it uses the first TrailRenderer in children.
   - Overlap: uses a small sphere at the player position to detect overlapping AbsorptionVolumes (works with CharacterController).
   - Combine: Max = strongest volume wins; Sum = stack absorption (clamped to 1).

3) Custom Trail material
   - Create a new Material, set Shader to "Custom/PhotonTrail".
   - Assign this material to the TrailRenderer's Material slot (replace the default).
   - IMPORTANT: The TrailRenderer MUST use a material with PhotonTrail shader, otherwise absorption won't work!
   - The shader is Additive (Blend SrcAlpha One). It uses UV.y (along trail) + time + noise to generate an absorption mask that locally reduces alpha, producing banded/fragmented "eaten" segments when _Absorb > 0.
   - PhotonTrailController sets _Absorb and the stripe/noise params via MaterialPropertyBlock when the player is inside AbsorptionVolumes.
   - Glow effect: The shader includes edge glow (_GlowIntensity, _GlowFalloff) for soft glowing edges.

Troubleshooting:
   - If absorption doesn't work: Check that TrailRenderer material uses "Custom/PhotonTrail" shader.
   - Increase PhotonTrailController's overlapRadius if volumes aren't detected.
   - Enable debugDrawOverlap to visualize the detection sphere in Scene view.
   - Ensure AbsorptionVolume has a Collider with Is Trigger = true.

Result: When passing through different media (AbsorptionVolumes), the trail shows partial "eaten" stripes/bands with glowing edges like in your sketch.
