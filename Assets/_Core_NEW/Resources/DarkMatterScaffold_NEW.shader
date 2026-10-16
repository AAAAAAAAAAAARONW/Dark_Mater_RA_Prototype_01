Shader "Custom/DarkMatterScaffold_NEW"
{
    // Dark matter, drawn: the soft scaffold the cosmic web's gas sits on.
    //
    // The same raymarch as Custom/PreBakedCloudVolume, one fetch a step, so it costs what
    // one of the web's volumes costs. Two differences:
    //
    //   COLOUR IS NOT BAKED. _BakedTex is density only (A), baked by
    //   DarkMatterScaffoldBake_NEW from the web's own volumes, blurred so it is wider and
    //   softer than the gas. The colour is _Color, so the art can change it in the
    //   Inspector without a rebake, and nothing in the voice-over names it.
    //
    //   IT IS REVEALED, NOT FADED. The global _DarkMatterReveal (0 hidden, 1 shown) is
    //   written by DarkMatterScaffold_NEW. It does not dim the whole volume evenly: it
    //   moves a density threshold, so showing it grows outward from the densest threads -
    //   where the gas already is - into the thin halo, and hiding it pulls back the same
    //   way. A global, not a material property, because WorldSwitcher_NEW and
    //   PhotonLayering_NEW both write this renderer's property block and material slots,
    //   and a global is the one thing neither of them can overwrite.
    //
    // _Opacity and _Emission are floats under those names on purpose: WorldSwitcher_NEW
    // scales exactly those to fade a layer, so the scaffold dissolves with the web in a
    // dive. _LayerAroundPhoton works as on the web's volumes; it is off by default, like
    // the purple haze, so the scaffold sits under the light.

    Properties
    {
        [NoScaleOffset] _BakedTex       ("Baked Density Texture 3D (A)", 3D) = "" {}
        [HDR] _Color    ("Colour",            Color) = (0.32, 0.55, 1.0, 1)
        _Steps          ("Raymarch Steps",    Range(8, 64))   = 24
        _StepWorldLength("Step World Length", Range(0.01,4.0)) = 0.6
        _Density        ("Density",           Range(0.1, 6.0)) = 1.2
        _Absorption     ("Absorption",        Range(0.2, 6.0)) = 0.8
        _Emission       ("Emission",          Range(0.1, 6.0)) = 1.4
        _Opacity        ("Opacity",           Range(0.05,1.0)) = 0.55
        _AlphaFog       ("Alpha Fog Response",Range(0.5, 3.0)) = 1.25

        [Header(Reveal)]
        _RevealSoftness ("Reveal Edge Softness", Range(0.02, 1.0)) = 0.25

        [Header(Layering)]
        [ToggleUI] _LayerAroundPhoton ("Layer Around Photon", Float) = 0
        [HideInInspector] _SplitPart ("Split Part", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler3D _BakedTex;
            float4 _Color;
            float _Steps, _StepWorldLength, _Density, _Absorption, _Emission;
            float _Opacity, _AlphaFog, _RevealSoftness;

            float  _DarkMatterReveal;   // global, DarkMatterScaffold_NEW

            float  _SplitPart;
            float4 _PhotonWorldPos;     // global, PhotonLayering_NEW
            float  _PhotonSplitBias;    // global, PhotonLayering_NEW

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos       : SV_POSITION;
                float3 rayOrigin : TEXCOORD0;
                float3 rayDir    : TEXCOORD1;
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
                if (tNear < 0.0)  tNear = 0.0;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float4 wp = mul(unity_ObjectToWorld, v.vertex);
                o.pos       = mul(UNITY_MATRIX_VP, wp);
                o.rayOrigin = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                o.rayDir    = normalize(v.vertex.xyz - o.rayOrigin);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float reveal = saturate(_DarkMatterReveal);
                if (reveal <= 0.001) return fixed4(0,0,0,0);

                float3 ro = i.rayOrigin;
                float3 rd = normalize(i.rayDir);

                float tNear, tFar;
                rayBox(ro, rd, tNear, tFar);
                if (tNear >= tFar || tFar <= 0.0) return fixed4(0,0,0,0);

                // Layering around the photon, as in PreBakedCloudVolume.
                int maxSteps = (int)_Steps;
                if (_SplitPart > 0.5)
                {
                    float3 camFwd    = -UNITY_MATRIX_V[2].xyz;
                    float  depthPerT = dot(mul((float3x3)unity_ObjectToWorld, rd), camFwd);
                    float  split     = dot(_PhotonWorldPos.xyz - _WorldSpaceCameraPos, camFwd) + _PhotonSplitBias;
                    float  tSplit    = depthPerT > 1e-5 ? split / depthPerT : 1e20;

                    float nearShare = saturate((min(tFar, tSplit) - tNear) / max(1e-6, tFar - tNear));
                    int   nearSteps = nearShare > 0.0 ? max(2, (int)round(_Steps * nearShare)) : 0;

                    if (_SplitPart < 1.5) { tNear = max(tNear, tSplit); maxSteps = max(4, (int)_Steps - nearSteps); }
                    else                  { tFar  = min(tFar, tSplit);  maxSteps = nearSteps; }

                    if (tNear >= tFar || maxSteps <= 0) return fixed4(0,0,0,0);
                }

                float travel = tFar - tNear;

                float3 pWldNear  = mul(unity_ObjectToWorld, float4(ro + rd*tNear, 1)).xyz;
                float3 pWldFar   = mul(unity_ObjectToWorld, float4(ro + rd*tFar,  1)).xyz;
                float  worldTravel = length(pWldFar - pWldNear);

                int steps    = clamp((int)ceil(worldTravel / max(0.01,_StepWorldLength)),
                                     min(4, maxSteps), maxSteps);
                float stepLen = travel / (float)steps;

                float jitter = frac(sin(dot(i.pos.xy, float2(12.9898,78.233)))*43758.5453);
                tNear += jitter * stepLen;

                float3 pBase = ro + rd * (tNear + 0.5 * stepLen);
                float3 pStep = rd * stepLen;

                float absorbFactor = _Absorption * stepLen * 2.2;

                // The threshold the reveal moves: 1 shows nothing, -softness shows it all.
                // Dense first, thin last, so it grows out of the threads.
                float soft = max(0.02, _RevealSoftness);
                float edge = lerp(1.0, -soft, reveal);

                float accAlpha = 0.0;

                [loop]
                for (int s = 0; s < steps; s++)
                {
                    float3 uv3 = pBase + pStep * s + 0.5;
                    float  a   = tex3D(_BakedTex, uv3).a;

                    float d = a * smoothstep(edge, edge + soft, a) * _Density;
                    if (d < 0.001) continue;

                    float alphaStep = 1.0 - exp(-d * absorbFactor);
                    accAlpha += (1.0 - accAlpha) * alphaStep;
                    if (accAlpha > 0.985) break;
                }

                // One colour, so the colour sum is the alpha sum times it: no need to
                // carry it through the loop.
                float3 col = accAlpha * _Color.rgb * _Emission;

                accAlpha = 1.0 - exp(-accAlpha * _AlphaFog);
                accAlpha = saturate(accAlpha * _Opacity);
                return fixed4(col, accAlpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
