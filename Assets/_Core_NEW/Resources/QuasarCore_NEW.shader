Shader "Custom/QuasarCore_NEW"
{
    // A quasar's core (QuasarVFX_NEW): the brightest thing in its galaxy. A quad turned to face
    // the camera round the object's middle: a white-hot point, a warm glow round it and a wide
    // soft halo; the black hole's shadow at its very middle with a thin ring of light round it
    // (the photon ring); and, because it is that bright, diffraction spikes and a horizontal
    // streak, as a telescope or a lens draws a point that bright.
    //
    // The glow is sized in the world, a fraction of the disc; the spikes are sized on the
    // screen, a fraction of its height, as a lens's are — so from far away the quasar is a
    // brilliant point with spikes, and close up the spikes do not grow to fill the sky. The
    // quad is as big as whichever needs more.
    //
    // QuasarVFX_NEW sets the brightness, the flicker and the eruption, which flashes the core
    // and throws the spikes out longer.
    //
    // Premultiplied alpha, so the glow adds light and the shadow blocks it. _Color fades it.
    // In Resources so a build always has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        _Size ("Glow size (of the object's scale)", Float) = 0.35
        [HDR] _Hot ("Core", Color) = (4, 3.7, 3.2, 1)
        [HDR] _Glow ("Glow", Color) = (1.1, 0.6, 0.25, 1)
        [HDR] _Halo ("Halo", Color) = (0.3, 0.1, 0.03, 1)
        _Shadow ("Shadow radius (of the glow size)", Range(0, 0.2)) = 0.03
        _Ring ("Photon ring", Float) = 1.5
        _Spikes ("Diffraction spikes", Float) = 0.6
        _SpikeLength ("Spike length (of half the screen's height)", Float) = 0.3
        _SpikeAngle ("Spike angle", Float) = 45
        _Streak ("Horizontal streak", Float) = 0.6

        // Set every frame by QuasarVFX_NEW.
        _Bright ("Brightness", Float) = 1
        _Pulse ("Flicker", Float) = 1
        _FlareAge ("Seconds since the eruption", Float) = 1000
        _FlareGain ("Eruption strength", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Size, _Shadow, _Ring, _Spikes, _SpikeLength, _SpikeAngle, _Streak;
            float _Bright, _Pulse, _FlareAge, _FlareGain;
            float4 _Hot, _Glow, _Halo;

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float2 core   : TEXCOORD0;   // in glow sizes from the middle
                float2 screen : TEXCOORD1;   // in half screen heights from the middle
                float3 flare  : TEXCOORD2;   // x: the flash, y: the spikes' length, z: how far off the camera is
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                float3 middle = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float scale = length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));
                float3 view = mul(UNITY_MATRIX_V, float4(middle, 1.0)).xyz;

                // Half the screen's height, in view units at the core's depth.
                float halfScreen = max(-view.z, 1e-4) / unity_CameraProjection._m11;
                float glow = scale * _Size;
                float flash = _FlareGain * exp(-_FlareAge / 0.5);
                float spikeLength = _SpikeLength * (1.0 + 1.5 * flash);
                float spikes = (_Spikes > 0.0 || _Streak > 0.0) ? spikeLength * 4.8 * halfScreen : 0.0;
                float extent = max(glow, spikes);

                view.xy += vertex.xy * extent;
                o.pos = mul(UNITY_MATRIX_P, float4(view, 1.0));
                o.core = vertex.xy * extent / max(glow, 1e-6);
                o.screen = vertex.xy * extent / halfScreen;
                // 0 with the camera inside the glow, 1 from twice its size out. See the shadow.
                o.flare = float3(flash, spikeLength, saturate(-view.z / max(glow, 1e-6) - 1.0));
                return o;
            }

            float Sq(float x) { return x * x; }

            // A thin line through the middle along `angle`, falling off along it.
            float Spike(float2 q, float angle, float length_)
            {
                float2 d = float2(cos(angle), sin(angle));
                float along = abs(dot(q, d));
                float across = abs(q.x * -d.y + q.y * d.x);
                return exp(-across / (0.0015 + 0.004 * along)) * (0.015 / (0.015 + along)) * exp(-along / length_);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float r = length(i.core);

                float hot = exp(-r * r * 220.0);
                float glow = exp(-r * r * 26.0);
                float halo = r < 1.0 ? exp(-r * 4.5) * (1.0 - r) : 0.0;
                float ring = _Shadow > 0.0 ? exp(-Sq((r - _Shadow * 1.25) / (_Shadow * 0.15))) * _Ring : 0.0;
                float3 light = _Hot.rgb * (hot + ring * 0.6) + _Glow.rgb * glow + _Halo.rgb * halo;

                float flash = i.flare.x;
                float spikeLength = i.flare.y;
                float a = radians(_SpikeAngle);
                float spikes = Spike(i.screen, a, spikeLength) + Spike(i.screen, a + 1.5708, spikeLength);
                float streak = exp(-abs(i.screen.y) / 0.002) * (0.02 / (0.02 + abs(i.screen.x)))
                             * exp(-abs(i.screen.x) / (spikeLength * 1.6));
                light += _Hot.rgb * spikes * _Spikes * 0.35
                       + _Hot.rgb * float3(0.75, 0.85, 1.25) * streak * _Streak * 0.25;

                light *= _Pulse * (1.0 + flash * 3.0) * _Bright;

                // The shadow: black, and blocking what is behind it. Not with the camera in
                // the glow — flying through the core (the tutorial does), the shadow would grow
                // to black out the whole frame just before the light came out the other side.
                float shadow = _Shadow > 0.0 ? (1.0 - smoothstep(_Shadow * 0.75, _Shadow, r)) * i.flare.z : 0.0;
                light *= 1.0 - shadow;

                // WorldSwitcher_NEW fades by scaling all four channels of _Color: the light
                // takes rgb, the shadow a — once each, a straight fade.
                return fixed4(light * _Color.rgb, shadow * _Color.a);
            }
            ENDCG
        }
    }

    FallBack Off
}
