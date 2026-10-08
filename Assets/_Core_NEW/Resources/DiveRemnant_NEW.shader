Shader "Custom/DiveRemnant_NEW"
{
    // What a world becomes when the photon leaves it for the scale above (DiveRemnant_NEW):
    // the quasar, collapsed into one point of light in the cosmic web. A camera-facing quad
    // at the point, sized on the screen, not in the world — it is a point however far off —
    // with a soft core never under a pixel and a half, a faint halo, and the diffraction
    // spikes and horizontal streak of QuasarCore_NEW, drawn the same way: the quasar's own core,
    // shrinking, turns into a star with them as its black hole becomes too small to see, and
    // this point takes over from it without a change.
    //
    // DiveRemnant_NEW drives it: _Fade is the remnant's own appearance times its world's fade
    // (WorldSwitcher_NEW does not hold it), and as it takes over from the world it was, it
    // flares — brighter, its spikes thrown out longer, a wider halo — and settles.
    //
    // Additive, queue 2990: inside the web's volumes, behind the photon trail.
    // In Resources so a build always has it: DiveRemnant_NEW loads it by name.

    Properties
    {
        [HDR] _Color ("Colour, times brightness", Color) = (1.6, 1.55, 1.8, 1)
        _Fade ("Fade", Range(0, 1)) = 1
        _Size ("Core radius, of the screen's height", Float) = 0.0025
        _Halo ("Halo radius, of the screen's height (0 = none)", Float) = 0.01
        _HaloAmount ("Halo brightness", Float) = 0.03
        _Spikes ("Spikes", Float) = 1
        _SpikeLength ("Spike length, of half the screen's height", Float) = 0.14
        _SpikeAngle ("Spike angle, degrees", Float) = 45
        _Streak ("Horizontal streak", Float) = 0.3
        [HDR] _SpikeTint ("Streak colour", Color) = (0.75, 0.85, 1.25, 1)
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

            float4 _Color, _SpikeTint;
            float _Fade, _Size, _Halo, _HaloAmount, _Spikes, _SpikeLength, _SpikeAngle, _Streak;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 q   : TEXCOORD0;   // from the point, in half screen heights, on the screen's axes
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                float3 view = UnityObjectToViewPos(float3(0.0, 0.0, 0.0));
                float depth = -view.z;

                // How far the quad reaches, in half screen heights: the spikes and the streak,
                // as QuasarCore_NEW sizes them, or the glow and the halo.
                float spikes = (_Spikes > 0.0 || _Streak > 0.0) ? _SpikeLength * 4.8 : 0.0;
                float reach = max(spikes, max(_Size * 12.0, _Halo * 5.2));
                reach = max(reach, 6.0 / _ScreenParams.y);
                float halfHeight = max(depth, 1e-4) / abs(unity_CameraProjection._m11);

                view.xy += vertex.xy * reach * halfHeight;
                o.pos = mul(UNITY_MATRIX_P, float4(view, 1.0));
                o.q = vertex.xy * reach;

                // Behind the camera: nothing.
                if (depth < 1e-3) o.pos = float4(0.0, 0.0, -2.0, 1.0);
                return o;
            }

            // QuasarCore_NEW's spike, so the one hands over to the other unchanged: a thin line
            // through the point along `angle`, falling off along it.
            float Spike(float2 q, float angle, float length_)
            {
                float2 d = float2(cos(angle), sin(angle));
                float along = abs(dot(q, d));
                float across = abs(q.x * -d.y + q.y * d.x);
                return exp(-across / (0.0015 + 0.004 * along)) * (0.015 / (0.015 + along)) * exp(-along / length_);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float pixel = 2.0 / _ScreenParams.y;             // one pixel, in half screen heights
                float r = length(i.q);
                float s = max(_Size * 2.0, 1.5 * pixel);        // never under a pixel and a half
                float core = exp(-r * r / (s * s)) + 0.15 * exp(-r / (s * 3.0));
                float h = max(_Halo * 2.0, 1e-4);
                float halo = _Halo > 0.0 ? exp(-r * r / (h * h)) * _HaloAmount : 0.0;   // gone by the quad's edge

                float a = radians(_SpikeAngle);
                float L = max(_SpikeLength, 1e-3);
                float spikes = Spike(i.q, a, L) + Spike(i.q, a + 1.5707963, L);
                float streak = exp(-abs(i.q.y) / 0.002) * (0.02 / (0.02 + abs(i.q.x))) * exp(-abs(i.q.x) / (L * 1.6));

                float3 light = _Color.rgb * (core + halo + spikes * _Spikes * 0.35)
                             + _Color.rgb * _SpikeTint.rgb * streak * _Streak * 0.25;
                return fixed4(light * _Fade, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
