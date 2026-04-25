Shader "Custom/PurpleCloudShell"
{
    Properties
    {
        [Header(Shape)]
        _Density ("Density", Range(0.0, 3.0)) = 1.15
        _Opacity ("Opacity", Range(0.0, 1.0)) = 0.85
        _EdgeWidth ("Edge Width", Range(0.02, 0.6)) = 0.22
        _EdgeFade ("Edge Fade", Range(0.5, 8.0)) = 2.4
        _LayerSharpness ("Layer Sharpness", Range(1.0, 16.0)) = 6.0
        _Coverage ("Coverage", Range(0.0, 1.0)) = 0.48
        _Softness ("Cloud Softness", Range(0.4, 3.0)) = 1.25

        [Header(Noise)]
        _NoiseScale ("Noise Scale", Range(0.1, 8.0)) = 1.4
        _DetailScale ("Detail Scale", Range(0.5, 10.0)) = 3.0
        _FlowSpeed ("Flow Speed", Range(0.0, 2.0)) = 0.12
        _SeedOffset ("Seed Offset", Vector) = (3.2, -1.7, 5.4, 0.0)

        [Header(Purple Palette)]
        _BaseColor ("Base Purple", Color) = (0.20, 0.10, 0.42, 1)
        _MidColor ("Mid Purple", Color) = (0.48, 0.26, 0.78, 1)
        _HighlightColor ("Highlight Lavender", Color) = (0.78, 0.60, 0.98, 1)
        _RimColor ("Rim Magenta", Color) = (0.90, 0.35, 0.86, 1)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.5
    }

    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Density;
            float _Opacity;
            float _EdgeWidth;
            float _EdgeFade;
            float _LayerSharpness;
            float _Coverage;
            float _Softness;

            float _NoiseScale;
            float _DetailScale;
            float _FlowSpeed;
            float4 _SeedOffset;

            float4 _BaseColor;
            float4 _MidColor;
            float4 _HighlightColor;
            float4 _RimColor;
            float _RimPower;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 objPos : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 normalW : TEXCOORD2;
                float3 viewDirW : TEXCOORD3;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise2(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                float x1 = lerp(a, b, f.x);
                float x2 = lerp(c, d, f.x);
                return lerp(x1, x2, f.y);
            }

            float fbm2(float2 p)
            {
                float s = 0.0;
                float a = 0.5;
                float f = 1.0;

                [unroll(3)]
                for (int i = 0; i < 3; i++)
                {
                    s += noise2(p * f) * a;
                    f *= 2.0;
                    a *= 0.5;
                }
                return s;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.pos = mul(UNITY_MATRIX_VP, worldPos);
                o.objPos = v.vertex.xyz;
                o.worldPos = worldPos.xyz;
                o.normalW = UnityObjectToWorldNormal(v.normal);
                o.viewDirW = _WorldSpaceCameraPos.xyz - worldPos.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 absP = abs(i.objPos);
                float maxAxis = max(absP.x, max(absP.y, absP.z));
                float toBoundary = saturate((0.5 - maxAxis) / max(0.001, _EdgeWidth));
                float edgeMask = pow(toBoundary, _EdgeFade);

                float radial = saturate(length(i.objPos) / 0.9);
                float coreMask = saturate(1.0 - radial);
                float thickness = saturate(edgeMask * (0.65 + 0.35 * coreMask));

                float3 nW = normalize(i.normalW);
                float3 vW = normalize(i.viewDirW);
                float fresnel = pow(saturate(1.0 - dot(nW, vW)), _RimPower);

                float t = _Time.y * _FlowSpeed;
                float2 uvA = i.worldPos.xz * _NoiseScale + _SeedOffset.xy;
                float2 uvB = i.worldPos.yz * _DetailScale + _SeedOffset.yz;

                float3 accColor = 0.0;
                float accAlpha = 0.0;

                [unroll(4)]
                for (int layer = 0; layer < 4; layer++)
                {
                    float d = ((float)layer + 0.5) * 0.25;
                    float layerMask = saturate((thickness - d * 0.9) * _LayerSharpness);

                    float n1 = fbm2(uvA + float2(0.31, 0.17) * d + t);
                    float n2 = fbm2(uvB * 1.27 + float2(-0.23, 0.41) * d - t * 0.73);
                    float shape = saturate((n1 * 0.72 + n2 * 0.48) - _Coverage);
                    float density = pow(shape, _Softness) * layerMask * _Density;

                    float a = density * (1.0 - accAlpha);
                    float lum = saturate(coreMask * 0.55 + shape * 0.35 + fresnel * 0.2);

                    float3 layerCol = lerp(_BaseColor.rgb, _MidColor.rgb, saturate(shape * 1.4));
                    layerCol = lerp(layerCol, _HighlightColor.rgb, saturate(shape * 0.85 + lum * 0.4));

                    accColor += a * layerCol;
                    accAlpha += a;
                }

                accColor += _RimColor.rgb * fresnel * (0.08 + 0.12 * thickness);
                accAlpha = saturate(accAlpha * _Opacity);
                return fixed4(accColor, accAlpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
