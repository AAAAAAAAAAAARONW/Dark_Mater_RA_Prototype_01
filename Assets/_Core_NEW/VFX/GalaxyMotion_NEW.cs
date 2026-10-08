using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes a particle galaxy (Galaxy2 and its copy in the Milky Way) move like one: faster, and
/// turning as a whole.
///
/// WHAT WAS WRONG WITH ITS MOTION. As built, almost nothing in it turns. Its disc layers orbit
/// at 0.02 to 0.1 radians a second — a turn every one to five minutes — so on screen the disc
/// sits still while its particles drift outward and wobble in noise; one layer of big stars
/// orbits the other way from the rest; its arms are streams flowing out of the middle; and
/// the core is particles born and dying every second, which pop. A galaxy reads by turning.
///
/// WHAT THIS DOES, all of it at runtime and put back when it is disabled — nothing in the
/// particle systems or the prefab is changed:
///
///   pace     Every system runs faster (its simulation speed, multiplied). Every flow keeps its
///            shape — the arms, the drift, the swirl — only quicker.
///   turn     The whole galaxy turns about its disc's axis as one body, the way its disc already
///            turns, so it visibly rotates. Its world-space systems (the arms) leave their
///            particles where they were born, so those are carried round with it each frame —
///            the arms turn with the disc instead of smearing. Which way the disc turns is
///            measured from its own particles once they move (see Measure), so no convention
///            of the particle system's orbital velocity is assumed.
///   wobble   The noise that jitters the particles is turned down, so they glide.
///   one way  A layer that orbits against the rest is turned to go with it.
///   soften   Short-lived particles with no fade of their own fade in as they are born and
///            out as they die, instead of popping — the core's, born and dying every second,
///            made it flicker. Long-lived layers are left as they are.
///
/// The axis and the middle are those of its orbiting disc layers (their orbital axis and
/// where they sit), kept in this object's own space, so a dive that moves, scales or turns
/// the world (LayerDive_NEW) takes them along. While every renderer of it is hidden it does
/// nothing.
///
/// Put it on the galaxy's root. Tools > Journey NEW > Add Galaxy Motion to Open Scene does,
/// for every galaxy in the render groups.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("GALAXY", "#B98CF2")]
public class GalaxyMotion_NEW : MonoBehaviour
{
    public enum TurnWay
    {
        /// <summary>The way its disc's own particles go, measured once they are moving.</summary>
        Auto,
        /// <summary>Clockwise seen from the side its axis points to.</summary>
        Clockwise,
        /// <summary>Anticlockwise seen from the side its axis points to.</summary>
        Anticlockwise
    }

    [Header("Pace")]
    [Tooltip("How much faster everything in it runs: each particle system's simulation speed, " +
             "multiplied. Every flow keeps its shape, only quicker. 1 = as built.")]
    [Range(0.25f, 4f)] [SerializeField] float pace = 1.6f;

    [Header("Turn")]
    [Tooltip("Degrees a second the whole galaxy turns about its disc's axis, as one body. Its " +
             "world-space arms are carried round with it. 0 = none.")]
    [Range(0f, 30f)] [SerializeField] float turn = 4f;

    [Tooltip("Which way it turns. Auto: the way its disc's own particles go.")]
    [SerializeField] TurnWay way = TurnWay.Auto;

    [Tooltip("Seconds it takes to come up to speed, once it knows which way to go.")]
    [Min(0f)] [SerializeField] float spinUp = 2f;

    [Header("Calm")]
    [Tooltip("The noise that wobbles its particles, as a share of what was built. Lower glides; " +
             "1 = as built.")]
    [Range(0f, 1f)] [SerializeField] float wobble = 0.6f;

    [Tooltip("A layer whose particles orbit against the rest is turned to go with them.")]
    [SerializeField] bool oneWay = true;

    [Tooltip("Short-lived particles with no fade of their own fade in as they are born and out as " +
             "they die, over this share of their lives at each end, instead of popping. 0 = off.")]
    [Range(0f, 0.4f)] [SerializeField] float soften = 0.15f;

