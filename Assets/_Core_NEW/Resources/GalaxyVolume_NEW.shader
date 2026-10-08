Shader "Custom/GalaxyVolume_NEW"
{
    // The Milky Way's depth (GalaxyVFX_NEW): the light of its old stars as a volume, so the
    // galaxy has thickness at a slant and edge-on instead of being a picture on a plane.
    //
    //   bulge     boxy, peanut-shaped: the bar seen from its side puffs up into an X, as the
    //             infrared maps of the Milky Way's centre show it; a bright nucleus inside
    //   thick     the old disc, exponential, flaring (thicker outward), warped with the gas
    //   thin      a faint blue glow hugging the middle, the young disc's scattered light
    //   halo      a dim round glow of old stars round everything
    //   dust      a thin layer along the middle; seen edge-on, or from inside the disc, the
    //             lane of darkness through the glow that the photographs of the Milky Way show
    //
    // Everything is smooth (no texture reads), so a few samples per ray are enough. The ray is
    // the camera's own through a box round the galaxy, drawn from its back faces so it works
    // from inside too. It is drawn twice, split where the ray crosses the disc's middle: the
    // far half before the gas sheet (GalaxyDisc_NEW), whose dust then darkens it, the near half
    // after. _Side picks the half: 1 the camera's side of the plane, -1 the far side.
    //
    // Additive. _Color fades it. In Resources so a build has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        _Side ("Half: 1 the camera's side of the disc, -1 the far side", Float) = 1
        _Box ("Box half size (x, y, z)", Vector) = (1.1, 0.45, 1.1, 0)
        _Bar ("Bar: angle (deg), length, width", Vector) = (27, 0.24, 0.075, 0)
        _Warp ("Warp at the rim (of the radius)", Float) = 0.15
        _Thickness ("Old disc scale height at the centre, and its flare", Vector) = (0.028, 0.04, 0, 0)
        _Volume ("Brightness", Range(0, 4)) = 1
        _DustVolume ("Dust along the middle", Range(0, 2)) = 0.45
        [HDR] _Core ("Bulge", Color) = (1.6, 1.25, 0.85, 1)
        [HDR] _Old ("Old disc", Color) = (1.0, 0.78, 0.52, 1)
        [HDR] _Young ("Young disc glow", Color) = (0.4, 0.62, 1.3, 1)
        [HDR] _Halo ("Halo", Color) = (0.35, 0.38, 0.5, 1)
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
            #include "Galaxy_NEW.cginc"

            fixed4 _Color;
            float _Side, _Volume, _DustVolume;
            float4 _Box, _Bar, _Thickness;
            float4 _Core, _Old, _Young, _Halo;

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

            float3 Emission(float3 p, float2 bar)
            {
                float r = length(p.xz);
                float y = p.y - GalaxyWarp(p.xz);
                float edge = smoothstep(1.05, 0.65, r);

                // The old disc, flaring, and the young disc's glow along its middle.
                float h = _Thickness.x + _Thickness.y * r;
                float thick = exp(-r / 0.24 - abs(y) / h) * edge;
                float thin = exp(-r / 0.34 - Sq(y / 0.012)) * smoothstep(0.08, 0.3, r) * edge;

                // The bulge: boxy, and taller off-centre along the bar (the peanut's X).
                float2 q = float2(p.x * bar.x + p.z * bar.y, -p.x * bar.y + p.z * bar.x);
                float hz = 0.05 + 0.035 * exp(-Sq((abs(q.x) - 0.1) / 0.06));
                float rb = pow(pow(abs(q.x) / (_Bar.y * 0.85), 2.4) + pow(abs(q.y) / (_Bar.z * 1.3), 2.4) + pow(abs(p.y) / hz, 2.4), 1.0 / 2.4);
                float R = length(p);
                float bulge = exp(-rb * 2.6) + 1.6 * exp(-R / 0.018);

                float halo = pow(1.0 + Sq(R / 0.16), -1.5);

                return _Old.rgb * thick * 1.2 + _Young.rgb * thin * 0.6 + _Core.rgb * bulge * 1.4 + _Halo.rgb * halo * 0.15;
            }

            // The dust's density: a thin layer along the middle, so thin that only a ray
            // running along it — edge-on, or from inside the disc — gathers much of it, and
            // there it is a dark lane through the glow.
            float Dust(float3 p)
            {
                float r = length(p.xz);
                float y = p.y - GalaxyWarp(p.xz);
                return _DustVolume * 100.0 * exp(-r / 0.32 - Sq(y / 0.01)) * smoothstep(0.06, 0.18, r) * smoothstep(1.0, 0.7, r);
            }

            // Samples on t = a + span u², crowding towards a; taken nearest the camera first, so
            // the dust each one is seen through is the dust before it.
            void March(float3 o, float3 d, float a, float span, float2 bar, inout float tau, inout float3 light)
            {
                const int Samples = 10;
                [unroll]
                for (int k = 0; k < Samples; k++)
                {
                    int j = span < 0.0 ? Samples - 1 - k : k;
                    float u = (j + 0.5) / Samples;
                    float dt = abs(span) * 2.0 * u / Samples;
                    float3 p = o + d * (a + span * u * u);
                    float dust = Dust(p) * dt;
                    tau += dust * 0.5;
                    light += Emission(p, bar) * exp(-tau) * dt;
                    tau += dust * 0.5;
                }
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 o = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                float3 d = normalize(i.obj - o);

                // The ray through the box.
                float3 inv = 1.0 / (abs(d) > 1e-6 ? d : 1e-6);
                float3 ta = (-_Box.xyz - o) * inv, tb = (_Box.xyz - o) * inv;
                float3 tlo = min(ta, tb), thi = max(ta, tb);
                float tEnter = max(max(max(tlo.x, tlo.y), tlo.z), 0.0);
                float tExit = min(min(thi.x, thi.y), thi.z);
                if (tExit <= tEnter) return 0;

                // The half on this side of the disc's middle, samples crowding towards the
                // plane, where the light and the dust are.
                float tPlane = abs(d.y) > 1e-5 ? -o.y / d.y : -1.0;
                bool crosses = tPlane > tEnter && tPlane < tExit;
                float2 bar = float2(cos(radians(_Bar.x)), sin(radians(_Bar.x)));
                float tau = 0.0;
                float3 light = 0.0;

                if (_Side > 0.0)
                {
                    if (crosses)
                    {
                        March(o, d, tPlane, tEnter - tPlane, bar, tau, light);
                    }
                    else
                    {
                        // Never reaching the plane: out from the middle of the way, both ways.
                        float middle = (tEnter + tExit) * 0.5;
                        March(o, d, middle, tEnter - middle, bar, tau, light);
                        March(o, d, middle, tExit - middle, bar, tau, light);
                    }
                }
                else
                {
                    if (!crosses) return 0;
                    // The dust on the near side first (its light is the other half's to draw).
                    const int Samples = 10;
                    float span = tEnter - tPlane;
                    [unroll]
                    for (int k = 0; k < Samples; k++)
                    {
                        float u = (k + 0.5) / Samples;
                        tau += Dust(o + d * (tPlane + span * u * u)) * abs(span) * 2.0 * u / Samples;
                    }
                    March(o, d, tPlane, tExit - tPlane, bar, tau, light);
                }

                return fixed4(light * _Volume * _Color.rgb, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
