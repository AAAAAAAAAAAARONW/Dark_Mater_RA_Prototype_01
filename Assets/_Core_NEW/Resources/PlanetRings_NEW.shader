Shader "Journey/Planet Rings"
{
    Properties
    {
        _MainTex ("Ring bands / alpha",2D)="white" {}
        _Color ("Transition fade",Color)=(1,1,1,1)
        _SunDirection ("Direction to Sun (object space)",Vector)=(1,0,0,0)
        _PlanetRadius ("Planet radius in ring space",Float)=0.704225
        _BackScatter ("Backlit glow (the dust scattering forward)",Range(0,4))=1.5
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
            float _PlanetRadius,_BackScatter;
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
                // Backlit: with the Sun beyond the rings, their fine dust scatters its light
                // forward and they glow; most in the thin parts, which are mostly dust, least
                // in the dense ones, which block it.
                float3 eye=mul(unity_WorldToObject,float4(_WorldSpaceCameraPos,1)).xyz;
                float forward=pow(saturate(dot(normalize(i.local-eye),l)),3.0);
                float thin=saturate(1.0-alpha*0.9);
                float3 glow=float3(1.0,0.9,0.75)*forward*thin*_BackScatter*(1-shadow);
                return float4((tex.rgb*float3(1.05,0.95,0.8)*illumination+glow)*alpha*_Color.rgb,alpha*_Color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
