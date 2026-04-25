Shader "Custom/PurpleCloudVolumeMulti"
{
    // Renders up to 4 PurpleCloudVolume sub-volumes in ONE draw call.
    //
    // Instead of N separate transparent passes (each with a full raymarch loop),
    // a single cube sized to the union AABB runs one loop.  Each step checks
    // which sub-volume(s) the sample point belongs to and only then computes
    // density — skipping the empty space between sub-volumes for free.
    //
    // Setup workflow:
    //   1. Select 2–4 GameObjects that have Custom/PurpleCloudVolume materials.
    //   2. Tools > Laxi > Create Multi-Volume from Selection.
    //   3. Original renderers are disabled; one PurpleCloudVolumeMulti cube takes over.
    //
    // The PurpleCloudMultiVolume.cs component updates sub-volume matrices every
    // frame via MaterialPropertyBlock so no material instance is created.
    //
    // Noise texture: same CloudNoiseTex3D.asset baked by BakeCloudNoise.cs.
    //   R = 3-octave FBM (1 fetch/step for macro shape)
    //   G = erosion, B = breakup, A = 2-octave detail FBM at 3x scale

    Properties
    {
        [Header(Volume)]
        _Steps           ("Raymarch Steps",    Range(16, 128)) = 32
        _StepWorldLength ("Step World Length", Range(0.01, 0.2)) = 0.08
        _Density         ("Density",           Range(0.1, 6.0)) = 1.9
        _Absorption      ("Absorption",        Range(0.2, 6.0)) = 1.4
        _Emission        ("Emission",          Range(0.1, 6.0)) = 1.3
        _Opacity         ("Opacity",           Range(0.05, 1.0)) = 0.86
        _EdgeFade        ("Edge Fade",         Range(0.5, 8.0)) = 2.6

        [Header(Cloud Shape)]
        _FeatureSize     ("Feature Size",      Range(0.1, 4.0)) = 1.0
        _DetailScale     ("Detail Scale",      Range(1.0, 8.0)) = 3.0
        _DetailWeight    ("Detail Weight",     Range(0.0, 1.0)) = 0.45
        _CloudSoftness   ("Cloud Softness",    Range(0.2, 3.0)) = 1.35
        _Coverage        ("Coverage",          Range(0.0, 1.0)) = 0.56
        _InnerGlow       ("Inner Glow",        Range(0.0, 2.0)) = 0.7
        _Erode           ("Erode Amount",      Range(0.0, 1.0)) = 0.38
        _EdgeWidth       ("Edge Width",        Range(0.02, 0.5)) = 0.24
        _AlphaFog        ("Alpha Fog Response",Range(0.5, 3.0)) = 1.25
        _BoundaryBreakup ("Boundary Breakup",  Range(0.0, 1.0)) = 0.55

        [Header(Purple Palette)]
        _BaseColor      ("Base Purple",      Color) = (0.20, 0.10, 0.42, 1)
        _MidColor       ("Mid Purple",       Color) = (0.48, 0.26, 0.78, 1)
        _HighlightColor ("Highlight Lavender",Color) = (0.78, 0.60, 0.98, 1)
        _RimColor       ("Rim Magenta",      Color) = (0.90, 0.35, 0.86, 1)

        [Header(Static Variation)]
        _SeedOffset  ("Seed Offset",      Vector) = (3.2, -1.7, 5.4, 0)
        _Anisotropy  ("Vertical Stretch", Range(0.3, 2.0)) = 1.05

        [Header(Noise Texture)]
        [NoScaleOffset] _NoiseTex ("Noise Texture 3D", 3D) = "" {}

        [Header(Sub-Volumes — managed by PurpleCloudMultiVolume component)]
        _VolCount ("Sub-Volume Count", Range(1, 4)) = 1
        // _VolMatrix0..3 are set via MaterialPropertyBlock — not exposed here.
    }

    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            // -----------------------------------------------------------------------
            // Uniforms
            // -----------------------------------------------------------------------
            float _Steps, _StepWorldLength, _Density, _Absorption, _Emission;
            float _Opacity, _EdgeFade;
            float _FeatureSize, _DetailScale, _DetailWeight, _CloudSoftness, _Coverage;
            float _InnerGlow, _Erode, _EdgeWidth, _AlphaFog, _BoundaryBreakup;
            float4 _BaseColor, _MidColor, _HighlightColor, _RimColor;
            float4 _SeedOffset;
            float  _Anisotropy;
            sampler3D _NoiseTex;

            // Sub-volume world-to-local matrices.  Set each frame by
            // PurpleCloudMultiVolume.cs via MaterialPropertyBlock.
            float4x4 _VolMatrix0;
            float4x4 _VolMatrix1;
            float4x4 _VolMatrix2;
            float4x4 _VolMatrix3;
            float    _VolCount;   // float avoids int-uniform issues on some drivers

            // -----------------------------------------------------------------------
            // Noise (same optimised channel layout as PurpleCloudVolume.shader)
            // -----------------------------------------------------------------------
            #define NTILE_INV 0.03125   // 1.0 / 32.0

            float4 sampleNoise(float3 p) { return tex3D(_NoiseTex, frac(p * NTILE_INV)); }
            float  noiseR(float3 p)      { return sampleNoise(p).r; }
            float  noiseG(float3 p)      { return sampleNoise(p).g; }
            float  noiseB(float3 p)      { return sampleNoise(p).b; }
            float  noiseA(float3 p)      { return sampleNoise(p).a; }

            // -----------------------------------------------------------------------
            // Structs / vertex
            // -----------------------------------------------------------------------
            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos       : SV_POSITION;
                float3 rayOrigin : TEXCOORD0;   // object space of multi-volume cube
                float3 rayDir    : TEXCOORD1;   // object space of multi-volume cube
            };

            void rayBox(float3 ro, float3 rd, out float tNear, out float tFar)
            {
                float3 rdS = rd;
                if (abs(rdS.x) < 1e-6) rdS.x = 1e-6 * sign(rdS.x + 1e-7);
                if (abs(rdS.y) < 1e-6) rdS.y = 1e-6 * sign(rdS.y + 1e-7);
                if (abs(rdS.z) < 1e-6) rdS.z = 1e-6 * sign(rdS.z + 1e-7);
                float3 t0   = (float3(-0.5,-0.5,-0.5) - ro) / rdS;
                float3 t1   = (float3( 0.5, 0.5, 0.5) - ro) / rdS;
                float3 tmin = min(t0, t1), tmax = max(t0, t1);
                tNear = max(max(tmin.x, tmin.y), tmin.z);
                tFar  = min(min(tmax.x, tmax.y), tmax.z);
                if (tNear > tFar) tFar = tNear;
                if (tFar  < 0.0) { tNear = 1e6; tFar = 0.0; return; }
                if (tNear < 0.0) tNear = 0.0;
            }

            // -----------------------------------------------------------------------
            // Sub-volume bounds check
            //
            // For each sub-volume, transforms worldPos into that volume's local
            // [-0.5, 0.5] space and computes its edge-proximity weight.
            // Returns the best (most-interior) weight across all sub-volumes;
            // writes that volume's local coords into pObjBest for the radial
            // inner-glow calculation in sampleCloudDensity.
            // Returns 0 when worldPos is outside every sub-volume → skip density.
            // -----------------------------------------------------------------------
            void checkVol(float4x4 m, float3 wp,
                          inout float best, inout float3 pObjBest)
            {
                float3 p     = mul(m, float4(wp, 1.0)).xyz;
                float  maxAx = max(abs(p.x), max(abs(p.y), abs(p.z)));
                if (maxAx > 0.51) return;
                float  tb = saturate((0.5 - maxAx) / max(0.001, _EdgeWidth));
                if (tb > best) { best = tb; pObjBest = p; }
            }

            float subVolumeBounds(float3 worldPos, out float3 pObjBest)
            {
                pObjBest = float3(0, 0, 0);
                float best = 0.0;
                              checkVol(_VolMatrix0, worldPos, best, pObjBest);
                if (_VolCount >= 2.0) checkVol(_VolMatrix1, worldPos, best, pObjBest);
                if (_VolCount >= 3.0) checkVol(_VolMatrix2, worldPos, best, pObjBest);
                if (_VolCount >= 4.0) checkVol(_VolMatrix3, worldPos, best, pObjBest);
                return best;
            }

            // -----------------------------------------------------------------------
            // Cloud density
            //
            // worldPos  — world-space point (noise is world-space → continuous seam)
            // pObj      — local coords of the nearest sub-volume (for inner glow)
            // toBoundary — edge weight from subVolumeBounds (replaces inline edge calc)
            // -----------------------------------------------------------------------
            float sampleCloudDensity(float3 worldPos, float3 pObj, float toBoundary,
                                     float invFeatureSize, float earlyExitThr, out float lum)
            {
                float3 q = worldPos * invFeatureSize;
                q.y *= _Anisotropy;
                q += _SeedOffset.xyz;

                // FETCH 0: pre-baked 3-octave FBM (1 fetch, was 3 via fbm3)
                float macro = noiseR(q * 0.85);
                [branch]
                if (macro < earlyExitThr) { lum = 0; return 0; }

                // FETCH 1: detail from A channel (pre-baked 2-octave FBM at 3x scale)
                float detail    = noiseA(q * (_DetailScale * 0.333) + float3(4.1, -2.5, 7.3));
                float baseShape = saturate(
                    (macro * 0.95 + detail * 0.55 - (0.34 + _Coverage * 0.38))
                    * (1.8 + _CloudSoftness));

                // FETCH 2: erosion (G channel)
                float erosion = noiseG(q * (_DetailScale * 2.1) + float3(-6.2, 5.4, -3.8));
                baseShape *= lerp(1.0,
                    saturate((erosion - (0.40 + _Erode * 0.35)) * 3.0), _Erode);

                // FETCH 3: breakup (B channel)
                float breakup = saturate(
                    lerp(1.0, noiseB(q * 0.63 + float3(12.3, -4.2, 8.7)), _BoundaryBreakup));

                float edge    = pow(saturate(toBoundary * breakup), _EdgeFade);
                float density = pow(saturate(baseShape * edge), _CloudSoftness);

                // Inner glow relative to nearest sub-volume center
                float inner = saturate(1.0 - dot(pObj, pObj) / (0.95 * 0.95));
                lum = saturate(inner * 0.65 + detail * 0.35);
                return density;
            }

            float3 sampleCloudColor(float density, float lum)
            {
                float midMask  = saturate(density * 1.2  + lum * 0.35);
                float highMask = saturate((density - 0.28) * 1.85 + lum * 0.62);
                float rimMask  = saturate((1.0 - density) * 0.7 + (1.0 - lum) * 0.4);
                float3 col = lerp(_BaseColor.rgb, _MidColor.rgb, midMask);
                col = lerp(col, _HighlightColor.rgb, highMask);
                col += _RimColor.rgb * rimMask * 0.2;
                return col;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.pos       = mul(UNITY_MATRIX_VP, worldPos);
                o.rayOrigin = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                o.rayDir    = normalize(v.vertex.xyz - o.rayOrigin);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 ro = i.rayOrigin;
                float3 rd = normalize(i.rayDir);

                float tNear, tFar;
                rayBox(ro, rd, tNear, tFar);
                if (tNear >= tFar || tFar <= 0.0) return fixed4(0,0,0,0);

                float travel = tFar - tNear;

                // Adaptive step count: avoids wasting steps on short ray paths.
                float3 pWldNear  = mul(unity_ObjectToWorld, float4(ro + rd * tNear, 1)).xyz;
                float3 pWldFar   = mul(unity_ObjectToWorld, float4(ro + rd * tFar,  1)).xyz;
                float  worldTravel = length(pWldFar - pWldNear);

                int stepsByDist = (int)ceil(worldTravel / max(0.01, _StepWorldLength));
                int steps       = clamp(stepsByDist, 10, (int)_Steps);
                float stepLen   = travel / (float)steps;

                float jitter = frac(sin(dot(i.pos.xy, float2(12.9898, 78.233))) * 43758.5453);
                tNear += jitter * stepLen;

                // Linearise world-space position for the loop — one matrix mul up front,
                // then cheap add per step instead of transform inside loop.
                float3 pObjBase   = ro + rd * (tNear + 0.5 * stepLen);
                float3 pWorldBase = mul(unity_ObjectToWorld, float4(pObjBase, 1.0)).xyz;
                float3 pWorldStep = mul((float3x3)unity_ObjectToWorld, rd * stepLen);

                float invFeatureSize = rcp(max(_FeatureSize, 0.001));
                float absorbFactor   = _Absorption * stepLen * 2.2;
                float emissiveBase   = 0.35 * _Emission;
                float emissiveLumMul = _InnerGlow * _Emission;
                float earlyExitThr   = ((0.34 + _Coverage * 0.38)
                                       - 0.4 * rcp(1.8 + _CloudSoftness)) / 0.95;

                float3 accColor = 0;
                float  accAlpha = 0;

                [loop]
                for (int s = 0; s < steps; s++)
                {
                    float3 pWorld = pWorldBase + pWorldStep * s;

                    // Bounds check: find the most-interior sub-volume containing pWorld.
                    // Returns 0 for points between sub-volumes → skip density immediately.
                    float3 pObjBest;
                    float  toBoundary = subVolumeBounds(pWorld, pObjBest);
                    if (toBoundary < 0.001) continue;

                    float lum;
                    float d = sampleCloudDensity(pWorld, pObjBest, toBoundary,
                                                 invFeatureSize, earlyExitThr, lum) * _Density;
                    if (d < 0.001) continue;

                    float3 cloudCol  = sampleCloudColor(d, lum);
                    float  alphaStep = 1.0 - exp(-d * absorbFactor);
                    float  emissive  = emissiveBase + lum * emissiveLumMul;
                    float  transmit  = 1.0 - accAlpha;

                    accColor += transmit * alphaStep * cloudCol * emissive;
                    accAlpha += transmit * alphaStep;
                    if (accAlpha > 0.985) break;
                }

                accAlpha = 1.0 - exp(-accAlpha * _AlphaFog);
                accAlpha = saturate(accAlpha * _Opacity);
                return fixed4(accColor, accAlpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
