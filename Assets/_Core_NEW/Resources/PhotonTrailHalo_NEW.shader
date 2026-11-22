// The soft light around the photon trail (TrailLook_NEW, option A), drawn on a mesh that
// TrailFacing_NEW builds from the trail's points, a few times the trail's width.
//
// BEAM (_Beam = 1, the default in the journey): the mesh is a round tube and the glow is how
// much of it the sight line passes through — n·v, full down the middle, nothing at the
// silhouette — so it is a soft cloud of light round the beam from every side, with no edges.
// It was a flat band first, which read as a ribbonfish.
// RIBBON (_Beam = 0): a band facing the camera, glow across uv.y.
//
// No spectrum, and kept faint over the beam itself: _Hollow is the share of this mesh's
// width the trail covers, and inside that the halo is cut to _Inside, so it never washes over
// the colours or fills an absorption line. Queued before the trail, so the trail's black
// absorption pass still lands on top of it.
//
// uv.x runs along the trail (0 = head, 1 = tail).
Shader "Custom/PhotonTrailHalo_NEW"
{
    Properties
    {
        _Color     ("Color", Color) = (0.69, 0.84, 1, 1)
        _Intensity ("Intensity", Range(0, 2)) = 0.3
        _Hollow    ("Trail share of width (set by script)", Range(0, 1)) = 0.38
        _Inside    ("Left over the trail", Range(0, 1)) = 0.25
        _Beam      ("Beam (set by script)", Float) = 1
        _HeadFade  ("Head fade, share of length (set by script)", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "Queue"="Transparent-1" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull [_Cull]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            float  _Intensity;
            float  _Hollow;
            float  _Inside;
            float  _Beam;
            float  _HeadFade;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float3 normal : NORMAL; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float c : TEXCOORD1; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = v.uv;
                o.color = v.color;
                // c: 0 down the middle of the glow as seen, 1 at its edge.
                if (_Beam > 0.5)
                {
                    float3 wp  = mul(unity_ObjectToWorld, v.vertex).xyz;
                    float3 n   = UnityObjectToWorldNormal(v.normal);
                    float  ndv = abs(dot(n, normalize(_WorldSpaceCameraPos - wp)));
                    o.c = sqrt(saturate(1.0 - ndv * ndv));
                }
                else o.c = abs(v.uv.y - 0.5) * 2.0;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float c     = saturate(i.c);
                float g     = exp(-c * c * 3.0) * smoothstep(1.0, 0.7, c);
                float off   = lerp(_Inside, 1.0, smoothstep(_Hollow * 0.6, _Hollow * 1.05, c));
                float tail  = pow(saturate(1.0 - i.uv.x), 1.3);
                tail *= _HeadFade > 0.0001 ? smoothstep(0.0, _HeadFade, i.uv.x) : 1.0;   // soft at the head
                float alpha = g * off * tail * i.color.a * _Intensity;
                return fixed4(_Color.rgb, saturate(alpha));
            }
            ENDCG
        }
    }
}
