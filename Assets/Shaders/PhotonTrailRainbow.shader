Shader "Custom/PhotonTrail"
{
    Properties
    {
        // Base
        _Color ("Color", Color) = (1,1,1,1)
        [NoScaleOffset] _MainTex ("Texture (optional)", 2D) = "white" {}

        // Spectrum
        [Toggle] _UseSpectrum ("Use Spectrum Gradient", Float) = 1
        _SpectrumTex ("Spectrum Texture", 2D) = "white" {}
        _SpectrumOffset ("Spectrum UV Offset", Range(0,1)) = 0
        _SpectrumScale  ("Spectrum UV Scale",  Range(0.1, 3)) = 1

        // IR / UV glow extras
        _IRGlowStrength  ("IR Glow Strength",  Range(0, 5)) = 1.2
        _UVGlowStrength  ("UV Glow Strength",  Range(0, 5)) = 1.8
        _IRGlowColor     ("IR Glow Color",  Color) = (0.6, 0.05, 0.0, 1)
        _UVGlowColor     ("UV Glow Color",  Color) = (0.45, 0.0, 0.9, 1)

        // Visible band highlight — added for the tutorial's "this is the light your eyes
        // can see" moment. Brightens only the wavelengths between the two edges.
        // Highlight 0 (the default) changes nothing, so existing materials look the same.
        _VisibleBandStart ("Visible Band Start",     Range(0, 1)) = 0.15
        _VisibleBandEnd   ("Visible Band End",       Range(0, 1)) = 0.85
        _VisibleHighlight ("Visible Band Highlight", Range(0, 3)) = 0

        // Glow
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 1.31
        _GlowFalloff   ("Glow Falloff",   Range(0.1, 10)) = 5.29

        // Emission
        _EmissionColor    ("Emission Color",    Color) = (0.3, 0.8, 1, 1)
        _EmissionStrength ("Emission Strength", Range(0, 5)) = 2.2
        _EmissionSoftness ("Emission Softness", Range(0.1, 5)) = 1.4

        // Edge Fade
        _EdgeFadeWidth  ("Edge Fade Width",  Range(0, 0.5)) = 0.15
        _EdgeFadePower  ("Edge Fade Power",  Range(0.1, 5)) = 2.0
        _EdgeSoftness   ("Edge Softness",    Range(0.1, 5)) = 1.3
        _EndFadeWidth   ("End Fade Width",   Range(0, 0.5)) = 0.12

        // Head indicator
        [Toggle] _ShowHead ("Show Head", Float) = 1
        _HeadWidth      ("Head Width",      Range(0, 0.5)) = 0.04
        _HeadColor      ("Head Color",      Color) = (1, 1, 1, 1)
        _HeadBrightness ("Head Brightness", Range(0, 3)) = 1.8

        // Lyman-alpha absorption lines
        [Toggle] _UseAbsorptionLine ("Use Absorption Lines", Float) = 0
        _AbsorptionLineTex ("Absorption Line Texture", 2D) = "black" {}
        _AbsorptionLineStrength ("Absorption Line Strength", Range(0, 1)) = 1.0
        _AbsorptionLineWidth ("Line Softness", Range(0.5, 4.0)) = 1.5

        // Widens the dark line ACROSS THE RIBBON ONLY — added for the tutorial, where
        // the ribbon is a fifth as wide on screen as the spectrum bar and a line that
        // reads as six pixels on the bar is one on the light.
        // Spread 0 (the default) changes nothing, so existing materials look the same.
        _AbsorptionLineSpread ("Absorption Line Spread", Range(0, 0.06)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_ST;
            sampler2D _SpectrumTex;

            float4 _Color;
            float  _UseSpectrum;
            float  _SpectrumOffset;
            float  _SpectrumScale;

            float4 _IRGlowColor;
            float4 _UVGlowColor;
            float  _IRGlowStrength;
            float  _UVGlowStrength;

            float  _VisibleBandStart;
            float  _VisibleBandEnd;
            float  _VisibleHighlight;

            float  _GlowIntensity;
            float  _GlowFalloff;

            float4 _EmissionColor;
            float  _EmissionStrength;
            float  _EmissionSoftness;

            float  _EdgeFadeWidth;
            float  _EdgeFadePower;
            float  _EdgeSoftness;
            float  _EndFadeWidth;

            float  _ShowHead;
            float  _HeadWidth;
            float4 _HeadColor;
            float  _HeadBrightness;

            float  _UseAbsorptionLine;
            sampler2D _AbsorptionLineTex;
            float  _AbsorptionLineStrength;
            float  _AbsorptionLineWidth;
            float  _AbsorptionLineSpread;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            float hash(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            float smoothNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(hash(i),               hash(i + float2(1,0)), f.x),
                    lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), f.x),
                    f.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                // Unity TrailRenderer UV convention:
                //   uv.x = along the LENGTH  (0 = head, 1 = tail)
                //   uv.y = across the WIDTH  (0 = one edge, 1 = other edge)
                //
                // Spectrum across WIDTH (uv.y):
                //   uv.y = 0 → UV (transparent violet)   ← REVERSED from before
                //   uv.y = 1 → IR (transparent red)

                // ── 1. Spectrum across WIDTH (uv.y), UV→IR direction ──────
                // Flip uv.y so uv.y=0 → IR end of texture, uv.y=1 → UV end
                // Texture is generated UV→IR (t=0=UV), so we invert here
                float specT = frac(((1.0 - uv.y) + _SpectrumOffset) * _SpectrumScale);
                // specT=0 → UV end, specT=1 → IR end
                float4 specColor = tex2D(_SpectrumTex, float2(specT, 0.5));
                float4 baseColor = lerp(_Color * i.color, specColor * i.color, _UseSpectrum);

                // UV glow at top edge (uv.y=1, specT=0), IR glow at bottom edge (uv.y=0, specT=1)
                float uvFactor = pow(saturate((0.18 - specT) * 6.0), 2.0);
                float irFactor = pow(saturate((specT - 0.82) * 6.0), 2.0);
                baseColor.rgb += _UVGlowColor.rgb * uvFactor * _UVGlowStrength * _UseSpectrum * 0.3;
                baseColor.rgb += _IRGlowColor.rgb * irFactor * _IRGlowStrength * _UseSpectrum * 0.3;

                // ── 1b. Visible band highlight ────────────────────────────
                // Same wavelength coordinate (specT) as the colours and the absorption
                // lines, so the edges land exactly on the band PhotonSpectrumTrail paints.
                // A very narrow smoothstep only to anti-alias the edge. Applied at the end,
                // in step 9 — see there for why.
                float visMask = smoothstep(_VisibleBandStart - 0.004, _VisibleBandStart + 0.004, specT)
                              * (1.0 - smoothstep(_VisibleBandEnd - 0.004, _VisibleBandEnd + 0.004, specT));

                // ── 2. Texture sample ─────────────────────────────────────
                float4 texSample = tex2D(_MainTex, uv);
                baseColor *= texSample;

                // ── 3. Glow — brightest at centre of width ────────────────
                float centreY  = abs(uv.y - 0.5) * 2.0;
                float glowMask = pow(saturate(1.0 - centreY), _GlowFalloff);
                float glow     = glowMask * _GlowIntensity;

                // ── 4. Soft edge fade across WIDTH ────────────────────────
                float edgeY    = min(uv.y, 1.0 - uv.y);
                float edgeFade = saturate(edgeY / max(_EdgeFadeWidth, 0.0001));
                edgeFade = pow(edgeFade, _EdgeFadePower);
                edgeFade = smoothstep(0, _EdgeSoftness * 0.2, edgeFade);

                // ── 5. Tail fade along LENGTH (uv.x) ─────────────────────
                float endFade = saturate((1.0 - uv.x) / max(_EndFadeWidth, 0.0001));

                // ── 6. Head — shader-driven semicircle cap ────────────────
                // Entirely in UV space — no geometry needed, looks round at any speed.
                //
                // The cap is a semicircle: pixels inside the circle are kept,
                // pixels outside are clipped. Center is at (uv.x=0, uv.y=0.5).
                //
                //   headLocal = 1 at the very tip, 0 at the end of the cap region
                //   centreY   = 0 at trail centre, 1 at edges
                //   dist      = distance from tip in UV space
                //   anything outside dist=1 gets clipped → semicircle shape
                float headGlow  = 0.0;
                float headAlpha = 1.0;
                float headClip  = 1.0;
                if (_ShowHead > 0.5)
                {
                    float headLocal  = saturate(1.0 - uv.x / max(_HeadWidth, 0.0001));
                    float centreY    = abs(uv.y - 0.5) * 2.0; // 0=centre, 1=edge

                    // Circular distance from tip — pure UV math, no geometry dependency
                    float dist = sqrt(headLocal * headLocal + centreY * centreY);

                    // Clip pixels outside the circle — sharp but anti-aliased edge
                    headClip = saturate((1.0 - dist) * 40.0);

                    // Bright white glow inside the cap
                    headGlow = headLocal * _HeadBrightness * headClip;

                    // Alpha boost so cap reads as solid
                    headAlpha = 1.0 + headLocal * headClip * 1.5;
                }

                // ── 7. Emission ───────────────────────────────────────────
                float emitMask = pow(glowMask, _EmissionSoftness);
                float4 emitCol = lerp(_EmissionColor, baseColor, max(_UseSpectrum, 0.8));
                float3 emission = emitCol.rgb * emitMask * _EmissionStrength;

                // ── 8. Lyman-alpha absorption lines ───────────────────────
                // Sample the absorption texture at this pixel's wavelength (specT).
                // The texture stores per-wavelength absorption strength in the red channel.
                // Lines are parallel to color bands — same wavelength darkened all along
                // the trail length simultaneously.
                float absorp = 0.0;
                if (_UseAbsorptionLine > 0.5)
                {
                    float lineStrength = tex2D(_AbsorptionLineTex, float2(specT, 0.5)).r;

                    // SPREAD WIDENS THE LINE WITHOUT MOVING IT. Four extra taps either
                    // side of this pixel's wavelength, keeping whichever is darkest, so a
                    // line one texel wide is painted across a band of the ribbon centred
                    // on the wavelength it actually belongs to.
                    //
                    // The absorption buffer is shared with the spectrum bar, and this
                    // does NOT touch it: the bar keeps the sharp dip it can afford at 720
                    // pixels wide, and the ribbon — a fifth of that on screen — gets a
                    // stripe thick enough to be a stripe. Same wavelength, same drift,
                    // two scales.
                    //
                    // Max rather than a blur, because absorption is light that is gone.
                    // Averaging would make a narrow deep line into a wide shallow one and
                    // lose exactly what has to be visible.
                    if (_AbsorptionLineSpread > 0.0001)
                    {
                        // Not named `step`: that is an HLSL intrinsic, and shadowing it
                        // compiles on one platform and argues on the next.
                        float tapStep = _AbsorptionLineSpread * 0.25;

                        // tex2Dlod, not tex2D: a sample inside flow control has no
                        // derivatives, and some compilers refuse it outright. The buffer
                        // is one texel tall with no mip chain, so asking for level 0
                        // explicitly costs nothing and is what was wanted anyway.
                        for (int tap = 1; tap <= 4; tap++)
                        {
                            float d = tapStep * tap;
                            lineStrength = max(lineStrength,
                                               tex2Dlod(_AbsorptionLineTex, float4(specT - d, 0.5, 0, 0)).r);
                            lineStrength = max(lineStrength,
                                               tex2Dlod(_AbsorptionLineTex, float4(specT + d, 0.5, 0, 0)).r);
                        }
                    }

                    absorp = lineStrength * _AbsorptionLineStrength;
                }

                // Darken the color at absorbed wavelengths — absorption removes light
                float4 absorbedColor = baseColor * (1.0 - absorp);

                // ── 9. Assemble ───────────────────────────────────────────
                float4 col = absorbedColor;
                col.rgb   += emission;
                col.rgb   *= (1.0 + glow * 0.5);
                col.rgb   += _HeadColor.rgb * headGlow;

                // Visible band highlight. ADDITIVE and at EQUAL BRIGHTNESS per hue, and
                // applied after the centre glow. Multiplying the colour instead made the
                // flash scale with how bright each colour already was, and the centre of
                // the ribbon — where the glow and emission peak, and where cyan and green
                // sit — outshone red and violet so far that only the middle appeared to
                // flash. Normalising each colour to its brightest channel lifts every
                // visible wavelength by the same amount in its own hue. Absorbed
                // wavelengths are not lifted, so lines stay dark through the flash.
                float huePeak = max(specColor.r, max(specColor.g, specColor.b));
                float3 hue    = specColor.rgb / max(huePeak, 0.001);
                col.rgb      += hue * visMask * _VisibleHighlight * 0.6 * (1.0 - absorp);

                // headClip cuts the rectangle corners into a semicircle shape
                float alpha = col.a
                            * edgeFade
                            * endFade
                            * headAlpha
                            * headClip;

                col.a = saturate(alpha);
                return col;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
