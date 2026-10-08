Shader "Custom/GalaxyStars_NEW"
{
    // The Milky Way's stars (GalaxyVFX_NEW), moved on the GPU: each quad carries only its seeds
    // and its kind, and the vertex shader works out where its star is now. Every kind follows
    // the disc's warp (Galaxy_NEW.cginc), as the stars of the real one do.
    //
    //   0 young      born on the arms — where the density wave piles up gas, which is where
    //                stars form — with a soft brightening, then orbiting on the flat rotation
    //                curve (the inside turns faster), away from the arm they were born on.
    //                Each lives a while, fades, and is born again on an arm.
    //
    // The arms hold still (the frame turns with the Sun). _FrameSpin adds the Sun's own turn
    // back to every star, as GalaxyDisc_NEW does to the gas: at 1 everything goes round one way,
    // faster inside, and the galaxy reads as turning.
    //   1 bulge      old, warm, on orbits stretched along the bar, in the peanut's thickness
    //   2 old disc   faint, warm, everywhere in the thick disc, thicker outward
    //   3 globular   clusters of old stars in the halo, round fuzzy points, slowly orbiting
    //   4 supernova  on the arms; now and then one flares far brighter than the galaxy round
    //                it, with spikes, and fades from blue-white to gold over a few seconds
    //
    // Additive points, never thinner than a pixel and a half. _Color fades it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _Noise ("Noise (QuasarNoise_NEW), for the arms' wander", 2D) = "gray" {}
        _SunRadius ("The Sun's radius (of the disc's)", Float) = 0.39
        _Pitch ("Arm pitch, degrees", Float) = 12.5
        _Bar ("Bar: angle (deg), length, width", Vector) = (27, 0.24, 0.075, 0)
        _Warp ("Warp at the rim (of the radius)", Float) = 0.15
        _Speed ("Orbital speed, radii a second (flat curve)", Float) = 0.02
        _FrameSpin ("The Sun's own turn added back: 0 the Sun's frame, 1 all of it", Range(0, 1)) = 1
        _Size ("Star size (of the radius)", Float) = 0.0035
        _Thickness ("Young disc thickness (of the radius)", Float) = 0.012
        _OldThickness ("Old disc scale height at the centre, and its flare", Vector) = (0.028, 0.04, 0, 0)
        [HDR] _Young ("Young stars", Color) = (0.72, 0.84, 1.2, 1)
        [HDR] _OldStars ("Bulge stars", Color) = (1.6, 1.2, 0.75, 1)
        [HDR] _DiscStars ("Old disc stars", Color) = (0.27, 0.22, 0.16, 1)
        [HDR] _Globular ("Globular clusters", Color) = (0.42, 0.37, 0.3, 1)
        [HDR] _Supernova ("Supernovae", Color) = (6, 7, 10, 1)
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
            #include "Galaxy_NEW.cginc"

            #define TAU 6.2831853

            sampler2D _Noise;
            fixed4 _Color;
            float _SunRadius, _Pitch, _Speed, _Size, _Thickness, _MaxSize, _FrameSpin;
            float4 _Bar, _OldThickness;
            float4 _Young, _OldStars, _DiscStars, _Globular, _Supernova;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 corner : TEXCOORD0;
                float4 seed   : TEXCOORD1;   // three seeds 0..1, and the kind (0..4)
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float2 corner : TEXCOORD0;
                float3 colour : TEXCOORD1;
                float2 shape  : TEXCOORD2;   // x: spikes; y: softness (globular clusters)
            };

            // A rough Gaussian from a uniform seed (about -2.5..2.5).
            float Gauss(float s) { s = clamp(s, 0.004, 0.996); return sign(s - 0.5) * sqrt(-2.0 * log(1.0 - abs(s - 0.5) * 2.0)); }

            // Where on an arm a star at radius r is born: which arm (the two major ones twice
            // as often), following the large-scale wander the disc gives its arms.
            float ArmAngle(float r, float pick, float jitter)
            {
                float arm = floor(pick * 6.0);
                float phase = arm < 2.0 ? 1.5707963 : arm < 4.0 ? -1.5707963 : arm < 5.0 ? 0.0 : 3.1415927;
                float tanP = tan(radians(_Pitch));
                float sag = -log(0.85) / tanP;
                float born = log(r / _SunRadius) / tanP + sag + phase + jitter;
                float2 guess = float2(cos(born), sin(born)) * r;
                return born - (tex2Dlod(_Noise, float4(guess * 0.55 + float2(0.31, 0.17), 0.0, 2.0)).r - 0.5) * 0.5;
            }

            // The flat rotation curve, in the frame turning with the Sun.
            float Omega(float r) { return _Speed / max(r, 0.04) - (1.0 - _FrameSpin) * _Speed / _SunRadius; }

            v2f vert(appdata v)
            {
                v2f o;
                float s0 = v.seed.x, s1 = v.seed.y, s2 = v.seed.z;
                float kind = v.seed.w;
                float T = _Time.y;
                float3 position;
                float3 colour;
                float size = _Size;
                float2 shape = float2(0.0, 0.0);

                if (kind < 0.5)
                {
                    // Young: born on an arm, orbiting, fading.
                    float life = lerp(25.0, 70.0, s1);
                    float age = frac(s0 * 13.7 + T / life);
                    float r = lerp(0.2, 0.95, pow(frac(s2 * 7.13), 1.2));
                    float born = ArmAngle(r, frac(s2 * 3.71), (frac(s0 * 5.3) - 0.5) * 0.25);
                    float angle = born - Omega(r) * age * life;
                    float y = Gauss(frac(s1 * 9.1)) * _Thickness * 0.5;
                    position = float3(cos(angle) * r, y, sin(angle) * r);

                    // Born brightening softly — a flash on thousands of them reads as the screen
                    // blinking — then a steady glow that fades as the star leaves its arm.
                    float b = smoothstep(0.0, 0.06, age) * smoothstep(1.0, 0.6, age) * (1.0 + 1.2 * exp(-age * life / 2.5));
                    colour = _Young.rgb * b * (0.5 + 0.8 * frac(s1 * 31.7)) * smoothstep(1.0, 0.7, r);
                    size *= (0.6 + 0.9 * frac(s0 * 17.3)) * (1.0 + 0.4 * exp(-age * life / 2.0));
                }
                else if (kind < 1.5)
                {
                    // Bulge: orbits stretched along the bar, in the peanut's thickness.
                    float a = 0.015 + 0.2 * pow(s2, 1.3);
                    float angle = s0 * TAU - Omega(a) * T * 0.6;
                    float2 q = float2(cos(angle) * a, sin(angle) * a * 0.42);
                    float2 bar = float2(cos(radians(_Bar.x)), sin(radians(_Bar.x)));
                    float2 xz = float2(q.x * bar.x - q.y * bar.y, q.x * bar.y + q.y * bar.x);
                    float hz = 0.04 + 0.03 * exp(-Sq((abs(q.x) - 0.1) / 0.06));
                    position = float3(xz.x, Gauss(s1) * hz * 0.6, xz.y);
                    colour = _OldStars.rgb * (0.35 + 0.65 * frac(s1 * 23.3));
                    size *= 0.75;
                }
                else if (kind < 2.5)
                {
                    // Old disc: exponential, faint, thicker outward.
                    float r = min(-0.24 * log(1.0 - s2 * 0.985) + 0.03, 1.02);
                    float angle = s0 * TAU - Omega(r) * T;
                    float h = _OldThickness.x + _OldThickness.y * r;
                    float y = Gauss(frac(s1 * 7.7)) * h * 0.7;
                    position = float3(cos(angle) * r, y, sin(angle) * r);
                    colour = _DiscStars.rgb * (0.25 + 0.6 * frac(s1 * 41.3)) * smoothstep(1.05, 0.8, r);
                    size *= 0.55 + 0.3 * frac(s0 * 11.1);
                }
                else if (kind < 3.5)
                {
                    // Globular clusters: round the centre in every direction, slowly.
                    float R = 0.06 + 0.7 * pow(s2, 1.6);
                    float cz = s1 * 2.0 - 1.0;
                    float angle = s0 * TAU + T * _Speed * 0.3 / R;
                    float sxy = sqrt(1.0 - cz * cz);
                    position = float3(cos(angle) * sxy, cz, sin(angle) * sxy) * R;
                    colour = _Globular.rgb * (0.5 + 0.5 * frac(s0 * 19.7));
                    size *= 2.4;
                    shape.y = 1.0;
                }
                else
                {
                    // Supernova: a point on an arm that now and then flares.
                    float r = lerp(0.22, 0.9, s2);
                    float angle = ArmAngle(r, frac(s2 * 3.71), (frac(s0 * 5.3) - 0.5) * 0.2);
                    float y = Gauss(frac(s1 * 9.1)) * _Thickness * 0.3;
                    position = float3(cos(angle) * r, y, sin(angle) * r);

                    float period = lerp(45.0, 120.0, s1);   // rare: a supernova is an event
                    float since = frac(T / period + s0) * period;          // seconds since it went off
                    float flare = smoothstep(0.0, 0.12, since) * exp(-since / 1.1) + 0.25 * exp(-since / 4.0) * smoothstep(0.0, 0.12, since);
                    float3 tint = lerp(float3(1.0, 0.75, 0.45), float3(1.0, 1.0, 1.0), exp(-since / 1.5));
                    colour = _Supernova.rgb * tint * flare;
                    size *= 1.0 + 6.0 * flare;
                    shape.x = saturate(flare * 3.0);
                }

                position.y += GalaxyWarp(position.xz);

                float scale = length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));
                float3 view = mul(UNITY_MATRIX_V, mul(unity_ObjectToWorld, float4(position, 1.0))).xyz;
                float depth = -view.z;
                float tanHalf = 1.0 / unity_CameraProjection._m11;
                float maxHalf = _MaxSize * tanHalf * (shape.x > 0.0 ? 5.0 : 1.0);
                float half_ = min(size * scale / max(depth, 1e-4), maxHalf);
                float pixel = 2.0 * tanHalf / _ScreenParams.y;
                float wide = max(half_, 1.5 * pixel);
                colour *= (half_ / wide) * (half_ / wide);

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
                float k = lerp(exp(-q * 4.0), exp(-q * 2.2) * 0.7, i.shape.y) * (1.0 - q);
                // A supernova's spikes, as a telescope draws a star far brighter than its field.
                float spikes = (exp(-abs(c.y) * 40.0) + exp(-abs(c.x) * 40.0)) * (1.0 - sqrt(q)) * 0.6;
                k = k * (1.0 - 0.75 * i.shape.x) + spikes * i.shape.x + exp(-q * 30.0) * i.shape.x;
                return fixed4(i.colour * k * _Color.rgb, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
