// The asteroid belt's dust (AsteroidBelt_NEW): soft round specks, added, so a few thousand of
// them read as a faint band from across the Solar System. The speck's brightness is its
// particle colour times its alpha; _Color is what WorldSwitcher_NEW scales to fade it.
//
// 21 Nov: SEEN EDGE ON. From inside the belt's plane, as the journey mostly sees it, the
// specks along each line of sight pile up into two bright smudges either side of where the
// light's line clears them. So each speck now fades as it is seen more nearly edge on
// (_BeltUp, the belt's up, from AsteroidBelt_NEW; _EdgeOn, how much is left fully edge on).
// From above, the belt is as it was.
Shader "Journey NEW/Asteroid Dust"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _BeltUp ("The belt's up, world space (set by script)", Vector) = (0, 1, 0, 0)
        _EdgeOn ("Left of a speck seen fully edge on", Range(0, 1)) = 0.12
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
            float4 _BeltUp;
            float _EdgeOn;

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
                float  seen : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv * 2 - 1;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 look = normalize(world - _WorldSpaceCameraPos);
                float grazing = abs(dot(look, normalize(_BeltUp.xyz)));
                // (22 Nov: a quarter of _EdgeOn left, faded over a wider band of angles; a third
                // of the way edge on still read as a smudge along the light's path.)
                o.seen = lerp(_EdgeOn * 0.25, 1.0, smoothstep(0.06, 0.6, grazing));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float f = saturate(1 - dot(i.uv, i.uv));
                f *= f;
                return fixed4(i.color.rgb * _Color.rgb * (i.color.a * f * i.seen), 1);
            }
            ENDCG
        }
    }
}
