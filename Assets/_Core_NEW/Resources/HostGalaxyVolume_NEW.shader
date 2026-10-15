Shader "Custom/HostGalaxyVolume_NEW"
{
    // The quasar's host galaxy's depth (HostGalaxyVFX_NEW): the light of its stars as a volume,
    // so it is puffy, as the scientists asked a galaxy in the early universe to be, rather than
    // a picture on a plane — a thick disc, a round bulge, the starburst ring standing up out of
    // the middle as a torus of light, a dim halo; and a dusty middle that, edge on or from inside,
    // is a dark lane through the glow.
    //
    // Everything is smooth (no texture reads), so a few samples a ray are enough. The ray is
    // the camera's own through a box round the galaxy, drawn from its back faces so it works
    // from inside too (the tutorial flies into it). Drawn twice, split where the ray crosses
    // the middle: the far half before the gas sheet (HostGalaxyDisc_NEW), whose dust darkens
    // it, the near half after. _Side picks the half: 1 the camera's side, -1 the far side.
    //
    // Additive. _Color fades it. In Resources so a build has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        _Side ("Half: 1 the camera's side of the disc, -1 the far side", Float) = 1
        _Box ("Box half size (x, y, z)", Vector) = (1.45, 0.5, 1.45, 0)
        _Bar ("Bar: angle (deg), half length, half width", Vector) = (20, 0.3, 0.1, 0)
        _RingRadius ("Starburst ring radius (of the radius)", Float) = 0.13
        _PatternSpeed ("Pattern turn, radians a second", Float) = 0.004
        _Pitch ("Arm pitch, degrees", Float) = 22
        _Lopsided ("Lopsided", Range(0, 1)) = 0.6
        _Thickness ("Disc scale height at the centre, and its growth outward", Vector) = (0.05, 0.06, 0, 0)
        _Volume ("Brightness", Range(0, 4)) = 1
        _DustVolume ("Dust along the middle", Range(0, 2)) = 0.6
        _Resolve ("Nearer than x the light is all stars, from y all glow (of the radius)", Vector) = (0.03, 0.4, 0, 0)
        [HDR] _Core ("Bulge", Color) = (1.5, 1.05, 0.55, 1)
        [HDR] _Old ("Disc", Color) = (0.9, 0.62, 0.4, 1)
        [HDR] _Ring ("Starburst ring", Color) = (1.4, 0.8, 0.55, 1)
        [HDR] _Halo ("Halo", Color) = (0.45, 0.36, 0.3, 1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent-11" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Front

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "HostGalaxy_NEW.cginc"

            fixed4 _Color;
            float _Side, _Volume, _DustVolume;
            float4 _Box, _Thickness, _Resolve;
            float4 _Core, _Old, _Ring, _Halo;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 obj : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.obj = vertex.xyz;
                return o;
            }

            float3 Emission(float3 p)
            {
                float r = length(p.xz);
                float y = p.y;
                float R = length(p);
                float edge = smoothstep(1.05, 0.6, r);

                // The disc, puffy and thicker outward; a lopsided swell on the first arm's side.
                float h = _Thickness.x + _Thickness.y * r;
                float side = 1.0 + 0.35 * _Lopsided * dot(p.xz / max(r, 1e-4), float2(cos(HGPattern()), sin(HGPattern())));
                float disc = exp(-r / 0.26 - abs(y) / h) * edge * side;

                // The bulge: round, with a bright nucleus round the quasar.
                float bulge = exp(-R / 0.05) * 0.6 + 0.5 * exp(-R / 0.015);

                // The starburst ring, standing up out of the middle as a torus.
                float ring = exp(-HGSq((r - _RingRadius) / 0.025) - HGSq(y / 0.022));

                float halo = pow(1.0 + HGSq(R / 0.22), -1.5);

                return _Old.rgb * disc * 1.1 + _Core.rgb * bulge * 0.9 + _Ring.rgb * ring * 0.6 + _Halo.rgb * halo * 0.15;
            }

            // The dust: a layer along the middle, thick enough to be a lane edge on.
            float Dust(float3 p)
            {
                float r = length(p.xz);
                return _DustVolume * 60.0 * exp(-r / 0.4 - HGSq(p.y / 0.018)) * smoothstep(_RingRadius * 0.9, _RingRadius * 1.6, r) * smoothstep(1.05, 0.7, r);
            }

            // Samples on t = a + span u², crowding towards a; nearest the camera first, so the
            // dust each one is seen through is the dust before it.
            //
            // Near the camera its light is not a glow but stars, each one seen: HostGalaxyStars_NEW
            // draws those. So the light close by is left out, and from inside — the tutorial
            // flies in to the quasar at its heart — the galaxy is stars on every side and the
            // glow of its far reaches as a band across the sky, not a fog. From outside nothing
            // is that close, and it is all there.
            void March(float3 o, float3 d, float a, float span, inout float tau, inout float3 light)
            {
                const int Samples = 12;
                [unroll]
                for (int k = 0; k < Samples; k++)
                {
                    int j = span < 0.0 ? Samples - 1 - k : k;
                    float u = (j + 0.5) / Samples;
                    float dt = abs(span) * 2.0 * u / Samples;
                    float3 p = o + d * (a + span * u * u);
                    float dust = Dust(p) * dt;
                    float seen = smoothstep(_Resolve.x, _Resolve.y, a + span * u * u);
                    tau += dust * 0.5;
                    light += Emission(p) * exp(-tau) * dt * seen;
                    tau += dust * 0.5;
                }
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 o = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                float3 d = normalize(i.obj - o);

                float3 inv = 1.0 / (abs(d) > 1e-6 ? d : 1e-6);
                float3 ta = (-_Box.xyz - o) * inv, tb = (_Box.xyz - o) * inv;
                float3 tlo = min(ta, tb), thi = max(ta, tb);
                float tEnter = max(max(max(tlo.x, tlo.y), tlo.z), 0.0);
                float tExit = min(min(thi.x, thi.y), thi.z);
                if (tExit <= tEnter) return 0;

                float tPlane = abs(d.y) > 1e-5 ? -o.y / d.y : -1.0;
                bool crosses = tPlane > tEnter && tPlane < tExit;
                float tau = 0.0;
                float3 light = 0.0;

                if (_Side > 0.0)
                {
                    if (crosses)
                    {
                        March(o, d, tPlane, tEnter - tPlane, tau, light);
                    }
                    else
                    {
                        // Never reaching the middle: out from the nearest point to the centre,
                        // both ways, where the light is.
                        float middle = clamp(-dot(o, d), tEnter, tExit);
                        March(o, d, middle, tEnter - middle, tau, light);
                        March(o, d, middle, tExit - middle, tau, light);
                    }
                }
                else
                {
                    if (!crosses) return 0;
                    const int Samples = 12;
                    float span = tEnter - tPlane;
                    [unroll]
                    for (int k = 0; k < Samples; k++)
                    {
                        float u = (k + 0.5) / Samples;
                        tau += Dust(o + d * (tPlane + span * u * u)) * abs(span) * 2.0 * u / Samples;
                    }
                    March(o, d, tPlane, tExit - tPlane, tau, light);
                }

                return fixed4(light * _Volume * _Color.rgb, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
