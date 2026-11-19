Shader "Custom/LightDust_NEW"
{
    // Dust the light flies through once it has left the quasar (TutorialLightDust_NEW): what
    // makes its speed something seen. One mesh of quads, placed and drawn entirely here.
    //
    // WHERE THE MOTES ARE. Each quad carries a seed in a unit cube. The cube is tiled round the
    // light and slid back by _Offset, how far the light has gone, so motes come from ahead,
    // pass, fall behind and come round again; the cube's faces are faded out, so none is seen
    // to jump. They are in the world, not on the lens: looking round turns them properly, and
    // the near ones cross the frame faster than the far ones.
    //
    // WHAT MAKES IT READ AS SPEED AND NOT AS SNOW.
    //   streaks   Each mote is drawn as a soft capsule along _Streak, the way it moved while
    //             the shutter was open: a point at rest, a line at speed. A longer one is
    //             dimmer by as much, as a real exposure is.
    //   the lamp  The motes are lit by the light they are passing: bright beside it, dim away
    //             from it, in its colour. That is what places the light among them.
    //   focus     Near the lens a mote is a wide dim disc; far off, a point never under a pixel
    //             and a half across, dimmed by what it was widened, so nothing sparkles.
    //
    // Additive, no depth written. Works in both eyes: placed from the light, not the camera.

    Properties
    {
        _Amount ("Amount (TutorialLightDust_NEW fades it in)", Range(0, 1)) = 1
        _Box ("Edge of the cube the motes are tiled in, units", Float) = 70
        _Centre ("The light", Vector) = (0, 0, 0, 0)
        _Offset ("How far the light has gone, in cubes", Vector) = (0, 0, 0, 0)
        _Streak ("How a mote moved while the shutter was open, units", Vector) = (0, 0, 0, 0)
        _Size ("Mote radius, units", Float) = 0.03
        _Ambient ("Brightness away from the light", Range(0, 2)) = 0.3
        _LampColor ("The light's colour times how brightly it lights them", Color) = (1.5, 1.6, 2.0, 1)
        _LightReach ("How far it lights them, units", Float) = 7
        _Warm ("Warm motes", Color) = (1.0, 0.84, 0.62, 1)
        _Cool ("Cool motes", Color) = (0.68, 0.8, 1.0, 1)
        _Focus ("Nearer than this a mote goes out of focus, units", Float) = 5
    }

    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
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

            float _Amount, _Box, _Size, _Ambient, _LightReach, _Focus;
            float4 _Centre, _Offset, _Streak;
            float4 _LampColor, _Warm, _Cool;

            struct appdata
            {
                float4 vertex : POSITION;      // the mote's seed, 0..1 each way
                float2 uv : TEXCOORD0;         // which corner, -1..1
                float4 rnd : TEXCOORD1;        // four numbers of its own, 0..1
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 uvl : TEXCOORD0;        // across (-1..1), along (in radii), length (in radii)
                float4 colour : TEXCOORD1;     // rgb, and how out of focus (0..1)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Where it is: tiled round the light, slid back by how far the light has gone,
                // and drifting a little on its own so that at rest the dust is not frozen.
                float3 cell = frac(v.vertex.xyz - _Offset.xyz) - 0.5;
                float3 drift = sin(_Time.y * (0.15 + 0.25 * v.rnd.xyz) + v.rnd.yzx * 6.2832) * 0.006;
                float3 world = _Centre.xyz + (cell + drift) * _Box;

                // The two ends of its streak, in the camera's space.
                float3 half_ = _Streak.xyz * (0.35 + 0.3 * v.rnd.x);
                float3 a = mul(UNITY_MATRIX_V, float4(world + half_, 1.0)).xyz;
                float3 b = mul(UNITY_MATRIX_V, float4(world - half_, 1.0)).xyz;
                float3 mid = 0.5 * (a + b);
                float depth = -mid.z;
                float dist = length(mid);

                // Which way the streak runs on the screen. End on, it has no way: a point.
                float2 pa = a.xy / max(-a.z, 0.05), pb = b.xy / max(-b.z, 0.05);
                float2 across = pb - pa;
                float run = length(across);
                float2 dir = run > 1e-5 ? across / run : float2(0.0, 1.0);
                float2 side = float2(-dir.y, dir.x);

                // How big: its own size, wider out of focus, never under a pixel and a half.
                float radius = _Size * (0.5 + 1.6 * v.rnd.w * v.rnd.w);
                float blur = saturate(1.0 - dist / max(_Focus, 0.01));
                float wide = radius * (1.0 + 4.0 * blur * blur);
                float pixel = 2.0 * max(depth, 0.05) / (abs(UNITY_MATRIX_P[1][1]) * _ScreenParams.y);
                float drawn = max(wide, 0.75 * pixel);
                float dimmed = (radius * radius) / (drawn * drawn);

                float len = length(b.xy - a.xy);
                float t = v.uv.y * 0.5 + 0.5;
                float3 view = lerp(a, b, t);
                view.xy += (side * v.uv.x + dir * v.uv.y) * drawn;
                o.pos = mul(UNITY_MATRIX_P, float4(view, 1.0));

                float lr = len / drawn;
                o.uvl = float3(v.uv.x, v.uv.y < 0.0 ? -1.0 : lr + 1.0, lr);

                // Lit by the light it is passing, in that light's colour, over its own.
                float3 toLight = (world - _Centre.xyz) / max(_LightReach, 0.01);
                float lamp = 1.0 / (1.0 + dot(toLight, toLight));
                float3 own = lerp(_Warm.rgb, _Cool.rgb, v.rnd.y);
                float3 colour = own * _Ambient + lerp(own, float3(1.0, 1.0, 1.0), 0.5) * _LampColor.rgb * lamp;

                // Out at the cube's faces, right at the lens, and behind it.
                float3 ac = abs(cell);
                float edge = 1.0 - smoothstep(0.36, 0.5, max(ac.x, max(ac.y, ac.z)));
                float seen = smoothstep(0.35, 1.6, dist) * step(0.05, min(-a.z, -b.z));
                float twinkle = 0.8 + 0.2 * sin(_Time.y * (0.6 + 2.0 * v.rnd.z) + v.rnd.x * 40.0);

                // Most are faint and a few are bright: evenly bright, dust reads as a pattern.
                float own_ = 0.2 + 1.6 * v.rnd.z * v.rnd.z * v.rnd.z;

                // A streak spreads the same light along its length; a wide disc over its area
                // (not all the way: an out-of-focus mote that kept nothing would not be seen).
                float spread = 1.0 / (1.0 + 0.45 * lr);
                float kept = lerp(dimmed, sqrt(dimmed), 0.5);
                o.colour = float4(colour * (_Amount * edge * seen * twinkle * spread * kept * own_), blur);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Distance from the streak's spine, in radii: a capsule.
                float along = i.uvl.y - clamp(i.uvl.y, 0.0, i.uvl.z);
                float d = length(float2(i.uvl.x, along));

                // In focus, a soft point; out of focus, a wider and softer one, dimmer in the
                // middle than a point is. Never a hard-edged disc: at speed that is a tube.
                float sharp = exp(-d * d * 4.5);
                float soft = (1.0 - smoothstep(0.15, 1.0, d)) * 0.6;
                float shape = lerp(sharp, soft, saturate(i.colour.a * 1.6)) * step(d, 1.0);

                // A streak thins out to nothing at both ends instead of stopping: a brush
                // stroke, not a rod.
                float u = saturate(i.uvl.y / max(i.uvl.z, 1e-3));
                float taper = smoothstep(0.0, 0.4, u) * (1.0 - smoothstep(0.55, 1.0, u)) * 1.3;
                shape *= lerp(1.0, taper, saturate(i.uvl.z * 0.4));

                return fixed4(i.colour.rgb * shape, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
