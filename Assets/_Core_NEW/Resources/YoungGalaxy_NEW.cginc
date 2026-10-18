// Shared by the young galaxy's shaders (YoungGalaxyVFX_NEW): where its giant clumps and its
// cold streams are, so the gas sheet, the glow of its depth and every star agree.
//
// Not the Milky Way, not the quasar's host: a galaxy at cosmic noon, ten billion years ago,
// when the universe made stars faster than ever before or since. No arms. A thick, turbulent
// disc of gas broken into a dozen giant clumps of star formation — each one a knot of
// blue-white stars in a magenta glow of ionised hydrogen, some of them a tenth of the galaxy
// across — round a small, dusty, orange core; cold gas streaming in along narrow filaments
// from the web outside, feeding it; and a wind of hot gas blown out above and below the disc
// by all that star formation. The clumps go round on their orbits, the gas streams in.
//
// Object space: the disc in XZ, radius 1 at the edge of its light.

#ifndef YOUNG_GALAXY_NEW_INCLUDED
#define YOUNG_GALAXY_NEW_INCLUDED

#define YG_TAU 6.2831853
#define YG_CLUMPS 12

float _Seed;          // which galaxy: where its clumps and streams fall
float _ClumpTurn;     // how fast the clumps go round, radians a second at radius 0.5
float4 _Thickness;    // x: the disc's scale height at the centre, y: its growth outward
float _Streams;       // how many cold streams, 0 to 3

float YGSq(float x) { return x * x; }
float YGHash(float n) { return frac(sin(n) * 43758.5453); }
float YGWrap(float a) { return a - YG_TAU * floor(a / YG_TAU + 0.5); }

// The disc's thickness at radius r.
float YGHeight(float r) { return _Thickness.x + _Thickness.y * r; }

// Clump i: its centre (in the disc, as thick as the disc is), its radius and its brightness.
// A few are huge, most middling. On a flat rotation curve, like everything else.
void YGClump(int i, out float3 centre, out float radius, out float bright)
{
    float s = _Seed * 7.31 + i * 13.17;
    float h0 = YGHash(s + 1.0), h1 = YGHash(s + 2.0), h2 = YGHash(s + 3.0), h3 = YGHash(s + 4.0);
    float r = 0.14 + 0.7 * pow(h0, 0.8);
    float a = h1 * YG_TAU + _Time.y * _ClumpTurn * 0.5 / max(r, 0.1);
    float y = (h2 - 0.5) * YGHeight(r) * 0.9;
    centre = float3(cos(a) * r, y, sin(a) * r);
    radius = 0.04 + 0.09 * h3 * h3;
    bright = 0.55 + 0.9 * YGHash(s + 5.0);
}

// The clumps at p: their cores (blue-white stars, by brightness) and the glow of ionised
// gas round them (wider; the big ones glow most). `tight` is how close round each clump
// the glow stays: the sheet draws it wide (0.45), the volume close (1.3), or from inside
// the disc a dozen clumps' glow along every ray adds up to a fog.
void YGClumps(float3 p, float tight, out float cores, out float glow)
{
    cores = 0.0;
    glow = 0.0;
    [unroll]
    for (int i = 0; i < YG_CLUMPS; i++)
    {
        float3 c;
        float rad, b;
        YGClump(i, c, rad, b);
        float3 d = p - c;
        float d2 = dot(d, d) / (rad * rad);
        cores += b * exp(-d2 * 2.4);
        glow += b * exp(-d2 * tight) * rad / 0.08;
    }
}

// Cold stream k: a narrow filament of gas spiralling in from well outside the disc. Its
// angle at radius r, and how much of it is at p (thin, in the plane, reaching in to the
// disc's middle and out past its edge). _Streams says how many there are.
float YGStreamAngle(float r, int k)
{
    float s = _Seed * 3.7 + k * 11.3;
    return YGHash(s) * YG_TAU + 1.1 * (1.5 - r);
}

float YGStream(float3 p, int k)
{
    if (k >= (int)ceil(_Streams - 0.01)) return 0.0;
    float r = length(p.xz);
    float az = atan2(p.z, p.x);
    float across = YGWrap(az - YGStreamAngle(r, k)) * r;
    float w = 0.02 + 0.035 * r;
    float reach = smoothstep(0.3, 0.6, r) * smoothstep(1.85, 1.3, r);
    float thin = exp(-YGSq(p.y / (0.03 + 0.03 * r)));
    float on = saturate(_Streams - k);   // the last one fades in with the fraction
    return exp(-YGSq(across / w)) * reach * thin * on;
}

// The wind: hot gas blown out above and below the disc by the star formation, a cone each
// way, brightest near the disc and fading up.
float YGWind(float3 p)
{
    float r = length(p.xz);
    float h = abs(p.y);
    float cone = exp(-YGSq(r / (0.1 + 0.5 * h)));
    return cone * smoothstep(0.03, 0.2, h) * exp(-h / 0.45);
}

#endif
