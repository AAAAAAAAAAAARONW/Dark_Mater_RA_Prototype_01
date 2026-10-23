// The shape of the first galaxy the light meets (RingGalaxyVFX_NEW), shared by its sheet, its
// glow and its stars so the three agree: a collisional ring galaxy, "peculiar" in Hubble's
// scheme — not a spiral.
//
// A smaller galaxy fell straight through this one's disc a few hundred million years ago. Its
// pull sent a ripple outward through the gas like a stone dropped in a pond, and where the
// ripple crowds the gas together stars are forming in a burst: a great blue ring, knotted with
// pink regions of ionised hydrogen, round an emptied middle in which the old yellow nucleus sits
// off centre, joined to the ring by faint spokes. The intruder is still near, an edge-on streak
// off to one side with a thin bridge of gas trailing back to the ring, which is brightest and
// widest on its side. (The Cartwheel Galaxy and Arp 148 are the pictures.)
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
    return lerp(1.0, 0.3 + 1.15 * c * c, _Ring.z);
}

// The ring's half width at angle a: wider where it is brighter.
float RGWidth(float a) { return _Ring.y * (0.55 + 0.45 * RGBright(a)); }

// Knot i: a giant region of star formation on the ring — where, how big, how bright. Scattered
// unevenly, crowded on the intruder's side, some in big complexes; they go round with the ring.
void RGKnot(int i, float T, out float2 p, out float size, out float bright)
{
    float h0 = RGHash(i * 7.31 + 1.7), h1 = RGHash(i * 3.17 + 5.3);
    float h2 = RGHash(i * 5.71 + 9.1), h3 = RGHash(i * 1.93 + 2.9);
    // Half of them pulled towards the bright side: uneven spacing, crowded where the ring is thick.
    float u = (i + h0) / RG_KNOTS * 2.0 - 1.0;
    float pull = frac(i * 0.5) < 0.25 ? 0.55 : 0.0;
    float a0 = RGBrightAngle() + 3.14159 * u * (1.0 - pull * (1.0 - abs(u)));
    float a = a0 + _Turn * T;
    float r = RGRingR(a) + (h1 - 0.5) * RGWidth(a) * 1.2;
    p = float2(cos(a), sin(a)) * r;
    float b = RGBright(a);
    bright = b * (0.25 + 1.0 * h2 * h2);
    size = (0.018 + 0.05 * h3 * h3 * h3) * (0.6 + 0.4 * b);
}

// The intruder's frame: L its long axis, W across its disc, N through it (its disc is the L-W
// plane). Tipped up out of the big galaxy's plane, so from the light it is a streak.
void RGCompanionFrame(out float3 L, out float3 W, out float3 N)
{
    L = normalize(float3(-0.25, 0.35, 1.0));
    W = normalize(cross(float3(0.0, 1.0, 0.0), L));
    N = cross(L, W);
}

// The bridge of gas trailing from the ring back to the intruder, s 0 at the ring to 1 at it:
// arcing up out of the plane and bowing to one side.
float3 RGBridge(float s)
{
    float a = RGBrightAngle();
    float3 from = float3(cos(a), 0.0, sin(a)) * RGRingR(a) * 1.04;
    float3 to = _Comp.xyz;
    float3 along = to - from;
    float3 side = normalize(cross(float3(0.0, 1.0, 0.0), along));
    float bow = sin(s * 3.14159);
    return lerp(from, to, s) + float3(0.0, 0.08, 0.0) * bow - side * 0.14 * bow;
}

#endif
