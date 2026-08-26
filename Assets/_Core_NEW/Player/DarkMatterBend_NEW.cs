using System;
using UnityEngine;

/// <summary>
/// Dark matter path bending, lifted out of the player controller.
///
/// This is a plain serializable class, not a MonoBehaviour, and PlayerRig_NEW calls
/// Tick() from the exact position in Update that the original occupied. Splitting it
/// into its own component would have moved it in the script execution order and that
/// is a change in feel, which is not on the table.
///
/// The maths is a line-for-line port. Two stages, exactly as before:
///
///   ANGLE RETURN   slerp back toward the pre-bend heading. The angular speed is the
///                  largest of: one that matches how long the bend lasted, one that
///                  guarantees completion inside returnMaxTime, and a floor.
///   HEIGHT RETURN  drive Y back to the entry height over the time it took to reach
///                  the apex, so the player is not left stranded above the path.
///
/// The only functional change is where the DarkMatter list comes from — see
/// DarkMatterRegistry_NEW. The set is the same and deflection is summed, so the
/// resulting direction is unchanged.
/// </summary>
[Serializable]
public class DarkMatterBend_NEW
{
    [Tooltip("Floor on the angular return speed, radians per second.")]
    [SerializeField] float returnSpeedMinRadPerSec = 1.5f;

    [Tooltip("Hard cap on how long the heading may take to recover.")]
    [SerializeField] float returnMaxTime = 3f;

    [Tooltip("Floor on the height-return duration, to avoid dividing by zero.")]
    [SerializeField] float heightReturnMinDuration = 0.3f;

    [Tooltip("Log when the player first enters an influence volume.")]
    [SerializeField] bool logEncounters = true;

    bool _hadInfluence;
    bool _returningHeading;
    bool _returningHeight;

    float _influenceTime;
    float _maxBendAngleRad;
    float _returnAngularSpeed;

    float _entryHeight;
    float _entryTime;
    float _maxHeight;
    float _maxHeightTime;
    float _heightReturnStart;
    float _heightReturnDuration;

    /// <summary>True while the player is inside any influence volume.</summary>
    public bool HasInfluence => _hadInfluence;

    /// <summary>
    /// Advance one frame. Mutates <paramref name="movementDirection"/> and, when the
    /// player is flying straight, re-baselines <paramref name="defaultDirection"/>.
    /// </summary>
    public void Tick(float dt, Vector3 position, float speed,
                     ref Vector3 movementDirection, ref Vector3 defaultDirection)
    {
        DarkMatter_NEW[] sources = DarkMatterRegistry_NEW.All;

        Vector3 totalDeflection = Vector3.zero;
        float now = Time.time;

        for (int i = 0; i < sources.Length; i++)
        {
            DarkMatter_NEW dm = sources[i];
            if (dm == null || !dm.IsInInfluence(position)) continue;
            totalDeflection += dm.GetDeflectionAt(position, defaultDirection);
        }

        bool hasInfluence = totalDeflection.sqrMagnitude > 0.0001f;

        if (hasInfluence)
        {
            if (!_hadInfluence)
            {
                if (logEncounters) Debug.Log("[Dark Matter] Encountered dark matter influence.");
                _entryHeight = position.y;
                _entryTime = now;
                _maxHeight = position.y;
                _maxHeightTime = now;
            }

            _returningHeading = false;
            _returningHeight = false;

            if (position.y > _maxHeight)
            {
                _maxHeight = position.y;
                _maxHeightTime = now;
            }

            Vector3 bent = (defaultDirection + totalDeflection).normalized;
            movementDirection = bent;
            _influenceTime += dt;

            float angle = Mathf.Acos(Mathf.Clamp01(Vector3.Dot(defaultDirection, bent)));
            if (angle > _maxBendAngleRad) _maxBendAngleRad = angle;
        }
        else
        {
            if (_hadInfluence)
            {
                _returningHeading = true;

                float effective = Mathf.Max(Mathf.Min(_influenceTime, returnMaxTime), 0.05f);
                float matched = _maxBendAngleRad / effective;
                float finishInMax = _maxBendAngleRad / returnMaxTime;
                _returnAngularSpeed = Mathf.Max(matched, finishInMax, returnSpeedMinRadPerSec);

                _heightReturnDuration = Mathf.Max(_maxHeightTime - _entryTime, heightReturnMinDuration);
                _heightReturnStart = now;
                _returningHeight = true;
            }

            _influenceTime = 0f;
            _maxBendAngleRad = 0f;

            if (_returningHeight)
            {
                TickHeightReturn(now, position, speed, ref movementDirection, defaultDirection);
            }
            else if (_returningHeading)
            {
                TickHeadingReturn(dt, ref movementDirection, defaultDirection);
            }
            else
            {
                // Flying straight: whatever direction we are on becomes the new baseline,
                // so an external SetMovementDirection call is respected by later bends.
                defaultDirection = movementDirection;
            }
        }

        _hadInfluence = hasInfluence;
    }

    void TickHeightReturn(float now, Vector3 position, float speed,
                          ref Vector3 movementDirection, Vector3 defaultDirection)
    {
        float elapsed = now - _heightReturnStart;
        float remaining = _heightReturnDuration - elapsed;

        if (remaining <= 0f || Mathf.Abs(position.y - _entryHeight) < 0.02f)
        {
            movementDirection = defaultDirection;
            _returningHeight = false;
            _returningHeading = false;
            return;
        }

        float desiredVy = (_entryHeight - position.y) / remaining;
        float dy = Mathf.Clamp(desiredVy / speed, -1f, 1f);

        Vector3 horizontal = new Vector3(defaultDirection.x, 0f, defaultDirection.z);
        float length = horizontal.magnitude;
        if (length < 0.0001f) horizontal = Vector3.forward;
        else horizontal /= length;

        float k = Mathf.Sqrt(Mathf.Max(0f, 1f - dy * dy));
        movementDirection = new Vector3(horizontal.x * k, dy, horizontal.z * k).normalized;

        float angle = Mathf.Acos(Mathf.Clamp01(Vector3.Dot(movementDirection, defaultDirection)));
        if (angle < 0.001f) _returningHeading = false;
    }

    void TickHeadingReturn(float dt, ref Vector3 movementDirection, Vector3 defaultDirection)
    {
        float angle = Mathf.Acos(Mathf.Clamp01(Vector3.Dot(movementDirection, defaultDirection)));
        float step = angle < 0.001f ? 1f : Mathf.Clamp01((_returnAngularSpeed * dt) / angle);

        movementDirection = Vector3.Slerp(movementDirection, defaultDirection, step);

        if (Vector3.Dot(movementDirection, defaultDirection) >= 0.9995f)
        {
            movementDirection = defaultDirection;
            _returningHeading = false;
        }
    }
}
