Shader "Custom/GhostFilamentVolume"
{
    Properties
    {
        [Header(Volume)]
        _Steps ("Raymarch Steps", Range(16, 128)) = 44
        _StepWorldLength ("Step World Length", Range(0.01, 0.2)) = 0.055
        _Density ("Density", Range(0.1, 8.0)) = 1.55
        _Absorption ("Absorption", Range(0.2, 8.0)) = 1.9
        _Emission ("Emission", Range(0.0, 4.0)) = 0.28
        _Opacity ("Opacity", Range(0.05, 1.0)) = 0.72
        _EdgeFade ("Edge Fade", Range(0.5, 8.0)) = 3.0
        _EdgeWidth ("Edge Width", Range(0.02, 0.5)) = 0.22
        _EdgeNoise ("Edge Noise", Range(0.0, 1.0)) = 0.35
        _AlphaFog ("Alpha Fog Response", Range(0.5, 3.0)) = 1.35

        [Header(Filament Shape)]
        _FeatureSize ("Feature Size", Range(0.08, 4.0)) = 0.8
        _DetailScale ("Detail Scale", Range(1.0, 10.0)) = 4.2
        _FilamentThreshold ("Filament Threshold", Range(0.1, 0.95)) = 0.58
        _FilamentContrast ("Filament Contrast", Range(0.5, 6.0)) = 2.8
        _FilamentThinness ("Filament Thinness", Range(1.0, 8.0)) = 4.6
        _Sparsity ("Sparsity", Range(0.0, 1.0)) = 0.52
        _SoftFog ("Soft Fog Amount", Range(0.0, 1.0)) = 0.3
        _Octaves ("Noise Octaves", Range(2, 6)) = 4

        [Header(Color)]
        _DarkColor ("Dark Color", Color) = (0.02, 0.02, 0.02, 1)
        _MidColor ("Mid Color", Color) = (0.22, 0.22, 0.22, 1)
        _BrightColor ("Bright Color", Color) = (0.68, 0.68, 0.68, 1)
        _TintColor ("Tint Color", Color) = (0.75, 0.72, 0.82, 1)
        _TintAmount ("Tint Amount", Range(0, 1)) = 0.12
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

            float _Steps, _StepWorldLength, _Density, _Absorption, _Emission, _Opacity, _EdgeFade;
            float _EdgeWidth, _EdgeNoise, _AlphaFog;
            float _FeatureSize, _DetailScale, _FilamentThreshold, _FilamentContrast, _FilamentThinness, _Sparsity, _SoftFog, _Octaves;
            float4 _DarkColor, _MidColor, _BrightColor, _TintColor;
            float _TintAmount;

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
                float sum = 0.0;
                float amp = 0.5;
                float freq = 1.0;
                float norm = 0.0;

                [unroll(6)]
                for (int i = 0; i < 6; i++)
                {
                    if (i >= (int)_Octaves) break;
                    sum += noise3(p * freq) * amp;
                    norm += amp;
                    amp *= 0.52;
                    freq *= 2.0;
                }
                return sum / max(norm, 0.0001);
            }

            void rayBox(float3 ro, float3 rd, out float tNear, out float tFar)
            {
                float3 rdSafe = rd;
                if (abs(rdSafe.x) < 1e-6) rdSafe.x = 1e-6 * sign(rdSafe.x + 1e-7);
                if (abs(rdSafe.y) < 1e-6) rdSafe.y = 1e-6 * sign(rdSafe.y + 1e-7);
                if (abs(rdSafe.z) < 1e-6) rdSafe.z = 1e-6 * sign(rdSafe.z + 1e-7);

                float3 boxMin = float3(-0.5, -0.5, -0.5);
                float3 boxMax = float3(0.5, 0.5, 0.5);
                float3 t0 = (boxMin - ro) / rdSafe;
                float3 t1 = (boxMax - ro) / rdSafe;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);

                tNear = max(max(tmin.x, tmin.y), tmin.z);
                tFar = min(min(tmax.x, tmax.y), tmax.z);

                if (tNear > tFar) tFar = tNear;
                if (tFar < 0.0) { tNear = 1e6; tFar = 0.0; return; }
                if (tNear < 0.0) tNear = 0.0;
            }

            void sampleFilament(float3 pObj, float3 pMeters, out float3 outCol, out float outDen)
            {
                float3 q = pMeters / max(0.001, _FeatureSize);
                float n0 = fbm(q);
                float n1 = fbm(q * _DetailScale + 6.7);
                float n2 = fbm(q * (_DetailScale * 1.85) - 9.4);

                float filament = saturate(n0 * 1.25 - n1 * 0.82 + n2 * 0.24);
                filament = saturate((filament - _FilamentThreshold) * _FilamentContrast);
                filament = pow(filament, _FilamentThinness);

                float gate = noise3(q * 0.9 + float3(12.4, -4.8, 7.1));
                gate = saturate((gate - (0.36 + 0.34 * _Sparsity)) * 3.0);
                filament *= lerp(1.0, gate, _Sparsity);

                float soft = saturate(n0 * 0.75 + n1 * 0.25);
                soft = pow(soft, 2.2) * _SoftFog;

                float3 box01 = abs(pObj.xyz) * 2.0;
                float boxBorder = max(box01.x, max(box01.y, box01.z));
                float edgeBase = 1.0 - smoothstep(1.0 - _EdgeWidth, 1.0, boxBorder);

                float edgeNoise = noise3(q * 0.55 + float3(-3.2, 5.8, -7.6));
                edgeNoise = lerp(1.0, saturate(edgeNoise * 1.35), _EdgeNoise);

                float radial = saturate(length(pObj.xyz) / 0.95);
                float roundFade = 1.0 - smoothstep(0.66, 1.0, radial);
                float edge = pow(saturate(edgeBase * edgeNoise), _EdgeFade);
                edge *= lerp(1.0, roundFade, 0.35);
                float den = saturate(max(filament, soft) * edge);

                float brightMask = saturate(den * 1.8 + filament * 0.8);
                float midMask = saturate(den * 1.2);

                float3 col = lerp(_DarkColor.rgb, _MidColor.rgb, midMask);
                col = lerp(col, _BrightColor.rgb, brightMask);
                col = lerp(col, col * _TintColor.rgb, _TintAmount);

                outCol = col;
                outDen = den * _Density;
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
                if (tNear >= tFar || tFar <= 0.0)
                    return fixed4(0, 0, 0, 0);

                float travel = tFar - tNear;
                float3 pObjNear = ro + rd * tNear;
                float3 pObjFar = ro + rd * tFar;
                float3 pWorldNear = mul(unity_ObjectToWorld, float4(pObjNear, 1.0)).xyz;
                float3 pWorldFar = mul(unity_ObjectToWorld, float4(pObjFar, 1.0)).xyz;
                float worldTravel = length(pWorldFar - pWorldNear);

                int stepsByDistance = (int)ceil(worldTravel / max(0.01, _StepWorldLength));
                int steps = min(128, max((int)_Steps, max(8, stepsByDistance)));
                float stepLen = travel / (float)steps;

                float3 accColor = 0.0;
                float accAlpha = 0.0;

                float3 centerW = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float3 axisX = normalize(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));
                float3 axisY = normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 1, 0)));
                float3 axisZ = normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 0, 1)));

                [loop]
                for (int s = 0; s < 128; s++)
                {
                    if (s >= steps) break;
                    float t = tNear + (s + 0.5) * stepLen;
                    float3 pObj = ro + rd * t;
                    float3 pWorld = mul(unity_ObjectToWorld, float4(pObj, 1.0)).xyz;
                    float3 offW = pWorld - centerW;
                    float3 pMeters = float3(dot(offW, axisX), dot(offW, axisY), dot(offW, axisZ));

                    float3 sampleCol;
                    float sampleDen;
                    sampleFilament(pObj, pMeters, sampleCol, sampleDen);

                    float alphaStep = 1.0 - exp(-sampleDen * _Absorption * stepLen * 2.4);
                    float3 litCol = sampleCol * (_Emission + sampleDen * 0.55);

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
