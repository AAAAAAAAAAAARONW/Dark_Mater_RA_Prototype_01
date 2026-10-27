Shader "Custom/HydrogenAbsorber_NEW"
{
    // A cloud of cold neutral hydrogen that the light passes through (HydrogenAbsorberVFX_NEW).
    // It ABSORBS, it does not glow: the gas darkens and tints what is behind it and adds only
    // a dim magenta of its own, kept under the bloom threshold. The colour borrows the Orion
    // Nebula's magenta; the brightness does not. It is seen as a silhouette on the bright sky
    // and the quasar, and by the stars it dims.
    //
    // ANALYTIC. The sphere it is drawn on is only the bound. The ray is cut against that sphere
    // in closed form and the gas is read at four points along the cut, each two reads of the
    // shared noise sheet (QuasarNoise_NEW) — sixteen texture reads a pixel, where
    // NebulaCloud_NEW, which this sits beside and does not replace, takes ninety-six. That
    // matters when the camera is inside the cloud and it covers the screen, twice over in VR.
    // Drawn from the sphere's back faces, so it works from inside too; real geometry, so both
    // eyes see it in the right place.
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
            fixed4 _GasColor, _DustColor;

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
            // line. 1 deep inside, 0 at the edge, below 0 outside; never past the bound.
            float Body(float3 p)
            {
                float s0 = _Seed * 3.3;
                float3 axis = normalize(float3(1.0, (Hash(s0 + 31.0) - 0.5) * 0.7, (Hash(s0 + 32.0) - 0.5) * 0.7));
                float env = -1.0;
                [unroll]
                for (int l = 0; l < 3; l++)
                {
                    float s = s0 + l * 11.7;
                    float along = (l - 1.0) * 0.38;
                    float3 off = (float3(Hash(s + 1.0), Hash(s + 2.0), Hash(s + 3.0)) - 0.5) * 0.3;
                    float r = 0.3 + 0.15 * Hash(s + 4.0) + (l == 1 ? 0.1 : 0.0);
                    env = max(env, 1.0 - length(p - axis * along - off) / r);
                }
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
                const int Samples = 4;
                float jitter = frac(sin(dot(i.pos.xy, float2(12.9898, 78.233))) * 43758.5453);
                float dt = (tFar - tNear) / Samples;
                float tau = 0.0;
                float dustTau = 0.0;

                [unroll]
                for (int k = 0; k < Samples; k++)
                {
                    float t = tNear + (k + jitter) * dt;
                    float3 p = o + d * t;

                    float4 n0 = Noise4(p * 0.7 + flow * 0.5 + 9.0);
                    float4 n1 = Noise4(p * 2.2 + flow + 3.1);
                    float env = Body(p + (n0.gba - 0.5) * 0.6);
                    float thin = saturate(1.0 - env);

                    // The thinner the body, the more of the noise is cut away: wisps at the edge.
                    float n = n0.r * 0.5 + n1.r * 0.35 + (n1.b - 0.5) * 0.4 * thin;
                    float thr = 0.2 + (0.25 + 0.35 * _Softness) * thin;
                    float gas = saturate((n - thr) * 5.0) * smoothstep(-0.4, 0.15, env);
                    float dust = smoothstep(0.58, 0.8, n1.g) * saturate(env + 0.2) * _DustAmount;

                    // Near the camera the gas is left out, so from inside it is a tint, not a wall.
                    float seen = smoothstep(_Resolve.x, _Resolve.y, t);
                    tau += gas * seen * dt;
                    dustTau += dust * gas * seen * dt;
                }

                float alpha = 1.0 - exp(-(tau + dustTau * 2.0) * _Opacity);
                float dustShare = saturate(dustTau / max(tau, 1e-4));
                float3 colour = lerp(_GasColor.rgb, _DustColor.rgb, dustShare);
                alpha *= _Color.a * _Appear;
                return fixed4(colour * _Color.rgb * alpha, alpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
