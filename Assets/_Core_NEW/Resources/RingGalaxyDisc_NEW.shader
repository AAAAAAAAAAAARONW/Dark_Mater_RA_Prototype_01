Shader "Custom/RingGalaxyDisc_NEW"
{
    // The ring galaxy's face as a sheet (RingGalaxyVFX_NEW; its shape is in RingGalaxy_NEW.cginc):
    //
    //   ring      a band of young blue star clusters, bunched with gaps between the bunches, its
    //             outer edge sharper than its inner, a thick bright crescent on the intruder's side
    //             and a thin faint arc opposite; up close it gives way to the stars themselves
    //   knots     giant regions of star formation along it: blue-white cores in a pink glow of
    //             ionised hydrogen, and a scatter of smaller ones
    //   middle    emptied by the ripple: the old disc's faint warm light, the yellow nucleus off
    //             centre in a small dusty ring of its own, and faint spokes out to the ring
    //   dust      a thin lane along the ring's inner edge, and a little round the nucleus
    //   outside   the faint remains of the old disc, and a plume of gas pulled out towards the
    //             intruder
    //
    // The ring and what is in it go round (_Turn), its colour shifting a little from blue-teal to
    // blue-violet along it. Every few seconds a ripple spreads from the nucleus (_Ripple), a faint
    // broken band of light across the empty middle that lights the ring and its knots as it
    // crosses them. Seen at a slant the light builds up and the dust closes, as through more of
    // the layer.
    //
    // Premultiplied over what is behind, queue 2990: after its far half of glow (2989), before
    // its stars (2992). _Color fades it. In Resources so a build has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _Noise ("Noise (QuasarNoise_NEW)", 2D) = "gray" {}
        _Seed ("Seed", Float) = 0
        _Turn ("Turn, radians a second", Float) = 0.02
        _Ripple ("Ripples: seconds to spread, strength; twinkle; a knot's life, seconds", Vector) = (14, 1, 0.6, 50)
        _Ring ("Ring: radius, half width, lopsidedness, egg-shape", Vector) = (0.72, 0.085, 0.85, 0.12)
        _CompTilt ("The intruder's disc turned about its long axis, radians", Float) = -0.785
        _Comp ("The intruder: where (of the radius), and its size", Vector) = (-0.98, 0.0, -0.05, 0.3)
        _Knots ("Knots of star formation", Range(0, 3)) = 1
        _Spokes ("Spokes", Range(0, 2)) = 0
        _Interior ("The emptied middle's old light", Range(0, 2)) = 1
        _Plume ("The plume towards the intruder", Range(0, 2)) = 1
        _Dust ("Dust", Range(0, 2)) = 1
        _Brightness ("Brightness", Range(0, 3)) = 0.6
        [HDR] _Core ("Nucleus", Color) = (1.6, 1.05, 0.55, 1)
        [HDR] _Inner ("The haze in the middle", Color) = (0.3, 0.38, 0.62, 1)
        [HDR] _Spoke ("Spokes", Color) = (0.45, 0.55, 0.85, 1)
        [HDR] _Gas ("The ring", Color) = (0.45, 0.62, 1.25, 1)
        [HDR] _Clump ("Knot cores", Color) = (1.5, 1.75, 2.4, 1)
        [HDR] _Glow ("Ionised gas round the knots", Color) = (1.4, 0.6, 1.1, 1)
        [HDR] _PlumeColor ("Plume and the old disc outside", Color) = (0.35, 0.5, 0.95, 1)
        _DustColor ("Dust", Color) = (0.05, 0.03, 0.03, 1)
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
            #include "RingGalaxy_NEW.cginc"

            sampler2D _Noise;
            fixed4 _Color;
            float _Knots, _Spokes, _Interior, _Plume, _Dust, _Brightness;
            float4 _Core, _Inner, _Spoke, _Gas, _Clump, _Glow, _PlumeColor;
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
                float T = _Time.y;

                // Angle round the ring and its change across the pixel, from the plane's (no seam
                // at ±180°: the noise is read a whole number of times round).
                float a = atan2(p.y, p.x);
                float2 dA = float2(p.x * dpx.y - p.y * dpx.x, p.x * dpy.y - p.y * dpy.x) / (r * r);
                float2 dR = float2(dot(p, dpx), dot(p, dpy)) / r;
                float aR = a - _Turn * T + _Seed;

                float2 uvA = float2(aR / RG_TAU * 6.0, r * 2.4);
                float2 adx = float2(dA.x / RG_TAU * 6.0, dR.x * 2.4), ady = float2(dA.y / RG_TAU * 6.0, dR.y * 2.4);
                float4 nA = Look(uvA, adx, ady);
                float4 nB = Look(uvA * 3.0 + float2(0.0, 0.37), adx * 3.0, ady * 3.0);
                float4 ng = Look(p * 5.0 + float2(0.37, 0.91) + _Seed * 0.1, dpx * 5.0, dpy * 5.0);
                float4 nc = Look(p * 1.3 + float2(0.83, 0.41) + _Seed * 0.1, dpx * 1.3, dpy * 1.3);
                float4 nf = Look(p * 11.0 + float2(0.13, 0.77) + _Seed * 0.1, dpx * 11.0, dpy * 11.0);
                float4 nL = Look(float2(aR / RG_TAU * 3.0, 0.5 + r * 0.8), float2(dA.x / RG_TAU * 3.0, dR.x * 0.8),
                                 float2(dA.y / RG_TAU * 3.0, dR.y * 0.8));

                // The ring: sharp outside, soft inside, wobbling, clumpy, lopsided.
                float ringR = RGRingR(a);
                float w = RGWidth(a);
                float bright = RGBright(a);
                w *= 0.55 + 0.9 * nc.g;
                float d = r - ringR + (nc.r - 0.5) * w * 1.4;
                float prof = d > 0.0 ? exp(-RGSq(d / (w * 0.6))) : exp(-RGSq(d / (w * 1.6)));
                // Not a glowing hoop: a band of star clusters, bunched along the ring with gaps
                // between the bunches, and only a little smooth light under them.
                float bunch = smoothstep(0.28, 0.72, nL.r);
                float clumps = smoothstep(0.35, 0.8, nA.r) * (0.4 + 0.9 * nB.r);
                float specks = smoothstep(0.6, 0.9, nf.r) * (0.5 + nf.g);
                float ring = prof * bright * (0.1 + bunch * (0.85 * clumps + 0.9 * specks));

                // The ripples (RGRipple), broken up so they are not drawn with a compass: they
                // light the ring as they cross it, and are a faint band of their own elsewhere.
                float rip = RGRipple(p + (nc.rg - 0.5) * 0.09, T);
                float flare = 1.0 + 1.5 * rip;
                ring *= flare;
                float wave = rip * (0.2 + 0.9 * smoothstep(0.3, 0.75, nA.g)) * smoothstep(1.7, 1.1, r);

                // The giant knots, and smaller ones where the ring is thickest.
                float cores = 0.0, glow = 0.0;
                [unroll]
                for (int k = 0; k < RG_KNOTS; k++)
                {
                    float2 kp; float ks, kb;
                    RGKnot(k, T, kp, ks, kb);
                    float2 dk = p - kp;
                    float q = dot(dk, dk) / (ks * ks);
                    cores += kb * exp(-q * 2.5);
                    glow += kb * exp(-q * 0.5);
                }
                glow *= 0.4 + 0.9 * nB.g;   // the ionised gas patchy, not a round halo
                float knotlets = smoothstep(0.62, 0.86, nB.g) * prof * bright * flare;

                // The emptied middle: the old disc's faint light, the nucleus off centre in its
                // own small dusty ring, and faint spokes out to the ring.
                float2 pn = p - RGNucleus();
                float rn = length(pn);
                float inside = smoothstep(ringR, ringR * 0.72, r);
                // A dim haze of the old disc's stars inside the ring, and the old nucleus barely
                // there in it: the ring is the thing seen, as in Arp 148.
                float oldDisc = (0.02 + 0.07 * exp(-rn / 0.2)) * inside * (0.55 + 0.6 * nc.a);
                float core = 0.14 * exp(-rn / 0.03) + 0.06 * exp(-RGSq(rn / 0.1));
                float innerRing = 0.0;
                // Spokes: straight-ish, broad and faint, breaking up as they go out.
                float an = atan2(pn.y, pn.x) - _Turn * T * 0.8;
                float spokes = pow(saturate(0.5 + 0.5 * cos(7.0 * an + (nc.g - 0.5) * 0.9 + rn * 1.5)), 12.0)
                             * smoothstep(0.16, 0.3, rn) * smoothstep(ringR * 0.95, ringR * 0.55, r) * smoothstep(0.3, 0.7, nA.g);

                // Outside: what is left of the old disc, and the plume pulled towards the intruder.
                float beyond = max(d, 0.0);
                float outer = d > 0.0 ? exp(-d / 0.08) * 0.12 * (0.5 + nc.b) : 0.0;
                float toward = exp(-RGSq(RGAngleTo(a, RGIntruderAngle()) / 0.4));
                float plume = toward * smoothstep(0.0, 0.08, d) * exp(-beyond / 0.28)
                            * (0.1 + 0.9 * smoothstep(0.55, 0.85, nB.b)) * smoothstep(1.6, 1.15, r) * 0.6;

                // Dust: a thin lane along the ring's inner edge, patches round the nucleus.
                float laneRing = exp(-RGSq((d + w * 1.05) / (w * 0.45))) * smoothstep(0.45, 0.8, nA.b);
                float dust = saturate(laneRing * 0.7 * bunch * _Dust);

                // Up close the ring's clusters are stars (RingGalaxyStars_NEW), each one drawn: the
                // painted texture of them gives way, so the ring under the light is not a carpet.
                float3 camera = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                float3 toCamera = camera - i.local;
                float near = lerp(0.06, 1.0, smoothstep(0.35, 1.4, length(toCamera)));
                ring *= near;
                knotlets *= near;
                specks *= near;

                // The ring's blue shifting along it, a little teal here, a little violet there.
                float3 gas = _Gas.rgb * lerp(float3(0.8, 1.06, 1.04), float3(1.22, 0.84, 1.06), smoothstep(0.3, 0.7, nL.g));

                float3 light = _Core.rgb * core
                             + _Inner.rgb * (oldDisc * _Interior + innerRing * 0.35)
                             + _Spoke.rgb * spokes * 0.1 * _Spokes
                             + gas * ring * 1.6
                             + lerp(_Gas.rgb, _Clump.rgb, 0.35) * wave * 0.07
                             + _Clump.rgb * (cores * 1.1 + knotlets * 0.35 + specks * prof * bright * bunch * 0.5) * _Knots
                             + _Glow.rgb * (glow * 0.1 + knotlets * 0.2) * _Knots
                             + _PlumeColor.rgb * (outer * 0.5 + plume * 0.5 * _Plume);
                light *= 0.8 + 0.4 * ng.a;   // grain

                // Seen at a slant, through more of the layer: the light builds up and the dust
                // closes — gently, and never to a hard line along the far rim.
                float slant = min(length(toCamera) / max(abs(toCamera.y), 1e-4), 8.0) / 1.84;
                float through = pow(1.0 - saturate(dust * 0.85), slant);
                float hide = 1.0 - through;
                light *= sqrt(min(slant, 2.0));
                float3 premultiplied = (light * through + _DustColor.rgb * hide) * _Brightness;
                float alpha = hide * 0.8;

                // In the plane itself there is no sheet to see, only its glow (RingGalaxyVolume_NEW).
                float within = smoothstep(0.015, 0.05, abs(camera.y)) + smoothstep(1.8, 2.1, length(camera.xz));
                premultiplied *= saturate(within);
                alpha *= saturate(within);

                float edge = step(r, 1.95);
                return fixed4(premultiplied * _Color.rgb * edge, alpha * _Color.a * edge);
            }
            ENDCG
        }
    }

    FallBack Off
}
