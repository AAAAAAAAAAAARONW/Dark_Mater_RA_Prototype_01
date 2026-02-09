Shader "Custom/CosmicWeb"
{
    Properties
    {
        [Header(Volume)]
        _VolumeSize ("Volume Size", Range(0.5, 5)) = 2
        _Steps ("Raymarch Steps", Range(32, 128)) = 64
        _StepSize ("Step Size", Range(0.003, 0.06)) = 0.02
        _DensityScale ("Density Scale", Range(0.5, 6)) = 2.5

        [Header(Web Structure)]
        _Scale ("Web Scale", Range(0.3, 3)) = 1
        _Octaves ("Octaves", Range(2, 6)) = 4
        _Persistence ("Persistence", Range(0.3, 0.8)) = 0.5
        _RidgedSharpness ("Filament Sharpness", Range(0.5, 3)) = 1.2
        _FilamentThickness ("Filament Thickness", Range(0.25, 2)) = 0.75
        _FilamentBias ("Filament vs Void", Range(0.15, 0.75)) = 0.35
        _WebContrast ("Web Contrast", Range(0.5, 4)) = 1.8
        _NetEdgeSharpness ("Net Edge Sharpness", Range(0.4, 1.5)) = 0.75
        _WebDetailScale ("Web Detail Scale", Range(0.5, 3)) = 1.6
        _WebDetailAmount ("Web Detail Amount", Range(0, 0.6)) = 0.35
        _StrandVariationScale ("Strand Thickness Variation", Range(2, 20)) = 8
        _StrandVariationAmount ("Strand Variation Amount", Range(0, 0.7)) = 0.45
        _NodeStrength ("Node Clump Strength", Range(0, 1)) = 0.5

        [Header(Colors Void to Core)]
        _ColorVoid ("Void Dark", Color) = (0.02, 0.01, 0.05, 1)
        _ColorPurple ("Filament Low Purple", Color) = (0.25, 0.08, 0.35, 1)
        _ColorMagenta ("Filament Mid Magenta", Color) = (0.6, 0.15, 0.5, 1)
        _ColorOrange ("Filament High Orange", Color) = (0.95, 0.4, 0.2, 1)
        _ColorCore ("Node Core Yellow White", Color) = (1, 0.95, 0.75, 1)
        _ColorCoreHot ("Core Hot White", Color) = (1, 1, 0.95, 1)
        _StarClusterBrightness ("Star Cluster Glow", Range(1, 12)) = 5

        [Header(Yellow Veins on Ridge Only)]
        _ColorVein ("Vein Yellow Orange", Color) = (1, 0.85, 0.5, 1)
        _VeinScale ("Vein Scale", Range(2, 30)) = 9
        _VeinCoherence ("Vein Coherence", Range(0.3, 2)) = 1
        _VeinThreshold ("Vein Threshold", Range(0.2, 0.85)) = 0.48
        _VeinAmount ("Vein Amount", Range(0, 1)) = 0.6
        _VeinSharpness ("Vein Sharpness", Range(0.5, 3)) = 1.2

        [Header(Star Points Yellow on Ridge Only)]
        _StarPointScale ("Star Point Scale", Range(40, 400)) = 160
        _StarPointThreshold ("Star Point Threshold", Range(0.85, 0.998)) = 0.96
        _StarPointAmount ("Star Point Amount", Range(0, 1)) = 0.75
        _StarPointBrightness ("Star Point Brightness", Range(0.5, 8)) = 3

        [Header(Particle Grain)]
        _GrainScale ("Grain Scale", Range(50, 300)) = 120
        _GrainAmount ("Grain Amount", Range(0, 0.6)) = 0.2

        [Header(Edge and Brightness)]
        _EdgeSoft ("Edge Softness", Range(0.2, 1.5)) = 0.6
        _OverallBrightness ("Overall Brightness", Range(0.4, 2)) = 1

        [Header(Animation)]
        _Animate ("Animate", Float) = 1
        _Speed ("Drift Speed", Range(0, 0.15)) = 0.03
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

            float _VolumeSize, _Steps, _StepSize, _DensityScale;
            float _Scale, _Persistence, _RidgedSharpness, _FilamentThickness, _FilamentBias, _WebContrast;
            float _NetEdgeSharpness, _WebDetailScale, _WebDetailAmount, _StrandVariationScale, _StrandVariationAmount, _NodeStrength;
            float _Octaves;
            float4 _ColorVoid, _ColorPurple, _ColorMagenta, _ColorOrange, _ColorCore, _ColorCoreHot;
            float4 _ColorVein;
            float _StarClusterBrightness;
            float _VeinScale, _VeinCoherence, _VeinThreshold, _VeinAmount, _VeinSharpness;
            float _StarPointScale, _StarPointThreshold, _StarPointAmount, _StarPointBrightness;
            float _GrainScale, _GrainAmount;
            float _EdgeSoft, _OverallBrightness;
            float _Animate, _Speed;

            // 3D hash
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

            // Ridged FBM: ridges form filaments, peaks form nodes
            float ridgedFBM(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                float maxValue = 0.0;

                for (int i = 0; i < 6; i++)
                {
                    if (i >= (int)_Octaves) break;
                    float n = valueNoise3D(p * frequency);
                    float ridge = 1.0 - abs(n - 0.5) * 2.0;
                    ridge = pow(saturate(ridge), _RidgedSharpness * (1.0 / max(0.25, _FilamentThickness)));
                    value += amplitude * ridge;
                    maxValue += amplitude;
                    amplitude *= _Persistence;
                    frequency *= 2.0;
                }
                return value / max(0.001, maxValue);
            }

            // Smoother noise for coherent vein streaks along the network
            float veinNoiseCoherent(float3 p, float coherence)
            {
                float n1 = valueNoise3D(p);
                float n2 = valueNoise3D(p * 0.5 + 31.1);
                float n3 = valueNoise3D(p * 0.25 + 17.3);
                return n1 * (1.0 - coherence * 0.5) + (n2 * 0.5 + n3 * 0.25) * coherence;
            }

            // Optional: sparse clumps for extra node density
            float nodeClumpNoise(float3 p)
            {
                float3 id = floor(p * 1.2);
                float3 gv = frac(p * 1.2) - 0.5;
                float d = length(gv);
                float n = hash(id);
                float r = 0.25 + n * 0.2;
                return saturate(1.0 - d / r) * step(0.5, n);
            }

            void sampleCosmicWeb(float3 pos, out float3 outColor, out float outDensity)
            {
                float3 samplePos = pos * _Scale;
                if (_Animate > 0.5)
                {
                    float t = _Time.y * _Speed;
                    samplePos += float3(t * 0.7, t * 0.3, t * 0.5);
                }

                float web = ridgedFBM(samplePos);
                float webDetail = ridgedFBM(samplePos * _WebDetailScale + 19.3);
                web = web * (1.0 - _WebDetailAmount) + (web * 0.7 + webDetail * 0.3) * _WebDetailAmount;
                float rawWeb = saturate((web - _FilamentBias) * 2.5);
                rawWeb = pow(rawWeb, _NetEdgeSharpness);
                float nodes = nodeClumpNoise(samplePos * 0.8);
                float density = saturate(rawWeb * _WebContrast) + nodes * _NodeStrength;
                float strandVar = 1.0 - _StrandVariationAmount + _StrandVariationAmount * (0.55 + 0.45 * valueNoise3D(samplePos * _StrandVariationScale + 5.1));
                density = saturate(density * strandVar);

                // On-ridge mask: yellow appears ONLY on the network (not random in void)
                float onRidge = saturate(rawWeb * 2.2);

                // Grain only on the network (purple nebula texture)
                float grain = valueNoise3D(samplePos * _GrainScale);
                density *= 1.0 + (grain - 0.5) * _GrainAmount * onRidge * (1.0 - saturate((density - 0.7) * 3.0));

                // Base: purple nebula along the network, void elsewhere
                float3 col = _ColorVoid.rgb;
                col = lerp(col, _ColorPurple.rgb, saturate(density * 2.0));
                col = lerp(col, _ColorMagenta.rgb, saturate((density - 0.25) * 2.0));
                col = lerp(col, _ColorOrange.rgb, saturate((density - 0.5) * 2.0));
                col = lerp(col, _ColorCore.rgb, saturate((density - 0.7) * 2.5));
                col = lerp(col, _ColorCoreHot.rgb, saturate((density - 0.88) * 4.0));

                // Yellow veins: ONLY on the ridge, coherent along the脉络
                float filamentMask = onRidge * saturate(density * 2.5) * (1.0 - saturate((density - 0.52) / 0.28));
                float veinNoise = veinNoiseCoherent(samplePos * _VeinScale + 11.7, _VeinCoherence) * onRidge;
                float vein = saturate((veinNoise - _VeinThreshold) / max(0.01, 1.0 - _VeinThreshold));
                vein = pow(vein, _VeinSharpness);
                float veinStrength = filamentMask * vein * _VeinAmount;
                col = lerp(col, _ColorVein.rgb, veinStrength);

                // Yellow stars: ONLY on the ridge, additive so they are obvious
                float starPointNoise = valueNoise3D(samplePos * _StarPointScale + 7.13);
                float starPoint = step(_StarPointThreshold, starPointNoise);
                float starOnRidge = onRidge * step(0.2, density) * starPoint * _StarPointAmount;
                col += _ColorCoreHot.rgb * starOnRidge * _StarPointBrightness;

                // Node/core glow: only on network (density already ridge-gated)
                float starCluster = saturate((density - 0.65) / 0.35);
                starCluster = starCluster * starCluster;
                col *= lerp(1.0, _StarClusterBrightness, starCluster);

                outColor = max(col, 0);
                outDensity = density * _DensityScale;
            }

            void rayBox(float3 ro, float3 rd, float3 boxMin, float3 boxMax, out float tNear, out float tFar)
            {
                float3 rdSafe = rd;
                if (abs(rdSafe.x) < 1e-6) rdSafe.x = 1e-6 * sign(rdSafe.x + 1e-7);
                if (abs(rdSafe.y) < 1e-6) rdSafe.y = 1e-6 * sign(rdSafe.y + 1e-7);
                if (abs(rdSafe.z) < 1e-6) rdSafe.z = 1e-6 * sign(rdSafe.z + 1e-7);
                float3 t0 = (boxMin - ro) / rdSafe;
                float3 t1 = (boxMax - ro) / rdSafe;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);
                tNear = max(max(tmin.x, tmin.y), tmin.z);
                tFar = min(min(tmax.x, tmax.y), tmax.z);
                if (tNear > tFar) tFar = tNear;
                if (tFar < 0.0) { tNear = 1e6; tFar = 0; return; }
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
                float3 objPos = v.vertex.xyz;
                o.rayDir = normalize(objPos - o.rayOrigin);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 ro = i.rayOrigin;
                float3 rd = normalize(i.rayDir);
                float halfSize = _VolumeSize * 0.5;
                float3 boxMin = float3(-halfSize, -halfSize, -halfSize);
                float3 boxMax = float3(halfSize, halfSize, halfSize);

                float tNear, tFar;
                rayBox(ro, rd, boxMin, boxMax, tNear, tFar);
                if (tNear >= tFar || tFar <= 0.0)
                    return fixed4(0, 0, 0, 0);

                float totalLen = tFar - tNear;
                int numSteps = (int)_Steps;
                numSteps = max(numSteps, 8);
                float stepLen = totalLen / (float)numSteps;
                if (stepLen > _StepSize) stepLen = _StepSize;
                numSteps = (int)(totalLen / stepLen);
                numSteps = max(numSteps, 8);
                stepLen = totalLen / (float)numSteps;

                float3 accColor = 0;
                float accOpacity = 0;

                for (int s = 0; s < 128; s++)
                {
                    if (s >= numSteps) break;
                    float t = tNear + (float(s) + 0.5) * stepLen;
                    float3 pos = ro + t * rd;

                    float3 sampleCol;
                    float sampleDens;
                    sampleCosmicWeb(pos, sampleCol, sampleDens);

                    float stepDens = sampleDens * stepLen * 2.0;
                    float absorb = 1.0 - exp(-stepDens);
                    accColor += (1.0 - accOpacity) * absorb * sampleCol;
                    accOpacity += (1.0 - accOpacity) * absorb;
                    if (accOpacity > 0.98) break;
                }

                float dist = length(i.rayDir);
                float edge = exp(-pow(saturate((dist - halfSize * 0.7) / max(0.01, _EdgeSoft * halfSize)), 2.0));
                accColor *= _OverallBrightness * edge;
                accOpacity = min(accOpacity, 1.0);
                if (accOpacity < 0.01) accOpacity = 0.01;
                return fixed4(accColor, accOpacity);
            }
            ENDCG
        }
    }
    FallBack Off
}
