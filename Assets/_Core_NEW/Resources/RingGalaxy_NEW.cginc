// The shape of the first galaxy the light meets (RingGalaxyVFX_NEW), shared by its sheet, its
// glow and its stars so the three agree: a collisional ring galaxy, "peculiar" in Hubble's
// scheme — not a spiral.
//
// A smaller galaxy fell straight through this one's disc a few hundred million years ago. Its
// pull sent a ripple outward through the gas like a stone dropped in a pond, and where the
// ripple crowds the gas together stars are forming in a burst: a great blue ring, knotted with
// pink regions of ionised hydrogen, round an emptied middle in which the old yellow nucleus sits
// off centre, joined to the ring by faint spokes. The intruder is still near: a warm streak aimed
// back at the ring, a tidal tail curling off its far side and a thin bridge of gas to the ring,
// which is brightest and widest on its side. (The Cartwheel Galaxy and Arp 148 are the pictures.)
//
// Object space: the plane is XZ, radius 1 is the object's scale, the ring's own centre is the
// origin. Plane coordinates are float2(x, z).

#ifndef RING_GALAXY_NEW_INCLUDED
#define RING_GALAXY_NEW_INCLUDED

#define RG_TAU 6.2831853
#define RG_KNOTS 18

float _Seed;
float _Turn;    // how fast the ring and what is in it go round, radians a second
float4 _Ring;   // x radius, y width, z lopsidedness (0 to 1), w egg-shape
float4 _Comp;   // the intruder: xyz where it is (of the radius), w its size (long half axis)

float RGSq(float x) { return x * x; }
float RGHash(float n) { return frac(sin(n * 12.9898 + _Seed * 78.233) * 43758.5453); }

// The old nucleus, off the ring's centre: the ripple spread from where the intruder hit.
float2 RGNucleus() { return float2(-0.15, 0.07); }

// Which way the intruder is, in the plane: the ring is brightest and widest on that side.
float RGBrightAngle() { return atan2(_Comp.z, _Comp.x); }

// Shortest angle between two angles, radians.
float RGAngleTo(float a, float b) { float d = a - b; return d - RG_TAU * floor(d / RG_TAU + 0.5); }

// The ring's wobble at angle a, a fraction of its radius: slow bumps, so it is not a hoop.
float RGWobble(float a)
{
    return 0.035 * sin(3.0 * a + _Seed * 1.7) + 0.022 * sin(5.0 * a + 2.1 + _Seed * 0.7) + 0.012 * sin(9.0 * a + 4.3);
}

// The ring's radius at angle a: egg-shaped, the long side towards the intruder, and bumpy.
float RGRingR(float a) { return _Ring.x * (1.0 + _Ring.w * cos(a - RGBrightAngle())) * (1.0 + RGWobble(a)); }

// How bright the ring is at angle a: lopsided towards the intruder, never quite dark opposite.
float RGBright(float a)
{
    float c = 0.5 + 0.5 * cos(a - RGBrightAngle());
    return lerp(1.0, 0.1 + 1.45 * c * c, _Ring.z);
}

// The ring's half width at angle a: wider where it is brighter.
float RGWidth(float a) { return _Ring.y * (0.35 + 0.65 * RGBright(a)); }

// Knot i: a giant region of star formation on the ring — where, how big, how bright. Scattered
// unevenly, crowded on the intruder's side, some in big complexes; they go round with the ring.
void RGKnot(int i, float T, out float2 p, out float size, out float bright)
{
    float h0 = RGHash(i * 7.31 + 1.7), h1 = RGHash(i * 3.17 + 5.3);
    float h2 = RGHash(i * 5.71 + 9.1), h3 = RGHash(i * 1.93 + 2.9);
    // At random round the ring, not spaced out: they bunch and leave gaps, as real ones do, and
    // crowd towards the bright side.
    float u = h0 * 2.0 - 1.0;
    float a0 = RGBrightAngle() + 3.14159 * sign(u) * pow(abs(u), 1.8);
    float a = a0 + _Turn * T;
    float r = RGRingR(a) + (h1 - 0.5) * RGWidth(a) * 1.4;
    p = float2(cos(a), sin(a)) * r;
    float b = RGBright(a);
    bright = b * (0.15 + 1.0 * h2 * h2 * h2);
    size = (0.015 + 0.045 * h3 * h3 * h3) * (0.6 + 0.4 * b);
}

// The intruder's frame: L its long axis, pointing back at the ring it came through, W across
// its disc, N through it (its disc is the L-W plane, nearly level). From the light it is a
// streak aimed at the ring, as the intruder of Arp 148 is.
void RGCompanionFrame(out float3 L, out float3 W, out float3 N)
{
    L = normalize(float3(-_Comp.x, 0.12, -_Comp.z));
    W = normalize(cross(float3(0.0, 1.0, 0.0), L));
    N = cross(L, W);
}

// The bridge of gas from the ring back to the intruder, s 0 at the ring to 1 at it: nearly
// straight, the intruder's own streak drawn out towards the ring, sagging a little.
float3 RGBridge(float s)
{
    float3 L, W, N;
    RGCompanionFrame(L, W, N);
    float a = RGBrightAngle();
    float3 from = float3(cos(a), 0.0, sin(a)) * RGRingR(a) * 1.02;
    float3 to = _Comp.xyz + L * _Comp.w * 0.5;
    float bow = sin(s * 3.14159);
    return lerp(from, to, s) + float3(0.0, 0.03, 0.0) * bow + W * 0.04 * bow;
}

// The intruder's tidal tail, s 0 at it to 1 at the end: drawn out of its far side, away from
// the ring, curling as it goes.
float3 RGTail(float s)
{
    float3 L, W, N;
    RGCompanionFrame(L, W, N);
    float3 from = _Comp.xyz - L * _Comp.w * 0.5;
    return from - L * _Comp.w * 2.2 * s + W * _Comp.w * 1.1 * s * s + N * _Comp.w * 0.15 * s;
}

#endif
