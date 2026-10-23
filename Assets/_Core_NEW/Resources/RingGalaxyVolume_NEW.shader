Shader "Custom/RingGalaxyVolume_NEW"
{
    // The ring galaxy's glow in depth (RingGalaxyVFX_NEW; its shape is in RingGalaxy_NEW.cginc):
    // the ring standing up as a band of light, its knots as glowing beads, the nucleus, the
    // intruder as an edge-on streak, the bridge of gas between them and a faint halo — so from
    // the light's low, slanting view it has body, not just a painted plane.
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
        _Turn ("Turn, radians a second", Float) = 0.004
        _Ring ("Ring: radius, half width, lopsidedness, egg-shape", Vector) = (0.72, 0.09, 0.75, 0.1)
        _Comp ("The intruder: where (of the radius), and its size", Vector) = (1.32, 0.14, 0.34, 0.28)
        _Volume ("Brightness", Range(0, 4)) = 1
        _Thickness ("Ring and disc half thickness (of the radius)", Float) = 0.03
        [HDR] _Core ("Nucleus", Color) = (1.6, 1.05, 0.55, 1)
        [HDR] _Inner ("The old disc in the middle", Color) = (0.55, 0.42, 0.32, 1)
        [HDR] _Gas ("The ring", Color) = (0.45, 0.62, 1.25, 1)
        [HDR] _Glow ("Knots", Color) = (1.8, 0.6, 1.0, 1)
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

                // The ring: sixteen long beads round it, overlapping, lopsided like the face.
                [loop]
                for (int j = 0; j < 16; j++)
                {
                    float a = (j + 0.5) / 16.0 * RG_TAU;
                    float rr = RGRingR(a);
                    float3 c = float3(cos(a), 0.0, sin(a)) * rr;
                    // Bunched like the face: some beads bright, some all but gone.
                    float bunch = saturate(0.5 + 0.9 * sin(a * 11.0 + _Seed + sin(a * 4.0) * 2.0));
                    float b = RGBright(a) * (0.2 + 0.8 * bunch) * (0.6 + 0.8 * RGHash(j * 4.7 + 0.3));
                    light += _Gas.rgb * (0.3 * b) * FlatBlob(c, a + 1.5707963, float3(rr * 0.17, h, RGWidth(a) * 0.9));
                }

                // The knots, as beads of pink light standing up a little out of the ring.
                [loop]
                for (int k = 0; k < RG_KNOTS; k++)
                {
                    float2 kp; float ks, kb;
                    RGKnot(k, T, kp, ks, kb);
                    light += _Glow.rgb * (0.6 * kb) * FlatBlob(float3(kp.x, 0.0, kp.y), 0.0, float3(ks, max(ks * 0.7, h), ks));
                }

                // The nucleus and the old disc round it.
                float2 nuc = RGNucleus();
                float3 cn = float3(nuc.x, 0.0, nuc.y);
                light += _Core.rgb * 0.8 * FlatBlob(cn, 0.0, float3(0.03, 0.025, 0.03));
                light += _Core.rgb * 0.15 * FlatBlob(cn, 0.0, float3(0.09, 0.06, 0.09));
                light += _Inner.rgb * 0.05 * FlatBlob(cn * 0.5, 0.0, float3(0.42, h * 1.3, 0.42));

                // The intruder: an edge-on disc with a bright middle and a faint round halo.
                float3 L, W, N;
                RGCompanionFrame(L, W, N);
                float size = _Comp.w;
                float3 cc = _Comp.xyz;
                light += _CompColor.rgb * 0.6 * Blob(cc, L, N, W, float3(0.16, 0.08, 0.12) * size);
                light += _CompColor.rgb * 0.22 * Blob(cc, L, N, W, float3(1.0, 0.09, 0.32) * size);
                light += _CompColor.rgb * 0.06 * Blob(cc, L, N, W, float3(0.6, 0.45, 0.5) * size);

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
                light += _Halo.rgb * 0.04 * FlatBlob(float3(0.0, 0.0, 0.0), 0.0, float3(1.0, 0.45, 1.0));

                return fixed4(light * _Volume * _Color.rgb * _Color.a, 0.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
