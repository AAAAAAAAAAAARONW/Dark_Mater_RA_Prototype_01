Shader "Custom/GalaxyDisc_NEW"
{
    // The Milky Way's disc, seen from outside (GalaxyVFX_NEW). One flat quad in the object's XZ
    // plane, radius 1 at the visible edge, and everything on it worked out per pixel in one
    // pass — no volume, no ray march — from the galaxy's own structure:
    //
    //   bar and bulge  old stars, warm: a bar 27° off the Sun's line, a third of the way to it
    //   the old disc   an exponential, brightest inside
    //   four arms      logarithmic spirals pitched 12.5°: Scutum-Centaurus and Perseus, the two
    //                  major arms, from the ends of the bar; Sagittarius and Norma, fainter;
    //                  young stars, blue-white, in clumps
    //   Orion spur     the short spur the Sun sits on, at (rSun, 0)
    //   HII regions    pink knots strung along the arms, where stars are being born
    //   dust           lanes on the arms' inner (leading) edges, feathers across them, straight
    //                  lanes along the bar
    //
    // The arms are a density wave and are drawn in the frame that turns with the Sun, which is
    // close to their corotation: in it they hardly move, and the Sun stays where it is. The
    // stars GalaxyStars_NEW draws are what move.
    //
    // Structure comes from QuasarNoise_NEW's texture in log-polar coordinates (stretched along
    // the spiral, as sheared gas is), with gradients that ignore atan2's seam.
    //
    // Premultiplied alpha: the stars glow, the dust darkens what is behind. _Color fades it.
    // In Resources so a build always has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _Noise ("Noise (QuasarNoise_NEW)", 2D) = "gray" {}
        _SunRadius ("The Sun's radius (of the disc's)", Range(0.2, 0.7)) = 0.39
        _Pitch ("Arm pitch, degrees", Range(5, 25)) = 12.5
        _ArmWidth ("Arm width at the centre, and its growth outward", Vector) = (0.022, 0.04, 0, 0)
        _ArmStrength ("Major arms, minor arms, Orion spur", Vector) = (1, 0.4, 0.45, 0)
        _Bar ("Bar: angle (deg), length, width", Vector) = (27, 0.24, 0.075, 0)
        _ScaleLength ("Old disc scale length (of the radius)", Float) = 0.16
        _Dust ("Dust", Range(0, 2)) = 0.85
        _HII ("Star-forming regions", Range(0, 3)) = 1.3
        _Sparkle ("Young clusters", Range(0, 2)) = 0.7
        _Haze ("Light between the arms", Range(0, 1)) = 0.32
        _Brightness ("Brightness", Range(0, 3)) = 0.6
        _Flow ("Gas flow along the orbits, radii a second", Float) = 0.012
        _Twinkle ("Star-forming regions and clusters flickering", Range(0, 1)) = 0.6
        [HDR] _Core ("Bulge and bar", Color) = (1.6, 1.25, 0.85, 1)
        [HDR] _Old ("Old disc", Color) = (1.0, 0.78, 0.52, 1)
        [HDR] _Young ("Young stars", Color) = (0.55, 0.70, 1.05, 1)
        [HDR] _OuterColor ("Outer disc", Color) = (0.30, 0.38, 0.62, 1)
        [HDR] _Pink ("HII regions", Color) = (1.7, 0.42, 0.75, 1)
        _DustColor ("Dust", Color) = (0.05, 0.03, 0.025, 1)
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

            #define TAU 6.2831853

            sampler2D _Noise;
            fixed4 _Color;
            float _SunRadius, _Pitch, _ScaleLength, _Dust, _HII, _Sparkle, _Haze, _Brightness, _Flow, _Twinkle;
            float4 _ArmWidth, _ArmStrength, _Bar;
            float4 _Core, _Old, _Young, _OuterColor, _Pink;
            fixed4 _DustColor;

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 plane : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.plane = vertex.xz;
                return o;
            }

            float Sq(float x) { return x * x; }
            float Wrap(float a) { return a - TAU * floor(a / TAU + 0.5); }

            // The four arms, each a Gaussian across a log spiral; `shift` moves the lane across.
            float Arms(float r, float az, float lnr, float tanP, float sinP, float widen, float shift)
            {
                float w = (_ArmWidth.x + _ArmWidth.y * r) * widen;
                float sag = -log(0.85) / tanP;   // Sagittarius just inside the Sun
                float base = az - (lnr + shift) / tanP;
                float sum = _ArmStrength.y * exp(-Sq(Wrap(base - sag) * r * sinP / w));                 // Sagittarius
                sum += _ArmStrength.x * exp(-Sq(Wrap(base - sag + 1.5707963) * r * sinP / w));          // Perseus
                sum += _ArmStrength.x * exp(-Sq(Wrap(base - sag - 1.5707963) * r * sinP / w));          // Scutum-Centaurus
                sum += _ArmStrength.y * exp(-Sq(Wrap(base - sag - 3.1415927) * r * sinP / w));          // Norma
                return sum;
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
                float lnr = log(r / _SunRadius);

                float tanP = tan(radians(_Pitch)), sinP = sin(radians(_Pitch));

                // Large-scale wander, so the arms are not drawn with a compass.
                float4 big = Look(p * 0.55 + float2(0.31, 0.17), dpx * 0.55, dpy * 0.55);
                float az = az0 + (big.r - 0.5) * 0.5;

                // Log-polar noise at two scales, and feathers at a wider pitch, streaming along
                // the orbits. The arms are a density wave and hold still (in this frame, which
                // turns with the Sun); the gas flows through them on the flat rotation curve,
                // ahead inside the Sun's orbit, behind outside. Sheared for ever it would wind up,
                // so it is read twice, half a cycle apart, and crossfaded.
                const float cycle = 30.0;
                float ph = frac(_Time.y / cycle);
                float drift = _Flow * (1.0 / r - 1.0 / _SunRadius) * cycle;
                float4 n = 0, m = 0, fe = 0;
                [unroll]
                for (int k = 0; k < 2; k++)
                {
                    float phase = k == 0 ? ph : frac(ph + 0.5);
                    float w = 1.0 - abs(phase * 2.0 - 1.0);   // weighted most mid-cycle; the two sum to 1
                    float azF = az - drift * phase + k * 2.1;
                    float2 uv = float2(azF / TAU * 4.0 + lnr * 0.9, lnr * 2.2 + azF / TAU);
                    float2 udx = float2(dAz.x / TAU * 4.0 + dLnr.x * 0.9, dLnr.x * 2.2 + dAz.x / TAU);
                    float2 udy = float2(dAz.y / TAU * 4.0 + dLnr.y * 0.9, dLnr.y * 2.2 + dAz.y / TAU);
                    n += Look(uv, udx * 2.0, udy * 2.0) * w;
                    m += Look(uv * 3.0 + float2(0.4, 0.1), udx * 4.2, udy * 4.2) * w;
                    float2 fuv = float2(azF / TAU * 6.0 + lnr * 2.6, lnr * 1.2 - azF / TAU * 2.0);
                    float2 fdx = float2(dAz.x / TAU * 6.0 + dLnr.x * 2.6, dLnr.x * 1.2 - dAz.x / TAU * 2.0);
                    float2 fdy = float2(dAz.y / TAU * 6.0 + dLnr.y * 2.6, dLnr.y * 1.2 - dAz.y / TAU * 2.0);
                    fe += Look(fuv, fdx * 1.4, fdy * 1.4) * w;
                }
                float4 nf = Look(p * 3.1 + float2(0.2, 0.7), dpx * 3.1 * 1.2, dpy * 3.1 * 1.2);
                float4 ng = Look(p * 9.0 + float2(0.5, 0.3), dpx * 9.0, dpy * 9.0);

                // The arms, clumpy and broken.
                float clump = (0.35 + 1.3 * m.r * (0.6 + 0.8 * n.r)) * (0.35 + 0.9 * smoothstep(0.3, 0.65, big.g));
                float arm = Arms(r, az, lnr, tanP, sinP, 1.0, 0.0) * clump;

                // The Orion spur, through the Sun.
                float tanS = 0.4040262, sinS = 0.3746066;   // 22°
                float spurD = Wrap(az - lnr / tanS) * r * sinS;
                arm += _ArmStrength.z * exp(-Sq(spurD / (0.02 + 0.03 * r))) * exp(-Sq(az / 0.65)) * clump;

                float inner = smoothstep(0.1, 0.24, r);
                float edge = smoothstep(1.0, 0.72, r + (n.r - 0.5) * 0.3);
                arm *= inner * edge;

                // Dust: lanes on the arms' inner edges, feathers, the bar's lanes, haze.
                float2 q = float2(p.x * cos(radians(_Bar.x)) + p.y * sin(radians(_Bar.x)),
                                  -p.x * sin(radians(_Bar.x)) + p.y * cos(radians(_Bar.x)));
                float lane = Arms(r, az, lnr, tanP, sinP, 0.6, 0.07) * inner * smoothstep(1.0, 0.6, r)
                           * (0.5 + 0.9 * smoothstep(0.3, 0.7, n.b));
                float feather = smoothstep(0.55, 0.85, fe.b) * saturate(arm * 1.4) * 0.8;
                float barLane = exp(-Sq((q.y - sign(q.x) * 0.05) / 0.012)) * exp(-Sq(q.x / (_Bar.y * 1.1))) * 0.35;
                float dust = saturate((lane + feather + barLane) * _Dust
                                      + smoothstep(0.55, 0.8, n.g) * 0.2 * inner * smoothstep(0.95, 0.5, r));

                // The old disc, the bar and the bulge.
                float disc = exp(-r / _ScaleLength) * 1.1 * edge;
                float bar = exp(-pow(Sq(q.x / _Bar.y) + Sq(q.y / _Bar.z), 1.1) * 2.2);
                float bulge = (exp(-Sq(r / 0.06)) * 0.8 + 0.5 * exp(-Sq(r / 0.018)) + 0.22 * exp(-Sq(r / 0.17)))
                            * (1.0 + 0.1 * sin(_Time.y * 0.55));   // the core breathing, slowly

                // Star formation, and the stars.
                float hii = smoothstep(0.56, 0.74, nf.r) * smoothstep(0.45, 0.8, m.b) * saturate(arm * 1.2) * _HII;
                // Each region and cluster flickering on its own clock.
                hii *= lerp(1.0, 0.55 + 0.9 * (0.5 + 0.5 * sin(_Time.y * (0.8 + 2.0 * nf.g) + nf.b * 40.0)), _Twinkle);
                float grain = 0.75 + 0.5 * ng.a;
                float sparkle = smoothstep(0.64, 0.9, nf.a) * (saturate(arm) + 0.15 * disc) * _Sparkle;
                sparkle *= lerp(1.0, 0.4 + 1.2 * (0.5 + 0.5 * sin(_Time.y * (2.0 + 5.0 * ng.r) + ng.g * 60.0)), _Twinkle);

                float3 young = lerp(_OuterColor.rgb, _Young.rgb, lerp(1.0, 0.55, smoothstep(0.5, 1.0, r)));
                float3 light = (_Core.rgb * (bulge * 1.4 + bar * 0.9)
                              + _Old.rgb * disc * (1.0 - 0.5 * smoothstep(0.2, 0.6, r))
                              + young * (arm * 0.9 + _Haze * exp(-r / 0.3) * edge)) * grain
                             + _Pink.rgb * hii * 1.1
                             + _Young.rgb * sparkle * 1.4;

                float hide = saturate(dust * 0.85);
                float3 premultiplied = (light * (1.0 - dust * 0.85) + _DustColor.rgb * hide) * _Brightness;
                float alpha = hide * 0.8;

                // Outside the disc, nothing. (After the derivatives, so they are taken everywhere.)
                float inside = step(r, 1.05);
                return fixed4(premultiplied * _Color.rgb * inside, alpha * _Color.a * inside);
            }
            ENDCG
        }
    }

    FallBack Off
}
