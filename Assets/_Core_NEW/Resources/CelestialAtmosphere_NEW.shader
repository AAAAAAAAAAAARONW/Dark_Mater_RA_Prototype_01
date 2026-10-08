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
    // Premultiplied: it adds light, and dims the stars behind the limb a little. _Color fades it.

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
                float3 world : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i, float face : VFACE) : SV_Target
            {
                float3 c = _Centre.xyz;
                float rg = _Radii.x, ra = _Radii.y;
                float3 o = _WorldSpaceCameraPos;
                float3 d = normalize(i.world - o);
                float3 oc = o - c;

                // Only the face the camera looks through first: outside, the near side; inside, the far.
                bool inside = dot(oc, oc) < ra * ra;
                if ((face > 0) == inside) return 0;

                // The ray through the air, stopping at the ground.
                float b = dot(oc, d);
                float disc = b * b - (dot(oc, oc) - ra * ra);
                if (disc <= 0.0) return 0;
                float s = sqrt(disc);
                float t0 = max(-b - s, 0.0), t1 = -b + s;
                float discG = b * b - (dot(oc, oc) - rg * rg);
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
                    float3 p = o + d * (t0 + (k + 0.5) * dt) - c;
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
                return fixed4(rgb * _Color.rgb, alpha * _Color.a);
            }
            ENDCG
        }
    }

    FallBack Off
}
