Shader "Custom/QuasarDisc_NEW"
{
    // A quasar's accretion disc (QuasarVFX_NEW), and the sheets of gas swirling round it.
    //
    // Drawn on a flat annulus in the object's XZ plane, radius 1. Each pixel works out where it
    // is on the disc — how far out, and which way round — and colours itself from that: white
    // hot at the inner edge, through yellow and orange to a deep red at the rim, in rings and
    // streaks of gas that spiral in, with dark lanes of dust between them, the outer edge
    // breaking up into tendrils.
    //
    // It turns as a disc of gas does: fast inside, slow outside (Kepler, a turn rate falling
    // as r^-1.5), and the spiral is sheared by it. Sheared for ever, the pattern would wind
    // tighter and tighter until it was noise, so it is drawn twice, half a cycle apart, each
    // sheared for at most one cycle, and the two are crossfaded — as flow maps are.
    //
    // Premultiplied alpha: the bright gas glows, and the dust is dark and blocks what is
    // behind it. _Color fades the whole of it (WorldSwitcher_NEW and the dives use it).
    // In Resources so a build always has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        _Inner ("Inner edge (of the radius)", Range(0.01, 0.6)) = 0.07
        _Spin ("Turns a second at the inner edge", Float) = 0.12
        _Twist ("Spiral twist", Float) = 2.2
        _Rings ("Rings across it", Float) = 12
        _Streaks ("Streaks round it", Float) = 2
        _Dust ("Dust lanes", Range(0, 1)) = 0.55
        _Tendrils ("How far the rim breaks up", Range(0, 1)) = 0.6
        _Rim ("Brightness of the inner edge", Float) = 2
        _Wisps ("Only the brightest strands", Range(0, 1)) = 0
        [HDR] _Hot ("Inner edge", Color) = (5, 4.6, 3.75, 1)
        [HDR] _Mid ("Middle", Color) = (2, 1, 0.28, 1)
        [HDR] _Outer ("Rim", Color) = (0.55, 0.1, 0.04, 1)
        _DustColor ("Dust", Color) = (0.06, 0.025, 0.015, 1)
        _Opacity ("How much it hides what is behind", Range(0, 1)) = 0.7
        _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Inner, _Spin, _Twist, _Rings, _Streaks, _Dust, _Tendrils, _Rim, _Wisps, _Opacity, _Seed;
            float4 _Hot, _Mid, _Outer;
            fixed4 _DustColor;

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 plane : TEXCOORD0;   // where on the disc, radius 1 at the rim
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.plane = vertex.xz;
                return o;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float Noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float Fbm(float3 x)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 4; k++)
                {
                    v += a * Noise(x);
                    x = x * 2.03 + 17.1;
                    a *= 0.5;
                }
                return v / 0.9375;
            }

            // The gas, the dust and its rings at one turn of the disc: `turn` is how far round,
            // in radians, the inner edge has gone. Sampled on a cylinder (cos, sin of the angle)
            // so it wraps round without a seam; stretched round more than across, so it streaks.
            float3 Gas(float r, float angle, float turn, float offset)
            {
                float a = angle - turn * pow(max(r, _Inner) / _Inner, -1.5) + _Twist * log(max(r, 1e-3));
                float2 round = float2(cos(a), sin(a)) * _Streaks;
                float3 q = float3(round, r * _Rings) + _Seed + offset;
                float gas = Fbm(q);
                float dust = Fbm(float3(round * 0.75, r * _Rings * 0.6) + _Seed + offset + 43.7);
                float rings = 0.5 + 0.5 * sin(r * _Rings * 6.2832 + gas * 3.0);
                return float3(gas, dust, rings);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float r = length(i.plane);
                if (r > 1.0 || r < _Inner * 0.85) return 0;

                float t = saturate((r - _Inner) / (1.0 - _Inner));   // 0 inner edge, 1 rim
                float angle = atan2(i.plane.y, i.plane.x);

                // Two draws half a cycle apart, each sheared for one cycle at most, crossfaded.
                const float cycle = 8.0;
                float phase = frac(_Time.y / cycle);
                float other = frac(phase + 0.5);
                float turn = _Spin * 6.2832 * cycle;
                float3 a = Gas(r, angle, turn * phase, 0.0);
                float3 b = Gas(r, angle, turn * other, 91.3);
                float3 g = lerp(a, b, abs(phase * 2.0 - 1.0));

                // White hot inside, yellow, orange, deep red at the rim.
                float3 colour = t < 0.12 ? lerp(_Hot.rgb * 1.5, _Hot.rgb, t / 0.12)
                              : t < 0.42 ? lerp(_Hot.rgb, _Mid.rgb, smoothstep(0.12, 0.42, t))
                              :            lerp(_Mid.rgb, _Outer.rgb, smoothstep(0.42, 1.0, t));

                // The disc's gas is soft all over; the sheets round it keep only their brightest
                // strands, so they read as filaments rather than haze.
                float soft = (0.3 + 0.7 * g.x) * (0.65 + 0.35 * g.z);
                float gas = lerp(soft, smoothstep(0.5, 0.82, g.x) * (0.5 + 0.5 * g.z), _Wisps);
                float dust = smoothstep(0.5, 0.78, g.y) * _Dust * smoothstep(0.08, 0.3, t);

                // Brighter inside; a thin hot ring at the inner edge; the rim breaking up.
                float fall = pow(1.0 - t, 1.3) * 0.85 + 0.15;
                float edge = exp(-pow((r - _Inner) / (0.02 + 0.05 * _Inner), 2.0)) * _Rim;
                float rim = smoothstep(1.0, 0.72 - 0.3 * _Tendrils, r + (g.x - 0.5) * 0.5 * _Tendrils);
                float inside = smoothstep(_Inner * 0.85, _Inner, r);

                float3 light = colour * (gas * fall * (1.0 - dust) + edge);
                float hide = saturate(_Opacity * (0.35 + 0.65 * gas) * (1.0 - t * 0.5) + dust * 0.8);
                float3 premultiplied = light + _DustColor.rgb * dust * hide;

                // Faded as WorldSwitcher_NEW fades: it scales all four channels of _Color, so
                // the light takes rgb and the cover takes a — once each, a straight fade.
                float k = rim * inside;
                return fixed4(premultiplied * _Color.rgb * k, hide * k * _Color.a);
            }
            ENDCG
        }
    }

    FallBack Off
}
