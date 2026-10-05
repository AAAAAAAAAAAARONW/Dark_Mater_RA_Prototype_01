Shader "Custom/EarthClouds_NEW"
{
    // The cloud deck the photon falls through as it comes into Earth's atmosphere, and the
    // atmosphere's haze round it (EarthClouds_NEW).
    //
    // Raymarched. Each pixel walks its view ray through a flat slab of cloud, endless across,
    // between _DeckBottom and _DeckTop: density from a tiling 3D noise (R big billowy shapes,
    // G the detail that wears their edges, B a slow "weather" that clumps them, with gaps),
    // cut to a coverage and shaped to cumulus — thin at the base, full through the middle,
    // rounded at the top. Long grazing rays get more, longer steps, and fade into the haze. Each sample is lit by the
    // sun (one look towards it for its own shadow, and forward scattering for bright edges
    // against the light), by the sky (bluer and brighter at the top of the deck), and by the
    // photon when it is close, so the cloud round the light glows. Behind the clouds, where
    // space was, the sky: deep blue overhead, paler at the horizon, a glow round the sun —
    // as much of it as _Haze says.
    //
    // Drawn on a cube round the camera, inside faces, ZTest Always, so it covers the screen
    // from anywhere — above the deck, in it, or under it. Premultiplied alpha. Queue 2980:
    // after the worlds, before the dive's lights (2990) and the photon trail (3000), so the
    // photon is never lost in the fog. In Resources so a build always has it.

    Properties
    {
        [NoScaleOffset] _Noise ("Noise (R shape, G detail)", 3D) = "" {}
        _DeckBottom ("Deck bottom (world y)", Float) = -10
        _DeckTop ("Deck top (world y)", Float) = -4
        _DeckOffset ("Noise offset (world)", Vector) = (0, 0, 0, 0)
        _ShapeScale ("Shape scale (tiles per world unit)", Float) = 0.12
        _DetailScale ("Detail scale (tiles per world unit)", Float) = 0.45
        _Coverage ("Coverage", Range(0, 1)) = 0.55
        _Erosion ("Detail erosion", Range(0, 1)) = 0.4
        _Weather ("Clumping", Range(0, 2)) = 0.9
        _Density ("Density", Float) = 1.4
        _Steps ("Steps", Range(8, 96)) = 40
        _MaxStep ("Longest step (world)", Float) = 0.6
        _MaxDistance ("Furthest cloud (world)", Float) = 60

        _SunDir ("Towards the sun", Vector) = (0.5, 0.5, 0.5, 0)
        _SunColor ("Sun", Color) = (1, 0.96, 0.9, 1)
        _AmbientTop ("Sky light, top of the deck", Color) = (0.62, 0.72, 0.9, 1)
        _AmbientBottom ("Sky light, its base", Color) = (0.3, 0.35, 0.46, 1)

        _PhotonPos ("Photon (world)", Vector) = (0, 0, 0, 0)
        _PhotonColor ("Photon glow (alpha = strength)", Color) = (1, 0.95, 0.85, 1.5)
        _PhotonRadius ("Photon glow radius (world)", Float) = 2.5

        _Zenith ("Sky overhead", Color) = (0.16, 0.3, 0.6, 1)
        _Horizon ("Sky at the horizon", Color) = (0.62, 0.74, 0.9, 1)
        _Ground ("Looking down, the ground through the air", Color) = (0.1, 0.16, 0.26, 1)
        _Haze ("Sky over space", Range(0, 1)) = 0
        _Opacity ("Clouds", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent-20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Front

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler3D _Noise;
            float _DeckBottom, _DeckTop, _ShapeScale, _DetailScale, _Coverage, _Erosion, _Weather, _Density;
            float _Steps, _MaxStep, _MaxDistance, _PhotonRadius, _Haze, _Opacity;
            float4 _DeckOffset, _SunDir, _PhotonPos;
            float4 _SunColor;   // HDR: the sun is brighter than white
            fixed4 _AmbientTop, _AmbientBottom, _PhotonColor, _Zenith, _Horizon, _Ground;

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float3 world  : TEXCOORD0;
                float4 screen : TEXCOORD1;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.world = mul(unity_ObjectToWorld, vertex).xyz;
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            float Remap(float v, float a, float b, float c, float d)
            {
                return c + (v - a) / max(1e-4, b - a) * (d - c);
            }

            // How high in the deck, 0 at its base, 1 at its top.
            float Height(float y)
            {
                return saturate((y - _DeckBottom) / max(1e-3, _DeckTop - _DeckBottom));
            }

            // Cloud at a world point: the shapes, cut to the coverage — more where the slow
            // weather is up, less where it is down — and to a cumulus profile, their edges worn
            // away by the detail.
            float Density(float3 p)
            {
                float h = Height(p.y);
                float profile = saturate(h * 5.0) * saturate((1.0 - h) * 2.2);
                float3 q = p - _DeckOffset.xyz;

                float shape = tex3Dlod(_Noise, float4(q * _ShapeScale, 0)).r;
                float weather = tex3Dlod(_Noise, float4(q * _ShapeScale * 0.25, 0)).b;
                float cover = saturate(_Coverage + (weather - 0.5) * _Weather);
                float base = saturate(Remap(shape * profile, 1.0 - cover, 1.0, 0.0, 1.0));
                if (base <= 0.0) return 0.0;

                float detail = tex3Dlod(_Noise, float4(q * _DetailScale, 0)).g;
                return saturate(Remap(base, detail * _Erosion, 1.0, 0.0, 1.0)) * _Density;
            }

            // Henyey-Greenstein: how much light scatters through an angle whose cosine is c.
            float Phase(float c, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) / (12.566 * pow(max(1e-4, 1.0 + g2 - 2.0 * g * c), 1.5));
            }

            float3 Sky(float3 dir, float3 sun)
            {
                float up = dir.y;
                float3 sky = up >= 0.0
                    ? lerp(_Horizon.rgb, _Zenith.rgb, saturate(up * 1.6 + 0.05))
                    : lerp(_Horizon.rgb, _Ground.rgb, saturate(-up * 2.5));
                float toward = saturate(dot(dir, sun));
                return sky + _SunColor.rgb * (pow(toward, 64.0) * 0.6 + pow(toward, 6.0) * 0.12);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 eye = _WorldSpaceCameraPos;
                float3 dir = normalize(i.world - eye);
                float3 sun = normalize(_SunDir.xyz);

                // Where the ray is in the deck.
                float near = 0.0, far = 0.0;
                if (abs(dir.y) < 1e-4)
                {
                    bool inside = eye.y > _DeckBottom && eye.y < _DeckTop;
                    far = inside ? _MaxDistance : 0.0;
                }
                else
                {
                    float a = (_DeckBottom - eye.y) / dir.y;
                    float b = (_DeckTop - eye.y) / dir.y;
                    near = max(min(a, b), 0.0);
                    far = min(max(a, b), _MaxDistance);
                }

                float3 light = 0.0;
                float through = 1.0;    // how much still gets through

                if (far > near && _Opacity > 0.001)
                {
                    // As many steps as asked, more for a long grazing ray, none longer than _MaxStep.
                    float steps = clamp(ceil((far - near) / max(0.05, _MaxStep)), max(8.0, _Steps), max(8.0, _Steps) * 2.0);
                    float stepLength = (far - near) / steps;

                    // Each pixel starts a different part of a step in, so the steps blur
                    // into noise rather than show as bands.
                    float2 pixel = i.screen.xy / max(1e-4, i.screen.w) * _ScreenParams.xy;
                    float jitter = frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
                    float t = near + stepLength * jitter;

                    float forward = Phase(dot(dir, sun), 0.6);
                    float spread = Phase(dot(dir, sun), -0.2);
                    float scatter = 0.25 + 6.0 * lerp(spread, forward, 0.7);

                    [loop]
                    for (int s = 0; s < 192; s++)
                    {
                        if (s >= steps || through < 0.02) break;

                        float3 p = eye + dir * t;
                        float fall = saturate(1.0 - t / _MaxDistance);
                        float d = Density(p) * fall * fall;
                        if (d > 0.002)
                        {
                            // Its own shadow: what lies between it and the sun.
                            float toSun = Density(p + sun * 0.7) * 0.7 + Density(p + sun * 2.0) * 1.3;
                            float lit = exp(-toSun * 1.4);
                            float powder = 1.0 - exp(-d * 2.0);

                            float h = Height(p.y);
                            float3 sky = lerp(_AmbientBottom.rgb, _AmbientTop.rgb, h);

                            float3 photon = p - _PhotonPos.xyz;
                            float glow = _PhotonColor.a / (1.0 + dot(photon, photon) / max(1e-3, _PhotonRadius * _PhotonRadius) * 4.0);

                            float3 colour = _SunColor.rgb * lit * scatter * lerp(0.6, 1.0, powder)
                                          + sky * (0.7 + 0.3 * h)
                                          + _PhotonColor.rgb * glow;

                            float a = 1.0 - exp(-d * stepLength * 1.6);
                            light += through * a * colour;
                            through *= 1.0 - a;
                        }
                        t += stepLength;
                    }
                }

                float cloud = (1.0 - through) * _Opacity;
                light *= _Opacity;

                // Behind the clouds, the sky, over whatever was there.
                float sky = (1.0 - cloud) * _Haze;
                float3 colour = light + Sky(dir, sun) * sky;
                return fixed4(colour, cloud + sky);
            }
            ENDCG
        }
    }

    FallBack Off
}
