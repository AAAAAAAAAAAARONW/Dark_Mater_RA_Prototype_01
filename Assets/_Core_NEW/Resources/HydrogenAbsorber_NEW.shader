Shader "Custom/HydrogenAbsorber_NEW"
{
    // A cloud of cold neutral hydrogen that the light passes through (HydrogenAbsorberVFX_NEW).
    // It ABSORBS, it does not glow: the gas darkens and tints what is behind it and adds only
    // a dim magenta of its own, kept under the bloom threshold. The colour borrows the Orion
    // Nebula's magenta; the brightness does not. It is seen as a silhouette on the bright sky
    // and the quasar, and by the stars it dims.
    //
    // ANALYTIC. The sphere it is drawn on is only the bound. The ray is cut against that sphere
    // in closed form and the gas is read at five points along the cut, each two reads of the
    // shared noise sheet (QuasarNoise_NEW) — twenty texture reads a pixel, where
    // NebulaCloud_NEW, which this sits beside and does not replace, takes ninety-six. That
    // matters when the camera is inside the cloud and it covers the screen, twice over in VR.
    // Drawn from the sphere's back faces, so it works from inside too; real geometry, so both
    // eyes see it in the right place.
    //
    // LATER: IT HAS VOLUME. It was one flat amount of gas a ray, in one colour: a stain on the
    // sky, with no near side or far side. Three things give it depth, the way the cosmic web's
    // gas has it (CosmicWebPyro_NEW), and none of them reads a texture:
    //   lit from one side   The quasar is behind it (_TowardLight, from HydrogenAbsorberVFX_NEW).
    //                       How much cloud lies between a point and that light is taken from
    //                       the body's own shape, a step towards the light: the lit side is
    //                       lighter, the far side and the heart darker.
    //   silver lining       Thin gas between the eye and the light scatters it forward: the
    //                       edges facing the quasar carry a dim pink rim (_RimColor), broken
    //                       into billows by the noise already read. Dim: still no bloom.
    //   near over far       The samples are laid over one another front to back, each with
    //                       its own light, so a near lobe is seen against a far one.
    //
    // NOT ROUND. Three lumps strung along the object's x make a short streak; the big noise
    // warps them, and the edge is eaten by the noise so it tears into wisps.
    //
    // Premultiplied over what is behind. _Color fades it. In Resources so a build has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        _Appear ("Appear (HydrogenAbsorberVFX_NEW fades it in when it arrives)", Range(0, 1)) = 1
        [NoScaleOffset] _Noise ("Noise (QuasarNoise_NEW)", 2D) = "gray" {}
        _Seed ("Seed", Float) = 0
        _Opacity ("How much it hides what is behind it", Range(0, 8)) = 2.5
        _Softness ("How far the wisps reach past the body", Range(0.1, 1)) = 0.55
        _DustAmount ("Dust lanes", Range(0, 2)) = 0.6
        _Flow ("Drift, radii a second", Float) = 0.004
        _Resolve ("Nearer than x the gas is left out, from y all there (of the radius)", Vector) = (0.05, 0.3, 0, 0)
        _GasColor ("Gas (dim magenta, keep it under 1)", Color) = (0.36, 0.12, 0.28, 1)
        _DustColor ("Dust", Color) = (0.05, 0.015, 0.03, 1)
        _TowardLight ("Towards the light, in the object's space", Vector) = (0.3, 0.4, 0.85, 0)
        _RimColor ("Light scattered by the thin gas (keep it under 1)", Color) = (0.85, 0.5, 0.7, 1)
        _Scatter ("How much of it", Range(0, 2)) = 0.8
        _Shadow ("How dark the side away from the light", Range(0, 6)) = 2.5
    }

    SubShader
    {
        Tags { "Queue"="Transparent-5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Front

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _Noise;
            fixed4 _Color;
            float _Seed, _Opacity, _Softness, _DustAmount, _Flow, _Appear;
            float4 _Resolve;
            fixed4 _GasColor, _DustColor, _RimColor;
            float4 _TowardLight;
            float _Scatter, _Shadow;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 obj : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.obj = v.vertex.xyz;
                return o;
            }

            float Hash(float n) { return frac(sin(n) * 43758.5453); }

            // Three dimensions from the flat sheet: two slabs in z, each shifted by its own
            // hash, blended. Two reads, all four channels (R gas, G dust, B ridged, A fine).
            float2 Slab(float z) { return float2(Hash(z * 1.7 + 0.3), Hash(z * 2.3 + 0.9)); }
            float4 Noise4(float3 q)
            {
                float z = q.z * 6.0;
                float zi = floor(z), f = z - zi;
                f = f * f * (3.0 - 2.0 * f);
                float4 a = tex2Dlod(_Noise, float4(q.xy + Slab(zi), 0, 0));
                float4 b = tex2Dlod(_Noise, float4(q.xy + Slab(zi + 1.0), 0, 0));
                return lerp(a, b, f);
            }

            // The body: three lumps along the object's x (tipped by the seed), knocked off the
            // line. Where they are and how big is the same for every pixel, so it is worked out
            // once a pixel (Lumps) and not once a sample.
            struct Lumps { float3 c0, c1, c2; float3 r; };

            Lumps MakeLumps()
            {
                Lumps m;
                float s0 = _Seed * 3.3;
                float3 axis = normalize(float3(1.0, (Hash(s0 + 31.0) - 0.5) * 0.7, (Hash(s0 + 32.0) - 0.5) * 0.7));
                float3 c[3];
                float r[3];
                [unroll]
                for (int l = 0; l < 3; l++)
                {
                    float s = s0 + l * 11.7;
                    c[l] = axis * ((l - 1.0) * 0.38) + (float3(Hash(s + 1.0), Hash(s + 2.0), Hash(s + 3.0)) - 0.5) * 0.3;
                    r[l] = 0.3 + 0.15 * Hash(s + 4.0) + (l == 1 ? 0.1 : 0.0);
                }
                m.c0 = c[0]; m.c1 = c[1]; m.c2 = c[2];
                m.r = float3(r[0], r[1], r[2]);
                return m;
            }

            // 1 deep inside, 0 at the edge, below 0 outside; never past the bound.
            float Body(Lumps m, float3 p)
            {
                float env = max(1.0 - length(p - m.c0) / m.r.x,
                            max(1.0 - length(p - m.c1) / m.r.y, 1.0 - length(p - m.c2) / m.r.z));
                return min(env, (0.95 - length(p)) * 3.0);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 o = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                float3 d = normalize(i.obj - o);

                // The bound: a sphere of radius 1, cut in closed form.
                float b = dot(o, d);
                float c = dot(o, o) - 1.0;
                float disc = b * b - c;
                if (disc <= 0.0) return 0;
                float sq = sqrt(disc);
                float tNear = max(-b - sq, 0.0), tFar = -b + sq;
                if (tFar <= tNear) return 0;

                float3 flow = float3(0.3, 0.1, -0.2) * _Time.y * _Flow + _Seed * 0.37;
                const int Samples = 5;
                float jitter = frac(sin(dot(i.pos.xy, float2(12.9898, 78.233))) * 43758.5453);
                float dt = (tFar - tNear) / Samples;

                Lumps lumps = MakeLumps();

                // The light, and how much of it thin gas throws on towards the eye: most when
                // the eye looks towards it, through the cloud's edge.
                float3 L = normalize(_TowardLight.xyz + float3(0.0, 1e-4, 0.0));
                float toward = saturate(dot(d, L));
                float phase = 0.25 + 1.35 * toward * toward * toward;

                float through = 1.0;        // how much still comes through from behind
                float3 light = 0.0;         // what the gas itself sends

                [unroll]
                for (int k = 0; k < Samples; k++)
                {
                    float t = tNear + (k + jitter) * dt;
                    float3 p = o + d * t;

                    float4 n0 = Noise4(p * 0.7 + flow * 0.5 + 9.0);
                    float4 n1 = Noise4(p * 2.2 + flow + 3.1);
                    float3 pw = p + (n0.gba - 0.5) * 0.6;
                    float env = Body(lumps, pw);
                    float thin = saturate(1.0 - env);

                    // The thinner the body, the more of the noise is cut away: wisps at the edge.
                    float n = n0.r * 0.5 + n1.r * 0.35 + (n1.b - 0.5) * 0.4 * thin;
                    float thr = 0.2 + (0.25 + 0.35 * _Softness) * thin;
                    float gas = saturate((n - thr) * 5.0) * smoothstep(-0.4, 0.15, env);
                    float dust = smoothstep(0.58, 0.8, n1.g) * saturate(env + 0.2) * _DustAmount;

                    // Near the camera the gas is left out, so from inside it is a tint, not a wall.
                    float seen = smoothstep(_Resolve.x, _Resolve.y, t);
                    float a = 1.0 - exp(-gas * (1.0 + dust * 2.0) * seen * dt * _Opacity);

                    // How much cloud lies between here and the light: the body's own shape a
                    // step towards it. No texture read.
                    float deep = saturate(Body(lumps, pw + L * 0.3) * 1.4 + 0.2);
                    float sun = exp(-_Shadow * deep * (0.4 + 0.6 * gas));
                    float billow = 0.55 + 0.9 * n1.b;

                    float3 own = lerp(_GasColor.rgb, _DustColor.rgb, saturate(dust * 1.5));
                    float3 c = own * (0.7 + 0.6 * sun) + _RimColor.rgb * (_Scatter * phase * sun * billow) * (1.0 - 0.7 * saturate(dust));

                    light += through * a * c;
                    through *= 1.0 - a;
                }

                // Never bright enough to bloom: it is cold gas, seen by what it hides.
                float alpha = (1.0 - through) * _Color.a * _Appear;
                light = min(light, 0.9) * _Color.rgb * _Color.a * _Appear;
                return fixed4(light, alpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
