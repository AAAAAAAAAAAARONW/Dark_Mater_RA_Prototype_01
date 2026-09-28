Shader "Custom/DiveHalo_NEW"
{
    // The glow of a cluster being left: a soft ball of light, bright in the middle and
    // fading outward as a Gaussian, that looks right from inside it as well as from out.
    // No raymarch: how much of a Gaussian a ray from the camera passes through has a
    // closed form, so each pixel evaluates it once. The mesh (a cube around the ball,
    // faces turned inward) only has to cover the pixels the ball can reach.
    //
    // Additive, queue 2985: after the cosmic web's volumes, before the dive's crowd (2990)
    // and the photon trail (3000). ZTest Always: it is light in the space between the
    // camera and everything else. In Resources so a build always has it: LayerDive_NEW
    // loads it by name.

    Properties
    {
        _Color ("Colour (alpha = brightness looking through its middle)", Color) = (1, 0.85, 0.45, 1)
        _Sigma ("Falloff radius (world units)", Float) = 10
    }

    SubShader
    {
        Tags { "Queue"="Transparent-15" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        ZTest Always
        Cull Front

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float  _Sigma;

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float3 world  : TEXCOORD0;
                float3 centre : TEXCOORD1;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.world = mul(unity_ObjectToWorld, vertex).xyz;
                o.centre = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                return o;
            }

            // Abramowitz and Stegun 7.1.26, within 1.5e-7.
            float Erf(float x)
            {
                float s = x < 0.0 ? -1.0 : 1.0;
                x = abs(x);
                float t = 1.0 / (1.0 + 0.3275911 * x);
                float y = 1.0 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t
                                   - 0.284496736) * t + 0.254829592) * t * exp(-x * x);
                return s * y;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // The ray from the camera through this pixel, and its closest approach to
                // the middle: t0 along it, b to the side.
                float3 d  = normalize(i.world - _WorldSpaceCameraPos);
                float3 oc = i.centre - _WorldSpaceCameraPos;
                float  t0 = dot(d, oc);
                float  b2 = max(0.0, dot(oc, oc) - t0 * t0);
                float  s  = max(_Sigma, 1e-3);

                // The share of the whole ball's light this ray passes through, from the
                // camera on: 1 looking through the middle from outside, 0.5 from the middle.
                float share = exp(-b2 / (s * s)) * 0.5 * (1.0 + Erf(t0 / s));

                return fixed4(_Color.rgb * (_Color.a * share), 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
