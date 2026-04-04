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

        // Absorption / Stripe
        _AbsorptionAmount       ("Absorption Amount",            Range(0, 1)) = 0
        _StripeFrequency        ("Stripe Frequency (along trail)",Range(0.1, 64)) = 8
        _StripeSharpness        ("Stripe Sharpness",             Range(0.01, 1)) = 0.1
        _NoiseScale             ("Noise Scale",                  Range(0.1, 16)) = 4
        _NoiseSpeed             ("Noise Speed",                  Range(0, 5))   = 1
        _UseTintShift           ("Use Tint Shift",               Range(0, 10))  = 3.99
        _TintWhereAbsorbed      ("Tint Where Absorbed",          Color) = (0.1, 0.1, 0.3, 1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One          // Additive — great for glowing photon trails
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "UnityCG.cginc"

            // ── Uniforms ────────────────────────────────────────────────
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

            float  _GlowIntensity;
            float  _GlowFalloff;

            float4 _EmissionColor;
            float  _EmissionStrength;
            float  _EmissionSoftness;

            float  _EdgeFadeWidth;
            float  _EdgeFadePower;
            float  _EdgeSoftness;
            float  _EndFadeWidth;

            float  _AbsorptionAmount;
            float  _StripeFrequency;
            float  _StripeSharpness;
            float  _NoiseScale;
            float  _NoiseSpeed;
            float  _UseTintShift;
            float4 _TintWhereAbsorbed;

            // ── Vertex I/O ───────────────────────────────────────────────
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

            // ── Simple noise helpers ─────────────────────────────────────
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

            // ── Vertex ──────────────────────────────────────────────────
            v2f vert(appdata v)
            {
                v2f o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            // ── Fragment ─────────────────────────────────────────────────
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                // Unity TrailRenderer UV convention (corrected):
                //   uv.x = along the LENGTH  (0 = head, 1 = tail)
                //   uv.y = across the WIDTH  (0 = one edge, 1 = other edge)

                // ── 1. Spectrum across WIDTH (uv.y) ───────────────────────
                float specT = frac((uv.y + _SpectrumOffset) * _SpectrumScale);
                float4 specColor = tex2D(_SpectrumTex, float2(specT, 0.5));

                float4 baseColor = lerp(_Color * i.color, specColor * i.color, _UseSpectrum);

                float irFactor = pow(saturate((specT - 0.82) * 6.0), 2.0);
                float uvFactor = pow(saturate((0.18 - specT) * 6.0), 2.0);
                baseColor.rgb += _IRGlowColor.rgb * irFactor * _IRGlowStrength * _UseSpectrum * 0.3;
                baseColor.rgb += _UVGlowColor.rgb * uvFactor * _UVGlowStrength * _UseSpectrum * 0.3;

                // ── 3. Texture sample ────────────────────────────────────
                float4 texSample = tex2D(_MainTex, uv);
                baseColor *= texSample;

                // ── 4. Glow — brightest at centre of width (uv.y = 0.5) ──
                float centreY  = abs(uv.y - 0.5) * 2.0;
                float glowMask = pow(saturate(1.0 - centreY), _GlowFalloff);
                float glow     = glowMask * _GlowIntensity;

                // ── 5. Soft edge fade across WIDTH (uv.y) ────────────────
                float edgeY    = min(uv.y, 1.0 - uv.y);
                float edgeFade = saturate(edgeY / max(_EdgeFadeWidth, 0.0001));
                edgeFade = pow(edgeFade, _EdgeFadePower);
                edgeFade = smoothstep(0, _EdgeSoftness * 0.2, edgeFade);

                // ── 6. Tail fade along LENGTH (uv.x) — head visible, tail fades
                float endFade = saturate((1.0 - uv.x) / max(_EndFadeWidth, 0.0001));

                // ── 7. Emission — always driven by spectrum colour so it doesn't
                //    override the rainbow with the fixed blue _EmissionColor
                float emitMask = pow(glowMask, _EmissionSoftness);
                float4 emitCol = lerp(_EmissionColor, baseColor, max(_UseSpectrum, 0.8));
                float3 emission = emitCol.rgb * emitMask * _EmissionStrength;

                // ── 7. Absorption / stripes ──────────────────────────────
                // Stripes along LENGTH (uv.x) — absorption lines cutting across all bands
                float stripe   = sin(uv.x * _StripeFrequency * 3.14159 * 2.0) * 0.5 + 0.5;
                stripe         = smoothstep(0.5 - _StripeSharpness, 0.5 + _StripeSharpness, stripe);

                float noise    = smoothNoise(uv * _NoiseScale + _Time.y * _NoiseSpeed * 0.1);
                float absorp   = saturate(_AbsorptionAmount * (stripe * 0.6 + noise * 0.4));

                float4 absorbedColor = lerp(baseColor,
                    _TintWhereAbsorbed + float4(baseColor.rgb * _UseTintShift * 0.05, 0),
                    absorp);

                // ── 8. Assemble ──────────────────────────────────────────
                float4 col  = absorbedColor;
                col.rgb    += emission;
                col.rgb    *= (1.0 + glow * 0.5);

                float alpha = col.a
                            * edgeFade
                            * endFade
                            * (1.0 - absorp * 0.8);

                col.a = saturate(alpha);
                return col;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
