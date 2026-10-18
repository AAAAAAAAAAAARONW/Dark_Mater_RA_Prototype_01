Shader "Custom/YoungGalaxyStars_NEW"
{
    // The young galaxy's stars (YoungGalaxyVFX_NEW), moved on the GPU: each quad carries only
    // its seeds and its kind, and the vertex shader works out where its star is now.
    //
    //   0 clump    young, blue-white, crowded into the giant clumps (YoungGalaxy_NEW.cginc),
    //              going round with them
    //   1 knot     smaller knots of star formation through the gas: soft magenta-white blobs
    //   2 disc     the thick disc's stars, faint and violet, everywhere
    //   3 stream   stars forming along the cold streams, faint and blue-green, out past the edge
    //   4 halo     old stars and clusters in a round halo, dim and warm
    //
    // Everything goes round on a flat rotation curve (faster inside). Additive points, never
    // thinner than two pixels, never bigger than _MaxSize of the screen, so one passing close
    // is still a star. _Color fades it star by star, as GalaxyStars_NEW does.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        _Seed ("Seed", Float) = 0
        _ClumpTurn ("Clump turn, radians a second at radius 0.5", Float) = 0.01
        _Thickness ("Disc scale height at the centre, and its growth outward", Vector) = (0.06, 0.07, 0, 0)
        _Streams ("Cold streams, 0 to 3", Float) = 2
        _Speed ("Orbital speed, radii a second (flat curve)", Float) = 0.01
        _Size ("Star size (of the radius)", Float) = 0.0018
        _MaxSize ("Largest on screen, of half its height", Float) = 0.01
        [HDR] _ClumpStars ("Clump stars", Color) = (1.5, 1.7, 2.3, 1)
        [HDR] _Knot ("Knots", Color) = (1.6, 0.8, 1.2, 1)
        [HDR] _DiscStars ("Disc stars", Color) = (0.7, 0.7, 1.1, 1)
        [HDR] _StreamStars ("Stream stars", Color) = (0.5, 0.85, 0.9, 1)
        [HDR] _HaloStars ("Halo stars", Color) = (0.4, 0.33, 0.3, 1)
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
            #include "YoungGalaxy_NEW.cginc"

            fixed4 _Color;
            float _Speed, _Size, _MaxSize;
            float4 _ClumpStars, _Knot, _DiscStars, _StreamStars, _HaloStars;

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
                float3 colour : TEXCOORD1;   // with the fade in it
                float2 shape  : TEXCOORD2;   // x: softness; y: magenta glow
            };

            float Gauss(float s) { s = clamp(s, 0.004, 0.996); return sign(s - 0.5) * sqrt(-2.0 * log(1.0 - abs(s - 0.5) * 2.0)); }

            // The flat rotation curve, radians a second.
            float Omega(float r) { return _Speed / max(r, 0.03); }

            v2f vert(appdata v)
            {
                v2f o;
                float s0 = v.seed.x, s1 = v.seed.y, s2 = v.seed.z;
                float kind = v.seed.w;
                float T = _Time.y;
                float3 position;
                float3 colour;
                float size = _Size;
                float2 shape = 0.0;
                float big = 1.0;

                if (kind < 0.5)
                {
                    // Clump: in one of the giant clumps, going round with it.
                    int which = (int)floor(s2 * YG_CLUMPS) % YG_CLUMPS;
                    float3 c;
                    float rad, b;
                    YGClump(which, c, rad, b);
                    float3 off = float3(Gauss(frac(s0 * 17.1 + s1 * 3.7)), Gauss(frac(s1 * 23.9 + s2 * 5.3)) * 0.6, Gauss(frac(s2 * 31.3 + s0 * 7.1))) * rad * 0.45;
                    position = c + off;
                    colour = _ClumpStars.rgb * b * (0.15 + 1.5 * pow(frac(s1 * 31.7), 4.0));
                    size *= 0.7 + 0.9 * frac(s0 * 17.3);
                }
                else if (kind < 1.5)
                {
                    // Knot: a small knot of star formation in the gas, soft, with a glow.
                    float r = 0.1 + 0.85 * pow(s2, 0.9);
                    float angle = s0 * YG_TAU + Omega(r) * T;
                    position = float3(cos(angle) * r, Gauss(s1) * YGHeight(r) * 0.4, sin(angle) * r);
                    float beat = 0.8 + 0.2 * sin(T * (0.08 + 0.15 * s1) + s0 * 30.0);
                    colour = _Knot.rgb * (0.25 + 0.9 * s1 * s1) * beat * smoothstep(1.05, 0.8, r);
                    size *= 2.5 + 5.0 * pow(frac(s0 * 3.3), 2.0);
                    shape = float2(1.0, 1.0);
                    big = 5.0;
                }
                else if (kind < 2.5)
                {
                    // Disc: exponential, thick, faint.
                    float r = min(-0.3 * log(1.0 - s2 * 0.985) + 0.02, 1.1);
                    float angle = s0 * YG_TAU + Omega(r) * T;
                    position = float3(cos(angle) * r, Gauss(frac(s1 * 7.7)) * YGHeight(r) * 0.7, sin(angle) * r);
                    colour = _DiscStars.rgb * (0.25 + 0.6 * frac(s1 * 41.3)) * smoothstep(1.1, 0.8, r);
                    size *= 0.55 + 0.3 * frac(s0 * 11.1);
                }
                else if (kind < 3.5)
                {
                    // Stream: along one of the cold streams, out past the edge.
                    int k = (int)floor(s2 * 3.0) % 3;
                    float r = lerp(0.35, 1.9, frac(s2 * 5.17));
                    float w = 0.025 + 0.045 * r;
                    float angle = YGStreamAngle(r, k) + Gauss(s0) * w / r * 0.7;
                    position = float3(cos(angle) * r, Gauss(s1) * (0.03 + 0.03 * r) * 0.6, sin(angle) * r);
                    float on = saturate(_Streams - k) * smoothstep(0.3, 0.6, r) * smoothstep(2.0, 1.4, r);
                    colour = _StreamStars.rgb * (0.3 + 0.8 * frac(s1 * 13.3)) * on;
                    size *= 0.7 + 0.5 * frac(s0 * 9.1);
                }
                else
                {
                    // Halo: round, dim, slow.
                    float R = 0.1 + 0.9 * pow(s2, 1.5);
                    float cz = s1 * 2.0 - 1.0;
                    float angle = s0 * YG_TAU + T * _Speed * 0.3 / R;
                    float sxy = sqrt(1.0 - cz * cz);
                    position = float3(cos(angle) * sxy, cz * 0.8, sin(angle) * sxy) * R;
                    colour = _HaloStars.rgb * (0.3 + 0.7 * frac(s0 * 19.7));
                    size *= 0.8 + 1.2 * step(0.93, frac(s1 * 3.9));   // a few clusters, bigger
                    shape.x = step(0.93, frac(s1 * 3.9));
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
                float sharp = exp(-q * 4.0);
                float soft = exp(-q * 2.6) * 0.75;
                float k = lerp(sharp, soft, i.shape.x) * (1.0 - q);
                float3 glow = float3(1.0, 0.45, 0.7) * exp(-q * 1.2) * (1.0 - q) * 0.3 * i.shape.y;
                return fixed4(i.colour * (k + glow), 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