    [Tooltip("What counts as short-lived for soften, in seconds of a particle's life.")]
    [Min(0f)] [SerializeField] float softenUnder = 5f;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    /// <summary>One system as built, to put back.</summary>
    struct Built
    {
        public ParticleSystem system;
        public float simulationSpeed;
        public Vector3 noise;           // strength multipliers, x y z
        public Vector3 orbital;         // orbital velocity multipliers, x y z
        public bool colourOverLife;
        public ParticleSystem.MinMaxGradient colour;
    }

    readonly List<Built> _built = new List<Built>();
    readonly List<ParticleSystem> _worldSpace = new List<ParticleSystem>();
    readonly List<ParticleSystem> _orbiting = new List<ParticleSystem>();
    readonly List<Renderer> _renderers = new List<Renderer>();
    bool _applied;

    // The disc's axis and middle, in this object's space.
    Vector3 _axis = Vector3.up;
    Vector3 _middle;

    // Which way it turns (+1: Quaternion.AngleAxis's positive way about the axis), 0 while
    // not known yet; and how far up to speed.
    float _sign;
    float _speed;

    // Measuring which way the disc's particles go: where each was, by its seed, and when.
    readonly Dictionary<uint, Vector3> _was = new Dictionary<uint, Vector3>();
    float _measureFrom = -1f;
    int _measureTries;
    const float MeasureSeconds = 0.6f;
    const int MeasureAttempts = 5;

    static ParticleSystem.Particle[] _buffer = new ParticleSystem.Particle[256];

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void OnEnable()
    {
        Gather();
        Apply();
        _sign = way == TurnWay.Auto ? 0f : (way == TurnWay.Clockwise ? 1f : -1f);
        _speed = 0f;
        _measureFrom = -1f;
        _measureTries = 0;
    }

    void OnDisable() => Restore();

