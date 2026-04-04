Shader "Custom/PurpleCloudVolume"
{
    Properties
    {
        [Header(Volume)]
        _Steps ("Raymarch Steps", Range(16, 128)) = 48
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
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
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
            float _Anisotropy;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 rayOrigin : TEXCOORD0;
                float3 rayDir : TEXCOORD1;
            };

            float hash31(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float noise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = hash31(i);
                float n100 = hash31(i + float3(1, 0, 0));
                float n010 = hash31(i + float3(0, 1, 0));
                float n110 = hash31(i + float3(1, 1, 0));
                float n001 = hash31(i + float3(0, 0, 1));
                float n101 = hash31(i + float3(1, 0, 1));
                float n011 = hash31(i + float3(0, 1, 1));
                float n111 = hash31(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);
                return lerp(nxy0, nxy1, f.z);
            }

            float fbm(float3 p)
            {
                float s = 0.0;
                float a = 0.5;
                float f = 1.0;
                float n = 0.0;

                [unroll(5)]
                for (int i = 0; i < 5; i++)
                {
                    s += noise3(p * f) * a;
                    n += a;
                    f *= 2.0;
                    a *= 0.52;
                }
                return s / max(0.0001, n);
            }

            void rayBox(float3 ro, float3 rd, out float tNear, out float tFar)
            {
                float3 rdSafe = rd;
                if (abs(rdSafe.x) < 1e-6) rdSafe.x = 1e-6 * sign(rdSafe.x + 1e-7);
                if (abs(rdSafe.y) < 1e-6) rdSafe.y = 1e-6 * sign(rdSafe.y + 1e-7);
                if (abs(rdSafe.z) < 1e-6) rdSafe.z = 1e-6 * sign(rdSafe.z + 1e-7);

                float3 boxMin = float3(-0.5, -0.5, -0.5);
                float3 boxMax = float3(0.5,  0.5,  0.5);
                float3 t0 = (boxMin - ro) / rdSafe;
                float3 t1 = (boxMax - ro) / rdSafe;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);

                tNear = max(max(tmin.x, tmin.y), tmin.z);
                tFar  = min(min(tmax.x, tmax.y), tmax.z);

                if (tNear > tFar) tFar = tNear;
                if (tFar < 0.0) { tNear = 1e6; tFar = 0.0; return; }
                if (tNear < 0.0) tNear = 0.0;
            }

            float sampleCloudDensity(float3 pObj, float3 pMeters, out float lum)
            {
                float3 q = pMeters / max(_FeatureSize, 0.001);
                q.y *= _Anisotropy;
                q += _SeedOffset.xyz;

                float macro   = fbm(q * 0.85);
                float detail  = fbm(q * _DetailScale + float3(4.1, -2.5, 7.3));
                float erosion = noise3(q * (_DetailScale * 2.1) + float3(-6.2, 5.4, -3.8));

                float baseShape = macro * 0.95 + detail * 0.55;
                baseShape = saturate((baseShape - (0.34 + _Coverage * 0.38)) * (1.8 + _CloudSoftness));
                float erodeMask = saturate((erosion - (0.40 + _Erode * 0.35)) * 3.0);
                baseShape *= lerp(1.0, erodeMask, _Erode);

                float3 absP   = abs(pObj.xyz);
                float maxAxis = max(absP.x, max(absP.y, absP.z));
                float toBoundary = saturate((0.5 - maxAxis) / max(0.001, _EdgeWidth));

                float edgeNoiseA = noise3(q * 0.45 + float3(12.3, -4.2,  8.7));
                float edgeNoiseB = noise3(q * 0.82 + float3(-6.4,  9.1, -3.3));
                float breakup    = saturate(lerp(1.0, edgeNoiseA * 0.65 + edgeNoiseB * 0.55, _BoundaryBreakup));

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

                // World-space travel for step count — same logic as before
                float3 pObjNear  = ro + rd * tNear;
                float3 pObjFar   = ro + rd * tFar;
                float3 pWorldNear = mul(unity_ObjectToWorld, float4(pObjNear, 1.0)).xyz;
                float3 pWorldFar  = mul(unity_ObjectToWorld, float4(pObjFar,  1.0)).xyz;
                float worldTravel = length(pWorldFar - pWorldNear);

                int stepsByDistance = (int)ceil(worldTravel / max(0.01, _StepWorldLength));
                int steps   = min(128, max((int)_Steps, max(10, stepsByDistance)));
                float stepLen = travel / (float)steps;

                // ── Precompute world-space axes (unchanged from original) ──
                float3 centerW = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float3 axisX   = normalize(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));
                float3 axisY   = normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 1, 0)));
                float3 axisZ   = normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 0, 1)));

                // ── OPT 1: Linearize pMeters — precompute base + per-step delta ──
                // pObj(s) = ro + rd * (tNear + (s + 0.5) * stepLen)
                //         = pObjBase + pObjStep * s
                // pWorld and pMeters are both linear in s, so we can replace the
                // per-step matrix multiply + 3 dot products with 3 additions.
                float3 pObjBase  = ro + rd * (tNear + 0.5 * stepLen);
                float3 pObjStep  = rd * stepLen;

                float3 pWorldBase = mul(unity_ObjectToWorld, float4(pObjBase, 1.0)).xyz;
                float3 pWorldStep = mul((float3x3)unity_ObjectToWorld, pObjStep);

                float3 offsetBase = pWorldBase - centerW;
                float3 pMetersBase = float3(dot(offsetBase, axisX),
                                            dot(offsetBase, axisY),
                                            dot(offsetBase, axisZ));
                float3 pMetersStep = float3(dot(pWorldStep, axisX),
                                            dot(pWorldStep, axisY),
                                            dot(pWorldStep, axisZ));

                // ── OPT 2: Hoist loop-invariant constants out of the loop ──
                float absorbFactor  = _Absorption * stepLen * 2.2;   // was inside loop
                float emissiveBase  = 0.35 * _Emission;              // was inside loop
                float emissiveLumMul = _InnerGlow * _Emission;       // was inside loop

                float3 accColor = 0.0;
                float  accAlpha = 0.0;

                [loop]
                for (int s = 0; s < 128; s++)
                {
                    if (s >= steps) break;

                    // pObj: still needed for boundary (abs + length in object space)
                    float3 pObj    = pObjBase + pObjStep * s;
                    // pMeters: 3 adds instead of matrix mul + 3 dots
                    float3 pMeters = pMetersBase + pMetersStep * s;

                    float lum;
                    float d = sampleCloudDensity(pObj, pMeters, lum) * _Density;
                    float3 cloudCol = sampleCloudColor(d, lum);

                    float alphaStep = 1.0 - exp(-d * absorbFactor);
                    float emissive  = (emissiveBase + lum * emissiveLumMul);
                    float3 litCol   = cloudCol * emissive;

                    accColor += (1.0 - accAlpha) * alphaStep * litCol;
                    accAlpha += (1.0 - accAlpha) * alphaStep;
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
