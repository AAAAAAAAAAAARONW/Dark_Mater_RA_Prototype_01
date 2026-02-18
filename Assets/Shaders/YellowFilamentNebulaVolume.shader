Shader "Custom/YellowFilamentNebulaVolume"
{
    Properties
    {
        [Header(Volume)]
        _Steps ("Min Raymarch Steps", Range(24, 128)) = 72
        _StepWorldLength ("Step World Length", Range(0.005, 0.2)) = 0.035
        _Density ("Density", Range(0.2, 6.0)) = 1.9
        _Brightness ("Brightness", Range(0.2, 4.0)) = 1.35
        _Opacity ("Opacity", Range(0.05, 1.0)) = 0.72
        _EdgeFade ("Edge Fade", Range(0.2, 4.0)) = 1.4

        [Header(Filament Shape)]
        _FeatureSize ("Feature Size", Range(0.08, 3.0)) = 0.65
        _DetailScale ("Detail Scale", Range(0.5, 8.0)) = 4.4
        _AlongAxisScale ("Along Axis Scale", Range(0.05, 2.0)) = 0.24
        _FilamentThreshold ("Filament Threshold", Range(0.1, 0.9)) = 0.52
        _FilamentContrast ("Filament Contrast", Range(0.5, 6.0)) = 4.1
        _FilamentThinness ("Filament Thinness", Range(1.0, 8.0)) = 5.2
        _StrandSparsity ("Strand Sparsity", Range(0.0, 1.0)) = 0.48
        _Persistence ("Persistence", Range(0.25, 0.9)) = 0.5
        _Octaves ("Octaves", Range(2, 6)) = 4

        [Header(Yellow Veins)]
        _VeinScale ("Vein Scale", Range(2, 40)) = 12
        _VeinThreshold ("Vein Threshold", Range(0.2, 0.95)) = 0.56
        _VeinSharpness ("Vein Sharpness", Range(0.5, 6)) = 2.1
        _VeinAmount ("Vein Amount", Range(0, 1)) = 0.85

        [Header(Color)]
        _ColorLow ("Low Yellow", Color) = (0.92, 0.58, 0.18, 1)
        _ColorMid ("Mid Yellow", Color) = (1.0, 0.76, 0.28, 1)
        _ColorHot ("Hot Core", Color) = (1.0, 0.95, 0.72, 1)
        _Transparency ("Transparency", Range(0, 1)) = 1

        [Header(Animation)]
        _Animate ("Animate", Float) = 1
        _Speed ("Drift Speed", Range(0, 0.2)) = 0.045
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

            float _Steps, _StepWorldLength, _Density, _Brightness, _Opacity, _EdgeFade;
            float _FeatureSize, _DetailScale, _AlongAxisScale, _FilamentThreshold, _FilamentContrast, _FilamentThinness, _StrandSparsity, _Persistence, _Octaves;
            float _VeinScale, _VeinThreshold, _VeinSharpness, _VeinAmount;
            float4 _ColorLow, _ColorMid, _ColorHot;
            float _Transparency;
            float _Animate, _Speed;

            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float valueNoise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = hash(i);
                float n100 = hash(i + float3(1, 0, 0));
                float n010 = hash(i + float3(0, 1, 0));
                float n110 = hash(i + float3(1, 1, 0));
                float n001 = hash(i + float3(0, 0, 1));
                float n101 = hash(i + float3(1, 0, 1));
                float n011 = hash(i + float3(0, 1, 1));
                float n111 = hash(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);
                return lerp(nxy0, nxy1, f.z);
            }

            float ridgedFBM(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                float norm = 0.0;

                [unroll(6)]
                for (int i = 0; i < 6; i++)
                {
                    if (i >= (int)_Octaves) break;
                    float n = valueNoise3D(p * frequency);
                    float ridge = 1.0 - abs(n - 0.5) * 2.0;
                    ridge = ridge * ridge;
                    value += ridge * amplitude;
                    norm += amplitude;
                    amplitude *= _Persistence;
                    frequency *= 2.0;
                }

                return value / max(0.001, norm);
            }

            void sampleFilament(float3 posObj, float3 posMeters, out float3 outColor, out float outDensity)
            {
                float t = (_Animate > 0.5) ? (_Time.y * _Speed) : 0.0;

                float3 p = posMeters / max(0.001, _FeatureSize);
                p.z *= _AlongAxisScale;
                p += float3(t * 0.45, t * 0.18, t * 0.32);

                float baseRidge = ridgedFBM(p);
                float detailRidge = ridgedFBM(p * _DetailScale + 11.7);
                float microRidge = ridgedFBM(p * (_DetailScale * 1.9) - 7.3);

                // Ridge difference keeps only thin connected crests, reducing cloud-like blobs.
                float filament = saturate(baseRidge * 1.35 - detailRidge * 0.8 + microRidge * 0.35);
                filament = saturate((filament - _FilamentThreshold) * _FilamentContrast);
                filament = pow(filament, _FilamentThinness);

                // Sparsity gate trims fat regions while preserving connected strands.
                float gate = valueNoise3D(p * 0.9 + float3(13.1, 7.2, -4.3));
                gate = saturate((gate - (0.35 + _StrandSparsity * 0.35)) * 3.0);
                filament *= lerp(1.0, gate, _StrandSparsity);

                float veinNoise = valueNoise3D(p * _VeinScale + float3(5.2, -3.4, 7.1));
                float vein = saturate((veinNoise - _VeinThreshold) / max(0.01, 1.0 - _VeinThreshold));
                vein = pow(vein, _VeinSharpness);

                float across = saturate(length(posObj.xy) / 0.71);
                float tubeMask = pow(1.0 - across, _EdgeFade);

                float density = filament * lerp(0.7, 1.5, vein * _VeinAmount);
                density *= tubeMask;

                float3 col = lerp(_ColorLow.rgb, _ColorMid.rgb, saturate(density * 1.35));
                col = lerp(col, _ColorHot.rgb, saturate((density - 0.5) * 2.2) * (0.4 + 0.6 * vein));

                outColor = col;
                outDensity = density * _Density;
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

                int stepsByDistance = (int)ceil(worldTravel / max(0.005, _StepWorldLength));
                int steps = max((int)_Steps, max(8, stepsByDistance));
                steps = min(steps, 256);
                float stepLen = travel / (float)steps;

                float3 accColor = 0.0;
                float accAlpha = 0.0;

                float3 centerW = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float3 axisX = normalize(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));
                float3 axisY = normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 1, 0)));
                float3 axisZ = normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 0, 1)));

                [loop]
                for (int s = 0; s < 256; s++)
                {
                    if (s >= steps) break;
                    float t = tNear + (s + 0.5) * stepLen;
                    float3 pObj = ro + rd * t;
                    float3 pWorld = mul(unity_ObjectToWorld, float4(pObj, 1.0)).xyz;
                    float3 offsetW = pWorld - centerW;
                    float3 pMeters = float3(dot(offsetW, axisX), dot(offsetW, axisY), dot(offsetW, axisZ));

                    float3 sampleCol;
                    float sampleDens;
                    sampleFilament(pObj, pMeters, sampleCol, sampleDens);

                    float absorb = 1.0 - exp(-sampleDens * stepLen * 2.2);
                    accColor += (1.0 - accAlpha) * absorb * sampleCol;
                    accAlpha += (1.0 - accAlpha) * absorb;
                    if (accAlpha > 0.98) break;
                }

                accColor *= _Brightness;
                accColor *= _Transparency;
                accAlpha = saturate(accAlpha * _Opacity * _Transparency);
                return fixed4(accColor, accAlpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
