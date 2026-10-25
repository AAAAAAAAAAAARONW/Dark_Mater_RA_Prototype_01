Shader "Custom/CosmicWebGas_NEW"
{
    // The cosmic web's gas as soft puffs (CosmicWebGas_NEW builds the mesh): thousands of
    // camera-facing blobs scattered over the web's own thread meshes, each a soft ball broken
    // up by a drifting 3D noise, overlapping into threads of gas. Seen from anywhere a thread
    // is a soft glow, thick in its middle and thinning out at its edges and its tips, never a
    // surface with a rim — which is what a thin shell of mesh shaded on its own surface could
    // not be.
    //
    // COLOURED LIKE A DENSITY MAP, GLOWING. The look is a map of the web's gas density (the
    // reference): violet threads, lilac where they thicken, peach where they crowd into a
    // node. Each puff takes its colour from that ramp by how dense the gas round it is (baked
    // into it) and glows: it adds its light, as a thin gas would, and hides only a little of
    // what is behind it (_Occlude of its cover). Where few puffs overlap that is a translucent,
    // emissive glow; where many do, the hiding catches up with the adding and the colour
    // settles at the ramp's own times _Brightness / _Occlude, enough to bloom a little,
    // instead of climbing on. Only added (as this first did), overlapping violet ran through magenta to
    // white and the camera's bloom spread that white into a milky haze over the web; only
    // painted over (_Occlude 1), the gas reads opaque, like candy.
    //
    // CHEAP. A puff is one quad; the threads need a few thousand to tens of thousands where the
    // meshes they replace had millions of triangles. Each pixel of a puff is one exp and one
    // read of a small tiling 3D noise. Not sorted: the puffs of one place are all about the
    // same colour, so the order they are painted in hardly shows.
    //
    // Each quad carries its own size, density and brightness (TEXCOORD1: size, density,
    // brightness, seed), baked from the mesh (CosmicWebGas/Tools~/bake_web_gas.py): most small,
    // a thread's own core, a few big and faint, the glow round it. Never smaller than
    // _MinPixels (covering less, as it is drawn bigger than it is, so far gas is smooth, not
    // grainy) and never bigger than _MaxSize of the screen, so one passing the camera is a
    // soft haze, not a wall; and it thins out nearer than _CameraFade.
    //
    // Fades with its world: WorldSwitcher_NEW scales _Opacity and _Emission, and the dark
    // matter toggle (DarkMatterToggleTest_NEW) scales _Density, as for the baked volumes.

    Properties
    {
        _Opacity ("Opacity (WorldSwitcher_NEW fades this)", Range(0, 1)) = 1
        _Emission ("Glow (WorldSwitcher_NEW scales this)", Range(0, 6)) = 1
        _Density ("Density (the dark matter toggle fades this)", Range(0, 6)) = 1
        _Brightness ("Brightness", Range(0, 4)) = 0.95
        _Cover ("How much light one puff gives: more, thicker gas", Range(0, 1)) = 0.12
        _Occlude ("How much of what is behind a puff hides: 0 only glows, 1 paints over", Range(0, 1)) = 0.45
        _Size ("Puff radius, in the mesh's own units", Float) = 0.0009
        _MaxSize ("Largest a puff is drawn, of half the screen's height", Float) = 0.07
        _MinPixels ("Smallest a puff is drawn, in pixels across its radius: gas stays smooth far off", Float) = 3.5
        _CameraFade ("Thins out nearer the camera than x, all there from y (world units)", Vector) = (10, 50, 0, 0)
        [NoScaleOffset] _Noise ("Tiling 3D noise (WebNebulaNoise_NEW)", 3D) = "" {}
        _NoiseScale ("Noise tiles per world unit", Float) = 0.04
        _Drift ("Drift, world units a second", Vector) = (0.4, 0.15, 0.25, 0)
        _Breakup ("How much the noise breaks each puff up", Range(0, 1)) = 0.5
        [HDR] _ColorA ("Thin gas: dim violet", Color) = (0.2, 0.13, 0.38, 1)
        [HDR] _ColorB ("Threads: violet", Color) = (0.42, 0.24, 0.7, 1)
        [HDR] _ColorC ("Thicker: lilac", Color) = (0.66, 0.34, 0.72, 1)
        [HDR] _ColorD ("Densest: peach", Color) = (0.95, 0.62, 0.48, 1)
        _Ramp ("Where violet, lilac and peach are reached along the density, 0 to 1", Vector) = (0.45, 0.82, 0.98, 0)
        _Vary ("How much each puff's own brightness counts", Range(0, 1)) = 0.5
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

            sampler3D _Noise;
            float _Opacity, _Emission, _Density, _Brightness, _Cover, _Size, _MaxSize, _MinPixels, _NoiseScale, _Breakup, _Vary, _Occlude;
            float4 _CameraFade, _Drift, _ColorA, _ColorB, _ColorC, _ColorD, _Ramp;

            struct appdata
            {
                float4 vertex : POSITION;    // the puff's middle, in the mesh's own space
                float2 corner : TEXCOORD0;   // -1..1
                float4 data   : TEXCOORD1;   // size, density, brightness, seed (0..1 each but size)
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float2 corner : TEXCOORD0;
                float4 colour : TEXCOORD1;   // light, and how much it hides; every fade in it
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

                // Its colour, by how dense the gas round it is: dim violet, violet, lilac, peach.
                float dense = v.data.y;
                float3 colour = lerp(_ColorA.rgb, _ColorB.rgb, smoothstep(0.0, _Ramp.x, dense));
                colour = lerp(colour, _ColorC.rgb, smoothstep(_Ramp.x, _Ramp.y, dense));
                colour = lerp(colour, _ColorD.rgb, smoothstep(_Ramp.y, _Ramp.z, dense));

                // How much light it gives, and hides a share of. A puff drawn bigger than it is
                // (too far to be a few pixels) gives less; one too near thins out.
                float far = min(1.0, radius / drawn);
                float near = smoothstep(_CameraFade.x, _CameraFade.y, depth);
                float cover = _Cover * lerp(1.0, v.data.z, _Vary) * far * near * saturate(_Density) * _Opacity;
                o.colour = float4(colour * (_Brightness * _Emission), _Occlude) * cover;

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
                puff *= lerp(1.0, smoothstep(0.25, 0.8, n) * 1.4, _Breakup);
                return i.colour * saturate(puff);
            }
            ENDCG
        }
    }

    FallBack Off
}
