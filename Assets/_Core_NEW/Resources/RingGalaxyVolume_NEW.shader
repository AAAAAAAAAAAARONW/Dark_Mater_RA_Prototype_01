Shader "Custom/RingGalaxyVolume_NEW"
{
    // The ring galaxy's glow in depth (RingGalaxyVFX_NEW; its shape is in RingGalaxy_NEW.cginc):
    // a faint haze under the ring's clusters, its knots as glowing puffs, the nucleus, the intruder
    // as a warm streak with its tidal tail, the bridge of gas between them and a faint halo — so from
    // the light's low, slanting view it has body, not just a painted plane. The ring's haze
    // brightens where a ripple crosses it (RGRipple) and the knots flare and fade (RGKnot).
    //
    // EXACT, NOT MARCHED. Every part is a soft blob — a 3D gaussian, stretched and turned — and
    // the light of a gaussian along a straight ray has a closed form (an erf), so each pixel adds
    // up some forty blobs exactly: no samples, no noise, no stepping, and thin parts (the ring is
    // a few hundredths of the radius thick) never fall between samples. A blob a ray passes
    // nowhere near costs one exp and is skipped.
    //
    // The ray is the camera's own through a box round the galaxy, drawn from its back faces so it
    // works from inside too (the light flies over the middle inside the box). Drawn twice, split
    // where the ray crosses the plane: the far half before the face sheet (RingGalaxyDisc_NEW),
    // whose dust darkens it, the near half after. _Side picks the half: 1 the camera's side,
    // -1 the far side.
    //
    // Additive. _Color fades it. In Resources so a build has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        _Side ("Half: 1 the camera's side of the plane, -1 the far side", Float) = 1
        _Box ("Box half size (x, y, z)", Vector) = (2.4, 0.6, 2.4, 0)
        _Seed ("Seed", Float) = 0
        _Turn ("Turn, radians a second", Float) = 0.02
        _Ripple ("Ripples: seconds to spread, strength; twinkle; a knot's life, seconds", Vector) = (14, 1, 0.6, 50)
        _Ring ("Ring: radius, half width, lopsidedness, egg-shape", Vector) = (0.72, 0.085, 0.85, 0.12)
        _CompTilt ("The intruder's disc turned about its long axis, radians", Float) = -0.785
        _Comp ("The intruder: where (of the radius), and its size", Vector) = (-0.98, 0.0, -0.05, 0.3)
        _Volume ("Brightness", Range(0, 4)) = 1
        _Thickness ("Ring and disc half thickness (of the radius)", Float) = 0.03
        [HDR] _Core ("Nucleus", Color) = (1.6, 1.05, 0.55, 1)
        [HDR] _Inner ("The haze in the middle", Color) = (0.3, 0.38, 0.62, 1)
        [HDR] _Gas ("The ring", Color) = (0.45, 0.62, 1.25, 1)
        [HDR] _Glow ("Knots", Color) = (1.4, 0.6, 1.1, 1)
        [HDR] _CompColor ("The intruder", Color) = (1.5, 1.2, 0.85, 1)
        [HDR] _PlumeColor ("The bridge", Color) = (0.35, 0.5, 0.95, 1)
        [HDR] _Halo ("Halo", Color) = (0.3, 0.32, 0.5, 1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent-11" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Front

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "RingGalaxy_NEW.cginc"

            fixed4 _Color;
            float _Side, _Volume, _Thickness;
            float4 _Box;
            float4 _Core, _Inner, _Gas, _Glow, _CompColor, _PlumeColor, _Halo;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 obj : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.obj = vertex.xyz;
                return o;
            }

            // The ray and the stretch of it this half adds up (set per pixel).
            static float3 gO, gD;
            static float gT0, gT1;

            float Erf(float x)
            {
                float x2 = x * x, ax2 = 0.147 * x2;
                return sign(x) * sqrt(1.0 - exp(-x2 * (1.2732395 + ax2) / (1.0 + ax2)));
            }

            // The light of exp(-|u + t v|^2) along t from gT0 to gT1, exactly.
            float Line(float3 u, float3 v)
            {
                float vv = max(dot(v, v), 1e-8);
                float uv = dot(u, v);
                float perp = dot(u, u) - uv * uv / vv;
                if (perp > 9.0) return 0.0;   // passes further than three of its widths from it
                float s = sqrt(vv), tc = uv / vv;
                return exp(-perp) * 0.8862269 / s * (Erf(s * (gT1 + tc)) - Erf(s * (gT0 + tc)));
            }

            // A blob at c with axes X, Y, Z and widths w along them. Divided by sqrt(pi) times its
            // width along Y, so seen face on (along Y) its peak is 1.
            float Blob(float3 c, float3 X, float3 Y, float3 Z, float3 w)
            {
                float3 o = gO - c;
                float3 u = float3(dot(o, X), dot(o, Y), dot(o, Z)) / w;
                float3 v = float3(dot(gD, X), dot(gD, Y), dot(gD, Z)) / w;
                return Line(u, v) / (1.7724539 * w.y);
            }

            // A blob lying in the plane, its first axis at angle a.
            float FlatBlob(float3 c, float a, float3 w)
            {
                return Blob(c, float3(cos(a), 0.0, sin(a)), float3(0.0, 1.0, 0.0), float3(-sin(a), 0.0, cos(a)), w);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 o = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                float3 d = normalize(i.obj - o);

                // The stretch of the ray inside the box, then this half of it.
                float3 inv = 1.0 / ((step(0.0, d) * 2.0 - 1.0) * max(abs(d), 1e-6));
                float3 ta = (-_Box.xyz - o) * inv, tb = (_Box.xyz - o) * inv;
                float3 tmin = min(ta, tb), tmax = max(ta, tb);
                float t0 = max(max(max(tmin.x, tmin.y), tmin.z), 0.0);
                float t1 = min(min(tmax.x, tmax.y), tmax.z);
                if (t1 <= t0) return 0;

                float side = o.y >= 0.0 ? 1.0 : -1.0;
                bool crosses = side * d.y < 0.0;
                float tp = crosses ? -o.y / d.y : 1e9;
                if (_Side > 0.0) t1 = min(t1, tp);
                else
                {
                    if (!crosses) return 0;
                    t0 = max(t0, tp);
                }
                if (t1 <= t0) return 0;

                gO = o; gD = d; gT0 = t0; gT1 = t1;
                float T = _Time.y;
                float h = _Thickness;
                float3 light = 0.0;

                // Near the plane the haze is seen edge on, through all of it at once, and would be a
                // fog over everything: it thins as the camera comes into the layer (the light flies
                // through the ring's middle). The knots, the nucleus and the intruder stay.
                float haze = lerp(0.2, 1.0, smoothstep(0.03, 0.2, abs(o.y)));

                // The ring: sixteen long beads round it, overlapping, lopsided like the face.
                [loop]
                for (int j = 0; j < 16; j++)
                {
                    float a = (j + 0.5) / 16.0 * RG_TAU;
                    float rr = RGRingR(a);
                    float3 c = float3(cos(a), 0.0, sin(a)) * rr;
                    // A faint haze under the ring's clusters, uneven, not a glowing tube.
                    float b = RGBright(a) * (0.3 + 0.9 * RGHash(j * 4.7 + 0.3)) * (1.0 + 0.8 * RGRipple(c.xz, T));
                    light += _Gas.rgb * (0.06 * b * haze) * FlatBlob(c, a + 1.5707963, float3(rr * 0.19, h * 2.0, RGWidth(a) * 1.1));
                }

                // The knots, as beads of pink light standing up a little out of the ring.
                [loop]
                for (int k = 0; k < RG_KNOTS; k++)
                {
                    float2 kp; float ks, kb;
                    RGKnot(k, T, kp, ks, kb);
                    light += _Glow.rgb * (0.3 * kb) * FlatBlob(float3(kp.x, 0.0, kp.y), 0.0, float3(ks, max(ks, h), ks));
                }

                // The nucleus and the old disc round it.
                float2 nuc = RGNucleus();
                float3 cn = float3(nuc.x, 0.0, nuc.y);
                light += _Core.rgb * 0.1 * FlatBlob(cn, 0.0, float3(0.04, 0.035, 0.04));
                light += _Inner.rgb * (0.006 * haze) * FlatBlob(float3(0.0, 0.0, 0.0), 0.0, float3(0.42, 0.14, 0.42));

                // The intruder: a long bright streak, its disc edge on, a dark lane of dust along
                // its middle (its light in two layers, above and below the lane, and the bright
                // middle split by it too), and a faint halo.
                float3 L, W, N;
                RGCompanionFrame(L, W, N);
                float size = _Comp.w;
                float3 cc = _Comp.xyz;
                float3 lane = N * size * 0.075;
                light += _CompColor.rgb * 0.32 * Blob(cc + lane, L, N, W, float3(0.22, 0.06, 0.16) * size);
                light += _CompColor.rgb * 0.32 * Blob(cc - lane, L, N, W, float3(0.22, 0.06, 0.16) * size);
                light += _CompColor.rgb * 0.11 * Blob(cc + lane * 0.9, L, N, W, float3(0.75, 0.05, 0.28) * size);
                light += _CompColor.rgb * 0.11 * Blob(cc - lane * 0.9, L, N, W, float3(0.75, 0.05, 0.28) * size);
                light += _CompColor.rgb * 0.03 * Blob(cc, L, N, W, float3(1.0, 0.35, 0.5) * size);

                // Its tidal tail, drawn out of the far side.
                [loop]
                for (int q = 0; q < 4; q++)
                {
                    float f = (q + 0.5) / 4.0;
                    float3 c = RGTail(f);
                    float3 along = normalize(RGTail(f + 0.05) - RGTail(max(f - 0.05, 0.0)));
                    float3 across = normalize(cross(float3(0.0, 1.0, 0.0), along));
                    light += _CompColor.rgb * (0.08 * (1.0 - 0.6 * f)) * Blob(c, along, cross(along, across), across, float3(0.2, 0.035, 0.06) * (0.6 + f));
                }

                // The bridge of gas from the ring back to it.
                [loop]
                for (int s = 0; s < 5; s++)
                {
                    float f = (s + 0.5) / 5.0;
                    float3 c = RGBridge(f);
                    float3 along = normalize(RGBridge(f + 0.05) - RGBridge(f - 0.05));
                    float3 across = normalize(cross(float3(0.0, 1.0, 0.0), along));
                    float patchy = 0.35 + 0.65 * RGHash(s * 3.9 + 1.1);
                    light += _PlumeColor.rgb * (0.05 * patchy) * Blob(c, along, cross(along, across), across, float3(0.16, 0.06, 0.1));
                }

                // A faint halo of old stars round it all.
                light += _Halo.rgb * (0.015 * haze) * FlatBlob(float3(0.0, 0.0, 0.0), 0.0, float3(1.0, 0.45, 1.0));

                return fixed4(light * _Volume * _Color.rgb * _Color.a, 0.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
