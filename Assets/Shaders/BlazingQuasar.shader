Shader "Custom/BlazingQuasar"
{
    Properties
    {
        [Header(Main Colors)]
        _CoreColor ("Core Color", Color) = (1.0, 0.95, 0.85, 1.0)
        _HaloColor ("Halo Color", Color) = (0.45, 0.75, 1.0, 1.0)
        _JetColor ("Jet Color", Color) = (0.6, 0.95, 1.0, 1.0)

        [Header(Brightness)]
        _CoreIntensity ("Core Intensity", Range(1, 80)) = 30
        _HaloIntensity ("Halo Intensity", Range(0, 30)) = 8
        _JetIntensity ("Jet Intensity", Range(0, 40)) = 12
        _OverallGain ("Overall Gain", Range(0.1, 8.0)) = 2.4

        [Header(Shape)]
        _CoreRadius ("Core Radius", Range(0.01, 0.5)) = 0.1
        _HaloRadius ("Halo Radius", Range(0.1, 1.2)) = 0.75
        _AccretionDisk ("Disk Strength", Range(0, 3)) = 1.2
        _JetWidth ("Jet Width", Range(0.01, 0.35)) = 0.08
        _JetLength ("Jet Length", Range(0.1, 1.2)) = 0.95

        [Header(Motion)]
        _PulseSpeed ("Pulse Speed", Range(0, 8)) = 2.5
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.3
        _SpinSpeed ("Disk Spin Speed", Range(-10, 10)) = 1.6
        _NoiseAmount ("Flicker Noise", Range(0, 1)) = 0.25
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

            float _CoreIntensity;
            float _HaloIntensity;
            float _JetIntensity;
            float _OverallGain;

            float _CoreRadius;
            float _HaloRadius;
            float _AccretionDisk;
            float _JetWidth;
            float _JetLength;

            float _PulseSpeed;
            float _PulseAmount;
            float _SpinSpeed;
            float _NoiseAmount;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 objDir : TEXCOORD0;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // Sphere-friendly direction from object center to surface.
                o.objDir = normalize(v.vertex.xyz);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.objDir);
                float2 uv;
                uv.x = atan2(dir.z, dir.x) / UNITY_PI; // [-1, 1]
                uv.y = dir.y;                          // [-1, 1]
                float r = length(uv);

                float t = _Time.y;
                float pulse = 1.0 + sin(t * _PulseSpeed) * _PulseAmount;

                // Super-bright compact core.
                float core = exp(-pow(r / max(_CoreRadius, 0.001), 3.5)) * pulse;

                // Wide halo glow.
                float halo = exp(-pow(r / max(_HaloRadius, 0.001), 1.8));

                // Rotating noisy accretion disk around center.
                float angle = atan2(uv.y, uv.x) + t * _SpinSpeed;
                float swirl = 0.5 + 0.5 * sin(angle * 10.0 + r * 30.0 - t * 3.0);
                float diskBand = exp(-pow(abs(uv.y) / 0.16, 2.0)) * exp(-pow(r / 0.7, 2.0));
                float noise = lerp(1.0, hash21(uv * 30.0 + t * 0.5), _NoiseAmount);
                float disk = diskBand * (0.4 + 0.6 * swirl) * noise * _AccretionDisk;

                // Bipolar jets along Y axis.
                float jetAxis = exp(-pow(abs(uv.x) / max(_JetWidth, 0.001), 2.2));
                float jetReach = smoothstep(0.05, _JetLength, abs(uv.y));
                float jetFade = exp(-pow((abs(uv.y) - _JetLength) * 4.0, 2.0));
                float jets = jetAxis * jetReach * saturate(jetFade + (1.0 - step(_JetLength, abs(uv.y))));

                float3 col = 0;
                col += _CoreColor.rgb * core * _CoreIntensity;
                col += _HaloColor.rgb * halo * _HaloIntensity;
                col += _HaloColor.rgb * disk * (_HaloIntensity * 0.7);
                col += _JetColor.rgb * jets * _JetIntensity * (0.8 + 0.2 * pulse);

                col *= _OverallGain;

                // Additive material can output alpha as glow mask.
                float alpha = saturate(core * 0.9 + halo * 0.35 + jets * 0.5);
                return float4(col, alpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
