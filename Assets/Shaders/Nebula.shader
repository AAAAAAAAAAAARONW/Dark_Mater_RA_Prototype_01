Shader "Custom/Nebula"
{
    Properties
    {
        [Header(Colors)]
        _ColorDark ("Dark Color", Color) = (0.08, 0.02, 0.18, 1)
        _ColorMid ("Mid Color", Color) = (0.3, 0.15, 0.5, 1)
        _ColorBright ("Bright Color", Color) = (0.6, 0.4, 0.9, 1)
        _ColorStar ("Star Color", Color) = (1, 0.95, 1, 1)

        [Header(Noise)]
        _Scale ("Scale", Range(0.5, 4)) = 1.2
        _Octaves ("Octaves", Range(1, 6)) = 4
        _Persistence ("Persistence", Range(0.2, 0.9)) = 0.5
        _Density ("Density", Range(0.2, 2)) = 0.8
        _Sharpness ("Sharpness", Range(0.5, 4)) = 1.5

        [Header(Stars)]
        _StarScale ("Star Scale", Range(20, 200)) = 80
        _StarThreshold ("Star Threshold", Range(0.95, 0.999)) = 0.992
        _StarBrightness ("Star Brightness", Range(0.5, 3)) = 1.2

        [Header(Animation)]
        _Animate ("Animate", Float) = 1
        _Speed ("Speed", Range(0, 0.5)) = 0.05
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _ColorDark, _ColorMid, _ColorBright, _ColorStar;
            float _Scale, _Persistence, _Density, _Sharpness;
            float _StarScale, _StarThreshold, _StarBrightness;
            float _Animate, _Speed;
            float _Octaves;

            // 3D hash
            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            // 3D value noise
            float valueNoise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // smooth

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

            // FBM multi-octave noise
            float fbm(float3 p, float persistence)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                float maxValue = 0.0;

                for (int i = 0; i < (int)_Octaves; i++)
                {
                    value += amplitude * valueNoise3D(p * frequency);
                    maxValue += amplitude;
                    amplitude *= persistence;
                    frequency *= 2.0;
                }
                return value / maxValue;
            }

            // star dots
            float stars(float3 dir)
            {
                float n = valueNoise3D(dir * _StarScale);
                return step(_StarThreshold, n) * _StarBrightness;
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // view direction
                float3 dir = normalize(i.worldPos - _WorldSpaceCameraPos);

                // drift sample
                float3 samplePos = dir * _Scale;
                if (_Animate > 0.5)
                {
                    float t = _Time.y * _Speed;
                    samplePos += float3(t * 0.7, t * 0.3, t * 0.5);
                }

                // nebula density
                float nebula = fbm(samplePos, _Persistence);
                nebula = saturate((nebula - (1.0 - _Density)) * _Sharpness);

                // dark -> mid -> bright
                float4 col = lerp(_ColorDark, _ColorMid, saturate(nebula * 2.0));
                col = lerp(col, _ColorBright, saturate((nebula - 0.4) * 1.5));
                col = lerp(col, _ColorStar, saturate((nebula - 0.75) * 2.0));

                // stars
                float star = stars(dir);
                col.rgb += _ColorStar.rgb * star;

                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
