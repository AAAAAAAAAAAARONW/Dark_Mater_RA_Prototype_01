// The HUD, drawn onto the Vizlab's seven rows. Used by GetReal3DHud_NEW and
// GetReal3DHudDraw_NEW; see GetReal3DHud_NEW for the whole arrangement.
//
// The texture is the HUD canvas rendered into a transparent target. uGUI blends every
// channel with SrcAlpha / OneMinusSrcAlpha, so over a clear (0,0,0,0) target the colour
// arrives already multiplied by coverage, and the alpha arrives as coverage squared. So:
// premultiplied blending, and the square root to get coverage back. Exact for one layer of
// UI, close for stacked ones - a half-faded blackout darkens the wall by half, not a quarter.
//
// Always on top, never fogged, no depth: the HUD lies on the screens themselves, in front
// of everything the world can put there.
Shader "Hidden/Journey/GetReal3DWallHud_NEW"
{
    Properties
    {
        _MainTex ("HUD", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                c.a = sqrt(saturate(c.a));
                return c;
            }
            ENDCG
        }
    }
}
