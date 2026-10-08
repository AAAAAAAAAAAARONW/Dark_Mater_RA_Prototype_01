Shader "Journey/Milky Way Spiral Volume"
{
    Properties
    {
        [NoScaleOffset] _Volume ("Baked galaxy density (rebake after art edits)", 3D) = "" {}
        _Color ("Transition fade", Color) = (1,1,1,1)
        [NoScaleOffset] _Noise ("Interstellar structure (linear)", 2D) = "gray" {}
        [HDR] _ArmColor ("Young stars in the arms", Color) = (0.42,0.62,0.95,1)
        [HDR] _CoreColor ("Old stars in the bar", Color) = (1.15,0.79,0.43,1)
        [HDR] _HIIColor ("Sparse star-forming regions", Color) = (1.1,0.15,0.3,1)
        _Brightness ("Exposure", Range(0.1,4)) = 0.85
        _Pitch ("Spiral winding", Range(1.5,5)) = 2.3
        _ArmCount ("Principal spiral arms", Range(2,6)) = 4
        _ArmWidth ("Arm width (radius fraction)", Range(0.02,0.15)) = 0.080
        _Dust ("Leading dust lanes", Range(0,3)) = 1.35
        _Phase ("Bar and arm orientation (radians)", Float) = 0.45
        _Thickness ("Disc thickness (cube units)", Range(0.002,0.03)) = 0.0045
        _Steps ("Volume samples", Range(12,96)) = 32
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        Cull Front
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma shader_feature MILKYWAY_BAKED_VOLUME
            #include "UnityCG.cginc"
            #include "MilkyWayDensity_NEW.cginc"
            sampler3D _Volume;
            float4 _Color;
            float _Steps;
            struct v2f { float4 pos:SV_POSITION; float3 local:TEXCOORD0; };
            v2f vert(appdata_base v)
            {
                // Tight proxy: avoid shading the empty part of a unit cube at oblique views.
                v.vertex.y*=0.15;
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.local=v.vertex.xyz; return o;
            }
            float4 frag(v2f i):SV_Target
            {
                if (max(_Color.r,max(_Color.g,_Color.b))<0.0001) return 0;
                float3 ro=mul(unity_WorldToObject,float4(_WorldSpaceCameraPos.xyz,1)).xyz;
                float3 rd=normalize(i.local-ro);
                float3 safe=lerp(-1.0,1.0,step(0,rd))*max(abs(rd),0.000001);
                float3 lo=(-float3(0.5,0.075,0.5)-ro)/safe;
                float3 hi=( float3(0.5,0.075,0.5)-ro)/safe;
                float3 nearV=min(lo,hi), farV=max(lo,hi);
                float start=max(0,max(max(nearV.x,nearV.y),nearV.z));
                float finish=min(min(farV.x,farV.y),farV.z);
                if (finish<=start) return 0;
                int count=clamp((int)_Steps,12,96);
                float stepSize=(finish-start)/count;
                float transmittance=1;
                float3 light=0;
                // Static screen-space stratification breaks coherent slice bands at grazing
                // angles. No time seed: a stationary camera never has animated noise.
                float jitter=frac(52.9829189*frac(dot(i.pos.xy,float2(0.06711056,0.00583715))));
                // Concentrate samples around the thin stellar plane. Uniform sampling
                // wastes most samples in empty space and makes grazing views streak.
                bool stratify=abs(rd.y)>0.12;
                float plane=stratify ? -ro.y/rd.y : 0;
                float2 limits=float2(start,finish)-plane;
                limits=sign(limits)*sqrt(abs(limits));
                [loop] for (int s=0;s<96;s++)
                {
                    if (s>=count || transmittance<0.015) break;
                    float distance=start+(s+jitter)*stepSize;
                    float interval=stepSize;
                    if(stratify)
                    {
                        float a=lerp(limits.x,limits.y,s/(float)count);
                        float b=lerp(limits.x,limits.y,(s+1)/(float)count);
                        float u=lerp(a,b,jitter);
                        distance=plane+sign(u)*u*u;
                        interval=sign(b)*b*b-sign(a)*a*a;
                    }
                    float3 e; float extinction;
                    float3 p=ro+rd*distance;
                    #if defined(MILKYWAY_BAKED_VOLUME)
                        // Nonlinear height stores more texels in the thin stellar disc.
                        float h=sign(p.y)*sqrt(saturate(abs(p.y)/0.075));
                        float4 density=tex3Dlod(_Volume,float4(p.xz+0.5,h*0.5+0.5,0));
                        e=density.rgb; extinction=density.a;
                    #else
                        galaxy(p,e,extinction);
                    #endif
                    float opticalDepth=extinction*interval*75;
                    float attenuation=exp(-opticalDepth);
                    light+=transmittance*e*(1-attenuation)/max(extinction,0.001);
                    transmittance*=attenuation;
                }
                // One premultiplied layer, compatible with both group fade and dive scaling.
                return float4(light*_Color.rgb,(1-transmittance)*_Color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
