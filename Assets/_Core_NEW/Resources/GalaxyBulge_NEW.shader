Shader "Custom/GalaxyBulge_NEW"
{
    // The Milky Way's bulge as a volume of old stars (GalaxyVFX_NEW): a soft warm glow on a
    // quad turned to the camera round the object's middle. The disc draws the bulge flat; this
    // is what keeps it round when the disc is seen at a slant, as a bulge is — a spheroid
    // standing out of a thin disc. Additive. _Color fades it. In Resources so a build has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        _Size ("Size (of the object's scale)", Float) = 0.16
        [HDR] _Glow ("Glow", Color) = (0.55, 0.42, 0.28, 1)
        [HDR] _Hot ("Centre", Color) = (0.9, 0.75, 0.55, 1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent-9" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Size;
            float4 _Glow, _Hot;

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
                float r2 = dot(i.uv, i.uv);
                float glow = exp(-r2 * 4.0) * saturate(1.0 - r2);
                float hot = exp(-r2 * 40.0);
                return fixed4((_Glow.rgb * glow + _Hot.rgb * hot) * _Color.rgb, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
