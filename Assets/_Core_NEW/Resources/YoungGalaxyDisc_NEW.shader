Shader "Custom/YoungGalaxyDisc_NEW"
{
    // The young galaxy's gas and young stars as a sheet (YoungGalaxyVFX_NEW; its shape is in
    // YoungGalaxy_NEW.cginc). A galaxy at cosmic noon: no arms —
    //
    //   gas      a thick, turbulent disc, violet-blue, broken into patches, with a lumpy edge
    //   clumps   a dozen giant knots of star formation: blue-white cores in a magenta glow of
    //            ionised hydrogen, and a scatter of smaller knots through the gas
    //   core     small and warm, orange, half hidden by its own dust
    //   dust     everywhere, in patches and torn lanes, not along any arm
    //   streams  cold gas falling in along narrow filaments from outside, faint and blue-green
    //
    // The clumps go round on their orbits; the gas streams round through the patches (read
    // twice half a cycle apart and crossfaded, so it never winds up); the streams flow in.
    // Seen at a slant the light builds up and the dust closes, as through more of the layer.
    //
    // Premultiplied over what is behind, queue 2990: after its far half of glow (2989),
    // before its stars (2992). _Color fades it. In Resources so a build has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _Noise ("Noise (QuasarNoise_NEW)", 2D) = "gray" {}
        _Seed ("Seed", Float) = 0
        _ClumpTurn ("Clump turn, radians a second at radius 0.5", Float) = 0.01
        _Thickness ("Disc scale height at the centre, and its growth outward", Vector) = (0.06, 0.07, 0, 0)
        _Streams ("Cold streams, 0 to 3", Float) = 2
        _ScaleLength ("Disc scale length (of the radius)", Float) = 0.3
        _Dust ("Dust", Range(0, 2)) = 1.1
        _Clumps ("Giant clumps", Range(0, 3)) = 1.2
        _Knots ("Smaller knots of star formation through the gas", Range(0, 3)) = 1
        _Haze ("The gas between the clumps", Range(0, 1)) = 0.7
        _Brightness ("Brightness", Range(0, 3)) = 0.6
        _Flow ("Gas flow along the orbits, radii a second", Float) = 0.01
        _Twinkle ("Clumps brightening and dimming", Range(0, 1)) = 0.4
        [HDR] _Core ("Core", Color) = (1.5, 0.85, 0.5, 1)
        [HDR] _Gas ("Gas", Color) = (0.45, 0.5, 1.0, 1)
        [HDR] _Clump ("Clump cores", Color) = (1.5, 1.75, 2.4, 1)
        [HDR] _Glow ("Ionised gas round the clumps", Color) = (1.7, 0.55, 0.85, 1)
        [HDR] _Stream ("Cold streams", Color) = (0.5, 0.85, 0.95, 1)
        _DustColor ("Dust", Color) = (0.05, 0.03, 0.06, 1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
            #include "YoungGalaxy_NEW.cginc"

            sampler2D _Noise;
            fixed4 _Color;
            float _ScaleLength, _Dust, _Clumps, _Knots, _Haze, _Brightness, _Flow, _Twinkle;
            float4 _Core, _Gas, _Clump, _Glow, _Stream;
            fixed4 _DustColor;

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 plane : TEXCOORD0;
                float3 local : TEXCOORD1;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.plane = vertex.xz;
                o.local = vertex.xyz;
                return o;
            }

            float4 Look(float2 uv, float2 dx, float2 dy) { return tex2Dgrad(_Noise, uv, dx, dy); }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.plane;
                float2 dpx = ddx(p), dpy = ddy(p);
                float r = max(length(p), 1e-4);

                // Angle and its change across the pixel, from the plane's (no seam at ±180°).
                float az = atan2(p.y, p.x);
                float2 dAz = float2(p.x * dpx.y - p.y * dpx.x, p.x * dpy.y - p.y * dpy.x) / (r * r);
                float2 dLnr = float2(dot(p, dpx), dot(p, dpy)) / (r * r);
                float lnr = log(r);

                // The gas: log-polar noise at two scales, streaming round on a flat rotation
                // curve (faster inside), read twice half a cycle apart and crossfaded.
                const float cycle = 30.0;
                float ph = frac(_Time.y / cycle);
                float drift = _Flow / max(r, 0.05) * cycle;
                float4 n = 0, m = 0;
                [unroll]
                for (int k = 0; k < 2; k++)
                {
                    float phase = k == 0 ? ph : frac(ph + 0.5);
                    float w = 1.0 - abs(phase * 2.0 - 1.0);
                    float azF = az - drift * phase + k * 2.3 + _Seed;
                    float2 uv = float2(azF / YG_TAU * 4.0 + lnr * 0.5, lnr * 1.8 - azF / YG_TAU * 0.7);
                    float2 udx = float2(dAz.x / YG_TAU * 4.0 + dLnr.x * 0.5, dLnr.x * 1.8 - dAz.x / YG_TAU * 0.7);
                    float2 udy = float2(dAz.y / YG_TAU * 4.0 + dLnr.y * 0.5, dLnr.y * 1.8 - dAz.y / YG_TAU * 0.7);
                    n += Look(uv, udx, udy) * w;
                    m += Look(uv * 2.7 + float2(0.71, 0.23), udx * 2.7, udy * 2.7) * w;
                }
                float4 ng = Look(p * 6.0 + float2(0.37, 0.91) + _Seed * 0.1, dpx * 6.0, dpy * 6.0);
                float4 nc = Look(p * 1.5 + float2(0.83, 0.41) + _Seed * 0.1, dpx * 1.5, dpy * 1.5);

                // The disc: exponential, torn into patches, with a lumpy edge.
                float edge = smoothstep(1.05, 0.7, r + (nc.r - 0.5) * 0.5);
                float patches = 0.3 + 1.4 * n.r * (0.5 + 0.8 * m.r);
                float disc = exp(-r / _ScaleLength) * edge;
                float gas = disc * patches;

                // The giant clumps, each on its own slow clock, and smaller knots where the gas
                // is thickest.
                float cores, glow;
                YGClumps(float3(p.x, 0.0, p.y), 0.45, cores, glow);
                float beat = lerp(1.0, 0.65 + 0.7 * (0.5 + 0.5 * sin(_Time.y * (0.1 + 0.25 * nc.g) + nc.b * 25.0)), _Twinkle);
                cores *= _Clumps * beat;
                glow *= _Clumps * (0.6 + 0.4 * beat);
                float knots = smoothstep(0.6, 0.85, ng.r) * smoothstep(0.45, 0.75, m.g) * saturate(gas * 2.0) * _Knots;

                // The core: small, warm, dusty.
                float core = exp(-r / 0.07) * 0.9 + 0.2 * exp(-YGSq(r / 0.18));

                // The streams, faint, out past the edge.
                float streams = 0.0;
                [unroll]
                for (int s = 0; s < 3; s++) streams += YGStream(float3(p.x, 0.0, p.y), s);
                streams *= (0.15 + 0.85 * smoothstep(0.3, 0.75, m.a)) * (0.5 + 0.5 * n.g);

                // Dust: patches everywhere, torn lanes (ridged noise), thickest in the middle
                // where the core hides behind it.
                float lanes = smoothstep(0.6, 0.85, n.b) * 0.6 + smoothstep(0.55, 0.8, m.b) * 0.35;
                float dust = saturate((lanes * edge * smoothstep(0.02, 0.12, r) + smoothstep(0.65, 0.9, ng.g) * 0.2 * edge
                                       + exp(-r / 0.1) * 0.6 * smoothstep(0.6, 0.3, nc.a)) * _Dust);

                float3 light = _Core.rgb * core * 1.2
                             + lerp(_Gas.rgb, _Core.rgb, exp(-r / 0.15)) * gas * _Haze * 2.2
                             + _Clump.rgb * cores * 1.1
                             + _Glow.rgb * (glow * 0.22 + knots * 0.8)
                             + _Clump.rgb * knots * 0.35
                             + _Stream.rgb * streams * 0.3;
                light *= 0.8 + 0.4 * ng.a;   // grain

                // Seen at a slant, through more of the layer: the light builds up and the dust
                // closes — gently, and never to a hard line along the far rim.
                float3 camera = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                float3 toCamera = camera - i.local;
                float slant = min(length(toCamera) / max(abs(toCamera.y), 1e-4), 8.0) / 1.84;
                float through = pow(1.0 - saturate(dust * 0.85), slant);
                float hide = 1.0 - through;
                light *= sqrt(min(slant, 2.0));
                float3 premultiplied = (light * through + _DustColor.rgb * hide) * _Brightness;
                float alpha = hide * 0.8;

                // From inside the layer there is no sheet to see: the clumps and the dust are all
                // round, and the glow of the volume (YoungGalaxyVolume_NEW) is what is there. So
                // it goes as the camera comes within a few of the layer's heights of its middle.
                float camR = length(camera.xz);
                float layer = YGHeight(camR);
                float within = smoothstep(layer * 1.5, layer * 5.0, abs(camera.y)) + smoothstep(1.8, 2.1, camR);
                premultiplied *= saturate(within);
                alpha *= saturate(within);

                float inside = step(r, 1.95);
                return fixed4(premultiplied * _Color.rgb * inside, alpha * _Color.a * inside);
            }
            ENDCG
        }
    }

    FallBack Off
}
