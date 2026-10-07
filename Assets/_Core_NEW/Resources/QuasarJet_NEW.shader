Shader "Custom/QuasarJet_NEW"
{
    // A quasar's jet (QuasarVFX_NEW): a beam along the object's +Y from the core, 1 long, turned
    // round its own axis to face the camera wherever it is, so it is a beam from every side.
    // Narrow and white at its heart, a wider blue sheath round it, brightest at the base and
    // fading out along it, with knots that flow outward. Additive. _Color fades it.
    //
    // The mesh is a strip: x is -1 or 1 across it, y how far along. In Resources so a build
    // always has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        _Width ("Width at the far end (of its length)", Float) = 0.035
        [HDR] _Core ("Heart", Color) = (2.6, 2.9, 3.2, 1)
        [HDR] _Sheath ("Sheath", Color) = (0.35, 0.55, 1.1, 1)
        _Flow ("Knots, lengths a second", Float) = 0.25
        _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent+2" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
            float _Width, _Flow, _Seed;
            float4 _Core, _Sheath;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;   // x across, -1..1; y along, 0..1
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                float along = vertex.y;
                float3 axisPoint = mul(unity_ObjectToWorld, float4(0, along, 0, 1)).xyz;
                float3 axis = mul((float3x3)unity_ObjectToWorld, float3(0, 1, 0));
                float length_ = length(axis);
                axis /= max(length_, 1e-5);

                // Across: square to the beam and to the way to the camera.
                float3 toCamera = _WorldSpaceCameraPos - axisPoint;
                float3 across = cross(axis, toCamera);
                float l = length(across);
                across = l > 1e-5 ? across / l : float3(1, 0, 0);

                // A narrow cone: a little wide at the base, widening along it.
                float width = _Width * length_ * (0.25 + 0.75 * along);
                float3 world = axisPoint + across * vertex.x * width;

                o.pos = mul(UNITY_MATRIX_VP, float4(world, 1.0));
                o.uv = float2(vertex.x, along);
                return o;
            }

            float Hash(float n) { return frac(sin(n) * 43758.5453); }

            float Noise(float x)
            {
                float i = floor(x);
                float f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(Hash(i + _Seed), Hash(i + 1.0 + _Seed), f);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float across = i.uv.x;
                float along = i.uv.y;

                float heart = exp(-across * across * 30.0);
                float sheath = exp(-across * across * 1.6) * 0.5;

                // Brightest at the base, fading out; soft at the very start, where the core is.
                float fall = pow(1.0 - along, 1.4) * smoothstep(0.0, 0.04, along);
                float knots = 0.6 + 0.4 * Noise(along * 14.0 - _Time.y * _Flow * 14.0)
                                    * (0.6 + 0.4 * Noise(along * 37.0 - _Time.y * _Flow * 37.0));

                float3 colour = _Core.rgb * heart + _Sheath.rgb * sheath;
                // WorldSwitcher_NEW fades by scaling all four channels of _Color; additive, so
                // rgb alone is the fade.
                return fixed4(colour * fall * knots * _Color.rgb, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
