Shader "Custom/SummitSky_NEW"
{
    // The night sky over Mount Wilson at the end of the journey (EarthSummit_NEW's skybox).
    //
    // Black overhead; along the horizon the orange glow of Los Angeles, strongest towards the
    // city. Stars, and the Milky Way as a band across the sky — clumped, a dark dust lane down
    // its middle, its core brightest low over the horizon — all made here, no texture of the
    // sky. Not as it really is from Mount Wilson, where the city washes the Milky Way nearly
    // out: overhead it is clear, and only towards the horizon, and the city most, does the
    // glow swallow it. They come up with _Reveal as the camera lifts to the sky.
    //
    // The frame — up, the way to the city, the galaxy's pole and centre — is set in world
    // space by EarthSummit_NEW. The noise is QuasarNoise_NEW's, read triplanar. Stars are a
    // point per cell of a grid over directions, as wide as a pixel whatever the resolution,
    // and no twinkle, so every copy on the wall draws the same sky.

    Properties
    {
        [NoScaleOffset] _Noise ("Noise (QuasarNoise_NEW)", 2D) = "gray" {}
        _Up ("Up (world)", Vector) = (0, 1, 0, 0)
        _ToCity ("Towards the city, level (world)", Vector) = (0, 0, 1, 0)
        _GalPole ("Galactic pole (world)", Vector) = (1, 0, 0, 0)
        _GalA ("Towards the galactic centre (world)", Vector) = (0, 0, 1, 0)
        _GalB ("Galactic longitude 90 (world)", Vector) = (0, 1, 0, 0)
        _Reveal ("Stars and Milky Way", Range(0, 1)) = 1
        _Zenith ("Overhead", Color) = (0.003, 0.004, 0.009, 1)
        _Horizon ("At the horizon", Color) = (0.03, 0.025, 0.03, 1)
        _Ground ("Below the horizon", Color) = (0.015, 0.012, 0.012, 1)
        [HDR] _Glow ("City glow", Color) = (0.32, 0.14, 0.05, 1)
        _GlowHeight ("City glow height (sine of elevation)", Range(0.01, 0.4)) = 0.07
        [HDR] _MilkyWay ("Milky Way", Color) = (0.22, 0.21, 0.19, 1)
        _StarBrightness ("Stars", Range(0, 4)) = 1.2
        // The Magellanic Clouds, for a southern sky: direction (world) and radius (radians) in w.
        _LMC ("Large Magellanic Cloud", Vector) = (0, 1, 0, 0.075)
        _SMC ("Small Magellanic Cloud", Vector) = (0, 1, 0, 0.05)
        _Clouds ("Magellanic Clouds, times the Milky Way", Range(0, 2)) = 0
        // Airglow: a faint band, green, low over the horizon, in streaks.
        [HDR] _Airglow ("Airglow", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Celestial_NEW.cginc"

            float4 _Up, _ToCity, _GalPole, _GalA, _GalB;
            float4 _Zenith, _Horizon, _Ground, _Glow, _MilkyWay;
            float _Reveal, _GlowHeight, _StarBrightness;
            float4 _LMC, _SMC, _Airglow;
            float _Clouds;

            // A soft round cloud of stars about c.xyz, c.w radians across its middle.
            float Cloud(float3 d, float4 c)
            {
                float q = 1.0 - dot(d, c.xyz);
                return exp(-q / (c.w * c.w * 0.5));
            }

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float3 Hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            // One star in this direction's cell of a grid scale cells to a unit, or none:
            // a disc a pixel and a bit across. Most are faint; a few are bright.
            float3 Star(float3 d, float scale, float threshold, float pixel)
            {
                float3 cell = floor(d * scale);
                float3 h = Hash33(cell);
                if (h.x < threshold) return 0;
                float3 at = normalize((cell + 0.25 + 0.5 * Hash33(cell + 17.0)) / scale);
                float off = length(d - at);
                float disc = saturate(1.0 - off / (pixel * 1.3));
                float bright = 0.12 + 2.5 * pow(h.y, 8.0);
                // A slow twinkle, each star at its own rate.
                bright *= 0.82 + 0.18 * sin(_Time.y * (1.5 + 3.0 * h.z) + h.x * 60.0);
                // Blue-white to yellow-white.
                float3 tint = lerp(float3(0.75, 0.85, 1.0), float3(1.0, 0.88, 0.7), h.z);
                return tint * bright * disc * disc;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 up = _Up.xyz;
                float e = dot(d, up);
                float3 level = normalize(d - up * e + 1e-5);
                float toCity = dot(level, _ToCity.xyz) * 0.5 + 0.5;

                // The sky's own colour, and the city's glow along the horizon.
                float3 sky = lerp(_Horizon.rgb, _Zenith.rgb, smoothstep(0.0, 0.5, e));
                sky += _Glow.rgb * exp(-max(e, 0.0) / _GlowHeight) * (0.25 + 0.75 * pow(toCity, 2.5));
                // Airglow: a band some ten degrees up, in faint streaks, over the whole horizon.
                float glowBand = exp(-pow((e - 0.16) / 0.1, 2.0)) * smoothstep(0.0, 0.04, e);
                sky += _Airglow.rgb * glowBand * (0.55 + 0.9 * Triplanar(float3(level.x * 2.0, e * 9.0, level.z * 2.0)).r) * _Reveal;
                sky = lerp(sky, _Ground.rgb, smoothstep(0.0, -0.06, e));

                // How much of the sky shows through: none at the horizon, the city's side least.
                float clear = smoothstep(0.0, 0.22, e) * (1.0 - 0.5 * pow(toCity, 3.0) * exp(-max(e, 0.0) / 0.25));
                clear *= _Reveal;

                // The Milky Way (22 Nov, redone: the first had blotches and dark veins). A smooth
                // band, wider towards the centre, with the bulge there; brighter star clouds in it,
                // from noise of several octaves, smooth, not the texture's cells; the Great Rift's
                // dark dust a little off its middle, ragged, strongest towards the centre, and
                // fainter dust in filaments; blue-white along the arms, gold at the core, the
                // dust reddening what it does not hide; a few pink knots of glowing gas.
                float b = dot(d, _GalPole.xyz);
                float l = atan2(dot(d, _GalB.xyz), dot(d, _GalA.xyz));
                float nearCore = exp(-l * l / (0.9 * 0.9));
                float width = 0.085 + 0.075 * nearCore;
                float band = exp(-b * b / (2.0 * width * width));
                float bulge = exp(-(l * l) / (2.0 * 0.28 * 0.28) - (b * b) / (2.0 * 0.15 * 0.15));
                float f1 = Triplanar(d * 2.3).r, f2 = Triplanar(d * 5.1 + 3.7).g, f3 = Triplanar(d * 11.3 + 7.1).b, f4 = Triplanar(d * 23.0 + 1.3).a;
                float fbm = f1 * 0.5 + f2 * 0.27 + f3 * 0.15 + f4 * 0.08;
                float starClouds = smoothstep(0.3, 0.72, fbm);
                float glowMW = band * (0.3 + 0.95 * starClouds) + bulge * 1.7 * (0.7 + 0.5 * starClouds);
                float riftAt = 0.012 + 0.03 * (f2 - 0.5);
                float rift = exp(-pow((b - riftAt) / (0.026 + 0.02 * nearCore), 2.0)) * smoothstep(0.3, 0.65, f3 * 0.7 + f1 * 0.5) * smoothstep(2.0, 0.7, abs(l));
                float filaments = smoothstep(0.58, 0.82, Triplanar(d * 8.0 + 11.0).r * 0.6 + f4 * 0.4) * band;
                float dust = saturate(rift * 0.85 + filaments * 0.4);
                float grain = 0.8 + 0.4 * f4 + 0.35 * (Triplanar(d * 60.0).a - 0.5);
                float mw = glowMW * (1.0 - dust) * grain;
                float3 tint = lerp(float3(0.78, 0.86, 1.0), float3(1.1, 0.9, 0.64), saturate(nearCore * 0.55 + bulge));
                tint = lerp(tint, tint * float3(1.06, 0.84, 0.68), dust * 0.6);
                float light = dot(_MilkyWay.rgb, float3(0.3, 0.59, 0.11)) * 1.25;
                sky += tint * light * mw * clear;
                float knot = smoothstep(0.8, 0.93, Triplanar(d * 17.0 + 4.4).g) * band * (1.0 - dust);
                sky += float3(0.9, 0.33, 0.45) * light * 0.55 * knot * clear;

                // The Magellanic Clouds: two fuzzy patches off the band, the Large one brighter.
                if (_Clouds > 0.0)
                {
                    float knots = 0.5 + 0.8 * saturate(Triplanar(d * 23.0).g * 1.3 - 0.15);
                    float clouds = (Cloud(d, _LMC) + 0.6 * Cloud(d, _SMC)) * knots * grain;
                    sky += _MilkyWay.rgb * _Clouds * clouds * clear;
                }

                // Stars: a sparse bright layer, and a fine one thickest along the band.
                float pixel = max(length(fwidth(d)), 1e-5);
                float3 stars = Star(d, 140.0, 0.9, pixel) + Star(d, 330.0, lerp(0.985, 0.88, band), pixel) * 0.5;
                sky += stars * _StarBrightness * clear;

                return fixed4(sky, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
