// Shared by CelestialSurface_NEW and CelestialClouds_NEW: noise on a sphere without seams or
// pole pinching (triplanar), and Earth's cloud cover, so the ground's cloud shadows and the
// clouds themselves are the same clouds.

#ifndef CELESTIAL_NEW_INCLUDED
#define CELESTIAL_NEW_INCLUDED

sampler2D _Noise;
float _CloudCover, _CloudSpin;

// QuasarNoise_NEW's texture read on the three planes facing the axes, blended by the normal.
float4 Triplanar(float3 p)
{
    float3 w = abs(normalize(p));
    w = pow(w, 4.0);
    w /= (w.x + w.y + w.z);
    return tex2D(_Noise, p.yz) * w.x + tex2D(_Noise, p.zx) * w.y + tex2D(_Noise, p.xy) * w.z;
}

// 0 clear, 1 overcast, at a unit direction from the planet's centre (object space). The
// weather turns slowly round the axis, the high latitudes a little slower.
float CloudCover(float3 n)
{
    float spin = _CloudSpin * _Time.y * (1.0 - 0.3 * n.y * n.y);
    float s = sin(spin), c = cos(spin);
    float3 p = float3(n.x * c - n.z * s, n.y, n.x * s + n.z * c);

    // Large weather systems, swirled; then detail.
    float4 large = Triplanar(p * 1.3 + 0.17);
    float3 q = p + (large.rgb - 0.5) * 0.35;
    float body = Triplanar(q * 2.6).r * 0.65 + Triplanar(q * 7.0).g * 0.35;

    // Bands: the subtropics clearer, the storm belts cloudier.
    float lat = abs(n.y);
    float belt = 1.0 - 0.35 * exp(-pow((lat - 0.42) / 0.12, 2.0)) + 0.15 * exp(-pow((lat - 0.85) / 0.15, 2.0));

    return smoothstep(1.0 - _CloudCover, 1.0 - _CloudCover + 0.22, body * belt);
}

#endif
