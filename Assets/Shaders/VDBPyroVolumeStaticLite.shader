Shader "Custom/VDBPyroVolumeStaticLite"
{
    Properties
    {
        [Header(Volume)]
        _Steps ("Raymarch Steps", Range(12, 96)) = 28
        _StepWorldLength ("Step World Length", Range(0.01, 0.2)) = 0.06
        _Density ("Density", Range(0.1, 8.0)) = 2.1
        _Absorption ("Absorption", Range(0.2, 8.0)) = 2.2
        _Emission ("Emission", Range(0.2, 8.0)) = 1.7
        _Opacity ("Opacity", Range(0.05, 1.0)) = 0.82
        _EdgeFade ("Edge Fade", Range(0.5, 8.0)) = 2.0

        [Header(Pyro Shape)]
        _FeatureSize ("Feature Size", Range(0.08, 4.0)) = 0.95
        _DetailScale ("Detail Scale", Range(0.5, 6.0)) = 2.4
        _DetailWeight ("Detail Weight", Range(0.0, 1.0)) = 0.45
        _NoiseContrast ("Noise Contrast", Range(0.5, 5.0)) = 1.8
        _Softness ("Density Softness", Range(0.1, 3.0)) = 1.1

        [Header(Temperature Flame)]
        _TemperatureBias ("Temperature Bias", Range(-1.0, 1.0)) = 0.06
        _TemperatureContrast ("Temperature Contrast", Range(0.3, 5.0)) = 1.5
        _HotCore ("Hot Core", Range(0.0, 2.0)) = 0.95

        [Header(Color)]
        _SmokeColor ("Smoke Color", Color) = (0.08, 0.09, 0.1, 1)
        _FlameColor ("Flame Color", Color) = (1.0, 0.47, 0.12, 1)
        _HotColor ("Hot Color", Color) = (1.0, 0.9, 0.66, 1)
        _AshColor ("Ash Rim Color", Color) = (0.26, 0.28, 0.31, 1)
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
            float _NoiseContrast;
            float _Softness;

            float _TemperatureBias;
            float _TemperatureContrast;
            float _HotCore;

            float4 _SmokeColor;
            float4 _FlameColor;
            float4 _HotColor;
            float4 _AshColor;

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

            float sampleDensity(float3 pObj, float3 pMeters, out float temperature)
            {
                float3 q = pMeters / max(_FeatureSize, 0.001);
                float baseN = noise3(q);
                float detailN = noise3(q * _DetailScale + float3(10.3, -5.7, 3.1));
                float shape = saturate(baseN - detailN * _DetailWeight);
                shape = pow(shape, _NoiseContrast);

                float radial = saturate(length(pObj) / 0.87);
                float edge = pow(1.0 - radial, _EdgeFade);
                float density = saturate(shape * edge);
                density = pow(density, _Softness);

                float buoyancy = saturate((pObj.y + 0.5) * 1.15);
                temperature = saturate((density * 0.85 + buoyancy * 0.25 + _TemperatureBias) * _TemperatureContrast);
                temperature = saturate(temperature + density * _HotCore * 0.35);
                return density;
            }

            float3 sampleColor(float density, float temperature)
            {
                float flameMask = saturate((temperature - 0.20) * 1.45);
                float hotMask = saturate((temperature - 0.62) * 2.3);
                float ashMask = saturate((1.0 - temperature) * 1.2) * saturate(density * 2.0);

                float3 smoke = lerp(_SmokeColor.rgb, _AshColor.rgb, ashMask);
                float3 fire = lerp(_FlameColor.rgb, _HotColor.rgb, hotMask);
                return lerp(smoke, fire, flameMask);
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
                    float3 offsetW = pWorld - centerW;
                    float3 pMeters = float3(dot(offsetW, axisX), dot(offsetW, axisY), dot(offsetW, axisZ));

                    float temperature;
                    float d = sampleDensity(pObj, pMeters, temperature) * _Density;
                    float3 col = sampleColor(d, temperature);

                    float alphaStep = 1.0 - exp(-d * _Absorption * stepLen * 2.6);
                    float3 litCol = col * (d * (0.35 + temperature) * _Emission);

                    accColor += (1.0 - accAlpha) * alphaStep * litCol;
                    accAlpha += (1.0 - accAlpha) * alphaStep;
                    if (accAlpha > 0.985) break;
                }

                accAlpha = saturate(accAlpha * _Opacity);
                return fixed4(accColor, accAlpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
