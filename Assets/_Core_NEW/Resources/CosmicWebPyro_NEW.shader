Shader "Custom/CosmicWebPyro_NEW"
{
    // The cosmic web's gas as a volume, like a smoke simulation's: billowing glowing gas along
    // the threads, soft and uneven inside, thinning out at its edges, dark voids between. A
    // short ray march, cheap enough to cover the screen.
    //
    // WHERE THE GAS IS. A 3D field of the web mesh's own shape (a WebGasField_*_NEW texture,
    // CosmicWebNebula/Tools~/bake_soft_edges.py, fine: 1 in the middle of a thread, falling to
    // 0 round it) says how much gas there is; a drifting tiling noise eats into it (_Erode, only
    // ever taking gas away, so it never spills past where the field ends) and shades it, bright
    // crowns and dim folds (_Billow).
    //
    // WHAT IS DRAWN. Not the web's own meshes: their marching-cubes skins are terraced and full
    // of tunnels and bubbles inside the thick of the gas, and every fold started a march of its
    // own, so the gas broke up into shards and rings. It is drawn on a smooth closed hull round
    // all the gas (a WebGasHull_*_NEW mesh, CosmicWebGas/Tools~/bake_gas_hull.py, cut from the
    // wider-blurred soft field, so it sits round the gas with room to spare), its far side only
    // (Cull Front): each pixel marches the gas from there back towards the camera, _Steps steps
    // over _Depth. Where hulls overlap the brightest wins (BlendOp Max): no sorting, and the
    // gas never adds up to white.
    //
    // FROM INSIDE. With the camera in the gas the hull is all round it and its far side folds
    // about, so rays side by side would start their marches in different places and the gas
    // near the camera would break up along the folds. So while the camera is in the gas (the
    // field where it is, past _Inside) the gas near it is marched first by _StepsInside steps
    // hung off the camera, the same for every ray (close near the camera, wider off, out to
    // _DepthInside), up to where the ray's own march takes over.
    //
    // COLOURED BY HEAT: violet where the gas is thin, magenta where it is thick, pink towards
    // the clusters' hearts (_Knot0-5, in the mesh's space, w their reach, as CosmicWebGas_NEW
    // has them). The galaxies along the threads are not this gas: they are glowing points of
    // their own (CosmicWebGas_NEW with WebGas_Stars_NEW). The colours are what the camera's
    // post-processing turns into the reference's: it cools everything (white balance -24.4),
    // so they are set warmer than they show.
    //
    // CHEAP. A hull of a few hundred thousand triangles (the meshes it replaces had millions);
    // per pixel it covers, two 3D texture reads a step, eight or so steps, plus a few more steps
    // only while the camera is in the gas. No extra passes, no render textures.
    //
    // Fades with its world: WorldSwitcher_NEW scales _Opacity and _Emission, and the dark
    // matter toggle (DarkMatterToggleTest_NEW) scales _Density.

    Properties
    {
        [NoScaleOffset] _Soft ("Where the gas is: the mesh's fine field (WebGasField_*_NEW)", 3D) = "" {}
        _SoftMin ("Field box: lowest corner, in the mesh's space", Vector) = (-0.058963, -0.049281, -0.042921, 0)
        _SoftSize ("Field box: size", Vector) = (0.1323, 0.0917, 0.0847, 0)
        [NoScaleOffset] _Noise ("Tiling 3D noise (WebNebulaNoise_NEW)", 3D) = "" {}
        _NoiseScale ("Billows: noise tiles per world unit", Float) = 0.06
        _Detail ("Fine detail: times the billows' frequency", Float) = 3.1
        _Drift ("Drift, world units a second", Vector) = (0.4, 0.15, 0.25, 0)
        _Shape ("Gas from field x, fully there from y", Vector) = (0.02, 0.3, 0, 0)
        _Erode ("How much the billows eat into the gas", Range(0, 1)) = 0.3
        _Billow ("How much the billows shade the gas: bright crowns, dim folds", Range(0, 1)) = 0.8
        _Glow ("Light, per world unit", Float) = 0.09
        _Thickness ("How much the gas hides behind it, per world unit", Float) = 0.12
        _CameraFade ("Thins out nearer the camera than x, all there from y (world units)", Vector) = (12, 70, 0, 0)
        [IntRange] _Steps ("Steps back from the hull's far side: the cost", Range(2, 16)) = 8
        _Depth ("How far they reach, world units", Float) = 30
        _Inside ("The camera is in the gas from field x, fully from y", Vector) = (0.03, 0.15, 0, 0)
        [IntRange] _StepsInside ("From in the gas, steps off the camera: the extra cost there", Range(0, 12)) = 6
        _DepthInside ("How far they reach, world units", Float) = 60
        _Inflate ("The hull pushed out along its normals, in the mesh's units", Float) = 0
        _Opacity ("Opacity (WorldSwitcher_NEW fades this)", Range(0, 1)) = 1
        _Emission ("Glow (WorldSwitcher_NEW scales this)", Range(0, 6)) = 1
        _Density ("Density (the dark matter toggle fades this)", Range(0, 6)) = 1
        [HDR] _ColorA ("Thin: violet", Color) = (0.375, 0.171, 0.502, 1)
        [HDR] _ColorB ("Thicker: purple", Color) = (0.677, 0.245, 0.602, 1)
        [HDR] _ColorC ("Thick: magenta", Color) = (0.921, 0.34, 0.563, 1)
        [HDR] _ColorD ("Hottest: pink", Color) = (1.028, 0.517, 0.521, 1)
        _Ramp ("Where purple, magenta and pink are reached along the heat", Vector) = (0.3, 0.65, 0.95, 0)
        _Heat ("How much the gas's thickness heats it", Range(0, 2)) = 0.45
        _NearHeat ("Nearer a cluster's heart, how much hotter", Range(0, 2)) = 0.4
        _Knot0 ("Cluster heart 1: xyz in the mesh's space, w its reach", Vector) = (0, 0, 0, 0)
        _Knot1 ("Cluster heart 2", Vector) = (0, 0, 0, 0)
        _Knot2 ("Cluster heart 3", Vector) = (0, 0, 0, 0)
        _Knot3 ("Cluster heart 4", Vector) = (0, 0, 0, 0)
        _Knot4 ("Cluster heart 5", Vector) = (0, 0, 0, 0)
        _Knot5 ("Cluster heart 6", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent-50" "RenderType"="Transparent" "IgnoreProjector"="True" }
        BlendOp Max
        Blend One One
        ZWrite Off
        Cull Front

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler3D _Soft, _Noise;
            float4 _SoftMin, _SoftSize, _Drift, _Shape, _CameraFade, _Inside, _Ramp;
            float4 _ColorA, _ColorB, _ColorC, _ColorD;
            float4 _Knot0, _Knot1, _Knot2, _Knot3, _Knot4, _Knot5;
            float _NoiseScale, _Detail, _Erode, _Billow, _Glow, _Thickness, _Steps, _Depth, _StepsInside, _DepthInside, _Inflate;
            float _Opacity, _Emission, _Density, _Heat, _NearHeat;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 world : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                float4 at = float4(v.vertex.xyz + v.normal * _Inflate, 1.0);
                o.pos = UnityObjectToClipPos(at);
                o.world = mul(unity_ObjectToWorld, at).xyz;
                return o;
            }

            // How near a cluster's heart: 1 at it, falling off over its reach (w 0: no heart).
            float Near(float3 p, float4 k)
            {
                float3 d = p - k.xyz;
                return k.w > 0.0 ? exp(-dot(d, d) / (k.w * k.w)) : 0.0;
            }

            float Heart(float3 o)
            {
                return saturate(Near(o, _Knot0) + Near(o, _Knot1) + Near(o, _Knot2) + Near(o, _Knot3) + Near(o, _Knot4) + Near(o, _Knot5));
            }

            float3 Ramp(float h)
            {
                float3 c = lerp(_ColorA.rgb, _ColorB.rgb, smoothstep(0.0, _Ramp.x, h));
                c = lerp(c, _ColorC.rgb, smoothstep(_Ramp.x, _Ramp.y, h));
                return lerp(c, _ColorD.rgb, smoothstep(_Ramp.y, _Ramp.z, h));
            }

            float Field(float3 o)
            {
                return tex3Dlod(_Soft, float4((o - _SoftMin.xyz) / _SoftSize.xyz, 0.0)).r;
            }

            // One step of the march, t along the ray and dt long: how much gas, how hot, how lit;
            // its light added front to back.
            void March(float3 cam, float3 dir, float3 oCam, float3 oDir, float t, float dt, float4 fine, float heart,
                       float3 drift, inout float T, inout float3 light)
            {
                float env = Field(oCam + oDir * t);
                env *= env;
                float4 n = tex3Dlod(_Noise, float4((cam + dir * t + drift) * _NoiseScale + (fine.gba - 0.5) * 0.35, 0.0));
                float billow = n.r * 0.65 + fine.r * 0.35;
                float gas = smoothstep(_Shape.x, _Shape.y, env - (1.0 - billow) * _Erode);
                float hot = saturate(env * _Heat * (0.7 + 0.6 * n.g) + heart * _NearHeat);
                float d = gas * smoothstep(_CameraFade.x, _CameraFade.y, t) * dt;
                float shade = lerp(1.0, saturate(0.2 + 1.6 * billow * billow), _Billow);
                light += T * Ramp(hot) * (d * _Glow * shade);
                T *= exp(-d * _Thickness);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 cam = _WorldSpaceCameraPos;
                float3 toBack = i.world - cam;
                float back = length(toBack);
                float3 dir = toBack / max(back, 1e-4);
                float3 oCam = mul(unity_WorldToObject, float4(cam, 1.0)).xyz;
                float3 oDir = mul((float3x3)unity_WorldToObject, dir);
                float3 drift = _Drift.xyz * _Time.y;
                // A different start along the step for each pixel, evenly spread (R2), so the
                // steps do not show as bands.
                float jitter = frac(dot(floor(i.pos.xy), float2(0.7548776662, 0.5698402910)));
                float span = min(_Depth, back);
                float start = back - span;
                float T = 1.0;
                float3 light = 0.0;

                // From in the gas: the gas near the camera first, by steps hung off the camera.
                float inside = Field(oCam);
                inside = smoothstep(_Inside.x, _Inside.y, inside * inside);
                if (inside > 0.001)
                {
                    float tMid = _CameraFade.x + _DepthInside * 0.25;
                    float4 fine = tex3Dlod(_Noise, float4((cam + dir * tMid + drift) * _NoiseScale * _Detail, 0.0));
                    float heart = Heart(oCam + oDir * tMid);
                    int count = (int)_StepsInside;
                    [loop] for (int s = 0; s < count; s++)
                    {
                        float u = (s + jitter) / count;
                        float t = _CameraFade.x + _DepthInside * u * u;
                        float dt = _DepthInside * (2.0 * u + 1.0 / count) / count;
                        float w = inside * saturate((start - t) / dt + 0.5);   // handed over where the ray's own march begins
                        March(cam, dir, oCam, oDir, t, dt * w, fine, heart, drift, T, light);
                    }
                }

                // The ray's own march: from the far side of the hull, back towards the camera.
                {
                    float tMid = back - span * 0.5;
                    float4 fine = tex3Dlod(_Noise, float4((cam + dir * tMid + drift) * _NoiseScale * _Detail, 0.0));
                    float heart = Heart(oCam + oDir * tMid);
                    int count = (int)_Steps;
                    float dt = span / count;
                    [loop] for (int s = 0; s < count; s++)
                        March(cam, dir, oCam, oDir, start + (s + jitter) * dt, dt, fine, heart, drift, T, light);
                }

                float k = _Opacity * saturate(_Density);
                return fixed4(light * _Emission * k, (1.0 - T) * k);
            }
            ENDCG
        }
    }

    FallBack Off
}
