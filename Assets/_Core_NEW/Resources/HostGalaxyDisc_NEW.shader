Shader "Custom/HostGalaxyDisc_NEW"
{
    // The quasar's host galaxy (HostGalaxyVFX_NEW), its gas and young stars as a sheet: not the
    // Milky Way, a younger, wilder galaxy round a quasar (see HostGalaxy_NEW.cginc for its shape).
    //
    //   arms    two, open, warm peach, knotted with giant clumps of star formation, dust lanes
    //           along their inner edges; lopsided, the first running on into a faint tidal plume
    //   bar     short and fat, gold, with straight dust lanes along its leading edges carrying
    //           gas in to the ring
    //   ring    round the quasar: a bright, knotted ring of star formation where that gas piles up
    //   bulge   gold, round, under the quasar's light
    //
    // Everything turns the way the quasar's disc does: the pattern slowly (_PatternSpeed), the
    // gas through it on its orbits, read twice half a cycle apart and crossfaded so it never
    // winds up. Seen at a slant the light builds up and the dust closes, as through more of
    // the layer.
    //
    // Premultiplied over what is behind, queue 2990: after its far half of glow (2989), before
    // its stars (2992) and the quasar's disc (2998+). _Color fades it. In Resources so a build
    // has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _Noise ("Noise (QuasarNoise_NEW)", 2D) = "gray" {}
        _Pitch ("Arm pitch, degrees", Float) = 22
        _Bar ("Bar: angle (deg), half length, half width", Vector) = (20, 0.3, 0.1, 0)
        _RingRadius ("Starburst ring radius (of the radius)", Float) = 0.13
        _RingWidth ("Starburst ring width (of the radius)", Float) = 0.016
        _Lopsided ("Lopsided: one arm on into a tidal plume", Range(0, 1)) = 0.6
        _PatternSpeed ("Pattern turn, radians a second", Float) = 0.004
        _ArmWidth ("Arm width at the bar's end, and its growth outward", Vector) = (0.035, 0.06, 0, 0)
        _ScaleLength ("Disc scale length (of the radius)", Float) = 0.22
        _Thickness ("Layer's height at the centre, and its growth outward", Vector) = (0.05, 0.06, 0, 0)
        _Dust ("Dust", Range(0, 2)) = 1.2
        _Clumps ("Giant clumps of star formation", Range(0, 3)) = 1.2
        _Haze ("Light between the arms", Range(0, 1)) = 0.25
        _Brightness ("Brightness", Range(0, 3)) = 0.6
        _Flow ("Gas flow along the orbits, radii a second", Float) = 0.012
        _Twinkle ("Clumps brightening and dimming", Range(0, 1)) = 0.4
        [HDR] _Core ("Bulge and bar", Color) = (1.5, 1.05, 0.55, 1)
        [HDR] _Arm ("Arms", Color) = (1.3, 0.92, 0.62, 1)
        [HDR] _Between ("Between the arms", Color) = (0.55, 0.38, 0.26, 1)
        [HDR] _Ring ("Starburst ring", Color) = (1.7, 1.25, 0.85, 1)
        [HDR] _Clump ("Clumps", Color) = (1.7, 1.2, 0.95, 1)
        [HDR] _Glow ("Glow round the clumps and the ring", Color) = (1.2, 0.5, 0.42, 1)
        [HDR] _Plume ("Tidal plume", Color) = (0.55, 0.42, 0.34, 1)
        _DustColor ("Dust", Color) = (0.1, 0.05, 0.025, 1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
            #include "HostGalaxy_NEW.cginc"

            sampler2D _Noise;
            fixed4 _Color;
            float _RingWidth, _ScaleLength, _Dust, _Clumps, _Haze, _Brightness, _Flow, _Twinkle;
            float4 _ArmWidth, _Thickness;
            float4 _Core, _Arm, _Between, _Ring, _Clump, _Glow, _Plume;
            fixed4 _DustColor;

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 plane : TEXCOORD0;
                float3 local : TEXCOORD1;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.plane = vertex.xz;
                o.local = vertex.xyz;
                return o;
            }

            float4 Look(float2 uv, float2 dx, float2 dy) { return tex2Dgrad(_Noise, uv, dx, dy); }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.plane;
                float2 dpx = ddx(p), dpy = ddy(p);
                float r = max(length(p), 1e-4);

                // Angle and its change across the pixel, from the plane's (no seam at ±180°).
                float az0 = atan2(p.y, p.x);
                float2 dAz = float2(p.x * dpx.y - p.y * dpx.x, p.x * dpy.y - p.y * dpy.x) / (r * r);
                float2 dLnr = float2(dot(p, dpx), dot(p, dpy)) / (r * r);
                float lnr = log(r);

                // Large-scale wander, so the arms are not drawn with a compass.
                float4 big = Look(p * 0.6 + float2(0.13, 0.57), dpx * 0.6, dpy * 0.6);
                float az = az0 + (big.r - 0.5) * 0.45;

                // The gas: log-polar noise at two scales, streaming round on a flat rotation
                // curve (faster inside), read twice half a cycle apart and crossfaded.
                const float cycle = 30.0;
                float ph = frac(_Time.y / cycle);
                float drift = _Flow / max(r, 0.05) * cycle;
                float4 n = 0, m = 0;
                [unroll]
                for (int k = 0; k < 2; k++)
                {
                    float phase = k == 0 ? ph : frac(ph + 0.5);
                    float w = 1.0 - abs(phase * 2.0 - 1.0);
                    float azF = az - drift * phase + k * 2.3;
                    float2 uv = float2(azF / HG_TAU * 5.0 + lnr * 0.8, lnr * 2.4 - azF / HG_TAU);
                    float2 udx = float2(dAz.x / HG_TAU * 5.0 + dLnr.x * 0.8, dLnr.x * 2.4 - dAz.x / HG_TAU);
                    float2 udy = float2(dAz.y / HG_TAU * 5.0 + dLnr.y * 0.8, dLnr.y * 2.4 - dAz.y / HG_TAU);
                    n += Look(uv, udx * 2.0, udy * 2.0) * w;
                    m += Look(uv * 3.0 + float2(0.71, 0.23), udx * 4.0, udy * 4.0) * w;
                }
                float4 ng = Look(p * 7.0 + float2(0.37, 0.91), dpx * 7.0, dpy * 7.0);
                float4 nc = Look(p * 1.7 + float2(0.83, 0.41), dpx * 1.7, dpy * 1.7);

                // The two arms, broken into clumps.
                float width = _ArmWidth.x + _ArmWidth.y * r;
                float clump = 0.3 + 1.2 * m.r * (0.55 + 0.9 * n.r);
                float arm0 = exp(-HGSq(HGArmAcross(r, az, 0.0, 0.0) / width)) * HGArmReach(r, 0.0);
                float arm1 = exp(-HGSq(HGArmAcross(r, az, 1.0, 0.0) / width)) * HGArmReach(r, 1.0);
                float arm = (arm0 + arm1) * clump;
                float plume = HGPlume(r, az, width) * (0.3 + 1.1 * n.g);

                // Giant clumps: round knots where the arms' gas is thickest, each on its own slow
                // clock — slow, or hundreds of them read as the screen flickering.
                float knots = smoothstep(0.58, 0.82, ng.r) * smoothstep(0.45, 0.72, ng.g) * smoothstep(0.4, 0.7, m.b);
                float beat = lerp(1.0, 0.6 + 0.8 * (0.5 + 0.5 * sin(_Time.y * (0.12 + 0.3 * nc.g) + nc.b * 25.0)), _Twinkle);
                float clumps = knots * saturate((arm0 + arm1) * 1.3) * _Clumps * beat;

                // The ring: knotted, bright, round the quasar.
                float ringAcross = (r - _RingRadius * (1.0 + (n.b - 0.5) * 0.12)) / _RingWidth;
                float ring = exp(-HGSq(ringAcross)) * (0.15 + 1.2 * smoothstep(0.42, 0.8, m.g) * (0.6 + 0.6 * n.a));
                float ringGlow = exp(-HGSq(ringAcross * 0.35));

                // The bar, the bulge and the disc of older stars.
                float2 q = HGBarFrame(p);
                float bar = exp(-pow(HGSq(q.x / _Bar.y) + HGSq(q.y / _Bar.z), 1.1) * 2.4);
                float bulge = exp(-HGSq(r / 0.03)) * 0.7 + 0.15 * exp(-HGSq(r / 0.1));
                float edge = smoothstep(1.02, 0.72, r + (n.r - 0.5) * 0.35);
                // Inside the ring the gas has fallen in to it: darker there, round the quasar's own light.
                float disc = exp(-r / _ScaleLength) * edge * lerp(0.15, 1.0, smoothstep(_RingRadius * 0.8, _RingRadius * 2.0, r));

                // Dust: lanes on the arms' inner edges, the bar's straight lanes into the ring, a
                // lane spiralling in round the ring, and a patchy haze.
                float lane = (exp(-HGSq(HGArmAcross(r, az, 0.0, -0.08) / (width * 0.55))) * HGArmReach(r, 0.0)
                            + exp(-HGSq(HGArmAcross(r, az, 1.0, -0.08) / (width * 0.55))) * HGArmReach(r, 1.0))
                           * (0.45 + 0.9 * smoothstep(0.3, 0.7, n.b));
                float barLane = exp(-HGSq((q.y - sign(q.x) * (_Bar.z * 0.45 + 0.08 * HGSq(q.x / _Bar.y))) / 0.02))
                              * smoothstep(_Bar.y * 1.05, _Bar.y * 0.7, abs(q.x)) * smoothstep(_RingRadius * 0.8, _RingRadius * 1.3, abs(q.x));
                float ringLane = exp(-HGSq((r - _RingRadius * 1.35) / 0.012)) * (0.3 + 0.7 * n.a);
                float dust = saturate((lane + barLane * 0.8 + ringLane * 0.5) * _Dust
                                      + smoothstep(0.55, 0.8, n.g) * 0.25 * _Dust * smoothstep(0.08, 0.2, r) * edge);

                float3 light = _Core.rgb * (bulge * 1.3 + bar * 0.45)
                             + lerp(_Between.rgb, _Core.rgb, exp(-r / 0.2)) * disc * _Haze * 2.0
                             + _Arm.rgb * arm * 0.85
                             + _Plume.rgb * plume
                             + _Ring.rgb * ring * 0.8 + _Glow.rgb * ringGlow * 0.06
                             + _Clump.rgb * clumps * 1.2 + _Glow.rgb * clumps * 0.4;
                light *= 0.8 + 0.4 * ng.a;   // grain

                // Seen at a slant, through more of the layer: the light builds up and the dust
                // closes — gently, and never to a hard line along the far rim.
                float3 camera = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                float3 toCamera = camera - i.local;
                float slant = min(length(toCamera) / max(abs(toCamera.y), 1e-4), 8.0) / 1.84;
                float through = pow(1.0 - saturate(dust * 0.85), slant);
                float hide = 1.0 - through;
                light *= sqrt(min(slant, 2.5));
                float3 premultiplied = (light * through + _DustColor.rgb * hide) * _Brightness;
                float alpha = hide * 0.8;

                // From inside the layer there is no sheet to see: the arms and lanes are all
                // round, and the glow of the volume (HostGalaxyVolume_NEW) is what is there. So it
                // goes as the camera comes within a few of the layer's heights of its middle.
                float camR = length(camera.xz);
                float layer = _Thickness.x + _Thickness.y * camR;
                float within = smoothstep(layer * 0.8, layer * 3.0, abs(camera.y)) + smoothstep(1.3, 1.6, camR);
                premultiplied *= saturate(within);
                alpha *= saturate(within);

                // Outside the plume's reach, nothing.
                float inside = step(r, 1.45);
                return fixed4(premultiplied * _Color.rgb * inside, alpha * _Color.a * inside);
            }
            ENDCG
        }
    }

    FallBack Off
}
