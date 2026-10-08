Shader "Custom/PlanetAtmosphere_NEW"
{
    // A planet's atmosphere (SolarSystemLook_NEW): a thin shell a little bigger than the planet,
    // glowing at its edge — brightest on the side towards the Sun, faint round the night side,
    // and brighter again where the Sun is behind the planet and its light comes through the
    // air at the limb. It is what makes a small planet read against black space, and a planet
    // seen from its night side a crescent instead of a hole.
    //
    // The shell's front faces, additive: across the planet's face the glow is nothing, so the
    // planet shows through untouched; it gathers towards the edge and fades out at the very
    // edge of the shell, so it has no hard rim.
    //
    // _Color fades it as WorldSwitcher_NEW fades a world. In Resources so a build always has it.

    Properties
    {
        _Color ("Fade (as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        [HDR] _Tint ("Atmosphere", Color) = (0.35, 0.6, 1, 1)
        _Power ("How close to the edge it gathers", Float) = 3
        _Edge ("How sharply it fades at the shell's edge", Float) = 5
        _Night ("Night side, of the day side", Range(0, 1)) = 0.25
        _Backlit ("Sun behind the planet", Float) = 1.5
        _SunPos ("Sun (world)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float4 _Tint, _SunPos;
            float _Power, _Edge, _Night, _Backlit;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 world  : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 view = normalize(_WorldSpaceCameraPos - i.world);
                float3 toSun = normalize(_SunPos.xyz - i.world);

                float facing = saturate(dot(n, view));
                float rim = pow(1.0 - facing, _Power) * saturate(facing * _Edge);

                // Day side, wrapped a little round the terminator; and the Sun behind it.
                float day = saturate(dot(n, toSun) * 0.6 + 0.4);
                float behind = pow(saturate(dot(-view, toSun)), 4.0);
                float light = rim * (lerp(_Night, 1.0, day) + behind * _Backlit);

                return float4(_Tint.rgb * light * _Color.rgb, 0.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
