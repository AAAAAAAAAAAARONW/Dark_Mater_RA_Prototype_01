Shader "Journey/Milky Way Resolved Stars"
{
    Properties { _Color ("Transition fade",Color)=(1,1,1,1) _Brightness ("Exposure",Range(0.1,3))=0.85 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float4 _Color;
            float _Brightness;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float2 size:TEXCOORD1; float4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            v2f vert(appdata v)
            {
                v2f o;
                float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
                float scale=length(unity_ObjectToWorld._m00_m10_m20);
                float3 right=UNITY_MATRIX_V._m00_m01_m02;
                float3 up=UNITY_MATRIX_V._m10_m11_m12;
                world+=(right*v.uv.x+up*v.uv.y)*v.size.x*scale;
                o.pos=UnityWorldToClipPos(world);
                o.uv=v.uv;
                o.color=v.color;
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float r2=dot(i.uv,i.uv);
                float core=exp(-r2*28)+0.08*exp(-r2*5);
                core*=1-smoothstep(0.55,1,r2);
                return float4(i.color.rgb*i.color.a*core*_Brightness*_Color.rgb,0);
            }
            ENDCG
        }
    }
    Fallback Off
}
