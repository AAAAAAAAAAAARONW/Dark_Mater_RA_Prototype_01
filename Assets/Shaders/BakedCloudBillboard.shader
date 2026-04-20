Shader "Custom/BakedCloudBillboard"
{
    // Simple unlit transparent shader for pre-baked cloud sprite textures.
    // Each cloud volume is baked offline to a 2D RGBA texture, then displayed
    // on a Quad via this shader.  Near-zero runtime cost.

    Properties
    {
        _MainTex    ("Baked Cloud Texture", 2D) = "white" {}
        _Brightness ("Brightness",  Range(0.1, 4.0)) = 1.0
        _AlphaMul   ("Alpha Scale", Range(0.0, 2.0)) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float     _Brightness;
            float     _AlphaMul;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                col.rgb *= _Brightness;
                col.a   *= _AlphaMul;
                return col;
            }
            ENDCG
        }
    }

    FallBack Off
}
