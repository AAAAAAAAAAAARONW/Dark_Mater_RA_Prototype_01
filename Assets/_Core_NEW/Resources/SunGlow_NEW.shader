Shader "Custom/SunGlow_NEW"
{
    // What is round the Sun's disc (SunLook_NEW): a quad turned to face the camera, centred on
    // the Sun, added over what is behind.
    //
    //   glow         three falloffs out from the limb — tight, soft and streaked, wide and faint.
    //                It swells a little while a region flares.
    //   spicules     the fuzzy edge in SDO's pictures: a thin fringe of jets just off the limb,
    //                its height flickering along it.
    //   loops        the arches of gas held up by an active region's magnetic field: three to a
    //                region, of different spans and heights, standing on the limb where the
    //                region is. As the Sun turns a region onto the limb its loops rise into view,
    //                and sink behind it as it goes round. Bright knots run along them, up one
    //                side and down the other; they sway and swell a little; and they flare up
    //                with their region.
    //
    // COLOUR. All three add up one brightness, turned into colour by SunRamp_NEW.cginc, the
    // ramp measured from NASA's picture, as the disc is. In that picture there is next to no
    // glow: a hair off the limb it is already #060100. So the glow is faint and dark red; the
    // fringe a dim red; only the loops reach orange, and amber when their region flares.
    // (21 Nov: it was a bright orange glow of its own colour, which the bloom turned white.)
    //
    // The regions come from SunLook_NEW in world space (_Regions) and are put on the limb here,
    // for whichever camera is drawing — every wall of a multi-camera display sees its own.
    //
    // Measured in the Sun's radii, so it keeps its shape at any distance. The disc
    // (SunSurface_NEW) is opaque and nearer than the quad's middle, so all of this shows only
    // round it; planets in front hide it. _Color fades it as WorldSwitcher_NEW fades a world.
    // In Resources so a build has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        _Strength ("Glow", Range(0, 2)) = 0.6
        _Spicules ("Fuzzy edge", Range(0, 2)) = 0.7
        _Loops ("Loops on the limb", Range(0, 2)) = 0.8
        _LoopFlow ("Knots running along the loops", Range(0, 2)) = 1
        _MeshRadius ("The Sun's mesh radius, object space", Float) = 0.5
        _Extent ("Quad half-size, in the Sun's radii", Float) = 5
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "SunRamp_NEW.cginc"

            #define REGIONS 6

            fixed4 _Color;
            float _Strength, _Spicules, _Loops, _LoopFlow, _MeshRadius, _Extent;
            float4 _Regions[REGIONS];       // xyz: direction from the centre, world space; w: size
            float _RegionFlare[REGIONS];    // 0 to 1

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 q   : TEXCOORD0;   // in the Sun's radii from its centre
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                float3 centre = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float radius = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x)) * _MeshRadius;
                float4 view = mul(UNITY_MATRIX_V, float4(centre, 1));
                view.xy += v.vertex.xy * radius * _Extent;
                o.pos = mul(UNITY_MATRIX_P, view);
                o.q = v.vertex.xy * _Extent;
                return o;
            }

            float Hash(float x) { return frac(sin(x * 127.1 + 3.7) * 43758.5453); }

            // Smooth 1D noise, 0 to 1.
            float Noise(float x)
            {
                float i = floor(x), f = frac(x);
                return lerp(Hash(i), Hash(i + 1.0), f * f * (3.0 - 2.0 * f));
            }

            float Wrap(float a) { return a - 6.2832 * floor((a + 3.1416) / 6.2832); }

            fixed4 frag(v2f i) : SV_Target
            {
                float r = length(i.q);
                float d = max(r - 1.0, 0.0);
                float a = atan2(i.q.y, i.q.x);          // -pi to pi
                float u = a / 6.2832 + 0.5;             // 0 to 1 round the limb
                float t = _Time.y;

                float flares = 0.0;
                [unroll]
                for (int f = 0; f < REGIONS; f++) flares += _RegionFlare[f];

                // Glow, swelling a little while a region flares.
                float streamers = 0.6 + 0.4 * sin(a * 7.0 + sin(a * 3.0 + t * 0.03) * 2.0);
                float glow = exp(-d * 14.0) * 0.35 + exp(-d * 3.0) * 0.08 * streamers + exp(-d * 0.8) * 0.03;
                float brightness = glow * _Strength * (1.0 + 0.6 * saturate(flares));

                float outside = step(1.0, r);

                // Spicules: a fringe whose height flickers along the limb.
                float height = 0.012 + 0.03 * Noise(u * 420.0 + t * 0.6) * Noise(u * 90.0 - t * 0.2);
                float fringe = 1.0 - smoothstep(0.0, height, d);
                brightness += 0.65 * fringe * _Spicules * outside;

                // Loops, three to a region, where the region is on the limb.
                float loops = 0.0;
                [unroll]
                for (int k = 0; k < REGIONS; k++)
                {
                    float3 dv = mul((float3x3)UNITY_MATRIX_V, _Regions[k].xyz);
                    // On the limb when square to the line of sight; fading out a little either side.
                    float onLimb = 1.0 - smoothstep(0.15, 0.65, abs(dv.z));
                    if (onLimb <= 0.0) continue;
                    float at = atan2(dv.y, dv.x);
                    // Just behind the limb, their feet are hidden and they stand lower.
                    float sink = max(-dv.z, 0.0) * 0.5;
                    float bright = onLimb * (1.0 + 2.5 * _RegionFlare[k]);

                    [unroll]
                    for (int j = 0; j < 3; j++)
                    {
                        float seed = k * 3.0 + j;
                        float span = 0.06 + 0.09 * Hash(seed + 1.0);
                        float da = Wrap(a - at - (Hash(seed) - 0.5) * 0.35);
                        float x = da / span;
                        if (abs(x) >= 1.0) continue;

                        float top = (0.04 + 0.09 * Hash(seed + 2.0)) * (1.0 + 0.15 * sin(t * 0.25 + seed * 1.7));
                        float h = top * sqrt(1.0 - x * x) + 0.008 * sin(x * 3.0 + t * 0.4 + seed) - sink;
                        if (h <= 0.0) continue;

                        float thick = 0.005 + 0.004 * Noise(x * 6.0 + t * 0.5 + seed * 5.0);
                        float band = (d - h) / thick;
                        // Knots running along it: up one side, over and down the other.
                        float dir = Hash(seed + 4.0) > 0.5 ? 1.0 : -1.0;
                        float speed = 0.3 + 0.5 * Hash(seed + 3.0);
                        float knots = 0.5 + 0.5 * _LoopFlow * (Noise(x * 5.0 - dir * t * speed + seed * 9.0) - 0.5) * 2.0;
                        loops += exp(-band * band) * saturate(knots) * bright;
                    }
                }
                brightness += loops * _Loops * outside;

                // To nothing at the quad's edge.
                brightness *= 1.0 - smoothstep(_Extent * 0.75, _Extent, r);
                float3 c = SunRamp(brightness);
                return fixed4(c * _Color.rgb * _Color.a, 0);
            }
            ENDCG
        }
    }

    FallBack Off
}
