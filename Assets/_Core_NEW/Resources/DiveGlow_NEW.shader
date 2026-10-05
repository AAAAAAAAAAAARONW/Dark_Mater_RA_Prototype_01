Shader "Custom/DiveGlow_NEW"
{
    // The light a dive goes into: a camera-facing quad with a soft radial glow, drawn
    // additively. Queue 2990 puts it after the cosmic web's volumes (2950-2954) and
    // before the photon trail (3000), so it sits inside the web and behind the light.
    //
    // As a star (a continuous dive) the glow takes only the middle of the quad, and the
    // rest has room for diffraction spikes: four long rays and four short ones between
    // them, a pixel or so wide however big the quad is, white at the star and cooling
    // towards their tips.
    //
    // Lives in Resources so a build always has it: LayerDive_NEW loads it by name and
    // no material in any scene refers to it.

    Properties
    {
        _Color ("Colour (alpha = intensity)", Color) = (1, 0.9, 0.7, 1)
        // 1: the glow fills the quad. Above 1 it takes the middle 1/_GlowScale of it.
        _GlowScale ("Glow scale", Float) = 1
        _Spikes ("Spikes (brightness)", Float) = 0
        _SpikeLength ("Spike length (of the quad's half-width)", Range(0, 1)) = 1
        _SpikeAngle ("Spike angle (radians)", Float) = 0
        _SpikeTint ("Spike colour at the tips", Color) = (0.72, 0.84, 1, 1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float  _GlowScale, _Spikes, _SpikeLength, _SpikeAngle;
            fixed4 _SpikeTint;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2.0 - 1.0;
                return o;
            }

            // One ray along `a`, `b` across it: about a pixel and a half wide (`px` is the
            // quad's units per pixel), bright at the star and fading out to `len`.
            float Ray(float a, float b, float px, float len)
            {
                float along = saturate(1.0 - abs(a) / len);
                float across = exp(-(b * b) / (2.0 * px * px));
                return across * along * along * (0.3 + 0.7 * exp(-abs(a) * 6.0 / len));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float r = length(i.uv) * _GlowScale;
                float halo = saturate(1.0 - r);
                halo = halo * halo * halo;
                float core = saturate(1.0 - r * 3.0);
                core = core * core;
                float3 light = _Color.rgb * (halo * 0.7 + core);

                // Outside the branch: screen-space derivatives want every pixel of the quad.
                float px = max(fwidth(i.uv.x), 1e-4) * 1.2;

                if (_Spikes > 0.0)
                {
                    float s, c;
                    sincos(_SpikeAngle, s, c);
                    float2 q = float2(c * i.uv.x + s * i.uv.y, c * i.uv.y - s * i.uv.x);
                    float2 d = float2(q.x + q.y, q.y - q.x) * 0.70710678;
                    float len = max(_SpikeLength, 1e-3);

                    float rays = Ray(q.x, q.y, px, len) + Ray(q.y, q.x, px, len)
                               + 0.4 * (Ray(d.x, d.y, px, 0.55 * len) + Ray(d.y, d.x, px, 0.55 * len));
                    float3 tint = lerp(_Color.rgb, _SpikeTint.rgb, saturate(length(i.uv) / len));
                    light += tint * rays * _Spikes;
                }

                return fixed4(light * _Color.a, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
