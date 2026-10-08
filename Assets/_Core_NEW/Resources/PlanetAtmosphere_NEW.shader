Shader "Journey/Thin Planet Atmosphere"
{
    Properties
    {
        _Color ("Transition fade", Color) = (1,1,1,1)
        _SunDirection ("Direction to Sun (object space)", Vector) = (1,0,0,0)
        [HDR] _DayColor ("Day scattering", Color) = (0.12,0.42,1.15,1)
        _DuskColor ("Terminator scattering", Color) = (0.72,0.24,0.08,1)
        _Strength ("Scattering strength", Range(0,2)) = 0.7
        _Falloff ("Thin limb falloff", Range(2,12)) = 5
    }
    SubShader
    {
        Tags { "Queue"="Transparent+2" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color, _SunDirection, _DayColor, _DuskColor;
            float _Strength, _Falloff;
            struct v2f { float4 pos:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; };
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float3 n = normalize(i.normal);
                float3 v = normalize(_WorldSpaceCameraPos.xyz-i.world);
                float3 l = normalize(mul((float3x3)unity_ObjectToWorld,_SunDirection.xyz));
                float mu = saturate(dot(n,v));
                float nl = dot(n,l);
                float day = smoothstep(-0.22,0.38,nl);
                float rim = pow(1-mu,_Falloff);
                // Taper the very outside: a soft veil rather than a hard neon outline.
                rim *= smoothstep(0,0.09,mu);
                float dusk = exp(-abs(nl)*12) * 0.35;
                float strength = rim * _Strength * (0.018+day);
                float3 scattering = lerp(_DayColor.rgb,_DuskColor.rgb,dusk);
                return float4(scattering*strength*_Color.rgb, saturate(strength*0.38)*_Color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
