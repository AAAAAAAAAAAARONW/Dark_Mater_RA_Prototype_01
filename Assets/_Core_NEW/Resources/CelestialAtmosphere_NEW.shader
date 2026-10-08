Shader "Custom/CelestialAtmosphere_NEW"
{
    // A planet's air (CelestialBody_NEW): a thin shell, and for each pixel the light scattered
    // towards the camera along its ray through it, added up over a few samples — air thinning
    // exponentially with height, lit where the Sun reaches it, scattered with Rayleigh's phase.
    // So it is what it is in the photographs from orbit: a thin bright band at the limb on the
    // day side, a faint veil over the ground, an orange rim along the terminator where the
    // light has come a long way through the air, and nothing on the night side.
    //
    // The ray is the camera's own, against the real spheres (_Centre, _Radii, set every frame),
    // so the shell's mesh only has to cover it. Drawn from whichever side faces the camera —
    // the outside, or the inside when the camera is in the air.
    //
    // The shell is so close above the ground that at the journey's distances the depth buffer
    // cannot tell them apart, and the two would flicker. So each vertex is slid along its own
    // line of sight towards the camera by the planet's radius: it lands on the same pixel, but
    // always in front of its own planet. (Only something within a radius in front of the
    // planet could be drawn over, and the air there is faint.)
    //
    // Premultiplied: it adds light, and dims the stars behind the limb a little. _Color fades it.
    //
    // LATER: NOT WHILE THE PLANET IS A FEW PIXELS. Growing out of a speck in the dive from the
    // Milky Way, a planet is a ten-thousandth of its size, hundreds of units from the origin:
    // the air is then thinner than a float can place a point by, and its density — exponential
    // in the height — came out as noise, a different speck of light every frame. The air comes
    // up as the planet grows from 6 to 16 pixels in radius (a band that thin is not seen
    // before), and every point is worked out from the planet's centre, not the origin.
    //
    // LATER STILL: STEADY AT ANY SIZE. The band of air at the limb is a fortieth of the
    // radius: under a pixel wide until the planet is forty pixels across, it landed on pixels
    // and missed them as the planet moved, and Earth's limb twinkled. It now comes up from 24
    // to 64 pixels in radius, where the band is wide enough to draw. And the ray each pixel
    // looks along is the camera's offset to the shell, interpolated, rather than the shell's
    // world position less the camera's: hundreds of units out, those positions are a float's
    // step apart by more than the air's own thickness, and each pixel's ray came out a little
    // different every frame. Where a ray passes the planet is measured square to it, not as
    // the difference of two large squares.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        _SunPosition ("The Sun, world space (set every frame)", Vector) = (0, 0, 0, 1)
        _Centre ("The planet's centre, world space (set every frame)", Vector) = (0, 0, 0, 1)
        _Radii ("Ground and top of the air, world units (set every frame)", Vector) = (1, 1.025, 0, 0)
        _ScaleHeight ("Scale height, of the air's depth", Range(0.05, 1)) = 0.25
        [HDR] _Scatter ("Scattering colour (Rayleigh: blue)", Color) = (0.3, 0.6, 1.5, 1)
        [HDR] _Sunset ("Through a long path (the terminator)", Color) = (1.1, 0.45, 0.18, 1)
        _Strength ("Strength", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
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

            fixed4 _Color;
            float4 _SunPosition, _Centre, _Radii, _Scatter, _Sunset;
            float _ScaleHeight, _Strength;

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float3 ray   : TEXCOORD0;   // from the camera to the shell, world space
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 fromCamera = world - _WorldSpaceCameraPos;
                float distance = max(length(fromCamera), 1e-5);
                float pulled = max(distance - _Radii.x, _ProjectionParams.y * 2.0);
                o.pos = mul(UNITY_MATRIX_VP, float4(_WorldSpaceCameraPos + fromCamera * (min(pulled, distance) / distance), 1.0));
                o.ray = fromCamera;
                return o;
            }

            fixed4 frag(v2f i, float face : VFACE) : SV_Target
            {
                float3 c = _Centre.xyz;
                float rg = _Radii.x, ra = _Radii.y;
                float3 o = _WorldSpaceCameraPos;
                float3 d = normalize(i.ray);
                float3 oc = o - c;

                // Only the face the camera looks through first: outside, the near side; inside, the far.
                bool inside = dot(oc, oc) < ra * ra;
                if ((face > 0) == inside) return 0;

                // Nothing while the planet is a few pixels across (see LATER, above).
                float pixels = rg / max(length(oc), 1e-6) * abs(unity_CameraProjection._m11) * _ScreenParams.y * 0.5;
                float show = smoothstep(24.0, 64.0, pixels);
                if (show <= 0.0) return 0;

                // The ray through the air, stopping at the ground.
                float b = dot(oc, d);
                float3 across = oc - d * b;   // from the centre to where the ray passes nearest
                float miss2 = dot(across, across);
                float disc = ra * ra - miss2;
                if (disc <= 0.0) return 0;
                float s = sqrt(disc);
                float t0 = max(-b - s, 0.0), t1 = -b + s;
                float discG = rg * rg - miss2;
                if (discG > 0.0)
                {
                    float tg = -b - sqrt(discG);
                    if (tg > 0.0) t1 = min(t1, tg);
                }
                if (t1 <= t0) return 0;

                float3 l = normalize(_SunPosition.xyz - c);
                float h = max((ra - rg) * _ScaleHeight, 1e-5);
                float cosTheta = dot(d, normalize(_SunPosition.xyz - o));
                float phase = 0.75 * (1.0 + cosTheta * cosTheta);

                const int Samples = 8;
                float dt = (t1 - t0) / Samples;
                float3 light = 0.0;
                float depth = 0.0;
                [unroll]
                for (int k = 0; k < Samples; k++)
                {
                    float3 p = oc + d * (t0 + (k + 0.5) * dt);
                    float r = length(p);
                    float density = exp(-(r - rg) / h);
                    float sunUp = dot(p / r, l);
                    float lit = smoothstep(-0.1, 0.25, sunUp);
                    // Near the terminator the light has come a long way through the air.
                    float3 colour = lerp(_Sunset.rgb, _Scatter.rgb, smoothstep(-0.02, 0.25, sunUp));
                    light += colour * density * lit * dt;
                    depth += density * dt;
                }

                // Normalised so a ray grazing the limb of a thin shell comes out about _Strength.
                float norm = 1.0 / (h * 12.0);
                float3 rgb = light * norm * phase * _Strength;
                float alpha = saturate(depth * norm * 0.25 * _Strength);
                return fixed4(rgb * _Color.rgb * show, alpha * _Color.a * show);
            }
            ENDCG
        }
    }

    FallBack Off
}
