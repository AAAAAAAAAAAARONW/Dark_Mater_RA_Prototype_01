Shader "Custom/PurpleCloudVolume"
{
    Properties
    {
        [Header(Volume)]
        _Steps ("Raymarch Steps", Range(16, 128)) = 32
        _StepWorldLength ("Step World Length", Range(0.01, 0.2)) = 0.08
        _Density ("Density", Range(0.1, 6.0)) = 1.9
        _Absorption ("Absorption", Range(0.2, 6.0)) = 1.4
        _Emission ("Emission", Range(0.1, 6.0)) = 1.3
        _Opacity ("Opacity", Range(0.05, 1.0)) = 0.86
        _EdgeFade ("Edge Fade", Range(0.5, 8.0)) = 2.6

        [Header(Cloud Shape)]
        _FeatureSize ("Feature Size", Range(0.1, 4.0)) = 1.0
        _DetailScale ("Detail Scale", Range(1.0, 8.0)) = 3.0
        _DetailWeight ("Detail Weight", Range(0.0, 1.0)) = 0.45
        _CloudSoftness ("Cloud Softness", Range(0.2, 3.0)) = 1.35
        _Coverage ("Coverage", Range(0.0, 1.0)) = 0.56
        _InnerGlow ("Inner Glow", Range(0.0, 2.0)) = 0.7
        _Erode ("Erode Amount", Range(0.0, 1.0)) = 0.38
        _EdgeWidth ("Edge Width", Range(0.02, 0.5)) = 0.24
        _AlphaFog ("Alpha Fog Response", Range(0.5, 3.0)) = 1.25
        _BoundaryBreakup ("Boundary Breakup", Range(0.0, 1.0)) = 0.55

        [Header(Purple Palette)]
        _BaseColor ("Base Purple", Color) = (0.20, 0.10, 0.42, 1)
        _MidColor ("Mid Purple", Color) = (0.48, 0.26, 0.78, 1)
        _HighlightColor ("Highlight Lavender", Color) = (0.78, 0.60, 0.98, 1)
        _RimColor ("Rim Magenta", Color) = (0.90, 0.35, 0.86, 1)

        [Header(Static Variation)]
        _SeedOffset ("Seed Offset", Vector) = (3.2, -1.7, 5.4, 0)
        _Anisotropy ("Vertical Stretch", Range(0.3, 2.0)) = 1.05

        [Header(Noise Texture)]
        // Assign via: Tools > Laxi > Bake Cloud Noise Texture (32^3)
        [NoScaleOffset] _NoiseTex ("Noise Texture 3D", 3D) = "" {}
    }

    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Steps;
            float _StepWorldLength;
            float _Density;
            float _Absorption;
            float _Emission;
            float _Opacity;
            float _EdgeFade;

            float _FeatureSize;
            float _DetailScale;
            float _DetailWeight;
            float _CloudSoftness;
            float _Coverage;
            float _InnerGlow;
            float _Erode;
            float _EdgeWidth;
            float _AlphaFog;
            float _BoundaryBreakup;

            float4 _BaseColor;
            float4 _MidColor;
            float4 _HighlightColor;
            float4 _RimColor;

            float4 _SeedOffset;
            float  _Anisotropy;

            sampler3D _NoiseTex;

            // ---------------------------------------------------------------------------
            // Texture-based noise  (32^3 RGBA32, baked by BakeCloudNoise.cs)
            //
            // Channel layout (optimised for minimum per-step fetches):
            //   R — 3-octave FBM pre-baked (1x+2x+4x, normalised to [0,1])
            //   G — erosion single-octave   (seed +17,+13,+7)
            //   B — breakup single-octave   (seed -11,+31,-19)
            //   A — 2-octave detail FBM at 3x internal scale (decorrelated from R)
            //
            // Per-step fetch budget (after optimisation):
            //   Early-exit path : 1 fetch  (was 3)
            //   Dense path      : 4 fetches (was 6)
            //
            // NTILE_INV = 1/32 maps world-space coords to [0,1]^3 texture UVs.
            // 32^3 RGBA32 = 128 KB — fits in GPU L2 texture cache.
            // After changing this shader re-bake via: Tools > Laxi > Bake Cloud Noise Texture (32^3)
            // ---------------------------------------------------------------------------
            #define NTILE_INV 0.03125   // 1.0 / 32.0

            float4 sampleNoise(float3 p)   { return tex3D(_NoiseTex, frac(p * NTILE_INV)); }
            float  noiseR(float3 p)        { return sampleNoise(p).r; }
            float  noiseG(float3 p)        { return sampleNoise(p).g; }
            float  noiseB(float3 p)        { return sampleNoise(p).b; }
            float  noiseA(float3 p)        { return sampleNoise(p).a; }

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos       : SV_POSITION;
                float3 rayOrigin : TEXCOORD0;
                float3 rayDir    : TEXCOORD1;
            };

            void rayBox(float3 ro, float3 rd, out float tNear, out float tFar)
            {
                float3 rdSafe = rd;
                if (abs(rdSafe.x) < 1e-6) rdSafe.x = 1e-6 * sign(rdSafe.x + 1e-7);
                if (abs(rdSafe.y) < 1e-6) rdSafe.y = 1e-6 * sign(rdSafe.y + 1e-7);
                if (abs(rdSafe.z) < 1e-6) rdSafe.z = 1e-6 * sign(rdSafe.z + 1e-7);

                float3 t0 = (float3(-0.5, -0.5, -0.5) - ro) / rdSafe;
                float3 t1 = (float3( 0.5,  0.5,  0.5) - ro) / rdSafe;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);

                tNear = max(max(tmin.x, tmin.y), tmin.z);
                tFar  = min(min(tmax.x, tmax.y), tmax.z);

                if (tNear > tFar) tFar = tNear;
                if (tFar  < 0.0) { tNear = 1e6; tFar = 0.0; return; }
                if (tNear < 0.0) tNear = 0.0;
            }

            // earlyExitThr: precomputed once in frag(), avoids recomputing per step.
            float sampleCloudDensity(float3 pObj, float3 pMeters, float invFeatureSize,
                                     float earlyExitThr, out float lum)
            {
                float3 q = pMeters * invFeatureSize;
                q.y *= _Anisotropy;
                q += _SeedOffset.xyz;

                // FETCH 0: pre-baked 3-octave FBM from R channel (was 3 fetches via fbm3).
                float macro = noiseR(q * 0.85);

                // Early exit for empty space — skips 3 remaining fetches.
                [branch]
                if (macro < earlyExitThr)
                {
                    lum = 0.0;
                    return 0.0;
                }

                // FETCH 1: detail from A channel (pre-baked 2-octave FBM at ~3x scale).
                // A is baked at 3x internal frequency, so sample at q*(_DetailScale/3)
                // to keep the same apparent detail scale as the old R-channel path.
                float detail = noiseA(q * (_DetailScale * 0.333) + float3(4.1, -2.5, 7.3));

                float baseShape = macro * 0.95 + detail * 0.55;
                baseShape = saturate((baseShape - (0.34 + _Coverage * 0.38)) * (1.8 + _CloudSoftness));

                // FETCH 2: erosion (G channel = independent seed)
                float erosion   = noiseG(q * (_DetailScale * 2.1) + float3(-6.2, 5.4, -3.8));
                float erodeMask = saturate((erosion - (0.40 + _Erode * 0.35)) * 3.0);
                baseShape *= lerp(1.0, erodeMask, _Erode);

                float3 absP       = abs(pObj.xyz);
                float  maxAxis    = max(absP.x, max(absP.y, absP.z));
                float  toBoundary = saturate((0.5 - maxAxis) / max(0.001, _EdgeWidth));

                // FETCH 3: breakup (B channel = independent seed)
                float breakupNoise = noiseB(q * 0.63 + float3(12.3, -4.2, 8.7));
                float breakup = saturate(lerp(1.0, breakupNoise, _BoundaryBreakup));

                float edge    = pow(saturate(toBoundary * breakup), _EdgeFade);
                float density = pow(saturate(baseShape * edge), _CloudSoftness);

                float radial = saturate(length(pObj.xyz) / 0.95);
                float inner  = saturate(1.0 - radial * radial);
                lum = saturate(inner * 0.65 + detail * 0.35);
                return density;
            }

            float3 sampleCloudColor(float density, float lum)
            {
                float midMask  = saturate(density * 1.2  + lum * 0.35);
                float highMask = saturate((density - 0.28) * 1.85 + lum * 0.62);
                float rimMask  = saturate((1.0 - density) * 0.7 + (1.0 - lum) * 0.4);

                float3 col = lerp(_BaseColor.rgb, _MidColor.rgb, midMask);
                col = lerp(col, _HighlightColor.rgb, highMask);
                col += _RimColor.rgb * rimMask * 0.2;
                return col;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.pos       = mul(UNITY_MATRIX_VP, worldPos);
                o.rayOrigin = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                o.rayDir    = normalize(v.vertex.xyz - o.rayOrigin);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 ro = i.rayOrigin;
                float3 rd = normalize(i.rayDir);

                float tNear, tFar;
                rayBox(ro, rd, tNear, tFar);
                if (tNear >= tFar || tFar <= 0.0)
                    return fixed4(0, 0, 0, 0);

                float travel = tFar - tNear;

                float3 pObjNear   = ro + rd * tNear;
                float3 pObjFar    = ro + rd * tFar;
                float3 pWorldNear = mul(unity_ObjectToWorld, float4(pObjNear, 1.0)).xyz;
                float3 pWorldFar  = mul(unity_ObjectToWorld, float4(pObjFar,  1.0)).xyz;
                float  worldTravel = length(pWorldFar - pWorldNear);

                // _Steps is a HARD MAXIMUM.  stepsByDistance adapts to short ray paths
                // (avoids wasting steps when the ray barely clips the volume).
                // Previously max() let stepsByDistance override _Steps → 120+ steps on
                // large volumes → the main cause of the 9–30 fps variance.
                int stepsByDistance = (int)ceil(worldTravel / max(0.01, _StepWorldLength));
                int steps     = clamp(stepsByDistance, 10, (int)_Steps);
                float stepLen = travel / (float)steps;

                // Jitter ray start to break up banding and allow lower step counts.
                float jitter = frac(sin(dot(i.pos.xy, float2(12.9898, 78.233))) * 43758.5453);
                tNear += jitter * stepLen;

                float3 centerW = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float3 axisX   = normalize(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));
                float3 axisY   = normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 1, 0)));
                float3 axisZ   = normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 0, 1)));

                // Linearize pMeters — precompute base + per-step delta to avoid matrix
                // multiply and 3 dot products inside the loop.
                float3 pObjBase   = ro + rd * (tNear + 0.5 * stepLen);
                float3 pObjStep   = rd * stepLen;
                float3 pWorldBase = mul(unity_ObjectToWorld, float4(pObjBase, 1.0)).xyz;
                float3 pWorldStep = mul((float3x3)unity_ObjectToWorld, pObjStep);
                float3 offsetBase = pWorldBase - centerW;
                float3 pMetersBase = float3(dot(offsetBase, axisX),
                                            dot(offsetBase, axisY),
                                            dot(offsetBase, axisZ));
                float3 pMetersStep = float3(dot(pWorldStep, axisX),
                                            dot(pWorldStep, axisY),
                                            dot(pWorldStep, axisZ));

                float invFeatureSize  = rcp(max(_FeatureSize, 0.001));
                float absorbFactor    = _Absorption * stepLen * 2.2;
                float emissiveBase    = 0.35 * _Emission;
                float emissiveLumMul  = _InnerGlow * _Emission;
                // Precompute early-exit threshold (moved out of per-step branch math)
                float earlyExitThr    = ((0.34 + _Coverage * 0.38) - 0.4 * rcp(1.8 + _CloudSoftness)) / 0.95;

                float3 accColor = 0.0;
                float  accAlpha = 0.0;

                // Dynamic upper bound avoids wasting iterations beyond actual step count.
                // Clamped to 128 at the property level so the compiler can still bound
                // the loop statically for platforms that require it.
                [loop]
                for (int s = 0; s < steps; s++)
                {
                    float3 pObj    = pObjBase + pObjStep * s;
                    float3 pMeters = pMetersBase + pMetersStep * s;

                    float lum;
                    float d = sampleCloudDensity(pObj, pMeters, invFeatureSize, earlyExitThr, lum) * _Density;

                    [branch]
                    if (d < 0.001) continue;

                    float3 cloudCol  = sampleCloudColor(d, lum);
                    float  alphaStep = 1.0 - exp(-d * absorbFactor);
                    float  emissive  = emissiveBase + lum * emissiveLumMul;

                    float transmit   = 1.0 - accAlpha;
                    accColor += transmit * alphaStep * cloudCol * emissive;
                    accAlpha += transmit * alphaStep;
                    if (accAlpha > 0.985) break;
                }

                accAlpha = 1.0 - exp(-accAlpha * _AlphaFog);
                accAlpha = saturate(accAlpha * _Opacity);
                return fixed4(accColor, accAlpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
