Shader "Journey/Planet Cloud Veil"
{
    Properties
    {
        _Color ("Transition fade", Color) = (1,1,1,1)
        [NoScaleOffset] _Noise ("Tileable weather field (linear)", 2D) = "gray" {}
        _SunDirection ("Direction to Sun (object space)", Vector) = (1,0,0,0)
        _Coverage ("Cloud coverage threshold", Range(0.3,0.8)) = 0.53
        _Density ("Veil density", Range(0,1)) = 0.65
        _Drift ("Weather drift", Float) = 0.001
    }
    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _Noise;
            float4 _Color, _SunDirection;
            float _Coverage, _Density, _Drift;
            struct v2f { float4 pos:SV_POSITION; float3 local:TEXCOORD0; };
            v2f vert(appdata_base v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.local=v.vertex.xyz; return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float3 n=normalize(i.local);
                float t=_Time.y*_Drift;
                // Triplanar weather has no longitude seam or pinched polar UVs.
                float3 p=n*2.7;
                p.xz += float2(sin(p.y*5.0),cos(p.y*4.0))*0.16;
                float3 w=pow(abs(n),4); w/=max(dot(w,float3(1,1,1)),0.001);
                float noise=tex2D(_Noise,p.yz+float2(t,0)).r*w.x
                    +tex2D(_Noise,p.xz+float2(t,0)).g*w.y
                    +tex2D(_Noise,p.xy+float2(t,0)).b*w.z;
                float detail=tex2D(_Noise,p.yz*3.8+p.x*0.2).a*w.x
                    +tex2D(_Noise,p.xz*3.8+p.y*0.2).a*w.y
                    +tex2D(_Noise,p.xy*3.8+p.z*0.2).a*w.z;
                float cloud=smoothstep(_Coverage,_Coverage+0.18,noise+(detail-0.5)*0.3);
                float nl=dot(n,normalize(_SunDirection.xyz));
                float day=smoothstep(-0.08,0.15,nl);
                float3 lit=lerp(float3(0.013,0.021,0.037),float3(0.88,0.94,1.0),day*(0.12+0.88*sqrt(saturate(nl))));
                float alpha=cloud*_Density;
                return float4(lit*alpha*_Color.rgb,alpha*_Color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
