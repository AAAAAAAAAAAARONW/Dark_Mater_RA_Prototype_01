Shader "Custom/QuasarSparks_NEW"
{
    // The quasar's moving matter (QuasarVFX_NEW): embers of hot gas orbiting in the disc and
    // drifting in, bright hot spots flaring near its inner edge, and clumps of plasma running
    // out along the jets. All of it moved on the GPU: each quad carries only its seeds, and
    // the vertex shader works out where its particle is now, so there is nothing to update
    // from the CPU and no particle system to keep in step.
    //
    // Each particle is drawn as a soft blob stretched along its own motion on the screen, so
    // fast ones streak: the embers near the core into arcs, the jet's plasma into lines — and
    // seen from inside a jet, into streaks rushing past.
    //
    // Orbits are Kepler's, at the disc's own rate (_Spin turns a second at its inner edge), on
    // the same clock as the disc (_Pace, _PaceOffset), so the embers turn with the gas.
    //
    // Drawn after everything, so with a black hole in the middle (QuasarHole_NEW) a particle
    // beyond the middle that is behind the hole's shadow on screen is put out — as the disc's
    // far half is — unless the camera is in the core's glow, where the hole is gone.
    //
    // Additive. _Color fades it. In Resources so a build always has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        _Inner ("Disc inner edge (of the radius)", Float) = 0.07
        _Spin ("Disc turns a second at the inner edge", Float) = 0.25
        _Drift ("How far in an ember drifts, of its radius", Float) = 0.3
        [HDR] _Hot ("Disc inner edge", Color) = (2.08, 1.6, 0.96, 1)
        [HDR] _Mid ("Disc middle", Color) = (1.36, 0.34, 0.04, 1)
        [HDR] _Outer ("Disc rim", Color) = (0.4, 0.028, 0.012, 1)
        [HDR] _JetColor ("Jet plasma", Color) = (1.5, 1.7, 2.1, 1)
        _JetLength ("Jet length, disc radii", Float) = 2.6
        _JetWidth ("Jet width at the tip (of its length)", Float) = 0.035
        _TwistTurns ("Jet filaments' turns along it", Float) = 1.5
        _JetSpeed ("Jet plasma speed, lengths a second (min, max)", Vector) = (0.25, 0.6, 0, 0)
        _CounterJet ("The far jet, against the near", Float) = 0.5
        _Sizes ("Sizes, disc radii: ember, hot spot, jet clump", Vector) = (0.008, 0.015, 0.01, 0)
        _Brightness ("Brightness", Float) = 0.7
        _Stretch ("Streak length, seconds of motion", Float) = 0.6
        _MaxSize ("Largest on screen, of half its height", Float) = 0.02
        _HoleRadius ("Black hole radius (of the object's scale); 0 = none", Float) = 0
        _HoleNear ("Hole gone within this far of the middle, x2 (of the object's scale)", Float) = 0.35

        // Set every frame by QuasarVFX_NEW.
        _Pace ("Clock rate", Float) = 1
        _PaceOffset ("Clock offset", Float) = 0
        _Bright ("Brightness", Float) = 1
        _FlareAge ("Seconds since the eruption", Float) = 1000
        _FlareGain ("Eruption strength", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent+3" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
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

            fixed4 _Color;
            float _Inner, _Spin, _Drift, _JetLength, _JetWidth, _TwistTurns, _CounterJet;
            float _Brightness, _Stretch, _MaxSize, _HoleRadius, _HoleNear;
            float4 _Hot, _Mid, _Outer, _JetColor, _JetSpeed, _Sizes;
            float _Pace, _PaceOffset, _Bright, _FlareAge, _FlareGain;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 corner : TEXCOORD0;   // -1 or 1, each way
                float4 seed   : TEXCOORD1;   // three seeds in 0..1, and the kind: 0 ember, 1 hot spot, 2 jet
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float2 corner : TEXCOORD0;
                float3 colour : TEXCOORD1;
            };

            float Omega(float r)
            {
                return _Spin * TAU * pow(max(r, _Inner) / _Inner, -1.5);
            }

            float3 Ramp(float r)
            {
                float t = saturate((r - _Inner) / (1.0 - _Inner));
                return t < 0.12 ? _Hot.rgb * 1.4
                     : t < 0.42 ? lerp(_Hot.rgb, _Mid.rgb, smoothstep(0.12, 0.42, t))
                     :            lerp(_Mid.rgb, _Outer.rgb, smoothstep(0.42, 1.0, t));
            }

            v2f vert(appdata v)
            {
                v2f o;
                float T = _Time.y * _Pace + _PaceOffset;
                float s0 = v.seed.x, s1 = v.seed.y, s2 = v.seed.z;
                float kind = v.seed.w;

                float3 position, velocity, colour;
                float size;

                if (kind < 0.5)
                {
                    // An ember: orbiting where it was born, drifting a little way in as it fades.
                    float life = lerp(4.0, 10.0, s1);
                    float age = frac(s0 + T / life);
                    float tau = age * life;
                    float born = _Inner * 1.2 + (1.0 - _Inner * 1.2) * s2 * s2;
                    float r0 = born;
                    float rHalf = born * (1.0 - _Drift * pow(age * 0.5, 1.5));
                    float r = born * (1.0 - _Drift * pow(age, 1.5));
                    // How far round it has gone: Simpson's rule over its orbit so far.
                    float angle = frac(s0 * 977.0 / TAU) * TAU + tau / 6.0 * (Omega(r0) + 4.0 * Omega(rHalf) + Omega(r));
                    float y = (s1 - 0.5) * 0.03 * r;
                    float2 round_ = float2(cos(angle), sin(angle));
                    float vt = Omega(r) * r;
                    float vr = -born * _Drift * 1.5 * sqrt(max(age, 1e-3)) / life;
                    position = float3(round_.x * r, y, round_.y * r);
                    velocity = float3(-round_.y * vt + round_.x * vr, 0.0, round_.x * vt + round_.y * vr);
                    float b = smoothstep(0.0, 0.12, age) * smoothstep(1.0, 0.85, age) * (0.6 + 0.4 * sin(T * (8.0 + 14.0 * s2) + s0 * 50.0));
                    colour = Ramp(r) * b;
                    size = _Sizes.x * (0.6 + 0.8 * s2);
                }
                else if (kind < 1.5)
                {
                    // A hot spot: a bright clump flaring and fading as it orbits near the inner edge.
                    float life = lerp(2.0, 5.0, s1);
                    float age = frac(s0 + T / life);
                    float r = lerp(_Inner * 1.15, _Inner * 2.6, s2);
                    float angle = frac(s0 * 977.0 / TAU) * TAU + Omega(r) * age * life;
                    float2 round_ = float2(cos(angle), sin(angle));
                    float vt = Omega(r) * r;
                    position = float3(round_.x * r, 0.0, round_.y * r);
                    velocity = float3(-round_.y * vt, 0.0, round_.x * vt);
                    float b = sin(3.14159265 * age);
                    colour = _Hot.rgb * 1.6 * b * b * (0.7 + 0.3 * sin(T * 23.0 + s0 * 40.0));
                    size = _Sizes.y * (0.7 + 0.6 * s1);
                }
                else
                {
                    // Plasma running out along a jet, wound round it as the filaments are.
                    float side = s2 > 0.5 ? 1.0 : -1.0;
                    float speed = lerp(_JetSpeed.x, _JetSpeed.y, s1);
                    float s = frac(s0 + T * speed);
                    float sigma = _JetWidth * _JetLength * (0.22 + 0.78 * s);
                    float rho = sigma * 0.7 * sqrt(frac(s0 * 7.31));
                    float phi = frac(s0 * 977.0 / TAU) * TAU + s * _TwistTurns * TAU / 3.0;
                    position = float3(rho * cos(phi), side * s * _JetLength, rho * sin(phi));
                    velocity = float3(0.0, side * speed * _JetLength, 0.0);
                    float b = smoothstep(0.0, 0.05, s) * pow(1.0 - s, 1.5) * (side > 0.0 ? 1.0 : _CounterJet);
                    colour = _JetColor.rgb * b;
                    size = _Sizes.z * (0.5 + s);
                }

                // The eruption brightens everything for a moment.
                colour *= _Brightness * _Bright * (1.0 + _FlareGain * exp(-_FlareAge / 0.8) * 1.5);

                // To the screen, stretched along its own motion there.
                float scale = length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));
                float3 world = mul(unity_ObjectToWorld, float4(position, 1.0)).xyz;
                float3 view = mul(UNITY_MATRIX_V, float4(world, 1.0)).xyz;
                float3 viewVelocity = mul((float3x3)UNITY_MATRIX_V, mul((float3x3)unity_ObjectToWorld, velocity));
                float depth = -view.z;

                // Behind the black hole: beyond its middle, and inside its shadow on screen.
                if (_HoleRadius > 0.0)
                {
                    float3 middle = mul(UNITY_MATRIX_V, float4(mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz, 1.0)).xyz;
                    float middleDepth = max(-middle.z, 1e-4);
                    if (depth > middleDepth)
                    {
                        float off = length(view.xy / max(depth, 1e-4) - middle.xy / middleDepth);
                        float shadow = _HoleRadius * scale / middleDepth;
                        float seen = saturate(middleDepth / max(scale * _HoleNear, 1e-6) - 1.0)
                                   * smoothstep(1.5, 4.0, shadow * abs(unity_CameraProjection._m11) * _ScreenParams.y * 0.5);
                        colour *= lerp(1.0, smoothstep(shadow * 0.95, shadow * 1.1, off), seen);
                    }
                }

                // On the screen plane, in units of depth (tangent of the angle off the axis).
                float tanHalf = 1.0 / unity_CameraProjection._m11;
                float2 motion = (viewVelocity.xy * depth + view.xy * viewVelocity.z) / max(depth * depth, 1e-6);
                // However fast it crosses the screen, no longer than a quarter of its height: one
                // passing right by the camera was drawn as a line across the whole frame.
                float streak = min(length(motion) * _Stretch, 0.5 * tanHalf);
                float2 along = streak > 1e-7 ? motion / length(motion) : float2(1.0, 0.0);
                float half_ = min(size * scale / max(depth, 1e-4), _MaxSize * tanHalf);

                // Never thinner than a pixel and a half: dimmer instead, so a distant ember does
                // not flicker in and out between pixels.
                float pixel = 2.0 * tanHalf / _ScreenParams.y;
                float wide = max(half_, 1.5 * pixel);
                colour *= half_ / wide;
                float halfLength = wide + streak * 0.5;
                // Its light spread along the streak, a little: a long streak is fainter.
                colour *= sqrt(wide / halfLength);

                float2 offset = (along * v.corner.x * halfLength + float2(-along.y, along.x) * v.corner.y * wide) * depth;
                view.xy += offset;

                // Right by the camera, faded out before it fills the frame.
                colour *= smoothstep(0.004, 0.02, depth / max(scale, 1e-6));

                // Behind the camera, or right on it: nothing.
                if (depth < 1e-3) view = float3(0.0, 0.0, 1.0);

                o.pos = mul(UNITY_MATRIX_P, float4(view, 1.0));
                o.corner = v.corner;
                o.colour = colour;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float q = dot(i.corner, i.corner);
                if (q >= 1.0) return 0;
                float k = exp(-q * 3.0) * (1.0 - q);
                return fixed4(i.colour * k * _Color.rgb, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
