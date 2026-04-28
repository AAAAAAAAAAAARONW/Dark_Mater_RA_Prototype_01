Shader "Hidden/BuiltInUIOverlayComposite"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _OverlayTex ("Overlay (RGBA)", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _OverlayTex;

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, i.uv);
                fixed4 overCol = tex2D(_OverlayTex, i.uv);

                fixed3 rgb = lerp(baseCol.rgb, overCol.rgb, overCol.a);
                fixed a = max(baseCol.a, overCol.a);
                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
    Fallback Off
}
