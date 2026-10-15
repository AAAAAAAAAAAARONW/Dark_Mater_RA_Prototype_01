// Shared by the quasar's host galaxy's shaders (HostGalaxyVFX_NEW): where its arms, its bar and
// its ring are, so the gas sheet, the glow of its stars and every star agree.
//
// Not the Milky Way. A quasar's galaxy, early in the universe: two open arms, lopsided — one
// runs further and brighter than the other, and goes on as a faint tidal plume, the mark of a
// neighbour's pull — out of the ends of a short, fat bar; and round the quasar, inside the bar,
// a ring of star formation, gas falling in along the bar's dust lanes and piling up there
// (NGC 1097 has one round its active nucleus). All of it turns: _PatternTurn is how far the
// pattern has gone round, in radians, the same way as the quasar's disc (atan2(z, x) rising).
//
// Object space: the disc in XZ, radius 1 at the edge of its light.

#ifndef HOST_GALAXY_NEW_INCLUDED
#define HOST_GALAXY_NEW_INCLUDED

#define HG_TAU 6.2831853
#define HG_PI 3.1415927

float _Pitch;          // arm pitch, degrees
float4 _Bar;           // x: angle (deg), y: half length (where the arms start), z: half width
float _RingRadius;     // the starburst ring, of the radius
float _Lopsided;       // 0 two even arms; 1 one runs on far further, the other dies early
float _PatternSpeed;   // how fast the pattern turns, radians a second

float HGSq(float x) { return x * x; }
float HGWrap(float a) { return a - HG_TAU * floor(a / HG_TAU + 0.5); }

// The bar's angle now, radians: it and the arms out of its ends turn together.
float HGPattern() { return radians(_Bar.x) + _Time.y * _PatternSpeed; }

// Arm k (0 or 1) at radius r: a trailing log spiral out of an end of the bar, as an angle.
float HGArmAngle(float r, float k)
{
    float tanP = tan(radians(_Pitch));
    return HGPattern() + k * HG_PI - log(max(r, 1e-4) / _Bar.y) / tanP;
}

// How far across arm k a point at (r, a) is, in radii; `shift` moves the line outward in
// log radius (the dust lane, on the inner edge, is a shift of -0.08).
float HGArmAcross(float r, float a, float k, float shift)
{
    float tanP = tan(radians(_Pitch));
    float base = a - (HGPattern() + k * HG_PI) + (log(max(r, 1e-4) / _Bar.y) + shift) / tanP;
    return HGWrap(base) * r * sin(radians(_Pitch));
}

// Where arm k runs: from the bar's end out — the first on past the edge, the second dying
// early, as far apart as _Lopsided says. And how bright it is.
float HGArmReach(float r, float k)
{
    float end = k < 0.5 ? 1.0 + 0.12 * _Lopsided : 0.95 - 0.25 * _Lopsided;
    float start = smoothstep(_Bar.y * 0.75, _Bar.y * 1.15, r);
    return start * smoothstep(end, end - 0.3, r) * (k < 0.5 ? 1.0 : 1.0 - 0.3 * _Lopsided);
}

// The tidal plume: the first arm going on past the edge, faint and wide.
float HGPlume(float r, float a, float width)
{
    float across = HGArmAcross(r, a, 0.0, 0.0);
    return exp(-HGSq(across / (width * 2.6))) * smoothstep(0.8, 1.0, r) * smoothstep(1.45, 1.05, r) * _Lopsided;
}

// The bar, in its own frame: x along it.
float2 HGBarFrame(float2 p)
{
    float b = HGPattern();
    float c = cos(b), s = sin(b);
    return float2(p.x * c + p.y * s, -p.x * s + p.y * c);
}

#endif
