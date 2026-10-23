// The shape of the first galaxy the light meets (RingGalaxyVFX_NEW), shared by its sheet, its
// glow and its stars so the three agree: a collisional ring galaxy, "peculiar" in Hubble's
// scheme — not a spiral.
//
// A smaller galaxy fell straight through this one's disc a few hundred million years ago. Its
// pull sent a ripple outward through the gas like a stone dropped in a pond, and where the
// ripple crowds the gas together stars are forming in a burst: a great ring of blue star
// clusters round a dim, hazy middle where the old nucleus is barely seen. The intruder is still
// there, touching the ring's edge: a long bright streak at right angles to the ring, a dark lane
// of dust along its middle, its tail drawn out away from the ring. The ring opens out thin and
// faint on the intruder's side and is thick and bright on the far side, so ring and streak make
// the "question mark" Mayall saw in 1940. Arp 148 is the picture.
//
// It moves: the ring and what is in it go round (_Turn); every few seconds a ripple spreads out
// from the nucleus, faint in the empty middle, lighting the ring and its knots as it passes —
// the wave that made the ring, shown again; the knots light up, live a while going round with
// the ring, and fade as others light up elsewhere; the stars twinkle.
//
// Object space: the plane is XZ, radius 1 is the object's scale, the ring's own centre is the
// origin. Plane coordinates are float2(x, z).

#ifndef RING_GALAXY_NEW_INCLUDED
#define RING_GALAXY_NEW_INCLUDED

#define RG_TAU 6.2831853
#define RG_KNOTS 18

float _Seed;
float _Turn;      // how fast the ring and what is in it go round, radians a second
float4 _Ring;     // x radius, y width, z lopsidedness (0 to 1), w egg-shape
float4 _Comp;     // the intruder: xyz where it is (of the radius), w its size (long half axis)
float _CompTilt;  // how far the intruder's disc is turned about its long axis, radians
float4 _Ripple;   // x seconds for a ripple to spread out, y its strength, z twinkle, w a knot's life in seconds

float RGSq(float x) { return x * x; }
float RGHash(float n) { return frac(sin(n * 12.9898 + _Seed * 78.233) * 43758.5453); }

// The old nucleus, off the ring's centre: the ripple spread from where the intruder hit.
float2 RGNucleus() { return float2(-0.15, 0.07); }

// Which way the intruder is, in the plane.
float RGIntruderAngle() { return atan2(_Comp.z, _Comp.x); }

// The ring's bright side: away from the intruder. On the intruder's side it opens out thin and faint.
float RGBrightAngle() { return atan2(-_Comp.z, -_Comp.x); }

// Shortest angle between two angles, radians.
float RGAngleTo(float a, float b) { float d = a - b; return d - RG_TAU * floor(d / RG_TAU + 0.5); }

// The ring's wobble at angle a, a fraction of its radius: slow bumps, so it is not a hoop.
float RGWobble(float a)
{
    return 0.035 * sin(3.0 * a + _Seed * 1.7) + 0.022 * sin(5.0 * a + 2.1 + _Seed * 0.7) + 0.012 * sin(9.0 * a + 4.3);
}

// The ring's radius at angle a: egg-shaped, the long side towards the intruder, and bumpy.
float RGRingR(float a) { return _Ring.x * (1.0 + _Ring.w * cos(a - RGBrightAngle())) * (1.0 + RGWobble(a)); }

// How bright the ring is at angle a: lopsided, bright away from the intruder, never quite dark.
float RGBright(float a)
{
    float c = 0.5 + 0.5 * cos(a - RGBrightAngle());
    return lerp(1.0, 0.1 + 1.45 * c * c, _Ring.z);
}

// The ring's half width at angle a: wider where it is brighter.
float RGWidth(float a) { return _Ring.y * (0.35 + 0.65 * RGBright(a)); }

// The ripples at plane point p: two always going, half a period apart, each a band of light
// spreading from the nucleus out past the ring, widening and fading as it goes. 0 between them.
float RGRipple(float2 p, float T)
{
    float d = length(p - RGNucleus());
    float sum = 0.0;
    [unroll]
    for (int w = 0; w < 2; w++)
    {
        float ph = frac(T / max(_Ripple.x, 0.5) + w * 0.5 + _Seed * 0.137);
        float width = 0.03 + 0.08 * ph;
        sum += exp(-RGSq((d - ph * 1.45) / width)) * (1.0 - ph) * smoothstep(0.0, 0.18, ph);
    }
    return sum * _Ripple.y;
}

