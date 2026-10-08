Shader "Custom/QuasarHole_NEW"
{
    // The black hole at the middle of a quasar (QuasarVFX_NEW), as the pictures of one draw
    // it: a disc of black — its shadow — edged by a thin, bright photon ring, with the far
    // side of the accretion disc bent round it by its gravity into an arc that hugs the
    // shadow, over its top and under its bottom.
    //
    // A quad turned to face the camera round the object's middle, drawn after the far half of
    // the disc and before the near half (QuasarVFX_NEW splits the disc at the middle's depth):
    // so the shadow blocks the gas behind the hole, the far jet and the far side of the disc,
    // and the near side of the disc still crosses in front of it. Its radius is _Radius of
    // the object's scale — of the disc's radius.
    //
    // The arc. Seen from above, the image of the far side of the disc is lifted over the
    // shadow; seen edge on, it is the bright band arching over the top and under the bottom
    // that the pictures show. So it is a ring just outside the photon ring, brightest across
    // the disc's line on screen the more edge on the disc is, streaked with the disc's own
    // gas turning at its inner edge, and brighter on the side whose gas comes towards the
    // camera (beaming), as the disc is.
    //
    // LATER: THE ARC AS THE PICTURES DRAW IT. It was a soft glow fading out from the ring, and
    // read as a haze round the shadow. Now it is a band with edges: from just outside the
    // photon ring — where the disc's inner edge is lensed to — out to a little past it face
    // on, and towards edge on out to half as far again as the shadow over the top, where it
    // is brightest, and a thinner, dimmer one under the bottom (the image of the disc's
    // underside); brightest along its inner edge, in the disc's own hot colour, cooling
    // outward.
    //
    // Flying into the core (the tutorial does), the hole goes before the camera reaches it, as
    // the core's own shadow does: inside the core's glow there is no hole, just the light. And
    // far off, when it is down to a few pixels — the journey's quasar shrinks into a point as
    // the light leaves it — it goes too, and the core is a point of light again.
    //
    // Premultiplied alpha: the ring and the arc add light, the shadow blocks what is behind.
    // _Color fades it as WorldSwitcher_NEW fades a world. In Resources so a build always has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _Noise ("Noise (QuasarNoise_NEW)", 2D) = "gray" {}
        _Radius ("Shadow radius (of the object's scale)", Float) = 0.1
        _Near ("Gone within this far of the middle, x2 (of the object's scale)", Float) = 0.35
        _Ring ("Photon ring", Float) = 1
        [HDR] _RingColor ("Photon ring colour", Color) = (2, 1.85, 1.6, 1)
        _Lens ("Lensed far side of the disc", Float) = 1
        [HDR] _ArcHot ("Lensed disc, inner", Color) = (2.08, 1.6, 0.96, 1)
        [HDR] _ArcMid ("Lensed disc, outer", Color) = (1.36, 0.34, 0.04, 1)
        _Spin ("Disc turns a second at its inner edge", Float) = 0.25
        _Beaming ("Relativistic beaming", Range(0, 1)) = 0.5
        _Seed ("Seed", Float) = 0

        // Set every frame by QuasarVFX_NEW.
        _Pace ("Clock rate", Float) = 1
        _PaceOffset ("Clock offset", Float) = 0
        _Bright ("Brightness", Float) = 1
        _Pulse ("Flicker", Float) = 1
        _FlareAge ("Seconds since the eruption", Float) = 1000
        _FlareGain ("Eruption strength", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent-1" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            #define TAU 6.2831853

            sampler2D _Noise;
            fixed4 _Color;
            float _Radius, _Near, _Ring, _Lens, _Spin, _Beaming, _Seed;
            float _Pace, _PaceOffset, _Bright, _Pulse, _FlareAge, _FlareGain;
            float4 _RingColor, _ArcHot, _ArcMid;

            // The quad reaches this many shadow radii out, for the arc.
            #define Reach 1.9

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 q     : TEXCOORD0;   // in shadow radii from the middle, on the screen's axes
                float4 axis  : TEXCOORD1;   // xy: the disc's axis on screen; z: how edge on (0 face on, 1 edge on); w: the fade near the camera
                float2 come  : TEXCOORD2;   // on screen, towards the side of the disc whose gas comes at the camera (0 face on)
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                float3 middle = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float scale = length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));
                float3 view = mul(UNITY_MATRIX_V, float4(middle, 1.0)).xyz;

                float radius = scale * _Radius;
                view.xy += vertex.xy * radius * Reach;
                o.pos = mul(UNITY_MATRIX_P, float4(view, 1.0));
                o.q = vertex.xy * Reach;

                // The disc's axis, and its tilt to the camera. View space: the camera looks
                // down -z, so z is the part of the axis pointing at or away from it.
                float3 up = normalize(mul((float3x3)UNITY_MATRIX_V, mul((float3x3)unity_ObjectToWorld, float3(0, 1, 0))));
                float2 axis = length(up.xy) > 1e-4 ? normalize(up.xy) : float2(0.0, 1.0);
                float edgeOn = saturate(1.0 - abs(up.z));

                // The disc's gas turns as (x, z) -> (-z, x); the side of it coming straight at
                // a camera seen from c (flat, in the disc's plane) is (c.z, -c.x).
                float3 camera = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                float2 c = camera.xz;
                float3 coming = length(c) > 1e-5 ? float3(c.y, 0.0, -c.x) / length(c) : float3(0.0, 0.0, 0.0);
                float3 comingView = mul((float3x3)UNITY_MATRIX_V, mul((float3x3)unity_ObjectToWorld, coming));
                o.come = length(comingView.xy) > 1e-5 ? normalize(comingView.xy) * edgeOn : float2(0.0, 0.0);

                // 0 with the camera inside the core's glow, 1 from twice its size out; and 0
                // with the shadow under a pixel and a half in radius, 1 from four.
                float fade = saturate(-view.z / max(scale * _Near, 1e-6) - 1.0);
                float pixels = radius / max(-view.z, 1e-4) * abs(unity_CameraProjection._m11) * _ScreenParams.y * 0.5;
                fade *= smoothstep(1.5, 4.0, pixels);
                o.axis = float4(axis, edgeOn, fade);
                return o;
            }

            float Sq(float x) { return x * x; }

            fixed4 frag(v2f i) : SV_Target
            {
                // The derivative first, while every pixel of the quad is still running.
                float r = length(i.q);
                float aa = max(fwidth(r), 1e-4);   // one pixel, in shadow radii
                if (r > Reach) return 0;

                float fade = i.axis.w;

                float2 dir = r > 1e-4 ? i.q / r : float2(0.0, 1.0);
                float T = _Time.y * _Pace + _PaceOffset;
                float flash = _FlareGain * exp(-_FlareAge / 0.5);

                // Brighter on the side coming at the camera, as the disc is.
                float beam = 1.0 + _Beaming * 0.8 * dot(dir, i.come);
                beam = max(beam, 0.15);
                beam *= beam;

                // The photon ring: thin, but never under a pixel and a half.
                float width = max(0.035, aa * 1.5);
                float ring = exp(-Sq((r - 1.0) / width)) * _Ring * (0.85 + 0.3 * beam);

                // The arc: the far side of the disc, lifted round the shadow (see LATER, above).
                // The gas in it turns at the disc's inner edge.
                float up = dot(dir, i.axis.xy);            // 1 over the top, -1 under the bottom
                float across = abs(up);
                float edgeOn = i.axis.z;
                float top = up > 0.0 ? 1.0 : 0.0;
                float lift = edgeOn * across * across;
                float inner = 1.04 + aa;
                float outer = 1.0 + lerp(0.14, lerp(0.24, 0.42, top), lift);
                float place = saturate((r - inner) / max(outer - inner, 1e-3));   // 0 its inner edge, 1 its outer
                float band = smoothstep(inner - aa, inner + max(aa, 0.02), r)
                           * (1.0 - smoothstep(outer - (outer - inner) * 0.45, outer + aa, r));
                float gather = lerp(0.6, lerp(0.45, 1.3, top) * (0.3 + 0.7 * across), edgeOn);
                float side = 1.0;
                band *= lerp(1.0, 0.55, place);           // brightest along its inner edge

                float a = atan2(dir.y, dir.x);
                float u = a / TAU * 3.0 - T * _Spin * 0.5 + _Seed * 0.173;
                float v = (r - 1.0) * 2.5 + _Seed * 0.291;
                float4 gas = tex2Dlod(_Noise, float4(u, v, 0.0, 1.0));
                float streak = 0.45 + 0.55 * smoothstep(0.3, 0.75, gas.r);

                float arc = band * gather * side * streak * _Lens * beam;
                float3 arcColour = lerp(_ArcHot.rgb, _ArcMid.rgb, smoothstep(0.15, 1.0, place));

                float3 light = (_RingColor.rgb * ring + arcColour * arc) * _Pulse * _Bright * (1.0 + 2.0 * flash);

                // The shadow: black inside the ring, its edge a pixel soft.
                float shadow = 1.0 - smoothstep(1.0 - aa, 1.0 + aa * 0.5, r);

                light *= fade;
                shadow *= fade;

                // WorldSwitcher_NEW fades by scaling all four channels of _Color: the light
                // takes rgb, the shadow a — once each, a straight fade.
                return fixed4(light * _Color.rgb, shadow * _Color.a);
            }
            ENDCG
        }
    }

    FallBack Off
}
