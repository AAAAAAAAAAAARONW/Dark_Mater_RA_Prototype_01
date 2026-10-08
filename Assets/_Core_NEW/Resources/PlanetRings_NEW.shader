Shader "Journey/Planet Rings"
{
    Properties
    {
        _MainTex ("Ring bands / alpha",2D)="white" {}
        _Color ("Transition fade",Color)=(1,1,1,1)
        _SunDirection ("Direction to Sun (object space)",Vector)=(1,0,0,0)
        _PlanetRadius ("Planet radius in ring space",Float)=0.704225
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        // The scene's RingMesh contains both windings. Cull the back winding
        // so a transparent band is blended once from either side.
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Color,_SunDirection;
            float _PlanetRadius;
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 local:TEXCOORD1; };
            v2f vert(appdata_base v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord.xy; o.local=v.vertex.xyz; return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float4 tex=tex2D(_MainTex,i.uv);
                float3 l=normalize(_SunDirection.xyz);
                float along=dot(i.local,l);
                float distanceToRay=length(i.local-l*along);
                float shadow=(1-smoothstep(_PlanetRadius*0.93,_PlanetRadius*1.08,distanceToRay))*step(along,0);
                float illumination=(0.28+0.72*sqrt(abs(l.y)))*(1-shadow*0.86);
                float alpha=tex.a*0.88;
                return float4(tex.rgb*float3(1.05,0.95,0.8)*illumination*alpha*_Color.rgb,alpha*_Color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
