Shader "Custom/CosmicWebNebula_NEW"
{
    // The cosmic web's gas as nebula, drawn on the web's own filament meshes (BIG): the threads
    // the light flies through read as clouds of hydrogen (LARC, 14 October, asked for gas met
    // as a translucent cloud) — in the web's own purple, as the purple threads were: blue-violet
    // and thin at a thread's edges, purple through it, lilac where it is thick and lit, broken
    // into clumps, torn by dark lanes of dust, slowly drifting. The yellow threads (Core,
    // Coreyellow) keep their own material: they are the galaxies' light.
    //
    // CHEAP. No ray march. The filament mesh is the gas's shape, so each pixel of it is shaded
    // once: how squarely the eye looks through the thread there (its soft edge), one read of
    // the web's own bake for where the gas is thick, and two reads of a small tiling 3D noise
    // for clumps, lanes and which gas is lit. Three texture reads and no loop, where the baked
    // volume it replaces (Custom/PreBakedCloudVolume) took 16 to 32 reads in a loop on every
    // layer of the mesh under every pixel. Front and back faces both draw (a thread seen
    // through is two layers of gas), so where threads cross the light builds up.
    //
    // Up close the gas thins out, so flying through a thread is a glow round the light, not a
    // wall; the noise's finer octave gives way to its mean where it would get smaller than a
    // pixel, so far threads stay smooth instead of sparkling.
    //
    // Premultiplied over what is behind. Fades with its world: WorldSwitcher_NEW scales
    // _Opacity (alpha) and _Emission (glow), the two floats the baked volumes have too.

    Properties
    {
        [NoScaleOffset] _BakedTex ("The web's bake (where the gas is thick)", 3D) = "" {}
        [NoScaleOffset] _Noise ("Tiling 3D noise (WebNebulaNoise_NEW)", 3D) = "" {}
        _NoiseScale ("Noise tiles per world unit", Float) = 0.025
        _Drift ("Drift, world units a second", Vector) = (0.4, 0.15, 0.25, 0)
        _Opacity ("Opacity (WorldSwitcher_NEW fades this)", Range(0, 1)) = 0.85
        _Emission ("Glow (WorldSwitcher_NEW scales this)", Range(0, 6)) = 1.3
        _Brightness ("Brightness", Range(0, 4)) = 1
        _Edge ("Edge softness: higher, the threads' edges fade sooner", Range(0.3, 4)) = 2.2
        _Absorb ("How much the gas hides what is behind it", Range(0, 1)) = 0.25
        _Dust ("Dark lanes", Range(0, 2)) = 1
        _Baked ("How much the web's own bake shapes it", Range(0, 2)) = 1
        _CameraFade ("Thins out nearer the camera than x, all there from y (world units)", Vector) = (1.5, 10, 0, 0)
        [HDR] _Lit ("Thick gas, lit: lilac", Color) = (0.78, 0.46, 0.95, 1)
        [HDR] _Mid ("Through a thread: the web's purple", Color) = (0.56, 0.4, 0.8, 1)
        [HDR] _Rim ("Thin, at the edges: blue-violet", Color) = (0.36, 0.3, 0.74, 1)
        _DustColor ("Dust", Color) = (0.05, 0.02, 0.03, 1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent-50" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler3D _BakedTex, _Noise;
            float _NoiseScale, _Opacity, _Emission, _Brightness, _Edge, _Absorb, _Dust, _Baked;
            float4 _Drift, _CameraFade;
            float4 _Lit, _Mid, _Rim;
            fixed4 _DustColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float3 world  : TEXCOORD0;
                float3 obj    : TEXCOORD1;
                float3 normal : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.obj = v.vertex.xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 toCamera = _WorldSpaceCameraPos - i.world;
                float distance = length(toCamera);
                float3 view = toCamera / max(distance, 1e-4);

                // How squarely the eye looks through the thread here: 1 through its middle, 0
                // at its edge. The mesh's normal, or the face's own if it came without one.
                float3 n = i.normal;
                float nn = dot(n, n);
                n = nn > 1e-6 ? n * rsqrt(nn) : normalize(cross(ddy(i.world), ddx(i.world)));
                float body = pow(abs(dot(n, view)), _Edge);

                // Where the gas is thick: the web's own bake, smooth at every distance.
                float thick = saturate(tex3D(_BakedTex, i.obj + 0.5).a * 8.0 * _Baked);

                // Clumps, lanes and which gas is lit: two octaves of the tiling noise, drifting.
                float3 q = (i.world + _Drift.xyz * _Time.y) * _NoiseScale;
                float footprint = length(fwidth(q)) * 32.0 * 2.71;     // the finer octave's texels a pixel
                float fine = saturate(2.0 - footprint);
                float4 n1 = tex3D(_Noise, q);
                float4 n2 = lerp(float4(0.5, 0.5, 0.5, 0.5), tex3D(_Noise, q * 2.71 + 0.37), fine);
                float gas = n1.r * 0.65 + n2.r * 0.35;
                float lanes = n1.g * 0.55 + n2.g * 0.45;
                float warm = n1.b * 0.7 + n2.a * 0.3;

                // How much gas the eye looks through: broken into clumps with thin gaps between,
                // so a thread is a ragged cloud, not a tube.
                float amount = body * (0.25 + 0.75 * thick) * (0.25 + 0.75 * smoothstep(0.3, 0.75, gas));

                // Blue-violet and thin at the edges, purple through, lilac where thick and lit.
                float3 colour = lerp(_Rim.rgb, _Mid.rgb, smoothstep(0.1, 0.55, body * (0.6 + gas)));
                float lit = smoothstep(0.6, 0.9, warm * 0.6 + thick * 0.4 + gas * 0.3);
                colour = lerp(colour, _Lit.rgb, lit * smoothstep(0.25, 0.75, body));

                // Dark lanes of dust, through the middle of a thread rather than its edge.
                float dust = smoothstep(0.62, 0.85, lanes) * smoothstep(0.2, 0.7, body) * _Dust;

                float near = smoothstep(_CameraFade.x, _CameraFade.y, distance);
                float3 light = colour * amount * (1.0 - 0.85 * saturate(dust)) * _Emission * _Brightness * 0.13;
                float hide = saturate(amount * _Absorb + dust * 0.45);
                float k = near * _Opacity;
                return fixed4((light + _DustColor.rgb * dust * 0.45) * k, hide * k);
            }
            ENDCG
        }
    }

    FallBack Off
}
