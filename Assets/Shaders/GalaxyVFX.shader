Shader "VFXGalaxy"
{
    Properties
    {
        [Header(Volume)]
        _VolumeSize ("Volume Size", Range(0.5, 3)) = 1
        _Steps ("Raymarch Steps", Range(24, 96)) = 48
        _StepSize ("Step Size", Range(0.005, 0.08)) = 0.025
        _DensityScale ("Density Scale", Range(1, 8)) = 3
        _Thickness ("Disc Thickness", Range(0.1, 0.8)) = 0.4

        [Header(Core Bulge)]
        _ColorBulge ("Bulge Color", Color) = (0.98, 0.92, 0.78, 1)
        _ColorBulgeWarm ("Bulge Warm Center", Color) = (1, 0.85, 0.6, 1)
        _BulgeRadius ("Bulge Radius", Range(0.02, 0.4)) = 0.15
        _BulgeFalloff ("Bulge Falloff", Range(1, 8)) = 3

        [Header(Disk)]
        _ColorDisk ("Disk Color", Color) = (0.45, 0.52, 0.72, 1)
        _ColorDiskOuter ("Disk Outer Blue", Color) = (0.35, 0.45, 0.7, 1)
        _DiskScale ("Disk Scale", Range(0.2, 1.2)) = 0.5
        _DiskBrightness ("Disk Brightness", Range(0.1, 1.5)) = 0.45

        [Header(Spiral Arms)]
        _ColorArm ("Arm Color", Color) = (0.65, 0.78, 1, 1)
        _ColorArmCyan ("Arm Cyan Tint", Color) = (0.5, 0.75, 0.95, 1)
        _ArmCount ("Arm Count", Range(2, 6)) = 2
        _Pitch ("Pitch", Range(0.3, 2.5)) = 0.9
        _ArmWidth ("Arm Width", Range(0.2, 2)) = 0.7
        _ArmNoiseScale ("Arm Noise Scale", Range(2, 25)) = 10
        _ArmNoiseAmount ("Arm Noise Amount", Range(0, 1)) = 0.5
        _ArmNoiseFine ("Arm Fine Detail Scale", Range(15, 80)) = 35

        [Header(HII)]
        _ColorHII ("HII Pink", Color) = (0.95, 0.5, 0.7, 1)
        _HIIAmount ("HII Amount", Range(0, 0.4)) = 0.15
        _HIIScale ("HII Scale", Range(5, 40)) = 15

        [Header(Dust)]
        _DustColor ("Dust Dark", Color) = (0.06, 0.04, 0.1, 1)
        _DustWarm ("Dust Warm Tint", Color) = (0.25, 0.12, 0.08, 1)
        _DustAmount ("Dust Amount", Range(0, 1)) = 0.5
        _DustScale ("Dust Scale", Range(2, 20)) = 8
        _Inclination ("Inclination", Range(0, 1)) = 0.4

        [Header(Particle Grain)]
        _GrainScale ("Grain Scale", Range(80, 400)) = 180
        _GrainAmount ("Grain Strength", Range(0.1, 0.5)) = 0.25
        _GrainContrast ("Grain Contrast", Range(0.5, 2)) = 1.2

        [Header(Stars)]
        _StarColor ("Star Color", Color) = (1, 1, 1, 1)
        _StarScale ("Star Scale", Range(30, 250)) = 100
        _StarThreshold ("Star Threshold", Range(0.96, 0.999)) = 0.99
        _StarScaleBright ("Star Bright Scale", Range(20, 120)) = 50
        _StarThresholdBright ("Star Bright Threshold", Range(0.998, 0.9999)) = 0.9993
        _StarDensity ("Star Density", Range(0.2, 2)) = 0.8

        [Header(Edge)]
        _EdgeSoft ("Edge Soft", Range(0.3, 2)) = 0.8
        _OverallBrightness ("Overall Brightness", Range(0.3, 2)) = 1

        [Header(Animation)]
        _Rotate ("Rotate", Float) = 0
        _RotateSpeed ("Rotate Speed", Range(-0.2, 0.2)) = 0.02
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

            float _VolumeSize, _Steps, _StepSize, _DensityScale, _Thickness;
            float4 _ColorBulge, _ColorBulgeWarm, _ColorDisk, _ColorDiskOuter;
            float4 _ColorArm, _ColorArmCyan, _ColorHII, _DustColor, _DustWarm, _StarColor;
            float _BulgeRadius, _BulgeFalloff, _DiskScale, _DiskBrightness;
            float _ArmCount, _Pitch, _ArmWidth, _ArmNoiseScale, _ArmNoiseAmount, _ArmNoiseFine;
            float _HIIAmount, _HIIScale;
            float _DustAmount, _DustScale, _Inclination;
            float _GrainScale, _GrainAmount, _GrainContrast;
            float _StarScale, _StarThreshold, _StarScaleBright, _StarThresholdBright, _StarDensity;
            float _EdgeSoft, _OverallBrightness;
            float _Rotate, _RotateSpeed;

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p.yx + 19.19);
                return frac(p.x * p.y);
            }

            float valueNoise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm2D(float2 p, float persistence, int octaves)
            {
                float v = 0.0, a = 0.5, f = 1.0, m = 0.0;
                for (int i = 0; i < octaves; i++)
                {
                    v += a * valueNoise2D(p * f);
                    m += a;
                    a *= persistence;
                    f *= 2.0;
                }
                return v / m;
            }

            float armAngularDist(float R, float phi, float armPhase)
            {
                float armAngle = _Pitch * log(R + 0.02) + armPhase;
                float d = phi - armAngle;
                d = frac(d * 0.1591549 + 0.5) - 0.5;
                return abs(d * 6.28318);
            }

            void sampleGalaxy(float3 pos, out float3 outColor, out float outDensity)
            {
                float2 discPos = pos.xy;
                float z = pos.z;
                float R = length(discPos) * 2.0;
                float phi = atan2(discPos.y, discPos.x);
                if (_Rotate > 0.5)
                    phi += _Time.y * _RotateSpeed;

                float heightFalloff = exp(-(z / _Thickness) * (z / _Thickness));
                float2 uv3 = discPos;

                float bulge = exp(-pow(R / _BulgeRadius, _BulgeFalloff));
                float3 colBulge = lerp(_ColorBulgeWarm.rgb, _ColorBulge.rgb, saturate(R / _BulgeRadius));
                float bulgeGrain = valueNoise2D(uv3 * _GrainScale * 1.1 + z * 4.0);
                colBulge *= 1.0 + (bulgeGrain - 0.5) * _GrainAmount * _GrainContrast;
                float3 col = colBulge * bulge;

                float disk = exp(-R / _DiskScale) * _DiskBrightness;
                float3 colDisk = lerp(_ColorDisk.rgb, _ColorDiskOuter.rgb, saturate(R * 1.2));
                float diskGrain = valueNoise2D(uv3 * _GrainScale * 0.9 + 7.7 + z * 3.0);
                colDisk *= 1.0 + (diskGrain - 0.5) * _GrainAmount * _GrainContrast;
                col += colDisk * disk;

                float minD = 6.28;
                for (int k = 0; k < 6; k++)
                {
                    if (k >= (int)_ArmCount) break;
                    float phase = 6.28318 * (float)k / _ArmCount;
                    float d = armAngularDist(R, phi, phase);
                    minD = min(minD, d);
                }
                float armContrib = exp(-minD * minD / (_ArmWidth * _ArmWidth));
                float armNoise = fbm2D(float2(R * _ArmNoiseScale, phi * 2.0), 0.5, 4);
                float armFine = valueNoise2D(float2(R * _ArmNoiseFine, phi * 3.0));
                armContrib *= 1.0 - _ArmNoiseAmount + _ArmNoiseAmount * (armNoise * 0.7 + armFine * 0.3);
                armContrib = saturate(armContrib);
                float3 colArm = lerp(_ColorArm.rgb, _ColorArmCyan.rgb, saturate(R * 0.8));
                float armGrain = valueNoise2D(uv3 * _GrainScale + 13.3 + z * 2.0);
                colArm *= 1.0 + (armGrain - 0.5) * _GrainAmount * _GrainContrast;
                col += colArm * armContrib * disk;

                float hiiNoise = valueNoise2D(float2(R * _HIIScale, phi * 2.5) + 31.1);
                float hii = armContrib * disk * step(0.6, hiiNoise) * _HIIAmount;
                col += _ColorHII.rgb * hii;

                float dustNoise = fbm2D(float2(R * _DustScale, phi * 1.5), 0.6, 4);
                float dustFine = valueNoise2D(float2(R * _DustScale * 2.5, phi) + 17.3);
                float farSide = saturate(-discPos.y * 2.0 + 0.5 + _Inclination);
                float dust = (dustNoise * 0.7 + dustFine * 0.3) * farSide * _DustAmount * (1.0 - bulge * 0.9);
                float3 colDust = lerp(_DustColor.rgb, _DustWarm.rgb, dustNoise * farSide * 0.5);
                col = lerp(col, colDust, dust);
                col *= 1.0 - dust * 0.65;

                float starMed = valueNoise2D(uv3 * _StarScale + z * 2.5);
                float starBright = valueNoise2D(uv3 * _StarScaleBright + 99.1 + z * 1.5);
                float armMask = saturate(armContrib * 1.5 + 0.2);
                float starMaskMed = step(_StarThreshold, starMed) * lerp(0.25, 1.0, armMask) * _StarDensity;
                float starMaskBright = step(_StarThresholdBright, starBright) * lerp(0.3, 1.0, armMask);
                col += _StarColor.rgb * (starMaskMed + starMaskBright * 1.5);

                float grain = valueNoise2D(uv3 * _GrainScale + 41.2 + z * 5.0);
                col *= 1.0 + (grain - 0.5) * _GrainAmount * _GrainContrast;

                float edge = exp(-pow(saturate((R - 0.95) / _EdgeSoft), 2.0));
                col *= edge;
                col = max(col, 0.0);

                float density = (bulge + disk * 0.6 + armContrib * 0.4) * heightFalloff * _DensityScale * 1.2;
                outColor = col;
                outDensity = density;
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

                for (int s = 0; s < 96; s++)
                {
                    if (s >= numSteps) break;
                    float t = tNear + (float(s) + 0.5) * stepLen;
                    float3 pos = ro + t * rd;

                    float3 sampleCol;
                    float sampleDens;
                    sampleGalaxy(pos, sampleCol, sampleDens);

                    float stepDens = sampleDens * stepLen * 2.0;
                    float absorb = 1.0 - exp(-stepDens);
                    accColor += (1.0 - accOpacity) * absorb * sampleCol;
                    accOpacity += (1.0 - accOpacity) * absorb;
                    if (accOpacity > 0.98) break;
                }

                accColor *= _OverallBrightness;
                accOpacity = min(accOpacity, 1.0);
                if (accOpacity < 0.01) accOpacity = 0.01;
                return fixed4(accColor, accOpacity);
            }
            ENDCG
        }
    }
    FallBack Off
}
