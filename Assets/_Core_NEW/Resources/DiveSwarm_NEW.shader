Shader "Custom/DiveSwarm_NEW"
{
    // What a dive's point resolves into: the galaxies of a cluster (and, on later gates,
    // the stars of a galaxy). One mesh, one draw — every member is a quad whose four
    // vertices all sit at its centre, and the vertex shader spreads them into a small
    // camera-facing glow there, stretched along its line away from the point while the
    // dive opens the members up, so the ones streaming past read as streaks.
    //
    // Additive, queue 2990: after the cosmic web's volumes, before the photon trail —
    // the members sit inside the web and behind the light, like the dive's own glow.
    // In Resources so a build always has it: LayerDive_NEW loads it by name.

    Properties
    {
        _Alpha    ("Alpha", Range(0, 4)) = 1
        _Streak   ("Streak", Range(0, 20)) = 0
        _SizeScale("Size scale", Float) = 1
        _NearFade ("Near fade (start, end)", Vector) = (0.5, 3, 0, 0)
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

            float  _Alpha, _Streak, _SizeScale;
            float4 _NearFade;

            struct appdata
            {
                float4 vertex : POSITION;    // member centre, object space
                fixed4 color  : COLOR;       // member colour; alpha = its brightness
                float2 uv     : TEXCOORD0;   // quad corner, -1..1
                float2 size   : TEXCOORD1;   // x = radius in object units
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;

                float3 centre = UnityObjectToViewPos(v.vertex.xyz);
                float3 origin = UnityObjectToViewPos(float3(0, 0, 0));
                float  scale  = length(unity_ObjectToWorld._m00_m10_m20);

                float2 offset = v.uv * v.size.x * scale * _SizeScale;

                // Stretch along the member's line away from the point, on screen.
                float2 away = centre.xy / max(-centre.z, 1e-3) - origin.xy / max(-origin.z, 1e-3);
                float  len  = length(away);
                float2 dir  = len > 1e-4 ? away / len : float2(0, 1);
                offset += dir * dot(offset, dir) * _Streak * saturate(len * 4.0);

                o.pos = mul(UNITY_MATRIX_P, float4(centre + float3(offset, 0), 1.0));
                o.uv = v.uv;

                // Members passing the camera would fill the screen: fade them out first.
                float near = saturate((-centre.z - _NearFade.x) / max(1e-3, _NearFade.y - _NearFade.x));

                // Members still inside the point are part of its glow, not yet resolved:
                // they brighten as they separate from it (full by about three degrees).
                float resolved = saturate((len - 0.01) / 0.04);

                o.color = v.color;
                o.color.a *= near * resolved * _Alpha;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float r = length(i.uv);
                float halo = saturate(1.0 - r);
                halo *= halo;
                float core = saturate(1.0 - r * 2.5);
                core *= core;
                return fixed4(i.color.rgb * (halo * 0.6 + core) * i.color.a, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
