Shader "Custom/HydrogenAbsorber_NEW"
{
    // A cloud of cold neutral hydrogen that the light passes through (HydrogenAbsorberVFX_NEW):
    // a real volume, lit from one side. It hides what is behind it, and is seen by the light it
    // catches: the lobes facing the light pink, the ones behind them and the cloud's heart deep
    // violet, so it has a near side, a far side and a shape.
    //
    // WHAT IT WAS. One flat amount of gas a ray in one colour, then the same with a shaded
    // tint: a stain on the sky. What makes a cloud read as a volume is its own shadow, and for
    // that the gas has to be asked how much of it lies towards the light.
    //
    // HOW. The sphere it is drawn on is only the bound; the ray is cut against it in closed
    // form and marched in ten steps, front to back. At each step:
    //   where the gas is   three lumps (the body, worked out once a pixel) pushed in and out by
    //                      a tiling 3D noise of billows (_Noise3D, red), and eaten at the edges
    //                      by a finer one (green): rounded lobes, not a blob with a rough rim.
    //   its shadow         the same body-and-billows a short step towards the light: one more
    //                      read. Much gas there: in shadow. None: lit.
    //   its light          lit colour to shadow colour by that, darker in the thin creases, and
    //                      a pale rim where thin gas lies between the eye and the light.
    // Thirty reads of a 32-cubed texture a pixel; NebulaCloud_NEW, which this sits beside,
    // takes ninety-six of a far bigger one. Never brighter than 0.95: cold gas, no bloom.
    //
    // Drawn from the sphere's back faces, so it works from inside too; real geometry, so both
    // eyes see it in the right place. Premultiplied over what is behind. _Color fades it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        _Appear ("Appear (HydrogenAbsorberVFX_NEW fades it in when it arrives)", Range(0, 1)) = 1
        [NoScaleOffset] _Noise3D ("Billows (red) and detail (green), tiling (HydrogenAbsorberVFX_NEW makes it)", 3D) = "gray" {}
        _Seed ("Seed", Float) = 0
        _Opacity ("How much it hides what is behind it", Range(0, 8)) = 2.5
        _Softness ("How soft its edge is", Range(0.1, 1)) = 0.55
        _DustAmount ("How dark its creases are", Range(0, 2)) = 0.6
        _Flow ("Drift, radii a second", Float) = 0.004
        _Resolve ("Nearer than x the gas is left out, from y all there (of the radius)", Vector) = (0.05, 0.3, 0, 0)
        _TowardLight ("Towards the light, in the object's space", Vector) = (0.5, 0.55, 0.65, 0)
        _LitColor ("Gas in the light", Color) = (1.0, 0.46, 0.66, 1)
        _ShadowColor ("Gas in its own shadow", Color) = (0.10, 0.035, 0.16, 1)
        _RimColor ("Light thin gas scatters on towards the eye", Color) = (1.0, 0.72, 0.82, 1)
        _Scatter ("How much of it", Range(0, 2)) = 0.9
        _Shadow ("How dark its own shadow is", Range(0, 6)) = 2.6
    }

    SubShader
    {
        Tags { "Queue"="Transparent-5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Front

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler3D _Noise3D;
            fixed4 _Color;
            float _Seed, _Opacity, _Softness, _DustAmount, _Flow, _Appear;
            float4 _Resolve, _TowardLight;
            fixed4 _LitColor, _ShadowColor, _RimColor;
            float _Scatter, _Shadow;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 obj : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.obj = v.vertex.xyz;
                return o;
            }

            float Hash(float n) { return frac(sin(n) * 43758.5453); }

            // The body: three lumps along the object's x (tipped by the seed), knocked off the
            // line. Where they are and how big is the same for every pixel, so it is worked out
            // once a pixel (Lumps) and not once a sample.
            struct Lumps { float3 c0, c1, c2; float3 r; };

            Lumps MakeLumps()
            {
                Lumps m;
                float s0 = _Seed * 3.3;
                float3 axis = normalize(float3(1.0, (Hash(s0 + 31.0) - 0.5) * 0.7, (Hash(s0 + 32.0) - 0.5) * 0.7));
                float3 c[3];
                float r[3];
                [unroll]
                for (int l = 0; l < 3; l++)
                {
                    float s = s0 + l * 11.7;
                    c[l] = axis * ((l - 1.0) * 0.38) + (float3(Hash(s + 1.0), Hash(s + 2.0), Hash(s + 3.0)) - 0.5) * 0.3;
                    r[l] = 0.3 + 0.15 * Hash(s + 4.0) + (l == 1 ? 0.1 : 0.0);
                }
                m.c0 = c[0]; m.c1 = c[1]; m.c2 = c[2];
                m.r = float3(r[0], r[1], r[2]);
                return m;
            }

            // 1 deep inside, 0 at the edge, below 0 outside; never past the bound.
            float Body(Lumps m, float3 p)
            {
                float env = max(1.0 - length(p - m.c0) / m.r.x,
                            max(1.0 - length(p - m.c1) / m.r.y, 1.0 - length(p - m.c2) / m.r.z));
                return min(env, (0.95 - length(p)) * 3.0);
            }

            // The body pushed in and out by the billows: above 0 there is gas, more the higher.
            float Coarse(Lumps m, float3 p, float3 flow)
            {
                float n = tex3Dlod(_Noise3D, float4(p * 0.8 + flow, 0.0)).r;
                return Body(m, p) * 1.25 + (n - 0.5) * 2.1 - 0.1;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 o = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                float3 d = normalize(i.obj - o);

                // The bound: a sphere of radius 1, cut in closed form.
                float b = dot(o, d);
                float c = dot(o, o) - 1.0;
                float disc = b * b - c;
                if (disc <= 0.0) return 0;
                float sq = sqrt(disc);
                float tNear = max(-b - sq, 0.0), tFar = -b + sq;
                if (tFar <= tNear) return 0;

                const int Samples = 10;
                // Interleaved gradient noise: the steps' start differs pixel to pixel in a fine
                // even pattern, so ten steps show as a soft grain, not as shells.
                float jitter = frac(52.9829189 * frac(dot(i.pos.xy, float2(0.06711056, 0.00583715))));
                float dt = (tFar - tNear) / Samples;

                Lumps lumps = MakeLumps();
                float3 drift = float3(0.3, 0.1, -0.2) * _Time.y * _Flow;
                float3 flow = drift + _Seed * 0.37;
                float3 flowFine = drift * 1.7 + _Seed * 0.61 + 3.1;

                float3 L = normalize(_TowardLight.xyz + float3(0.0, 1e-4, 0.0));
                float toward = saturate(dot(d, L));
                float phase = 0.35 + 1.6 * toward * toward * toward;
                float edge = 0.4 + 0.8 * _Softness;

                float through = 1.0;        // how much still comes through from behind
                float3 light = 0.0;         // what the gas itself sends

                [unroll]
                for (int k = 0; k < Samples; k++)
                {
                    float t = tNear + (k + jitter) * dt;
                    float3 p = o + d * t;

                    float e = Coarse(lumps, p, flow);
                    float fine = tex3Dlod(_Noise3D, float4(p * 1.9 + flowFine, 0.0)).g;
                    float gas = smoothstep(0.0, edge, e - (1.0 - fine) * 0.5 * (1.0 - smoothstep(0.4, 1.2, e)));

                    // Near the camera the gas is left out, so from inside it is a veil, not a wall.
                    float seen = smoothstep(_Resolve.x, _Resolve.y, t);
                    float a = 1.0 - exp(-gas * seen * dt * _Opacity * 4.4);

                    // Its shadow: how much gas a short step towards the light.
                    float sun = exp(-_Shadow * smoothstep(-0.1, 0.9, Coarse(lumps, p + L * 0.16, flow)));
                    float thick = 1.0 - exp(-gas * 3.0);

                    float3 col = lerp(_ShadowColor.rgb, _LitColor.rgb, sun)
                               * (0.55 + 0.45 * thick) * (0.75 + 0.5 * fine) * (1.0 - 0.35 * _DustAmount * (1.0 - fine));
                    col += _RimColor.rgb * (_Scatter * phase * sun * (1.0 - gas));

                    light += through * a * col;
                    through *= 1.0 - a;
                }

                // Never bright enough to bloom: it is cold gas.
                float fade = _Color.a * _Appear;
                return fixed4(min(light, 0.95) * _Color.rgb * fade, (1.0 - through) * fade);
            }
            ENDCG
        }
    }

    FallBack Off
}
