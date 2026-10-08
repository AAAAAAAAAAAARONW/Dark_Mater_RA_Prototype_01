// Used by CelestialSurface_NEW: noise on a sphere without seams or pole pinching (triplanar),
// and Earth's cloud cover, read both for the clouds and for their shadows on the ground, so
// they are the same clouds.

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

    // Large weather systems, swirled hard enough to curl; then two finer octaves. The noise
    // sits about 0.5 +- 0.1, and the triplanar blend narrows it further, so it is centred and
    // stretched before the cover cuts it: read raw, the cover cut through a narrow grey
    // middle, and the clouds came out as faint, even smudges ruled into latitude bands.
    float4 large = Triplanar(p * 1.1 + 0.17);
    float3 q = p + (large.rgb - 0.5) * 1.6;
    float body = (Triplanar(q * 2.4).r - 0.5) * 0.6 + (Triplanar(q * 5.5).g - 0.5) * 0.3
               + (Triplanar(q * 13.0).a - 0.5) * 0.15;
    body = 0.5 + body * 4.0;

    // Bands, gently: the subtropics a little clearer, the storm belts a little cloudier.
    float lat = abs(n.y);
    body += -0.12 * exp(-pow((lat - 0.42) / 0.12, 2.0)) + 0.06 * exp(-pow((lat - 0.85) / 0.15, 2.0));

    float cut = 1.0 - _CloudCover;
    return smoothstep(cut - 0.12, cut + 0.18, body);
}

#endif
