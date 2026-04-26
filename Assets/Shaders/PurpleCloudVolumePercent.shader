Shader "Custom/PurpleCloudVolumePercent"
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
        _CloudSoftness ("Cloud Softness", Range(0.2, 3.0)) = 1.35
        _Coverage ("Coverage", Range(0.0, 1.0)) = 0.56
        _InnerGlow ("Inner Glow", Range(0.0, 2.0)) = 0.7
        _Erode ("Erode Amount", Range(0.0, 1.0)) = 0.38
        _EdgeWidth ("Edge Width", Range(0.02, 0.5)) = 0.24
        _AlphaFog ("Alpha Fog Response", Range(0.5, 3.0)) = 1.25
        _BoundaryBreakup ("Boundary Breakup", Range(0.0, 1.0)) = 0.55

        [Header(Percent Selection)]
        _RenderPercent ("Render Percent (0-1)", Range(0.0, 1.0)) = 0.3
        _PercentColor ("Percent Color", Color) = (1.0, 0.25, 0.9, 1.0)
        _PercentSeedOffset ("Percent Seed Offset", Vector) = (9.3, -3.1, 2.7, 0)

        [Header(Noise Texture)]
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
            float _CloudSoftness;
            float _Coverage;
            float _InnerGlow;
            float _Erode;
            float _EdgeWidth;
            float _AlphaFog;
            float _BoundaryBreakup;

            float _RenderPercent;
            float4 _PercentColor;
            float4 _PercentSeedOffset;

            sampler3D _NoiseTex;

            #define NTILE_INV 0.03125

            float4 sampleNoise(float3 p) { return tex3D(_NoiseTex, frac(p * NTILE_INV)); }
            float noiseR(float3 p) { return sampleNoise(p).r; }
            float noiseG(float3 p) { return sampleNoise(p).g; }
            float noiseB(float3 p) { return sampleNoise(p).b; }
            float noiseA(float3 p) { return sampleNoise(p).a; }

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 rayOrigin : TEXCOORD0;
                float3 rayDir : TEXCOORD1;
            };

            void rayBox(float3 ro, float3 rd, out float tNear, out float tFar)
            {
                float3 rdS = rd;
                if (abs(rdS.x) < 1e-6) rdS.x = 1e-6 * sign(rdS.x + 1e-7);
                if (abs(rdS.y) < 1e-6) rdS.y = 1e-6 * sign(rdS.y + 1e-7);
                if (abs(rdS.z) < 1e-6) rdS.z = 1e-6 * sign(rdS.z + 1e-7);
                float3 t0 = (float3(-0.5, -0.5, -0.5) - ro) / rdS;
                float3 t1 = (float3( 0.5,  0.5,  0.5) - ro) / rdS;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);
                tNear = max(max(tmin.x, tmin.y), tmin.z);
                tFar  = min(min(tmax.x, tmax.y), tmax.z);
                if (tNear > tFar) tFar = tNear;
                if (tFar < 0.0) { tNear = 1e6; tFar = 0.0; return; }
                if (tNear < 0.0) tNear = 0.0;
            }

            float sampleCloudDensity(float3 pObj, float3 pWorld, float invFeatureSize, float earlyExitThr, out float lum)
            {
                float3 q = pWorld * invFeatureSize;
                q += _PercentSeedOffset.xyz;

                float macro = noiseR(q * 0.85);
                if (macro < earlyExitThr)
                {
                    lum = 0.0;
                    return 0.0;
                }

                float detail = noiseA(q * (_DetailScale * 0.333) + float3(4.1, -2.5, 7.3));
                float baseShape = saturate(
                    (macro * 0.95 + detail * 0.55 - (0.34 + _Coverage * 0.38))
                    * (1.8 + _CloudSoftness));

                float erosion = noiseG(q * (_DetailScale * 2.1) + float3(-6.2, 5.4, -3.8));
                baseShape *= lerp(1.0, saturate((erosion - (0.40 + _Erode * 0.35)) * 3.0), _Erode);

                float3 absP = abs(pObj.xyz);
                float maxAxis = max(absP.x, max(absP.y, absP.z));
                float toBoundary = saturate((0.5 - maxAxis) / max(0.001, _EdgeWidth));
                float breakup = saturate(lerp(1.0, noiseB(q * 0.63 + float3(12.3, -4.2, 8.7)), _BoundaryBreakup));
                float edge = pow(saturate(toBoundary * breakup), _EdgeFade);
                float density = pow(saturate(baseShape * edge), _CloudSoftness);

                float inner = saturate(1.0 - dot(pObj, pObj) / (0.95 * 0.95));
                lum = saturate(inner * 0.65 + detail * 0.35);
                return density;
            }

            // 从空间噪声中抽样一个 0~1，决定当前采样点是否属于“要渲染的百分比”
            float getPercentMask(float3 pWorld)
            {
                float n = noiseG(pWorld * 0.11 + _PercentSeedOffset.xyz * 0.37);
                return step(n, saturate(_RenderPercent));
            }

            v2f vert(appdata v)
            {
                v2f o;
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.pos = mul(UNITY_MATRIX_VP, worldPos);
                o.rayOrigin = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                o.rayDir = normalize(v.vertex.xyz - o.rayOrigin);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 ro = i.rayOrigin;
                float3 rd = normalize(i.rayDir);

                float tNear, tFar;
                rayBox(ro, rd, tNear, tFar);
                if (tNear >= tFar || tFar <= 0.0) return fixed4(0, 0, 0, 0);

                float travel = tFar - tNear;
                float3 pObjNear = ro + rd * tNear;
                float3 pObjFar = ro + rd * tFar;
                float3 pWorldNear = mul(unity_ObjectToWorld, float4(pObjNear, 1.0)).xyz;
                float3 pWorldFar = mul(unity_ObjectToWorld, float4(pObjFar, 1.0)).xyz;
                float worldTravel = length(pWorldFar - pWorldNear);

                int stepsByDist = (int)ceil(worldTravel / max(0.01, _StepWorldLength));
                int steps = clamp(stepsByDist, 10, (int)_Steps);
                float stepLen = travel / (float)steps;

                float jitter = frac(sin(dot(i.pos.xy, float2(12.9898, 78.233))) * 43758.5453);
                tNear += jitter * stepLen;

                float3 pObjBase = ro + rd * (tNear + 0.5 * stepLen);
                float3 pObjStep = rd * stepLen;
                float3 pWorldBase = mul(unity_ObjectToWorld, float4(pObjBase, 1.0)).xyz;
                float3 pWorldStep = mul((float3x3)unity_ObjectToWorld, pObjStep);

                float invFeatureSize = rcp(max(_FeatureSize, 0.001));
                float absorbFactor = _Absorption * stepLen * 2.2;
                float emissiveBase = 0.35 * _Emission;
                float emissiveLumMul = _InnerGlow * _Emission;
                float earlyExitThr = ((0.34 + _Coverage * 0.38) - 0.4 * rcp(1.8 + _CloudSoftness)) / 0.95;

                float3 accColor = 0;
                float accAlpha = 0;

                [loop]
                for (int s = 0; s < steps; s++)
                {
                    float3 pObj = pObjBase + pObjStep * s;
                    float3 pWorld = pWorldBase + pWorldStep * s;

                    float mask = getPercentMask(pWorld);
                    if (mask < 0.5) continue;

                    float lum;
                    float d = sampleCloudDensity(pObj, pWorld, invFeatureSize, earlyExitThr, lum) * _Density;
                    if (d < 0.001) continue;

                    float alphaStep = (1.0 - exp(-d * absorbFactor)) * mask;
                    float emissive = emissiveBase + lum * emissiveLumMul;
                    float transmit = 1.0 - accAlpha;

                    accColor += transmit * alphaStep * _PercentColor.rgb * emissive;
                    accAlpha += transmit * alphaStep;
                    if (accAlpha > 0.985) break;
                }

                accAlpha = 1.0 - exp(-accAlpha * _AlphaFog);
                accAlpha = saturate(accAlpha * _Opacity * _PercentColor.a);
                return fixed4(accColor, accAlpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
