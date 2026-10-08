Shader "Custom/FadeParticle_NEW"
{
    // A stand-in that WorldSwitcher_NEW swaps onto a particle renderer for as long as its
    // world is fading, for particles whose own shader cannot fade: Mobile/Particles/Additive
    // and Alpha Blended declare nothing but a texture, so the alpha written to a group never
    // reached them and a galaxy could only be switched on or off. The galaxies are made of
    // them.
    //
    // It draws what those draw — the texture times the particle's colour, blended additively
    // or by alpha as the original was (_SrcBlend, _DstBlend) — times _Color: rgb the glow a
    // world brightens by as it dissolves, a the fade. And, while a dive flies the camera
    // through the world, it fades particles that come close to the camera (_NearFade): a
    // particle is a sprite facing the camera, and one that reaches the near plane is cut
    // away whole, at half the screen's size — a flash, one after another as a magnified
    // galaxy streams past.
    //
    // In Resources so a build always has it: WorldSwitcher_NEW loads it by name.

    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _Color ("Fade (rgb: glow, a: alpha)", Color) = (1, 1, 1, 1)
        _NearFade ("Near fade, world units from the camera (from, to); to <= from is off", Vector) = (0, 0, 0, 0)
        [HideInInspector] _SrcBlend ("Source blend", Float) = 5
        [HideInInspector] _DstBlend ("Destination blend", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Blend [_SrcBlend] [_DstBlend]
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _NearFade;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
                float2 uv    : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;

                if (_NearFade.y > _NearFade.x)
                {
                    float depth = -UnityObjectToViewPos(v.vertex.xyz).z;
                    o.color.a *= saturate((depth - _NearFade.x) / (_NearFade.y - _NearFade.x));
                }
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv) * i.color;
            }
            ENDCG
        }
    }

    FallBack Off
}