    void OnValidate()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !_applied) return;
        Restore();
        Apply();
        if (way != TurnWay.Auto) _sign = way == TurnWay.Clockwise ? 1f : -1f;
    }

    void LateUpdate()
    {
        if (!AnyVisible()) return;

        if (_sign == 0f)
        {
            Measure();
            return;
        }

        if (turn <= 0f) return;

        _speed = spinUp > 0f ? Mathf.MoveTowards(_speed, 1f, Time.deltaTime / spinUp) : 1f;
        float degrees = _sign * turn * Smooth(_speed) * Time.deltaTime;
        if (Mathf.Abs(degrees) < 1e-6f) return;

        Vector3 axis = transform.TransformDirection(_axis).normalized;
        Vector3 middle = transform.TransformPoint(_middle);
        transform.RotateAround(middle, axis, degrees);
        CarryWorldParticles(Quaternion.AngleAxis(degrees, axis), middle);
    }

    // ── Setting up ───────────────────────────────────────────────────────────

    /// <summary>
    /// Its systems, which of them simulate in world space (to be carried), which orbit in their
    /// own space (the disc layers: they give the axis and the middle), and its renderers.
    /// </summary>
    void Gather()
    {
        _worldSpace.Clear();
        _orbiting.Clear();
        _renderers.Clear();
        GetComponentsInChildren(true, _renderers);

        Vector3 axis = Vector3.zero, middle = Vector3.zero;
        float weight = 0f;
        Vector3 first = Vector3.zero;

        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.main.simulationSpace == ParticleSystemSimulationSpace.World)
            {
                _worldSpace.Add(ps);
                continue;
            }

            Vector3 w = OrbitalAxisWorld(ps);
            if (w.sqrMagnitude < 1e-10f) continue;

            _orbiting.Add(ps);
            Vector3 dir = w.normalized;
            if (first == Vector3.zero) first = dir;
            axis += Vector3.Dot(dir, first) >= 0f ? dir : -dir;
            middle += ps.transform.position;
            weight += 1f;
        }

        if (weight > 0f && axis.sqrMagnitude > 1e-8f)
        {
            // Up, in its own space, so 'clockwise' means the same for every galaxy.
            if (Vector3.Dot(axis, transform.up) < 0f) axis = -axis;
            _axis = transform.InverseTransformDirection(axis.normalized);
            _middle = transform.InverseTransformPoint(middle / weight);
        }
        else
        {
            _axis = Vector3.up;
            _middle = Vector3.zero;
        }

        if (debugLog)
            Debug.Log($"[GalaxyMotion_NEW] '{name}': {_orbiting.Count} orbiting layers, {_worldSpace.Count} " +
                      $"world-space systems; turns about {transform.TransformDirection(_axis)} through " +
                      $"{transform.TransformPoint(_middle)}.", this);
    }

    /// <summary>A system's orbital velocity as built, as a direction and rate in the world (zero if it does not orbit).</summary>
    static Vector3 OrbitalAxisWorld(ParticleSystem ps)
    {
        ParticleSystem.VelocityOverLifetimeModule v = ps.velocityOverLifetime;
        if (!v.enabled) return Vector3.zero;
        Vector3 local = new Vector3(v.orbitalXMultiplier, v.orbitalYMultiplier, v.orbitalZMultiplier);
        return local.sqrMagnitude < 1e-10f ? Vector3.zero : ps.transform.TransformDirection(local);
    }

    void Apply()
    {
        if (_applied) return;
        _applied = true;
        _built.Clear();

        Vector3 axis = transform.TransformDirection(_axis).normalized;
        float majority = 0f;
        if (oneWay)
            foreach (ParticleSystem ps in _orbiting)
                majority += Vector3.Dot(OrbitalAxisWorld(ps), axis);

        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            ParticleSystem.NoiseModule noise = ps.noise;
            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            ParticleSystem.ColorOverLifetimeModule colour = ps.colorOverLifetime;

            var b = new Built
            {
                system = ps,
                simulationSpeed = main.simulationSpeed,
                noise = new Vector3(noise.strengthXMultiplier, noise.strengthYMultiplier, noise.strengthZMultiplier),
                orbital = new Vector3(velocity.orbitalXMultiplier, velocity.orbitalYMultiplier, velocity.orbitalZMultiplier),
                colourOverLife = colour.enabled,
                colour = colour.color
            };
            _built.Add(b);

            main.simulationSpeed = b.simulationSpeed * pace;

            if (noise.enabled)
            {
                noise.strengthXMultiplier = b.noise.x * wobble;
                noise.strengthYMultiplier = b.noise.y * wobble;
                noise.strengthZMultiplier = b.noise.z * wobble;
            }

            // A disc layer orbiting against the rest goes with it.
            if (oneWay && Mathf.Abs(majority) > 1e-6f && _orbiting.Contains(ps)
                && Vector3.Dot(OrbitalAxisWorld(ps), axis) * majority < 0f)
            {
                velocity.orbitalXMultiplier = -b.orbital.x;
                velocity.orbitalYMultiplier = -b.orbital.y;
                velocity.orbitalZMultiplier = -b.orbital.z;
                if (debugLog) Debug.Log($"[GalaxyMotion_NEW] '{ps.name}' orbited against the rest; turned to go with it.", ps);
            }

            if (soften > 0f && !b.colourOverLife && main.startLifetimeMultiplier <= softenUnder)
            {
                colour.enabled = true;
                colour.color = new ParticleSystem.MinMaxGradient(FadeInOut(soften));
            }
        }
    }

    void Restore()
    {
        if (!_applied) return;
        _applied = false;

        foreach (Built b in _built)
        {
            if (b.system == null) continue;

            ParticleSystem.MainModule main = b.system.main;
            main.simulationSpeed = b.simulationSpeed;

            ParticleSystem.NoiseModule noise = b.system.noise;
            noise.strengthXMultiplier = b.noise.x;
            noise.strengthYMultiplier = b.noise.y;
            noise.strengthZMultiplier = b.noise.z;

            ParticleSystem.VelocityOverLifetimeModule velocity = b.system.velocityOverLifetime;
            velocity.orbitalXMultiplier = b.orbital.x;
            velocity.orbitalYMultiplier = b.orbital.y;
            velocity.orbitalZMultiplier = b.orbital.z;

            ParticleSystem.ColorOverLifetimeModule colour = b.system.colorOverLifetime;
            colour.color = b.colour;
            colour.enabled = b.colourOverLife;
        }
        _built.Clear();
    }

    /// <summary>White all the way, its alpha up from 0 over the first <paramref name="edge"/> of a life and down over the last.</summary>
    static Gradient FadeInOut(float edge)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, edge),
                new GradientAlphaKey(1f, 1f - edge), new GradientAlphaKey(0f, 1f)
            });
        return g;
    }

    // ── Which way ────────────────────────────────────────────────────────────

    /// <summary>
    /// Which way the disc turns, from its particles: where each one of the orbiting layers is,
    /// and MeasureSeconds later where it has got to, round the axis. Summed over all of them,
    /// the drift outward and the noise cancel and the turn is left. Measured in each system's
    /// own space, round the axis as seen there, so it holds whatever way the orbital velocity
    /// is reckoned; Quaternion.AngleAxis turns the same way as the cross product of where a
    /// particle was and where it is. Gives up after a few tries and goes clockwise.
    /// </summary>
    void Measure()
    {
        if (_orbiting.Count == 0 || way != TurnWay.Auto)
        {
            _sign = way == TurnWay.Anticlockwise ? -1f : 1f;
            return;
        }

        if (_measureFrom < 0f)
        {
            Sample(_was);
            _measureFrom = Time.time;
            return;
        }

        if (Time.time - _measureFrom < MeasureSeconds) return;

        Vector3 axisWorld = transform.TransformDirection(_axis).normalized;
        double sum = 0.0;
        foreach (ParticleSystem ps in _orbiting)
        {
            if (ps == null) continue;
            Vector3 axisLocal = ps.transform.InverseTransformDirection(axisWorld);
            Vector3 centre = OrbitCentre(ps);

            int count = Read(ps);
            for (int i = 0; i < count; i++)
            {
                if (!_was.TryGetValue(_buffer[i].randomSeed, out Vector3 before)) continue;
                Vector3 now = _buffer[i].position;
                sum += Vector3.Dot(Vector3.Cross(before - centre, now - centre), axisLocal);
            }
        }

        _was.Clear();
        _measureFrom = -1f;

        if (System.Math.Abs(sum) > 1e-6)
        {
            _sign = sum > 0.0 ? 1f : -1f;
            if (debugLog)
                Debug.Log($"[GalaxyMotion_NEW] '{name}' turns {(_sign > 0f ? "clockwise" : "anticlockwise")} " +
                          "seen from the side its axis points to.", this);
        }
        else if (++_measureTries >= MeasureAttempts)
        {
            _sign = 1f;
            if (debugLog) Debug.Log($"[GalaxyMotion_NEW] '{name}': could not tell which way its disc turns; clockwise.", this);
        }
    }

    /// <summary>Every orbiting layer's particles, by seed, in their own space.</summary>
    void Sample(Dictionary<uint, Vector3> into)
    {
        into.Clear();
        foreach (ParticleSystem ps in _orbiting)
        {
            if (ps == null) continue;
            int count = Read(ps);
            for (int i = 0; i < count; i++) into[_buffer[i].randomSeed] = _buffer[i].position;
        }
    }

    /// <summary>Where a system's particles orbit round, in its own space.</summary>
    static Vector3 OrbitCentre(ParticleSystem ps)
    {
        ParticleSystem.VelocityOverLifetimeModule v = ps.velocityOverLifetime;
        return new Vector3(v.orbitalOffsetXMultiplier, v.orbitalOffsetYMultiplier, v.orbitalOffsetZMultiplier);
    }

    // ── Carrying ─────────────────────────────────────────────────────────────

    /// <summary>
    /// World-space systems leave their particles where they were born, whatever the galaxy
    /// does: turned round <paramref name="middle"/> by <paramref name="turn"/> with it, they
    /// keep the shape they had.
    /// </summary>
    void CarryWorldParticles(Quaternion turn, Vector3 middle)
    {
        foreach (ParticleSystem ps in _worldSpace)
        {
            if (ps == null) continue;
            int count = Read(ps);
            if (count == 0) continue;

            for (int i = 0; i < count; i++)
            {
                _buffer[i].position = middle + turn * (_buffer[i].position - middle);
                _buffer[i].velocity = turn * _buffer[i].velocity;
            }
            ps.SetParticles(_buffer, count);
        }
    }

    static int Read(ParticleSystem ps)
    {
        int alive = ps.particleCount;
        if (_buffer.Length < alive) _buffer = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(alive)];
        return ps.GetParticles(_buffer);
    }

    bool AnyVisible()
    {
        for (int i = 0; i < _renderers.Count; i++)
            if (_renderers[i] != null && _renderers[i].enabled && _renderers[i].gameObject.activeInHierarchy)
                return true;
        return false;
    }

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
