Shader "Custom/HostGalaxyStars_NEW"
{
    // The quasar's host galaxy's stars (HostGalaxyVFX_NEW), moved on the GPU: each quad carries
    // only its seeds and its kind, and the vertex shader works out where its star is now.
    //
    //   0 arm       young, born on the two arms (HostGalaxy_NEW.cginc), orbiting away from them,
    //               fading and born again
    //   1 ring      bright young clusters strung round the starburst ring, close round the quasar
    //   2 clump     giant clumps of star formation on the arms: big soft knots of light with a
    //               rose glow — the early universe's galaxies are made of them
    //   3 nucleus   old, gold, in a round swarm crowding in on the quasar, so that from the disc
    //               round it there are stars on every side, near and far
    //   4 disc      old, faint, through the puffy disc
    //   5 halo      globular clusters, round fuzzy points, slowly orbiting
    //
    // Everything goes round the way the quasar's disc does, on a flat rotation curve (faster
    // inside). Additive points, never thinner than two pixels, never bigger than _MaxSize of the
    // screen, so one passing close is still a star. _Color fades it star by star, as
    // GalaxyStars_NEW does.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _Noise ("Noise (QuasarNoise_NEW), for the arms' wander", 2D) = "gray" {}
        _Pitch ("Arm pitch, degrees", Float) = 22
        _Bar ("Bar: angle (deg), half length, half width", Vector) = (20, 0.3, 0.1, 0)
        _RingRadius ("Starburst ring radius (of the radius)", Float) = 0.13
        _Lopsided ("Lopsided", Range(0, 1)) = 0.6
        _PatternSpeed ("Pattern turn, radians a second", Float) = 0.004
        _Speed ("Orbital speed, radii a second (flat curve)", Float) = 0.012
        _Size ("Star size (of the radius)", Float) = 0.0018
        _Thickness ("Disc scale height at the centre, and its growth outward", Vector) = (0.05, 0.06, 0, 0)
        _NucleusReach ("How far out the nuclear swarm reaches (of the radius)", Float) = 0.22
        [HDR] _Young ("Arm stars", Color) = (1.4, 1.15, 0.9, 1)
        [HDR] _RingStars ("Ring clusters", Color) = (1.8, 1.45, 1.1, 1)
        [HDR] _Clump ("Clumps", Color) = (1.5, 1.05, 0.85, 1)
        [HDR] _Nucleus ("Nuclear stars", Color) = (1.3, 0.95, 0.6, 1)
        [HDR] _DiscStars ("Disc stars", Color) = (0.32, 0.24, 0.17, 1)
        [HDR] _Globular ("Globular clusters", Color) = (0.42, 0.36, 0.3, 1)
        _MaxSize ("Largest on screen, of half its height", Float) = 0.01
    }

    SubShader
    {
        Tags { "Queue"="Transparent-8" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
            #include "HostGalaxy_NEW.cginc"

            sampler2D _Noise;
            fixed4 _Color;
            float _Speed, _Size, _MaxSize, _NucleusReach;
            float4 _Thickness;
            float4 _Young, _RingStars, _Clump, _Nucleus, _DiscStars, _Globular;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 corner : TEXCOORD0;
                float4 seed   : TEXCOORD1;   // three seeds 0..1, and the kind (0..5)
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float2 corner : TEXCOORD0;
                float3 colour : TEXCOORD1;   // with the fade in it
                float3 shape  : TEXCOORD2;   // x: softness (clusters, clumps); y: rose glow
            };

            float Gauss(float s) { s = clamp(s, 0.004, 0.996); return sign(s - 0.5) * sqrt(-2.0 * log(1.0 - abs(s - 0.5) * 2.0)); }

            // The flat rotation curve, radians a second.
            float Omega(float r) { return _Speed / max(r, 0.03); }

            // Where on arm k a star at radius r is born, with the arms' wander.
            float BornOnArm(float r, float k, float jitter)
            {
                float a = HGArmAngle(r, k) + jitter;
                float2 guess = float2(cos(a), sin(a)) * r;
                return a + (tex2Dlod(_Noise, float4(guess * 0.6 + float2(0.13, 0.57), 0.0, 2.0)).r - 0.5) * 0.45;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float s0 = v.seed.x, s1 = v.seed.y, s2 = v.seed.z;
                float kind = v.seed.w;
                float T = _Time.y;
                float3 position;
                float3 colour;
                float size = _Size;
                float3 shape = 0.0;
                float big = 1.0;   // how much bigger than _MaxSize it may be drawn

                if (kind < 0.5)
                {
                    // Arm: born on an arm, drifting off it round its orbit, fading.
                    float arm = s2 < 0.6 ? 0.0 : 1.0;
                    float life = lerp(25.0, 70.0, s1);
                    float age = frac(s0 * 13.7 + T / life);
                    float r = lerp(_Bar.y * 0.9, arm < 0.5 ? 1.05 : 0.85, pow(frac(s2 * 7.13), 1.1));
                    float born = BornOnArm(r, arm, (frac(s0 * 5.3) - 0.5) * 0.3);
                    float angle = born + (Omega(r) - _PatternSpeed) * age * life;
                    float y = Gauss(frac(s1 * 9.1)) * (_Thickness.x * 0.35);
                    position = float3(cos(angle) * r, y, sin(angle) * r);
                    float b = smoothstep(0.0, 0.08, age) * smoothstep(1.0, 0.6, age);
                    colour = _Young.rgb * b * (0.12 + 1.6 * pow(frac(s1 * 31.7), 4.0)) * HGArmReach(r, arm) * 1.4;
                    size *= 0.6 + 0.9 * frac(s0 * 17.3);
                }
                else if (kind < 1.5)
                {
                    // Ring: young clusters strung round the starburst ring.
                    float r = _RingRadius * (1.0 + Gauss(s2) * 0.06);
                    float angle = s0 * HG_TAU + Omega(r) * T;
                    position = float3(cos(angle) * r, Gauss(s1) * 0.006, sin(angle) * r);
                    float beat = 0.75 + 0.25 * sin(T * (0.1 + 0.25 * s1) + s2 * 40.0);
                    colour = _RingStars.rgb * (0.3 + 1.2 * pow(frac(s1 * 13.3), 2.0)) * beat;
                    size *= 1.4 + 1.2 * frac(s0 * 7.7);
                    shape.x = 0.4;
                    big = 2.0;
                }
                else if (kind < 2.5)
                {
                    // Clump: a giant knot of star formation on an arm.
                    float arm = s2 < 0.62 ? 0.0 : 1.0;
                    float r = lerp(_Bar.y * 1.1, arm < 0.5 ? 0.95 : 0.78, frac(s2 * 5.17));
                    float angle = BornOnArm(r, arm, (frac(s0 * 3.1) - 0.5) * 0.12);   // turning with the arm
                    position = float3(cos(angle) * r, Gauss(s1) * 0.01, sin(angle) * r);
                    float beat = 0.8 + 0.2 * sin(T * (0.08 + 0.15 * s1) + s0 * 30.0);
                    colour = _Clump.rgb * (0.2 + 0.9 * s1 * s1) * beat * HGArmReach(r, arm);
                    size *= 3.0 + 7.0 * pow(frac(s0 * 3.3), 2.0);
                    shape = float3(1.0, 1.0, 0.0);
                    big = 6.0;
                }
                else if (kind < 3.5)
                {
                    // Nucleus: a round swarm crowding in on the quasar, on every side of it.
                    float R = 0.004 + _NucleusReach * pow(s2, 2.0);
                    float cz = (s1 * 2.0 - 1.0);
                    float sxy = sqrt(1.0 - cz * cz);
                    float angle = s0 * HG_TAU + Omega(R) * T * 0.7;
                    position = float3(cos(angle) * sxy, cz * 0.75, sin(angle) * sxy) * R;
                    colour = _Nucleus.rgb * (0.15 + 1.1 * pow(frac(s1 * 23.3 + s0 * 7.1), 3.0));
                    size *= 0.7;
                }
                else if (kind < 4.5)
                {
                    // Disc: exponential, faint, thick.
                    float r = min(-0.26 * log(1.0 - s2 * 0.985) + 0.03, 1.05);
                    float angle = s0 * HG_TAU + Omega(r) * T;
                    float h = _Thickness.x + _Thickness.y * r;
                    position = float3(cos(angle) * r, Gauss(frac(s1 * 7.7)) * h * 0.7, sin(angle) * r);
                    colour = _DiscStars.rgb * (0.25 + 0.6 * frac(s1 * 41.3)) * smoothstep(1.05, 0.8, r);
                    size *= 0.55 + 0.3 * frac(s0 * 11.1);
                }
                else
                {
                    // Halo: globular clusters, slowly round the centre in every direction.
                    float R = 0.08 + 0.75 * pow(s2, 1.6);
                    float cz = s1 * 2.0 - 1.0;
                    float angle = s0 * HG_TAU + T * _Speed * 0.3 / R;
                    float sxy = sqrt(1.0 - cz * cz);
                    position = float3(cos(angle) * sxy, cz, sin(angle) * sxy) * R;
                    colour = _Globular.rgb * (0.5 + 0.5 * frac(s0 * 19.7));
                    size *= 2.4;
                    shape.x = 1.0;
                }

                float scale = length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));
                float3 view = mul(UNITY_MATRIX_V, mul(unity_ObjectToWorld, float4(position, 1.0))).xyz;
                float depth = -view.z;
                float tanHalf = 1.0 / unity_CameraProjection._m11;
                float maxHalf = _MaxSize * tanHalf * big;
                float half_ = min(size * scale / max(depth, 1e-4), maxHalf);
                float pixel = 2.0 * tanHalf / _ScreenParams.y;
                float wide = max(half_, 2.0 * pixel);
                colour *= (half_ / wide) * (half_ / wide);

                // The fade, star by star (see GalaxyStars_NEW).
                float fade = _Color.a;
                float turn = frac(s0 * 71.37 + s1 * 19.13 + s2 * 5.71) * 0.8;
                colour *= smoothstep(turn, turn + 0.2, fade);

                view.xy += v.corner * wide * depth;
                if (depth < 1e-3) view = float3(0.0, 0.0, 1.0);

                o.pos = mul(UNITY_MATRIX_P, float4(view, 1.0));
                o.corner = v.corner;
                o.colour = colour;
                o.shape = shape;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 c = i.corner;
                float q = dot(c, c);
                if (q >= 1.0) return 0;
                // A star sharp; a cluster or a clump soft, a clump with a rose glow round it.
                float sharp = exp(-q * 4.0);
                float soft = exp(-q * 2.6) * 0.75;
                float k = lerp(sharp, soft, i.shape.x) * (1.0 - q);
                float3 glow = float3(1.0, 0.55, 0.5) * exp(-q * 1.2) * (1.0 - q) * 0.3 * i.shape.y;
                return fixed4(i.colour * (k + glow), 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
