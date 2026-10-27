// The asteroid belt's rocks (AsteroidBelt_NEW): mesh particles lit from the Sun's position,
// like the planets (CelestialSurface_NEW), not by the scene's lights — those are per-planet
// fills. Drawn GPU-instanced when the particle renderer can, batched otherwise; both work,
// since a batched mesh particle's vertices are already in world space.
//
// _Color is what WorldSwitcher_NEW scales to fade the Solar System in and out.
Shader "Journey NEW/Asteroid Rock"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _Exposure ("Sunlight", Float) = 1.4
        _Fill ("Fill on the night side", Range(0, 0.3)) = 0.04
        _SunPosition ("The Sun, world space (set every frame)", Vector) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:vertInstancingSetup

            #include "UnityCG.cginc"
            #include "UnityStandardParticleInstancing.cginc"

            fixed4 _Color;
            float _Exposure, _Fill;
            float4 _SunPosition;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 world : TEXCOORD1;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);

                fixed4 c = v.color;
                vertInstancingColor(c);

                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityWorldToClipPos(o.world);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.color = c * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 l = normalize(_SunPosition.xyz - i.world);
                float lit = saturate(dot(normalize(i.normal), l));
                return fixed4(i.color.rgb * (lit * _Exposure + _Fill), 1);
            }
            ENDCG
        }
    }
}
