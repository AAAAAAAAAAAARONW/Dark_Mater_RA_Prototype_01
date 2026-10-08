Shader "Journey/Planet Surface"
{
    Properties
    {
        _MainTex ("Surface albedo", 2D) = "white" {}
        _Color ("Transition fade", Color) = (1,1,1,1)
        _SunDirection ("Direction to Sun (object space)", Vector) = (1,0,0,0)
        _SunColor ("Sunlight", Color) = (1,0.96,0.9,1)
        _NightColor ("Night bounce", Color) = (0.025,0.035,0.055,1)
        _RimColor ("Atmospheric limb tint", Color) = (0.12,0.32,0.65,1)
        _Rim ("Limb strength", Range(0,0.5)) = 0.025
        _Contrast ("Albedo contrast", Range(0.7,1.5)) = 1.06
        _Exposure ("Sunlit exposure", Range(0.5,2)) = 1.2
        _Specular ("Surface highlight", Range(0,1)) = 0.06
        _Shininess ("Highlight tightness", Range(8,160)) = 28
        _Ocean ("Earth ocean mask from albedo", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST, _Color, _SunDirection, _SunColor, _NightColor, _RimColor;
            float _Rim, _Contrast, _Exposure, _Specular, _Shininess, _Ocean;
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; float3 normal:TEXCOORD2; };
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float3 n = normalize(i.normal);
                float3 l = normalize(mul((float3x3)unity_ObjectToWorld, _SunDirection.xyz));
                float3 v = normalize(_WorldSpaceCameraPos.xyz - i.world);
                float3 albedo = max(tex2D(_MainTex, i.uv).rgb, 0.001);
                albedo = pow(albedo, _Contrast);
                float nl = dot(n, l);
                float day = smoothstep(-0.065, 0.14, nl);
                float diffuse = pow(saturate(nl), 0.75);
                float ocean = _Ocean * saturate((albedo.b-albedo.r)/max(albedo.b,0.015)*2.5)
                    * saturate((albedo.b-albedo.g)/max(albedo.b,0.015)*3.0);
                float spec = pow(saturate(dot(n, normalize(l+v))), lerp(_Shininess,100,ocean));
                float fresnel = pow(1-saturate(dot(n,v)),5);
                float3 light = _NightColor.rgb + _SunColor.rgb * day * (0.055 + diffuse * _Exposure);
                float3 color = albedo * light;
                color += _SunColor.rgb * spec * day * lerp(_Specular,0.65,ocean) * (0.4+0.6*fresnel);
                color += _RimColor.rgb * pow(1-saturate(dot(n,v)),4) * day * _Rim;
                // WorldSwitcher_NEW scales _Color; keep all planet materials scene-local.
                return float4(color * _Color.rgb, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
