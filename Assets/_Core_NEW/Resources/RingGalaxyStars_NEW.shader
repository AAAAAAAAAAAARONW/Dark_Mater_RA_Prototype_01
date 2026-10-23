Shader "Custom/RingGalaxyStars_NEW"
{
    // The ring galaxy's stars (RingGalaxyVFX_NEW; its shape is in RingGalaxy_NEW.cginc), moved
    // on the GPU: each quad carries only its seeds and its kind, and the vertex shader works out
    // where its star is now.
    //
    //   0 ring       young, blue-white, along the ring and crowded into its knots, lopsided
    //                like the ring, going round with it
    //   1 glow       the knots' ionised gas: soft pink blobs, a few to each knot
    //   2 old        the old disc's stars, warm and faint, thickest round the nucleus, all
    //                through the emptied middle
    //   3 debris     faint blue stars along the spokes, the bridge and the plume, and warm ones
    //                along the intruder's tidal tail
    //   4 intruder   the companion's stars: a warm, flattened, edge-on swarm
    //   5 halo       a few old stars and clusters all round, dim
    //
    // Additive points, never thinner than two pixels, never bigger than _MaxSize of the screen,
    // so one passing close is still a star. _Color fades it star by star, as GalaxyStars_NEW does.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        _Seed ("Seed", Float) = 0
        _Turn ("Turn, radians a second", Float) = 0.004
        _Ring ("Ring: radius, half width, lopsidedness, egg-shape", Vector) = (0.72, 0.1, 0.9, 0.1)
        _Comp ("The intruder: where (of the radius), and its size", Vector) = (1.15, 0.12, 0.6, 0.26)
        _Size ("Star size (of the radius)", Float) = 0.0018
        _MaxSize ("Largest on screen, of half its height", Float) = 0.01
        [HDR] _RingStars ("Ring stars", Color) = (1.5, 1.75, 2.4, 1)
        [HDR] _Glow ("Knot glow", Color) = (1.4, 0.6, 1.1, 1)
        [HDR] _OldStars ("Old stars", Color) = (0.75, 0.6, 0.42, 1)
        [HDR] _Debris ("Spokes, bridge and plume", Color) = (0.45, 0.6, 1.0, 1)
        [HDR] _CompStars ("The intruder's stars", Color) = (1.2, 1.0, 0.75, 1)
        [HDR] _HaloStars ("Halo", Color) = (0.4, 0.35, 0.32, 1)
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
            #include "RingGalaxy_NEW.cginc"

            fixed4 _Color;
            float _Size, _MaxSize;
            float4 _RingStars, _Glow, _OldStars, _Debris, _CompStars, _HaloStars;

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
                float2 shape  : TEXCOORD2;   // x: softness; y: pink glow
            };

            float Gauss(float s) { s = clamp(s, 0.004, 0.996); return sign(s - 0.5) * sqrt(-2.0 * log(1.0 - abs(s - 0.5) * 2.0)); }

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
                float h = 0.025;

                if (kind < 0.5)
                {
                    if (s2 < 0.45)
                    {
                        // In a knot.
                        int which = (int)floor(s2 / 0.45 * RG_KNOTS) % RG_KNOTS;
                        float2 kp; float ks, kb;
                        RGKnot(which, T, kp, ks, kb);
                        float3 off = float3(Gauss(frac(s0 * 17.1 + s1 * 3.7)), Gauss(frac(s1 * 23.9 + s2 * 5.3)) * 0.8,
                                            Gauss(frac(s2 * 31.3 + s0 * 7.1))) * ks * 0.55;
                        position = float3(kp.x, 0.0, kp.y) + off;
                        colour = _RingStars.rgb * kb * (0.2 + 1.4 * pow(frac(s1 * 31.7), 4.0));
                    }
                    else
                    {
                        // Along the ring.
                        float a = s0 * RG_TAU + _Turn * T;
                        float r = RGRingR(a) + Gauss(s1) * RGWidth(a) * 0.55;
                        position = float3(cos(a) * r, Gauss(frac(s1 * 7.7)) * h * 1.8, sin(a) * r);
                        // Bunched along the ring, as its gas is, with gaps (no regular pattern).
                        float bunch = saturate(0.45 + 0.55 * sin(a * 3.0 + _Seed * 1.3) * sin(a * 7.3 + 1.7) + 0.35 * sin(a * 13.1 + 4.2));
                        colour = _RingStars.rgb * RGBright(a) * (0.1 + 1.2 * bunch) * (0.1 + 1.1 * pow(frac(s2 * 41.3), 4.0));
                    }
                    size *= 0.6 + 1.4 * pow(frac(s0 * 17.3), 3.0);
                }
                else if (kind < 1.5)
                {
                    // Knot glow: a few soft pink blobs to each knot.
                    int which = (int)floor(s2 * RG_KNOTS) % RG_KNOTS;
                    float2 kp; float ks, kb;
                    RGKnot(which, T, kp, ks, kb);
                    position = float3(kp.x, 0.0, kp.y) + float3(Gauss(frac(s0 * 17.1 + s1 * 3.7)), Gauss(frac(s1 * 23.9 + s2 * 5.3)) * 0.9, Gauss(frac(s2 * 31.3 + s0 * 7.1))) * ks * 0.45;
                    float beat = 0.85 + 0.15 * sin(T * (0.08 + 0.15 * s1) + s0 * 30.0);
                    colour = _Glow.rgb * kb * (0.08 + 0.22 * s1) * beat;
                    size = ks * (0.5 + 0.6 * frac(s0 * 3.3));
                    shape = float2(1.0, 1.0);
                    big = 6.0;
                }
                else if (kind < 2.5)
                {
                    // Old: warm, thickest round the nucleus, all through the middle.
                    float2 nuc = RGNucleus();
                    float r = min(-0.12 * log(1.0 - s2 * 0.985), 0.6);
                    float a = s0 * RG_TAU + _Turn * T * 0.8;
                    position = float3(nuc.x + cos(a) * r, Gauss(frac(s1 * 7.7)) * h * (1.0 + 2.0 * exp(-r / 0.08)), nuc.y + sin(a) * r);
                    colour = _OldStars.rgb * (0.15 + 0.45 * frac(s1 * 41.3)) * smoothstep(0.6, 0.3, r);
                    size *= 0.55 + 0.35 * frac(s0 * 11.1);
                }
                else if (kind < 3.5)
                {
                    if (s2 < 0.45)
                    {
                        // Along a spoke, from the nucleus out to the ring.
                        float2 nuc = RGNucleus();
                        float spoke = floor(s0 * 7.0);
                        float a = (spoke + 0.5) / 7.0 * RG_TAU + Gauss(s1) * 0.05 + _Turn * T * 0.8;
                        float r = lerp(0.2, 0.62, frac(s0 * 7.0));
                        position = float3(nuc.x + cos(a) * r, Gauss(frac(s1 * 5.3)) * h, nuc.y + sin(a) * r);
                        colour = _Debris.rgb * (0.15 + 0.4 * frac(s1 * 13.3));
                    }
                    else if (s2 < 0.62)
                    {
                        // Along the intruder's tidal tail.
                        float f = frac(s0 * 3.7 + s1 * 0.13);
                        position = RGTail(f) + float3(Gauss(frac(s1 * 23.9 + s2 * 5.3)), Gauss(frac(s2 * 31.3 + s0 * 7.1)) * 0.4, Gauss(frac(s0 * 17.1 + s1 * 3.7))) * 0.035 * (0.6 + f);
                        colour = _CompStars.rgb * (0.3 + 0.6 * frac(s1 * 17.3)) * (1.0 - 0.6 * f);
                    }
                    else if (s2 < 0.85)
                    {
                        // Along the bridge back to the intruder.
                        float f = frac(s0 * 3.7 + s1 * 0.13);
                        position = RGBridge(f) + float3(Gauss(frac(s1 * 23.9 + s2 * 5.3)), Gauss(frac(s2 * 31.3 + s0 * 7.1)) * 0.6, Gauss(frac(s0 * 17.1 + s1 * 3.7))) * 0.05;
                        colour = _Debris.rgb * (0.2 + 0.5 * frac(s1 * 17.3)) * (0.5 + 0.5 * sin(f * 3.14159));
                    }
                    else
                    {
                        // The plume, pulled out of the ring towards the intruder.
                        float a = RGBrightAngle() + 0.2 + Gauss(s0) * 0.4;
                        float r = RGRingR(a) + 0.05 + abs(Gauss(s1)) * 0.25;
                        position = float3(cos(a) * r, Gauss(frac(s1 * 5.3)) * h * 2.0, sin(a) * r);
                        colour = _Debris.rgb * (0.12 + 0.35 * frac(s2 * 21.7));
                    }
                    size *= 0.7 + 0.5 * frac(s0 * 9.1);
                }
                else if (kind < 4.5)
                {
                    // The intruder: a flattened swarm, edge on, bright in the middle.
                    float3 L, W, N;
                    RGCompanionFrame(L, W, N);
                    float sz = _Comp.w;
                    float g0 = Gauss(frac(s0 * 17.1 + s1 * 3.7)), g1 = Gauss(frac(s1 * 23.9 + s2 * 5.3)), g2 = Gauss(frac(s2 * 31.3 + s0 * 7.1));
                    float3 off = L * g0 * sz * 0.4 + W * g1 * sz * 0.17 + N * g2 * sz * 0.06;
                    float core = step(frac(s0 * 53.1 + s2 * 11.3), 0.3);
                    off *= lerp(1.0, 0.3, core);
                    position = _Comp.xyz + off;
                    colour = _CompStars.rgb * (0.2 + 0.7 * frac(s1 * 29.1)) * (1.0 + core);
                    size *= 0.6 + 0.5 * frac(s0 * 5.3);
                }
                else
                {
                    // Halo: round, dim.
                    float R = 0.15 + 1.05 * pow(s2, 1.5);
                    float cz = s1 * 2.0 - 1.0;
                    float angle = s0 * RG_TAU;
                    float sxy = sqrt(1.0 - cz * cz);
                    position = float3(cos(angle) * sxy, cz * 0.6, sin(angle) * sxy) * R;
                    float cluster = step(0.93, frac(s1 * 3.9));
                    colour = _HaloStars.rgb * (0.3 + 0.7 * frac(s0 * 19.7));
                    size *= 0.8 + 1.2 * cluster;
                    shape.x = cluster;
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
