Shader "Custom/GalaxyStars_NEW"
{
    // The Milky Way's bright stars (GalaxyVFX_NEW), moved on the GPU: each quad carries only its
    // seeds and the vertex shader works out where its star is now.
    //
    // Young stars are born on the arms — where the density wave piles up gas, which is where
    // stars form — at a random point of a random arm, and then orbit on the galaxy's flat
    // rotation curve (the same speed at every radius, so the inside turns faster). The frame
    // turns with the Sun, as the disc's arms are drawn in: stars inside the Sun's orbit pull
    // ahead of the arms, stars outside fall behind. Each lives a while and fades, and is born
    // again on an arm, so the arms stay lit by their own young stars.
    //
    // Bulge stars are old, warm, and orbit the centre in a thick cloud.
    //
    // Additive points, never thinner than a pixel and a half. _Color fades it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _Noise ("Noise (QuasarNoise_NEW), for the arms' wander", 2D) = "gray" {}
        _SunRadius ("The Sun's radius (of the disc's)", Float) = 0.39
        _Pitch ("Arm pitch, degrees", Float) = 12.5
        _Speed ("Orbital speed, radii a second (flat curve)", Float) = 0.004
        _Size ("Star size (of the radius)", Float) = 0.0035
        _Thickness ("Disc thickness (of the radius)", Float) = 0.012
        [HDR] _Young ("Young stars", Color) = (1.2, 1.4, 2.0, 1)
        [HDR] _OldStars ("Bulge stars", Color) = (1.6, 1.2, 0.75, 1)
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

            #define TAU 6.2831853

            sampler2D _Noise;
            fixed4 _Color;
            float _SunRadius, _Pitch, _Speed, _Size, _Thickness, _MaxSize;
            float4 _Young, _OldStars;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 corner : TEXCOORD0;
                float4 seed   : TEXCOORD1;   // three seeds 0..1, and the kind: 0 young, 1 bulge
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float2 corner : TEXCOORD0;
                float3 colour : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float s0 = v.seed.x, s1 = v.seed.y, s2 = v.seed.z;
                float T = _Time.y;
                float3 position;
                float3 colour;
                float size = _Size;

                if (v.seed.w < 0.5)
                {
                    // Born on an arm: which arm (the two major ones twice as often), how far out.
                    float life = lerp(25.0, 70.0, s1);
                    float age = frac(s0 * 13.7 + T / life);
                    float r = lerp(0.2, 0.95, pow(frac(s2 * 7.13), 0.8));
                    float arm = floor(frac(s2 * 3.71) * 6.0);
                    float phase = arm < 2.0 ? 1.5707963 : arm < 4.0 ? -1.5707963 : arm < 5.0 ? 0.0 : 3.1415927;
                    float tanP = tan(radians(_Pitch));
                    float sag = -log(0.85) / tanP;
                    float born = log(r / _SunRadius) / tanP + sag + phase + (frac(s0 * 5.3) - 0.5) * 0.25;

                    // The disc lets its arms wander on a large scale; follow them, so the stars
                    // are born on the arm that is drawn and not on the compass curve beside it.
                    float2 guess = float2(cos(born), sin(born)) * r;
                    born -= (tex2Dlod(_Noise, float4(guess * 0.55 + float2(0.31, 0.17), 0.0, 2.0)).r - 0.5) * 0.5;

                    // A flat rotation curve, in the frame turning with the Sun: ahead inside its
                    // orbit, behind outside. Trailing arms wind outward against the turn.
                    float omega = _Speed / r - _Speed / _SunRadius;
                    float angle = born - omega * age * life;
                    float y = (frac(s1 * 9.1) - 0.5) * _Thickness;
                    position = float3(cos(angle) * r, y, sin(angle) * r);

                    float b = smoothstep(0.0, 0.1, age) * smoothstep(1.0, 0.6, age);
                    colour = _Young.rgb * b * (0.5 + 0.8 * frac(s1 * 31.7));
                    size *= 0.6 + 0.9 * frac(s0 * 17.3);
                }
                else
                {
                    // The bulge: old stars in a thick cloud round the centre.
                    float r = 0.02 + 0.16 * pow(s2, 1.5);
                    float angle = s0 * TAU - (_Speed / max(r, 0.05) - _Speed / _SunRadius) * T;
                    float y = (s1 - 0.5) * r * 0.9;
                    position = float3(cos(angle) * r, y, sin(angle) * r);
                    colour = _OldStars.rgb * (0.4 + 0.6 * frac(s1 * 23.3));
                    size *= 0.8;
                }

                float scale = length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));
                float3 view = mul(UNITY_MATRIX_V, mul(unity_ObjectToWorld, float4(position, 1.0))).xyz;
                float depth = -view.z;
                float tanHalf = 1.0 / unity_CameraProjection._m11;
                float half_ = min(size * scale / max(depth, 1e-4), _MaxSize * tanHalf);
                float pixel = 2.0 * tanHalf / _ScreenParams.y;
                float wide = max(half_, 1.5 * pixel);
                colour *= (half_ / wide) * (half_ / wide);

                view.xy += v.corner * wide * depth;
                if (depth < 1e-3) view = float3(0.0, 0.0, 1.0);

                o.pos = mul(UNITY_MATRIX_P, float4(view, 1.0));
                o.corner = v.corner;
                o.colour = colour;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float q = dot(i.corner, i.corner);
                if (q >= 1.0) return 0;
                float k = exp(-q * 4.0) * (1.0 - q);
                return fixed4(i.colour * k * _Color.rgb, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
