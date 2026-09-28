Shader "Custom/DiveGlow_NEW"
{
    // The light a dive goes into: a camera-facing quad with a soft radial glow, drawn
    // additively. Queue 2990 puts it after the cosmic web's volumes (2950-2954) and
    // before the photon trail (3000), so it sits inside the web and behind the light.
    //
    // Lives in Resources so a build always has it: LayerDive_NEW loads it by name and
    // no material in any scene refers to it.

    Properties
    {
        _Color ("Colour (alpha = intensity)", Color) = (1, 0.9, 0.7, 1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
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

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2.0 - 1.0;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float r = length(i.uv);
                float halo = saturate(1.0 - r);
                halo = halo * halo * halo;
                float core = saturate(1.0 - r * 3.0);
                core = core * core;
                return fixed4(_Color.rgb * (halo * 0.7 + core) * _Color.a, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