// Knot i: a giant region of star formation on the ring — where, how big, how bright. Scattered
// unevenly, crowded towards the bright side, some in big complexes. Each lives a while (about
// _Ripple.w seconds) going round with the ring, then fades, and the next lights up somewhere
// else: so however long the game has run they stay crowded on the bright side. A ripple
// passing makes them flare.
void RGKnot(int i, float T, out float2 p, out float size, out float bright)
{
    float life = max(_Ripple.w, 5.0) * (0.7 + 0.6 * RGHash(i * 2.39 + 0.7));
    float age = T / life + RGHash(i * 8.17 + 3.3);
    float n = fmod(floor(age), 61.0);
    age = frac(age);
    float id = i + n * 3.71;
    float h0 = RGHash(id * 7.31 + 1.7), h1 = RGHash(id * 3.17 + 5.3);
    float h2 = RGHash(id * 5.71 + 9.1), h3 = RGHash(id * 1.93 + 2.9);
    // At random round the ring, not spaced out: they bunch and leave gaps, as real ones do, and
    // crowd towards the bright side.
    float u = h0 * 2.0 - 1.0;
    float a0 = RGBrightAngle() + 3.14159 * sign(u) * pow(abs(u), 1.8);
    float a = a0 + _Turn * age * life;
    float r = RGRingR(a) + (h1 - 0.5) * RGWidth(a) * 1.4;
    p = float2(cos(a), sin(a)) * r;
    float b = RGBright(a);
    float lit = smoothstep(0.0, 0.2, age) * smoothstep(1.0, 0.75, age);
    float shimmer = 0.85 + 0.15 * sin(T * (0.6 + 0.8 * h1) + h0 * 40.0);
    bright = b * (0.15 + 1.0 * h2 * h2 * h2) * lit * shimmer * (1.0 + 1.5 * RGRipple(p, T));
    size = (0.015 + 0.045 * h3 * h3 * h3) * (0.6 + 0.4 * b);
}

// A star's twinkle, from its seeds: 1 steady, dipping by up to _Ripple.z.
float RGTwinkle(float s0, float s1, float T)
{
    return 1.0 - _Ripple.z * 0.45 * (0.5 + 0.5 * sin(T * (1.3 + 3.5 * s1) + s0 * 61.0));
}

// The intruder's frame: L its long axis, pointing at the ring's middle, W across its disc, N
// through it (its disc is the L-W plane). _CompTilt turns the disc about L; set to the opposite
// of the galaxy's own tilt it is edge on to a light coming in level, a streak with a dust lane.
void RGCompanionFrame(out float3 L, out float3 W, out float3 N)
{
    L = normalize(float3(-_Comp.x, 0.0, -_Comp.z));
    float3 w0 = normalize(cross(float3(0.0, 1.0, 0.0), L));
    float3 n0 = cross(L, w0);
    N = n0 * cos(_CompTilt) + w0 * sin(_CompTilt);
    W = cross(N, L);
}

// The bridge of gas from the ring back to the intruder, s 0 at the ring to 1 at it: short, as
// they touch, the intruder's streak running on into the ring.
float3 RGBridge(float s)
{
    float3 L, W, N;
    RGCompanionFrame(L, W, N);
    float a = RGIntruderAngle();
    float3 from = float3(cos(a), 0.0, sin(a)) * RGRingR(a) * 1.02;
    float3 to = _Comp.xyz + L * _Comp.w * 0.5;
    float bow = sin(s * 3.14159);
    return lerp(from, to, s) + float3(0.0, 0.03, 0.0) * bow + W * 0.04 * bow;
}

// The intruder's tail, s 0 at it to 1 at the end: drawn out of its far side, straight on along
// its own line away from the ring, bending a little at the end.
float3 RGTail(float s)
{
    float3 L, W, N;
    RGCompanionFrame(L, W, N);
    float3 from = _Comp.xyz - L * _Comp.w * 0.6;
    return from - L * _Comp.w * 1.5 * s + W * _Comp.w * 0.35 * s * s;
}

#endif
