Shader "Custom/CosmicWebGas_NEW"
{
    // The cosmic web's gas as soft glowing puffs (CosmicWebGas_NEW builds the mesh): thousands
    // of camera-facing blobs scattered over the web's own thread meshes, each a soft ball of
    // light broken up by a drifting 3D noise, overlapping into threads of glowing hydrogen. Seen
    // from anywhere a thread is a soft glow, thick in its middle and thinning out at its edges
    // and its tips, never a surface with a rim — which is what a thin shell of mesh shaded on
    // its own surface could not be.
    //
    // CHEAP. A puff is one quad; the threads need a few thousand to tens of thousands where the
    // meshes they replace had millions of triangles. Each pixel of a puff is one exp and one
    // read of a small tiling 3D noise. Additive, so the puffs need no sorting.
    //
    // Each quad carries its own size, colour and brightness (TEXCOORD1: size, hue, brightness,
    // seed), baked from the mesh (CosmicWebGas/Tools~/bake_web_gas.py): most small and bright,
    // a thread's own core, a few big and faint, the glow round it. Never smaller than
    // _MinPixels (the light it would have spread is kept, so far gas is smooth, not grainy)
    // and never bigger than
    // _MaxSize of the screen, so one passing the camera is a soft haze, not a wall; and it
    // thins out nearer than _CameraFade.
    //
    // Fades with its world: WorldSwitcher_NEW scales _Opacity and _Emission, and the dark
    // matter toggle (DarkMatterToggleTest_NEW) scales _Density, as for the baked volumes.

    Properties
    {
        _Opacity ("Opacity (WorldSwitcher_NEW fades this)", Range(0, 1)) = 1
        _Emission ("Glow (WorldSwitcher_NEW scales this)", Range(0, 6)) = 1
        _Density ("Density (the dark matter toggle fades this)", Range(0, 6)) = 1
        _Brightness ("Brightness", Range(0, 4)) = 1
        _Size ("Puff radius, in the mesh's own units", Float) = 0.0012
        _MaxSize ("Largest a puff is drawn, of half the screen's height", Float) = 0.3
        _MinPixels ("Smallest a puff is drawn, in pixels across its radius: gas stays smooth far off", Float) = 3.5
        _CameraFade ("Thins out nearer the camera than x, all there from y (world units)", Vector) = (2, 14, 0, 0)
        [NoScaleOffset] _Noise ("Tiling 3D noise (WebNebulaNoise_NEW)", 3D) = "" {}
        _NoiseScale ("Noise tiles per world unit", Float) = 0.04
        _Drift ("Drift, world units a second", Vector) = (0.4, 0.15, 0.25, 0)
        _Breakup ("How much the noise breaks each puff up", Range(0, 1)) = 0.75
        [HDR] _ColorA ("Gas: thin", Color) = (0.36, 0.3, 0.74, 1)
        [HDR] _ColorB ("Gas: most of it", Color) = (0.56, 0.4, 0.8, 1)
        [HDR] _ColorC ("Gas: thick and lit", Color) = (0.78, 0.46, 0.95, 1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent-50" "RenderType"="Transparent" "IgnoreProjector"="True" }
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

            sampler3D _Noise;
            float _Opacity, _Emission, _Density, _Brightness, _Size, _MaxSize, _MinPixels, _NoiseScale, _Breakup;
            float4 _CameraFade, _Drift, _ColorA, _ColorB, _ColorC;

            struct appdata
            {
                float4 vertex : POSITION;    // the puff's middle, in the mesh's own space
                float2 corner : TEXCOORD0;   // -1..1
                float4 data   : TEXCOORD1;   // size, hue, brightness, seed (0..1 each but size)
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float2 corner : TEXCOORD0;
                float3 colour : TEXCOORD1;   // with every fade in it
                float3 world  : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float scale = length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));
                float3 centre = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
                float3 view = mul(UNITY_MATRIX_V, float4(centre, 1.0)).xyz;
                float depth = -view.z;

                float radius = _Size * v.data.x * scale;
                float tanHalf = 1.0 / unity_CameraProjection._m11;
                float most = _MaxSize * tanHalf * max(depth, 1e-3);
                float pixel = 2.0 * tanHalf * max(depth, 1e-3) / _ScreenParams.y;
                float drawn = clamp(radius, _MinPixels * pixel, most);

                // Its colour: thin blue-violet, most of it the web's purple, the thick lit parts
                // lighter. A puff drawn smaller than it is (too close) or bigger (too far to be
                // two pixels) keeps the light it would have spread.
                float hue = v.data.y;
                float3 colour = lerp(_ColorA.rgb, _ColorB.rgb, smoothstep(0.0, 0.55, hue));
                colour = lerp(colour, _ColorC.rgb, smoothstep(0.55, 1.0, hue));
                float energy = min(1.0, (radius * radius) / (drawn * drawn));
                float near = smoothstep(_CameraFade.x, _CameraFade.y, depth);
                o.colour = colour * v.data.z * energy * near * _Brightness * _Density * _Emission * _Opacity * 0.1;

                view.xy += v.corner * drawn;
                if (depth < 1e-3) view = float3(0.0, 0.0, 1.0);
                o.pos = mul(UNITY_MATRIX_P, float4(view, 1.0));
                o.corner = v.corner;
                o.world = centre + (UNITY_MATRIX_V[0].xyz * v.corner.x + UNITY_MATRIX_V[1].xyz * v.corner.y) * drawn;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float q = dot(i.corner, i.corner);
                if (q >= 1.0) return 0;
                float puff = exp(-q * 3.2) * (1.0 - q);

                // Broken up by the drifting noise, so the overlapping puffs read as one ragged gas.
                float n = tex3D(_Noise, (i.world + _Drift.xyz * _Time.y) * _NoiseScale).r;
                puff *= lerp(1.0, smoothstep(0.25, 0.8, n) * 1.7, _Breakup);
                return fixed4(i.colour * puff, 0.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
