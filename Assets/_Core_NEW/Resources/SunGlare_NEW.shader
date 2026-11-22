Shader "Custom/SunGlare_NEW"
{
    // The Sun as a camera sees it (SunLook_NEW): what the lens adds over the ultraviolet disc,
    // gently, so the disc's texture still shows through.
    //
    //   core     the middle of the disc a little overexposed: a soft warm-white wash, strongest
    //            at the centre, gone by the limb.
    //   glare    a soft halo round the limb, wider and whiter than the dark red glow
    //            (SunGlow_NEW) - the lens's, not the Sun's.
    //   spikes   diffraction spikes: four long, four short between them, thin - about a pixel
    //            and a half, however near the Sun is - warm at the Sun and cooling to their tips.
    //
    // A quad turned to the camera, centred on the Sun but brought forward to just in front of
    // the disc, so it lies over the disc and yet a planet passing in front hides it. Added over
    // what is behind. Measured in the Sun's radii. _Color fades it as WorldSwitcher_NEW fades
    // a world. In Resources so a build has it. (22 Nov.)
    //
    // KEPT UNDER THE BLOOM. The camera's bloom spreads anything over 1 into a white haze, so
    // all of this stays well under it; and it fades away as the Sun shrinks to a few pixels
    // on the screen, where a pixel-wide spike would be wider than the Sun and fill the quad -
    // a bright square (22 Nov, first try in Unity). It falls to nothing before the quad's edge.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        _Core ("Overexposed middle", Range(0, 2)) = 0.15
        _Glare ("Glare round the limb", Range(0, 2)) = 0.35
        _Spikes ("Diffraction spikes", Range(0, 2)) = 0.55
        _SpikeLength ("Spike length, the Sun's radii", Range(1, 12)) = 6
        _SpikeAngle ("Spike angle, radians", Float) = 0.35
        _MeshRadius ("The Sun's mesh radius, object space", Float) = 0.5
        _Extent ("Quad half-size, in the Sun's radii", Float) = 12
    }

    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Core, _Glare, _Spikes, _SpikeLength, _SpikeAngle, _MeshRadius, _Extent;

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 q   : TEXCOORD0;   // in the Sun's radii, across the screen
                float  px  : TEXCOORD1;   // the Sun's radii a pixel, about
                float  seen : TEXCOORD2;  // 0 while the Sun is only a few pixels across
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 centre = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float radius = length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0))) * _MeshRadius;
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                float3 toEye = normalize(_WorldSpaceCameraPos - centre);
                // Just in front of the disc's nearest point, so it is drawn over the disc.
                float3 front = centre + toEye * radius * 1.02;
                float2 q = v.vertex.xy * _Extent;
                float3 world = front + (right * q.x + up * q.y) * radius;
                o.pos = mul(UNITY_MATRIX_VP, float4(world, 1));
                o.q = q;
                float dist = max(length(_WorldSpaceCameraPos - centre), radius * 1.05);
                // Pixels across the screen's height for a radius at that distance.
                float pixelsPerRadius = radius / dist * _ScreenParams.y * 0.5 * UNITY_MATRIX_P[1][1];
                o.px = 1.0 / max(pixelsPerRadius, 1e-3);
                o.seen = smoothstep(3.0, 12.0, pixelsPerRadius);
                return o;
            }

            // One spike along a, b across it: about a pixel and a half wide (px radii a pixel),
            // a little wider near the Sun, bright there and fading out to len.
            float Spike(float a, float b, float px, float len)
            {
                float along = saturate(1.0 - abs(a) / len);
                float w = min(px * 1.5, 0.05) + 0.015 * along;
                return exp(-(b * b) / (2.0 * w * w)) * along * along;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float r = length(i.q);

                // The overexposed middle: on the disc only, softly.
                float core = exp(-r * r / (0.55 * 0.55)) * (1.0 - smoothstep(0.85, 1.02, r));
                float3 c = float3(1.0, 0.82, 0.58) * core * _Core;

                // Glare round the limb, falling off outwards.
                float off = max(r - 1.0, 0.0);
                float glare = exp(-off / 0.55) * 0.6 + exp(-off / 2.2) * 0.25;
                c += float3(1.0, 0.72, 0.45) * glare * _Glare * smoothstep(0.85, 1.05, r);

                // Diffraction spikes: four long, four short at 45 degrees to them.
                float s = sin(_SpikeAngle), k = cos(_SpikeAngle);
                float2 p = float2(k * i.q.x + s * i.q.y, -s * i.q.x + k * i.q.y);
                float2 d = float2(p.x + p.y, p.x - p.y) * 0.70710678;
                float spikes = Spike(p.x, p.y, i.px, _SpikeLength) + Spike(p.y, p.x, i.px, _SpikeLength)
                             + 0.4 * (Spike(d.x, d.y, i.px, _SpikeLength * 0.45) + Spike(d.y, d.x, i.px, _SpikeLength * 0.45));
                float3 tint = lerp(float3(1.0, 0.85, 0.62), float3(0.78, 0.86, 1.0), saturate((r - 1.0) / _SpikeLength));
                c += tint * spikes * _Spikes * smoothstep(0.9, 1.1, r);

                c *= i.seen * (1.0 - smoothstep(_Extent * 0.7, _Extent, r));
                return fixed4(min(c, 0.8) * _Color.rgb * _Color.a, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
