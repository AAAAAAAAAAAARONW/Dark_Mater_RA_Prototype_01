Shader "Custom/BlazingQuasarVolumeSphere"
{
    Properties
    {
        [Header(Color)]
        _CoreColor ("Core Color", Color) = (1.0, 0.95, 0.88, 1.0)
        _HaloColor ("Halo Color", Color) = (0.40, 0.72, 1.0, 1.0)
        _JetColor ("Jet Color", Color) = (0.70, 0.95, 1.0, 1.0)

        [Header(Volume)]
        _SphereRadius ("Sphere Radius (Object Space)", Range(0.05, 1.0)) = 0.5
        _Steps ("Raymarch Steps", Range(24, 192)) = 96
        _Density ("Density", Range(0.1, 8.0)) = 2.6
        _Emission ("Emission", Range(0.1, 12.0)) = 3.0
        _Opacity ("Opacity", Range(0.05, 1.0)) = 0.8

        [Header(Quasar Shape)]
        _CoreRadius ("Core Radius", Range(0.01, 0.4)) = 0.08
        _HaloFalloff ("Halo Falloff", Range(0.2, 8.0)) = 2.1
        _DiskRadius ("Disk Radius", Range(0.05, 1.0)) = 0.34
        _DiskThickness ("Disk Thickness", Range(0.01, 0.25)) = 0.07
        _JetWidth ("Jet Width", Range(0.01, 0.3)) = 0.06
        _JetLength ("Jet Length", Range(0.05, 1.0)) = 0.46

        [Header(Animation)]
        _SpinSpeed ("Disk Spin Speed", Range(-12, 12)) = 2.0
        _PulseSpeed ("Pulse Speed", Range(0, 12)) = 3.0
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.28
        _Turbulence ("Turbulence", Range(0, 2)) = 0.55
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend One One
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _CoreColor;
            float4 _HaloColor;
            float4 _JetColor;

            float _SphereRadius;
            float _Steps;
            float _Density;
            float _Emission;
            float _Opacity;

            float _CoreRadius;
            float _HaloFalloff;
            float _DiskRadius;
            float _DiskThickness;
            float _JetWidth;
            float _JetLength;

            float _SpinSpeed;
            float _PulseSpeed;
            float _PulseAmount;
            float _Turbulence;

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

            float valueNoise3D(float3 p)
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

            void raySphere(float3 ro, float3 rd, float radius, out float tNear, out float tFar)
            {
                float b = dot(ro, rd);
                float c = dot(ro, ro) - radius * radius;
                float h = b * b - c;

                if (h < 0.0)
                {
                    tNear = 1e6;
                    tFar = 0.0;
                    return;
                }

                h = sqrt(h);
                tNear = -b - h;
                tFar = -b + h;

                if (tFar < 0.0)
                {
                    tNear = 1e6;
                    tFar = 0.0;
                    return;
                }

                if (tNear < 0.0) tNear = 0.0;
            }

            void sampleQuasar(float3 p, float t, out float3 outColor, out float outDensity)
            {
                float r = length(p);
                float pulse = 1.0 + sin(t * _PulseSpeed) * _PulseAmount;

                // Tight bright core.
                float core = exp(-pow(r / max(_CoreRadius, 0.001), 3.2)) * pulse;

                // Base halo inside the sphere.
                float halo = exp(-r * _HaloFalloff);

                // Rotating accretion disk around XZ plane.
                float xz = length(p.xz);
                float angle = atan2(p.z, p.x) + t * _SpinSpeed;
                float swirl = 0.5 + 0.5 * sin(angle * 14.0 + xz * 34.0 - t * 4.0);
                float diskBand = exp(-pow(abs(p.y) / max(_DiskThickness, 0.001), 2.0));
                float diskRadial = exp(-pow(xz / max(_DiskRadius, 0.001), 2.0));
                float disk = diskBand * diskRadial * (0.35 + 0.65 * swirl);

                // Bipolar jets along Y axis.
                float jetAxis = exp(-pow(xz / max(_JetWidth, 0.001), 2.3));
                float jetAlong = saturate(1.0 - abs(p.y) / max(_JetLength, 0.001));
                float jets = jetAxis * pow(jetAlong, 0.35);

                float turb = valueNoise3D(p * 10.0 + float3(0.0, t * 0.7, t * 0.3));
                turb = lerp(1.0, 0.65 + 0.7 * turb, saturate(_Turbulence));

                // Fade to zero near sphere boundary to avoid hard shell look.
                float edgeFade = saturate(1.0 - smoothstep(_SphereRadius * 0.85, _SphereRadius, r));

                float density = (core * 2.4 + halo * 0.65 + disk * 1.2 + jets * 1.35) * turb * edgeFade;
                float3 col = 0;
                col += _CoreColor.rgb * (core * 1.55 + disk * 0.28);
                col += _HaloColor.rgb * (halo * 0.95 + disk * 0.5);
                col += _JetColor.rgb * (jets * 1.15);

                outColor = col;
                outDensity = density;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.pos = mul(UNITY_MATRIX_VP, worldPos);
                o.rayOrigin = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                o.rayDir = normalize(v.vertex.xyz - o.rayOrigin);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 ro = i.rayOrigin;
                float3 rd = normalize(i.rayDir);

                float tNear, tFar;
                raySphere(ro, rd, _SphereRadius, tNear, tFar);
                if (tNear >= tFar || tFar <= 0.0)
                    return float4(0, 0, 0, 0);

                int steps = min(256, max(8, (int)_Steps));
                float travel = tFar - tNear;
                float stepLen = travel / (float)steps;

                float3 accColor = 0.0;
                float accAlpha = 0.0;
                float t = _Time.y;

                [loop]
                for (int s = 0; s < 256; s++)
                {
                    if (s >= steps) break;
                    float d = tNear + (s + 0.5) * stepLen;
                    float3 p = ro + rd * d;

                    float3 sampleCol;
                    float sampleDensity;
                    sampleQuasar(p, t, sampleCol, sampleDensity);

                    float absorb = 1.0 - exp(-sampleDensity * _Density * stepLen * 5.0);
                    accColor += (1.0 - accAlpha) * absorb * sampleCol;
                    accAlpha += (1.0 - accAlpha) * absorb;
                    if (accAlpha > 0.995) break;
                }

                accColor *= _Emission;
                accAlpha = saturate(accAlpha * _Opacity);
                return float4(accColor, accAlpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
