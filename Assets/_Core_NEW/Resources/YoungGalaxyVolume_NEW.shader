Shader "Custom/YoungGalaxyVolume_NEW"
{
    // The young galaxy's depth (YoungGalaxyVFX_NEW): the light of its gas and stars as a
    // volume, so it is thick and puffy, as the galaxies of cosmic noon were — a fat turbulent
    // disc, the giant clumps as knots of glow standing up out of it, a small warm core, the
    // cold streams as threads of light falling in from outside, and the wind of hot gas the
    // star formation blows out above and below. Everything is smooth (no texture reads), so
    // a few samples a ray are enough.
    //
    // The ray is the camera's own through a box round the galaxy, drawn from its back faces
    // so it works from inside too (the light flies through). Drawn twice, split where the ray
    // crosses the middle: the far half before the gas sheet (YoungGalaxyDisc_NEW), whose dust
    // darkens it, the near half after. _Side picks the half: 1 the camera's side, -1 the far
    // side. Near the camera its light is left out (_Resolve): that is stars, each one drawn.
    //
    // Additive. _Color fades it. In Resources so a build has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        _Side ("Half: 1 the camera's side of the disc, -1 the far side", Float) = 1
        _Box ("Box half size (x, y, z)", Vector) = (2.0, 0.8, 2.0, 0)
        _Seed ("Seed", Float) = 0
        _ClumpTurn ("Clump turn, radians a second at radius 0.5", Float) = 0.01
        _Thickness ("Disc scale height at the centre, and its growth outward", Vector) = (0.06, 0.07, 0, 0)
        _Streams ("Cold streams, 0 to 3", Float) = 2
        _Volume ("Brightness", Range(0, 4)) = 1
        _DustVolume ("Dust along the middle", Range(0, 2)) = 0.5
        _Wind ("The wind above and below the disc", Range(0, 3)) = 1
        _Resolve ("Nearer than x the light is all stars, from y all glow (of the radius)", Vector) = (0.02, 0.12, 0, 0)
        [HDR] _Core ("Core", Color) = (1.5, 0.85, 0.5, 1)
        [HDR] _Gas ("The thick disc", Color) = (0.45, 0.5, 1.0, 1)
        [HDR] _Glow ("Ionised gas round the clumps", Color) = (1.7, 0.55, 0.85, 1)
        [HDR] _Clump ("Clump cores", Color) = (1.5, 1.75, 2.4, 1)
        [HDR] _Stream ("Cold streams", Color) = (0.5, 0.85, 0.95, 1)
        [HDR] _WindColor ("Wind", Color) = (0.8, 0.4, 1.0, 1)
        [HDR] _Halo ("Halo", Color) = (0.3, 0.32, 0.55, 1)
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
            #include "YoungGalaxy_NEW.cginc"

            fixed4 _Color;
            float _Side, _Volume, _DustVolume, _Wind;
            float4 _Box, _Resolve;
            float4 _Core, _Gas, _Glow, _Clump, _Stream, _WindColor, _Halo;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 obj : TEXCOORD0;
            };

            // How far inside the disc's layer the camera is, 0 outside to 1 within it: from
            // inside, the glow of the gas all round would be a fog, so most of it is left out
            // and the clumps, the core and the far reaches are what is seen (set per pixel).
            static float gInside = 0.0;

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
                float edge = smoothstep(1.1, 0.6, r);

                // The thick disc, and the warm core in it.
                float h = YGHeight(r);
                float disc = exp(-r / 0.3 - abs(y) / h) * edge;
                float core = exp(-R / 0.06) * 0.7 + 0.3 * exp(-R / 0.015);

                // The clumps as knots of glow, and their cores.
                float cores, glow;
                YGClumps(p, 1.3, cores, glow);

                // The streams, the wind, a dim halo.
                float streams = 0.0;
                [unroll]
                for (int s = 0; s < 3; s++) streams += YGStream(p, s);
                float wind = YGWind(p) * _Wind;
                float halo = pow(1.0 + YGSq(R / 0.3), -1.5);

                float keep = 1.0 - 0.85 * gInside;
                return _Gas.rgb * disc * 0.7 * keep + _Core.rgb * core * 0.9
                     + _Glow.rgb * glow * 0.2 * (1.0 - 0.6 * gInside) + _Clump.rgb * cores * 0.35
                     + _Stream.rgb * streams * 0.2 + _WindColor.rgb * wind * 0.25 * keep
                     + _Halo.rgb * halo * 0.12 * keep;
            }

            // The dust: a layer along the middle, thick enough to be a lane edge on.
            float Dust(float3 p)
            {
                float r = length(p.xz);
                return _DustVolume * 50.0 * exp(-r / 0.35 - YGSq(p.y / 0.02)) * smoothstep(1.05, 0.7, r);
            }

            // Samples on t = a + span u², crowding towards a; nearest the camera first, so the
            // dust each one is seen through is the dust before it. The light close by is left
            // out (_Resolve): from inside, the galaxy is stars on every side and the glow of its
            // far reaches, not a fog.
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
                float camR = length(o.xz);
                gInside = smoothstep(YGHeight(camR) * 5.0, YGHeight(camR) * 1.5, abs(o.y)) * smoothstep(1.3, 0.95, camR);

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
