// The asteroid belt's dust (AsteroidBelt_NEW): soft round specks, added, so a few thousand of
// them read as a faint band from across the Solar System. The speck's brightness is its
// particle colour times its alpha; _Color is what WorldSwitcher_NEW scales to fade it.
Shader "Journey NEW/Asteroid Dust"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
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

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv * 2 - 1;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float f = saturate(1 - dot(i.uv, i.uv));
                f *= f;
                return fixed4(i.color.rgb * _Color.rgb * (i.color.a * f), 1);
            }
            ENDCG
        }
    }
}
