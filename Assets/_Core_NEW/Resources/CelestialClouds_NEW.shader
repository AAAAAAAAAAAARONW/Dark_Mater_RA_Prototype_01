Shader "Custom/CelestialClouds_NEW"
{
    // Earth's clouds (CelestialBody_NEW): a shell just above the ground, the weather worked out
    // on the sphere (Celestial_NEW.cginc) and drifting round it, lit by the Sun: white by day,
    // warm at the terminator, dark at night, thinning to the limb. The ground under them takes
    // their shadows from the same function. _Color fades it. In Resources so a build has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _Noise ("Noise (QuasarNoise_NEW)", 2D) = "gray" {}
        _SunPosition ("The Sun, world space (set every frame)", Vector) = (0, 0, 0, 1)
        _CloudCover ("Cover", Range(0, 1)) = 0.55
        _CloudSpin ("Drift, radians a second", Float) = 0.01
        _Brightness ("Brightness", Range(0, 3)) = 1.1
        _Terminator ("Terminator colour", Color) = (1.0, 0.55, 0.3, 1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent-1" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
            #include "Celestial_NEW.cginc"

            fixed4 _Color;
            float4 _SunPosition, _Terminator;
            float _Brightness;

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float3 local : TEXCOORD0;
                float3 world : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.local = v.vertex.xyz;
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 nObj = normalize(i.local);
                float3 n = normalize(mul((float3x3)unity_ObjectToWorld, nObj));
                float3 l = normalize(_SunPosition.xyz - i.world);
                float3 v = normalize(_WorldSpaceCameraPos - i.world);

                float cover = CloudCover(nObj);
                float nl = dot(n, l);
                float day = smoothstep(-0.12, 0.25, nl);
                float3 sun = lerp(float3(1, 1, 1), _Terminator.rgb, exp(-abs(nl) * 7.0));
                float lit = saturate(nl * 0.7 + 0.35);

                // Thinner at a glance near the limb, so the edge of the world is soft.
                float mu = saturate(dot(n, v));
                float alpha = cover * smoothstep(0.0, 0.25, mu) * 0.92;

                float3 colour = sun * lit * day * _Brightness + float3(0.003, 0.004, 0.007);
                return fixed4(colour * alpha * _Color.rgb, alpha * _Color.a);
            }
            ENDCG
        }
    }

    FallBack Off
}
