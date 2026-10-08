Shader "Custom/SunCorona_NEW"
{
    // The Sun's corona (SolarSystemLook_NEW): the light round a star, which the Sun's own
    // shader — a lit ball with a rim — does not draw. A bright inner corona hugging the limb,
    // broken into slow streamers round it, and a wide faint halo beyond.
    //
    // A quad turned to face the camera round the Sun's middle, sized in the Sun's own radii
    // (_Radius is the sphere's radius in its mesh's units, so the corona follows the Sun's
    // scale through a dive). Additive. _Color fades it as WorldSwitcher_NEW fades a world.
    // In Resources so a build always has it.

    Properties
    {
        _Color ("Fade (as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        [HDR] _Tint ("Corona", Color) = (1.4, 0.85, 0.4, 1)
        _Radius ("The Sun's radius, in its mesh's units", Float) = 0.5
        _Inner ("Inner corona: falls to a third this far out, in radii", Float) = 0.22
        _Outer ("Halo: falls to a third this far out, in radii", Float) = 1.1
        _OuterAmount ("Halo brightness, of the corona's", Float) = 0.18
        _Streamers ("Streamers", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define Reach 4.0

            fixed4 _Color;
            float4 _Tint;
            float _Radius, _Inner, _Outer, _OuterAmount, _Streamers;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 q   : TEXCOORD0;   // in the Sun's radii from its middle
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                float3 middle = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float scale = length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));
                float3 view = mul(UNITY_MATRIX_V, float4(middle, 1.0)).xyz;
                view.xy += vertex.xy * scale * _Radius * Reach;
                o.pos = mul(UNITY_MATRIX_P, float4(view, 1.0));
                o.q = vertex.xy * Reach;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float r = length(i.q);
                if (r > Reach) return 0;

                float out_ = max(r - 1.0, 0.0);
                float inner = exp(-out_ / max(_Inner, 1e-3));
                float halo = exp(-out_ / max(_Outer, 1e-3)) * _OuterAmount;

                // Streamers: slow, uneven rays round the limb.
                float a = atan2(i.q.y, i.q.x);
                float t = _Time.y;
                float rays = 0.5 + 0.25 * sin(a * 7.0 + t * 0.05) + 0.15 * sin(a * 13.0 - t * 0.07 + 1.3)
                           + 0.1 * sin(a * 23.0 + t * 0.11 + 4.1);
                inner *= lerp(1.0, 0.55 + 0.9 * rays, _Streamers * saturate(out_ * 6.0));

                // Over the disc itself, the Sun's own shader draws it: only a little here.
                float disc = lerp(0.25, 1.0, smoothstep(0.85, 1.0, r));
                float edge = 1.0 - smoothstep(Reach * 0.8, Reach, r);

                return float4(_Tint.rgb * (inner + halo) * disc * edge * _Color.rgb, 0.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
