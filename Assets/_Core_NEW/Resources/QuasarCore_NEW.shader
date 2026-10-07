Shader "Custom/QuasarCore_NEW"
{
    // A quasar's core (QuasarVFX_NEW): the brightest thing in its galaxy. A quad turned to face
    // the camera round the object's middle: a white-hot point, a warm glow round it and a wide
    // soft halo — and, if _Shadow is above 0, the black hole's shadow at its very middle, a
    // small disc of black against the glow. Premultiplied alpha, so the glow adds light and
    // the shadow blocks it. _Color fades it. In Resources so a build always has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        _Size ("Size (of the object's scale)", Float) = 0.35
        [HDR] _Hot ("Core", Color) = (6, 5.8, 5.2, 1)
        [HDR] _Glow ("Glow", Color) = (1.4, 0.9, 0.45, 1)
        [HDR] _Halo ("Halo", Color) = (0.35, 0.16, 0.08, 1)
        _Shadow ("Shadow radius (of the size)", Range(0, 0.2)) = 0.02
    }

    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Size, _Shadow;
            float4 _Hot, _Glow, _Halo;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                float3 middle = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float scale = length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));
                float3 view = mul(UNITY_MATRIX_V, float4(middle, 1.0)).xyz;
                view.xy += vertex.xy * scale * _Size;
                o.pos = mul(UNITY_MATRIX_P, float4(view, 1.0));
                o.uv = vertex.xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float r = length(i.uv);
                if (r > 1.0) return 0;

                float hot = exp(-r * r * 220.0);
                float glow = exp(-r * r * 26.0);
                float halo = exp(-r * 4.5) * (1.0 - r);
                float3 light = _Hot.rgb * hot + _Glow.rgb * glow + _Halo.rgb * halo;

                // The shadow: black, and blocking what is behind it.
                float shadow = _Shadow > 0.0 ? 1.0 - smoothstep(_Shadow * 0.75, _Shadow, r) : 0.0;
                light *= 1.0 - shadow;

                // WorldSwitcher_NEW fades by scaling all four channels of _Color: the light
                // takes rgb, the shadow a — once each, a straight fade.
                return fixed4(light * _Color.rgb, shadow * _Color.a);
            }
            ENDCG
        }
    }

    FallBack Off
}
