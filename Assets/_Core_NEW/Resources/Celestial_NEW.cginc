// Used by CelestialSurface_NEW: noise on a sphere without seams or pole pinching (triplanar),
// and Earth's cloud cover, read both for the clouds and for their shadows on the ground, so
// they are the same clouds.

#ifndef CELESTIAL_NEW_INCLUDED
#define CELESTIAL_NEW_INCLUDED

sampler2D _Noise;
sampler2D _CloudMap;
float _CloudCover, _CloudSpin, _CloudMapOn;
float4 _ClearSky, _ClearSkyCos;

// QuasarNoise_NEW's texture read on the three planes facing the axes, blended by the normal.
float4 Triplanar(float3 p)
{
    float3 w = abs(normalize(p));
    w = pow(w, 4.0);
    w /= (w.x + w.y + w.z);
    return tex2D(_Noise, p.yz) * w.x + tex2D(_Noise, p.zx) * w.y + tex2D(_Noise, p.xy) * w.z;
}

// Earth's real cloud map, where it has one (_CloudMapOn): equirectangular, laid out as the
// day map is on PlanetSphere_NEW's mesh, so the clouds sit over the weather that made them.
// The longitude is worked out from the direction, so its derivatives jump by a whole turn at
// the seam; they are folded back, or the seam would read the smallest mip, a line of grey.
float CloudMapCover(float3 p)
{
    float2 uv = float2(atan2(-p.x, -p.z) / 6.2831853, 1.0 - acos(clamp(p.y, -1.0, 1.0)) / 3.1415927);
    float2 dx = ddx(uv), dy = ddy(uv);
    dx.x -= round(dx.x);
    dy.x -= round(dy.x);
    float m = tex2Dgrad(_CloudMap, frac(uv), dx, dy).r;

    // A little fine noise on the edges, for when Earth fills the screen and a texel is a few
    // pixels: the map alone is soft there.
    m += (Triplanar(p * 13.0).a - 0.5) * 0.12 * saturate(m * 4.0);

    float cut = 1.0 - _CloudCover;
    return smoothstep(cut - 0.4, cut + 0.3, m);
}

// 0 clear, 1 overcast, at a unit direction from the planet's centre (object space). The
// weather turns slowly round the axis, the high latitudes a little slower.
float CloudCover(float3 n)
{
    // A map turns as one, or it would shear; the noise's high latitudes lag a little.
    float spin = _CloudSpin * _Time.y * (_CloudMapOn > 0.5 ? 1.0 : 1.0 - 0.3 * n.y * n.y);
    float s = sin(spin), c = cos(spin);
    float3 p = float3(n.x * c - n.z * s, n.y, n.x * s + n.z * c);

    // Round the landing point (_ClearSky, fixed to the ground, not the weather) a clear sky.
    float clear = saturate((dot(n, _ClearSky.xyz) - _ClearSkyCos.y) / max(_ClearSkyCos.x - _ClearSkyCos.y, 1e-5));
    clear = clear * clear * (3.0 - 2.0 * clear);

    if (_CloudMapOn > 0.5) return CloudMapCover(p) * (1.0 - clear);

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
    return smoothstep(cut - 0.12, cut + 0.18, body) * (1.0 - clear);
}

#endif
