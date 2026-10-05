Shader "Custom/DiveOrbit_NEW"
{
    // A planet's orbit, drawn round its star as a continuous dive opens the star up into its
    // system: a line renderer round the orbit, from the planet and back to it (u 0 to 1),
    // shown up to _Draw, with a brighter head where it is being drawn — the line unrolling
    // from the planet round the way it goes. Additive, with the dive's other lights (queue
    // 2990). In Resources so a build always has it: LayerDive_NEW loads it by name.

    Properties
    {
        _Color ("Colour (alpha = brightness)", Color) = (0.62, 0.78, 1, 0.35)
        _Draw ("Drawn, 0 to 1 round from the planet", Range(0, 1)) = 1
        _Head ("Head brightness", Float) = 0
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
            float  _Draw, _Head;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // How far back round the orbit from where it is being drawn.
                float behind = _Draw - i.uv.x;
                float drawn = smoothstep(-0.003, 0.003, behind);
                float head = exp(-max(behind, 0.0) * 30.0) * _Head;

                // Soft across the line, which is only a pixel or two wide.
                float across = 1.0 - abs(i.uv.y * 2.0 - 1.0);

                return fixed4(_Color.rgb * (_Color.a * drawn * (1.0 + 3.0 * head) * across), 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
