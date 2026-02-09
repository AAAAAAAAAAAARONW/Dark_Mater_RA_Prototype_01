Shader "Custom/PhotonTrail"
{
    Properties
    {
        [Header(Base)]
        _Color ("Color", Color) = (1, 1, 0.5, 1)
        _MainTex ("Texture (optional)", 2D) = "white" {}
        [Header(Glow)]
        _GlowIntensity ("Glow intensity", Range(0, 5)) = 2.5
        _GlowFalloff ("Glow falloff", Range(0.1, 10)) = 1.5
        [Header(Edge Fade)]
        _EdgeFadeWidth ("Edge fade width", Range(0.01, 0.5)) = 0.15
        _EdgeFadePower ("Edge fade power", Range(0.5, 6)) = 2
        _EndFadeWidth ("End fade width", Range(0.0, 0.5)) = 0.12

        [Header(Absorption)]
        _Absorb ("Absorption amount", Range(0, 1)) = 0
        _StripeFrequency ("Stripe frequency (along trail)", Float) = 8
        _StripeSharpness ("Stripe sharpness", Range(0.1, 10)) = 3
        _NoiseScale ("Noise scale", Float) = 4
        _NoiseSpeed ("Noise speed", Float) = 1
        _UseTintShift ("Use tint shift", Float) = 0
        _TintShift ("Tint where absorbed", Color) = (0.3, 0.5, 1, 0.2)
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
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Absorb;
            float _StripeFrequency;
            float _StripeSharpness;
            float _NoiseScale;
            float _NoiseSpeed;
            float _UseTintShift;
            float4 _TintShift;
            float _GlowIntensity;
            float _GlowFalloff;
            float _EdgeFadeWidth;
            float _EdgeFadePower;
            float _EndFadeWidth;

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p.yx + 19.19);
                return frac(p.x * p.y);
            }

            float noise1D(float t)
            {
                float i = floor(t);
                float f = frac(t);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(hash(float2(i, 0)), hash(float2(i + 1, 0)), f);
            }

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseTex = tex2D(_MainTex, i.uv);
                fixed4 col = _Color * baseTex * i.color;

                // Edge fade (both sides): alpha softly fades to 0 near edges
                // UV.x across width: 0 (left edge) -> 1 (right edge)
                float edgeDist = min(i.uv.x, 1.0 - i.uv.x); // 0 at edges, 0.5 at center
                float edgeFade = smoothstep(0.0, _EdgeFadeWidth, edgeDist);
                edgeFade = pow(edgeFade, _EdgeFadePower);

                // End fade (along trail length): fade at head/tail to avoid rectangular ends
                float endDist = min(i.uv.y, 1.0 - i.uv.y); // 0 at ends, 0.5 at middle
                float endFade = smoothstep(0.0, _EndFadeWidth, endDist);

                col.a *= edgeFade * endFade;

                // Glow effect at edges (UV.x = across trail width)
                float centerDist = abs(i.uv.x - 0.5) * 2.0; // 0 at center, 1 at edge
                float edgeFactor = 1.0 - centerDist; // 1 at edge, 0 at center
                float glow = pow(edgeFactor, _GlowFalloff);
                float glowMult = 1.0 + glow * _GlowIntensity;
                col.rgb *= glowMult;
                // Slight alpha boost at edges, but keep fade-to-transparent
                col.a *= (1.0 + glow * _GlowIntensity * 0.2);

                // UV.y = along trail (0 at one end, 1 at other)
                float along = i.uv.y;
                float t = _Time.y * _NoiseSpeed;

                // Banded stripes along trail: sharp bands
                float stripe = sin(along * _StripeFrequency * 6.28318);
                stripe = stripe * 0.5 + 0.5;
                stripe = pow(abs(stripe), 1.0 / max(_StripeSharpness, 0.01));

                // Fragmentation: noise so gaps are irregular
                float n = noise2D(float2(along * _NoiseScale, t));
                float fragMask = stripe * (0.7 + 0.3 * n);

                // Absorption: where mask is high, reduce alpha (eaten)
                float absorbMask = saturate(fragMask);
                // Ensure absorption is visible: when _Absorb > 0, create gaps
                float alphaMult = 1.0 - _Absorb * absorbMask;
                col.a *= alphaMult;
                
                // Debug: if _Absorb is high, make it very obvious
                if (_Absorb > 0.01)
                {
                    // The absorption mask should create visible gaps
                    // If _Absorb is 1.0 and absorbMask is 1.0, alpha becomes 0 (fully eaten)
                }

                // Optional tint where absorbed (e.g. blue scattering)
                if (_UseTintShift > 0.5 && absorbMask > 0.01)
                {
                    col.rgb = lerp(col.rgb, _TintShift.rgb, _TintShift.a * _Absorb * absorbMask);
                }

                return col;
            }
            ENDCG
        }
    }
    Fallback "Particles/Additive"
}
