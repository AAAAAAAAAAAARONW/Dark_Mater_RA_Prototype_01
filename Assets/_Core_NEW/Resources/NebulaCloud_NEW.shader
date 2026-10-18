Shader "Custom/NebulaCloud_NEW"
{
    // A cloud of hydrogen gas (NebulaCloudVFX_NEW), as the scientists asked for one: not a
    // ball, a translucent, reddish-purple nebula like the Orion Nebula — gas glowing red and
    // pink where the stars born inside it light it up, violet and blue further out, torn by
    // dark lanes of dust, with a few bright stars in its hollow heart; and see-through, so what
    // is behind it (the web's threads, the stars) shows through.
    //
    // NOT ROUND. The body is a few lumps grown together where the seed puts them, each pushed
    // about by the big noise so none is a ball, and where the body thins only the noise's
    // thicker parts and the ridged filaments pass — so the edge is torn into wisps and the
    // outline is a different ragged shape from every side. The sphere it is drawn in is only
    // the bound; the gas never reaches it.
    //
    // CHEAP. One ray a pixel through the cloud's sphere, a dozen samples, each eight reads of
    // the shared noise texture (QuasarNoise_NEW), blended between two slabs for a
    // three-dimensional density. Drawn from the sphere's back faces, so it works from inside
    // too: the light flies through these, and the camera with it. Near the camera the gas is
    // left out (_Resolve), so from inside it is a glow all round and a tint on what is
    // beyond, not a flat wall of colour.
    //
    // Premultiplied over what is behind. _Color fades it. In Resources so a build has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _Noise ("Noise (QuasarNoise_NEW)", 2D) = "gray" {}
        _Seed ("Seed", Float) = 0
        _Density ("Density", Range(0, 2)) = 0.85
        _Softness ("How far the wisps reach out past the body's lumps", Range(0.1, 1)) = 0.5
        _Hollow ("How hollow the middle is, round the stars", Range(0, 1)) = 0.6
        _DustAmount ("Dust lanes", Range(0, 2)) = 0.9
        _Brightness ("Brightness", Range(0, 4)) = 1
        _Absorb ("How much the gas hides what is behind it", Range(0, 4)) = 0.8
        _Flow ("Drift, radii a second", Float) = 0.004
        _Resolve ("Nearer than x the gas is left out, from y all there (of the radius)", Vector) = (0.05, 0.3, 0, 0)
        _Stars ("How many stars inside (0 to 4) and how bright", Vector) = (3, 1, 0, 0)
        [HDR] _Core ("Gas where the stars light it", Color) = (1.5, 0.4, 0.45, 1)
        [HDR] _Mid ("Gas further out", Color) = (0.6, 0.3, 0.85, 1)
        [HDR] _Rim ("The faint outer gas", Color) = (0.25, 0.3, 0.8, 1)
        _DustColor ("Dust", Color) = (0.06, 0.025, 0.03, 1)
        [HDR] _StarColor ("The stars inside", Color) = (3, 3.2, 4, 1)
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

            sampler2D _Noise;
            fixed4 _Color;
            float _Seed, _Density, _Softness, _Hollow, _DustAmount, _Brightness, _Absorb, _Flow;
            float4 _Resolve, _Stars;
            float4 _Core, _Mid, _Rim, _StarColor;
            fixed4 _DustColor;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 obj : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.obj = vertex.xyz;
                return o;
            }

            float Sq(float x) { return x * x; }
            float Hash(float n) { return frac(sin(n) * 43758.5453); }

            // Noise in three dimensions from the shared sheet: the sheet read at two slabs in
            // z, each shifted by its own hash, and the two blended — so there is no plane the
            // noise is constant along, and two reads give all four channels (R, G soft; B
            // ridged; A fine).
            float2 Slab(float z) { return float2(Hash(z * 1.7 + 0.3), Hash(z * 2.3 + 0.9)); }
            float4 Noise4(float3 q)
            {
                float z = q.z * 6.0;
                float zi = floor(z), f = z - zi;
                f = f * f * (3.0 - 2.0 * f);
                float4 a = tex2Dlod(_Noise, float4(q.xy + Slab(zi), 0, 0));
                float4 b = tex2Dlod(_Noise, float4(q.xy + Slab(zi + 1.0), 0, 0));
                return lerp(a, b, f);
            }

            // The cloud's body: not a sphere but a streak — six lumps of different sizes
            // strung along one axis (the object's x, tipped by the seed), each knocked off
            // the line a little, thicker in the middle, thinner or fatter across as the seed
            // has it. 1 deep inside a lump, 0 at its edge, less outside; and never past the
            // sphere the cloud is drawn in. Turn the object to turn the streak.
            float Body(float3 p)
            {
                float s0 = _Seed * 3.3;
                float3 axis = normalize(float3(1.0, (Hash(s0 + 31.0) - 0.5) * 0.8, (Hash(s0 + 32.0) - 0.5) * 0.8));
                float across = 0.8 + 0.5 * Hash(s0 + 33.0);
                float env = -1.0;
                [unroll]
                for (int l = 0; l < 6; l++)
                {
                    float s = s0 + l * 11.7;
                    float along = (l / 5.0 * 2.0 - 1.0) * 0.45 * (0.7 + 0.3 * Hash(s + 5.0));
                    float3 off = (float3(Hash(s + 1.0), Hash(s + 2.0), Hash(s + 3.0)) - 0.5) * 0.36;
                    float3 c = axis * along + off - axis * dot(off, axis) * 0.5;
                    float r = 0.2 + 0.2 * Hash(s + 4.0) + 0.14 * (1.0 - abs(along) / 0.45);
                    float3 d = p - c;
                    float a = dot(d, axis);
                    env = max(env, 1.0 - length(axis * a + (d - axis * a) * across) / r);
                }
                return min(env, (0.95 - length(p)) * 3.0);
            }

            // The star i inside the cloud: where, and how bright. A few, off centre, in its hollow.
            float3 Star(int i, out float bright)
            {
                float s = _Seed * 5.1 + i * 7.7;
                float3 p = float3(Hash(s + 1.0), Hash(s + 2.0), Hash(s + 3.0)) * 2.0 - 1.0;
                p *= 0.28 * (0.4 + 0.6 * Hash(s + 4.0));
                bright = (0.5 + 0.8 * Hash(s + 5.0)) * saturate(_Stars.x - i) * _Stars.y;
                return p;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 o = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                float3 d = normalize(i.obj - o);

                // The sphere, radius 1.
                float b = dot(o, d);
                float c = dot(o, o) - 1.0;
                float disc = b * b - c;
                if (disc <= 0.0) return 0;
                float sq = sqrt(disc);
                float tNear = max(-b - sq, 0.0), tFar = -b + sq;
                if (tFar <= tNear) return 0;

                // The stars, once.
                float3 starPos[4];
                float starBright[4];
                [unroll]
                for (int si = 0; si < 4; si++) starPos[si] = Star(si, starBright[si]);

                float3 flow = float3(0.3, 0.1, -0.2) * _Time.y * _Flow + _Seed * 0.37;
                const int Samples = 12;
                float jitter = frac(sin(dot(i.pos.xy, float2(12.9898, 78.233))) * 43758.5453);
                float dt = (tFar - tNear) / Samples;
                float T = 1.0;
                float3 light = 0.0;

                [unroll]
                for (int k = 0; k < Samples; k++)
                {
                    float t = tNear + (k + jitter) * dt;
                    float3 p = o + d * t;
                    float R = length(p);

                    // The gas: noise at three scales. The big one also pushes the body's lumps
                    // about (the warp), so no lump is round; and the thinner the body, the
                    // more of the noise is cut away — only its thicker parts and the ridged
                    // filaments pass at the edge — so the edge is wisps, not a soft ball.
                    // Hollow in the middle, round the stars.
                    float4 n0 = Noise4(p * 0.6 + flow * 0.5 + 9.0);
                    float4 n1 = Noise4(p * 1.1 + flow);
                    float4 n2 = Noise4(p * 2.6 + flow * 1.5 + 3.1);
                    float4 n3 = Noise4(p * 5.5 + 7.7);
                    float3 warp = (n0.gba - 0.5) * 0.6;
                    float env = Body(p + warp);
                    float thin = saturate(1.0 - env);
                    float n = n1.r * 0.45 + n2.r * 0.3 + n3.a * 0.2 + (n0.r - 0.5) * 0.3 + (n1.b - 0.5) * 0.35 * thin;
                    float thr = 0.28 + (0.3 + 0.35 * _Softness) * thin + 0.2 * saturate(-env * 2.0) - 0.1 * _Density;
                    float hollow = lerp(1.0, smoothstep(0.06, 0.4, R), _Hollow);
                    float gas = saturate((n - thr) * 6.0) * smoothstep(-0.5, 0.1, env) * hollow;

                    // Dust: torn lanes through the gas (the sheet's blue channel is ridged).
                    float dust = smoothstep(0.6, 0.8, n2.b) * saturate(env + 0.3) * _DustAmount * smoothstep(0.08, 0.3, R);

                    // The stars' light on the gas: red-pink close to them, violet, then blue.
                    float lit = 0.0;
                    float cores = 0.0;
                    [unroll]
                    for (int s = 0; s < 4; s++)
                    {
                        float3 ds = p - starPos[s];
                        float d2 = dot(ds, ds);
                        lit += starBright[s] * 0.05 / (0.03 + d2);
                        cores += starBright[s] * exp(-d2 * 900.0);
                    }
                    float3 colour = lerp(_Rim.rgb, _Mid.rgb, saturate(lit * 2.0));
                    colour = lerp(colour, _Core.rgb, saturate(lit * 0.9 - 0.15));

                    // Near the camera the gas is left out: from inside, a glow all round.
                    float seen = smoothstep(_Resolve.x, _Resolve.y, t);

                    float3 emit = (colour * gas * (0.45 + 0.8 * lit) * _Density + _StarColor.rgb * cores * 2.0) * seen;
                    float absorb = (gas * _Absorb * _Density + dust * 2.5) * seen;
                    light += T * emit * dt;
                    light += T * _DustColor.rgb * dust * 0.3 * dt * seen;
                    T *= exp(-absorb * dt);
                }

                float alpha = 1.0 - T;
                return fixed4(light * _Brightness * _Color.rgb, alpha * _Color.a);
            }
            ENDCG
        }
    }

    FallBack Off
}
