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
    // sheared for at most one cycle, and the two are crossfaded — as flow maps are. The gas
    // drifts inward as it turns.
    //
    // The side of the disc turning towards the camera is brighter and whiter, the side turning
    // away dimmer and redder — relativistic beaming, which is what makes it read as fast. Seen
    // face on, nothing turns towards the camera, and it is even.
    //
    // QuasarVFX_NEW sets the clock (_Pace, _PaceOffset: the disc speeds up as the quasar winds
    // up), the brightness, the flicker, and the eruption: a ring of light running out across
    // the disc while its middle flashes.
    //
    // The noise is QuasarNoise_NEW's texture, read in polar coordinates with gradients worked
    // out from the plane's own, so the seam of atan2 at ±180° never picks the wrong mip.
    //
    // Premultiplied alpha: the bright gas glows, and the dust is dark and blocks what is
    // behind it. _Color fades the whole of it (WorldSwitcher_NEW and the dives use it).
    // In Resources so a build always has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _Noise ("Noise (QuasarNoise_NEW)", 2D) = "gray" {}
        _Inner ("Inner edge (of the radius)", Range(0.01, 0.6)) = 0.07
        _Spin ("Turns a second at the inner edge", Float) = 0.25
        _Twist ("Spiral twist", Float) = 2.2
        _Rings ("Rings across it", Float) = 12
        _Around ("Noise tiles round it (a whole number)", Float) = 3
        _Inflow ("Inward drift, tiles a second", Float) = 0.05
        _Dust ("Dust lanes", Range(0, 1)) = 0.75
        _Tendrils ("How far the rim breaks up", Range(0, 1)) = 0.6
        _Rim ("Brightness of the inner edge", Float) = 2
        _Wisps ("Only the brightest strands", Range(0, 1)) = 0
        _Shimmer ("Fine flicker", Range(0, 1)) = 0.35
        _Beaming ("Relativistic beaming", Range(0, 1)) = 0.5
        _Speed ("Orbital speed at the inner edge, of light's", Range(0, 0.9)) = 0.45
        [HDR] _Hot ("Inner edge", Color) = (2.08, 1.6, 0.96, 1)
        [HDR] _Mid ("Middle", Color) = (1.36, 0.34, 0.04, 1)
        [HDR] _Outer ("Rim", Color) = (0.4, 0.028, 0.012, 1)
        _DustColor ("Dust", Color) = (0.03, 0.01, 0.005, 1)
        _Opacity ("How much it hides what is behind", Range(0, 1)) = 0.7
        _WaveLight ("Eruption ring brightness", Float) = 1
        _WaveSpeed ("Eruption ring speed, radii a second", Float) = 0.35
        _WaveDecay ("Eruption ring fade, seconds", Float) = 1.6
        _Seed ("Seed", Float) = 0

        // Set every frame by QuasarVFX_NEW.
        _Pace ("Clock rate", Float) = 1
        _PaceOffset ("Clock offset", Float) = 0
        _Bright ("Brightness", Float) = 1
        _Pulse ("Flicker", Float) = 1
        _FlareAge ("Seconds since the eruption", Float) = 1000
        _FlareGain ("Eruption strength", Float) = 0
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

            #define TAU 6.2831853

            sampler2D _Noise;
            fixed4 _Color;
            float _Inner, _Spin, _Twist, _Rings, _Around, _Inflow, _Dust, _Tendrils, _Rim, _Wisps, _Shimmer;
            float _Beaming, _Speed, _Opacity, _WaveLight, _WaveSpeed, _WaveDecay, _Seed;
            float _Pace, _PaceOffset, _Bright, _Pulse, _FlareAge, _FlareGain;
            float4 _Hot, _Mid, _Outer;
            fixed4 _DustColor;

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 plane : TEXCOORD0;   // where on the disc, radius 1 at the rim
                float3 view  : TEXCOORD1;   // from the camera to here, in object space
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.plane = vertex.xz;
                float3 camera = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                o.view = vertex.xyz - camera;
                return o;
            }

            float Sq(float x) { return x * x; }

            // One look at the pattern, sheared by `phase` of a cycle's turn: gas (x), dust (y),
            // filaments (z), and the fine noise (w), which drifts by `drift` on its own.
            float4 Look(float a, float r, float v, float2 da, float2 dr, float2 dv, float phase,
                        float turn, float kep, float2 offset, float drift)
            {
                float u = (a - turn * phase * kep + _Twist * log(max(r, 1e-3))) / TAU * _Around + _Seed * 0.173 + offset.x;
                float dudr = (1.5 * turn * phase * kep + _Twist) / max(r, 1e-3);
                float2 du = (da + dudr * dr) * (_Around / TAU);
                float vv = v + offset.y;

                float4 g = tex2Dgrad(_Noise, float2(u, vv), float2(du.x, dv.x), float2(du.y, dv.y));
                float dust = tex2Dgrad(_Noise, float2(u, vv * 0.6 + 0.31),
                                       float2(du.x, dv.x * 0.6), float2(du.y, dv.y * 0.6)).g;
                float fine = tex2Dgrad(_Noise, float2(u * 2.0, vv * 3.0 + drift),
                                       float2(du.x * 2.0, dv.x * 3.0), float2(du.y * 2.0, dv.y * 3.0)).a;
                return float4(g.r, dust, g.b, fine);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Derivatives first, while every pixel of the quad is still running.
                float2 p = i.plane;
                float2 dpx = ddx(p), dpy = ddy(p);

                float r = length(p);
                if (r > 1.0 || r < _Inner * 0.85) return 0;

                float t = saturate((r - _Inner) / (1.0 - _Inner));   // 0 inner edge, 1 rim
                float a = atan2(p.y, p.x);

                // How angle and radius change across the pixel, from the plane's own change.
                float r2 = max(r * r, 1e-8);
                float2 da = float2(p.x * dpx.y - p.y * dpx.x, p.x * dpy.y - p.y * dpy.x) / r2;
                float2 dr = float2(dot(p, dpx), dot(p, dpy)) / max(r, 1e-4);

                float T = _Time.y * _Pace + _PaceOffset;

                // Two looks half a cycle apart, each sheared for one cycle at most, crossfaded.
                const float cycle = 8.0;
                float ph = frac(T / cycle);
                float ph2 = frac(ph + 0.5);
                float turn = _Spin * TAU * cycle;
                float kep = pow(max(r, _Inner) / _Inner, -1.5);
                float v = r * _Rings * 0.25 + T * _Inflow + _Seed * 0.291;
                float2 dv = dr * (_Rings * 0.25);

                float4 lookA = Look(a, r, v, da, dr, dv, ph, turn, kep, float2(0.0, 0.0), T * 0.15);
                float4 lookB = Look(a, r, v, da, dr, dv, ph2, turn, kep, float2(0.37, 0.61), T * 0.15);
                float4 g = lerp(lookA, lookB, abs(ph * 2.0 - 1.0));
                float rings = 0.5 + 0.5 * sin(r * _Rings * TAU + g.x * 3.0);

                // White hot inside, yellow, orange, deep red at the rim.
                float3 colour = t < 0.12 ? lerp(_Hot.rgb * 1.5, _Hot.rgb, t / 0.12)
                              : t < 0.42 ? lerp(_Hot.rgb, _Mid.rgb, smoothstep(0.12, 0.42, t))
                              :            lerp(_Mid.rgb, _Outer.rgb, smoothstep(0.42, 1.0, t));

                // The disc's gas is soft all over; the sheets round it keep only their
                // filaments, gathered into arms where the soft gas is thick.
                float soft = (0.3 + 0.7 * g.x) * (0.65 + 0.35 * rings);
                float wisp = smoothstep(0.45, 0.8, g.z) * (0.55 + 0.45 * rings) * smoothstep(0.38, 0.66, g.x) * 1.6;
                float gas = lerp(soft, wisp, _Wisps) * (1.0 + _Shimmer * (g.w - 0.5) * 2.0);
                float dust = smoothstep(0.47, 0.68, g.y) * _Dust * smoothstep(0.08, 0.3, t);

                // Brighter inside; a thin hot ring at the inner edge; the rim breaking up.
                float fall = pow(1.0 - t, 1.3) * 0.85 + 0.15;
                float edge = exp(-Sq((r - _Inner) / (0.02 + 0.05 * _Inner))) * _Rim;
                float rim = smoothstep(1.0, 0.72 - 0.3 * _Tendrils, r + (g.x - 0.5) * 0.5 * _Tendrils);
                float inside = smoothstep(_Inner * 0.85, _Inner, r);

                // Relativistic beaming: brighter and whiter where the gas comes at the camera.
                float beam = 1.0;
                float3 tint = 1.0;
                if (_Beaming > 0.0)
                {
                    float3 velocity = float3(-p.y, 0.0, p.x) / max(r, 1e-4);
                    float beta = min(0.9, _Speed * pow(max(r, _Inner) / _Inner, -0.5));
                    float towards = -dot(velocity, normalize(i.view));
                    float doppler = sqrt(1.0 - beta * beta) / (1.0 - beta * towards);
                    beam = pow(doppler, 3.0 * _Beaming);
                    tint = doppler > 1.0
                        ? lerp(1.0, float3(0.9, 1.0, 1.2), saturate((doppler - 1.0) * 1.5) * _Beaming)
                        : lerp(1.0, float3(1.1, 0.8, 0.62), saturate((1.0 - doppler) * 2.0) * _Beaming);
                }

                // The eruption: a ring of light running out across the disc, the middle flashing.
                float age = _FlareAge;
                float wave = exp(-Sq((r - (_Inner + age * _WaveSpeed)) / (0.025 + 0.05 * age)))
                           * exp(-age / _WaveDecay) * _FlareGain * (0.4 + g.x);
                float flash = 1.0 + _FlareGain * exp(-age / 0.7) * 2.5 * Sq(1.0 - t);
                float pulse = lerp(1.0, _Pulse, Sq(1.0 - t));

                float lum = (gas * fall * (1.0 - dust) + edge) * beam * pulse * flash * _Bright;
                float3 light = colour * tint * lum + _Hot.rgb * wave * 1.6 * _WaveLight;
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
