Shader "Custom/PreBakedCloudVolume"
{
    // Extremely fast volumetric cloud renderer for pre-baked density textures.
    //
    // BakeVolumeDensityTex.cs evaluates the full PurpleCloudVolume density +
    // colour field offline and stores the result in a per-volume 64^3 RGBA16
    // Texture3D.  This shader reads it with ONE tex3D fetch per raymarch step
    // (vs. 4-6 fetches in the original shader).
    //
    // Channel layout of _BakedTex:
    //   RGB = colour * emission-luminance  (premultiplied at bake time)
    //   A   = raw density
    //
    // Runtime-tweakable params (NOT baked): Density, Absorption, Emission,
    // Opacity, AlphaFog, Steps, StepWorldLength.
    //
    // Layering around the photon (PhotonLayering_NEW): a volume with Layer Around
    // Photon ticked is drawn in two parts - the stretch of each ray beyond the photon's
    // depth before the photon trail, the stretch between the camera and the photon
    // after it - so its filaments pass both in front of and behind the light. The two
    // parts share one step budget, so the cost per ray is unchanged. Unticked volumes
    // (and every volume in a scene without PhotonLayering_NEW) draw exactly as before.

    Properties
    {
        [NoScaleOffset] _BakedTex       ("Baked Density Texture 3D", 3D) = "" {}
        _Steps          ("Raymarch Steps",    Range(8, 64))   = 24
        _StepWorldLength("Step World Length", Range(0.01,0.3)) = 0.10
        _Density        ("Density",           Range(0.1, 6.0)) = 1.9
        _Absorption     ("Absorption",        Range(0.2, 6.0)) = 1.4
        _Emission       ("Emission",          Range(0.1, 6.0)) = 1.3
        _Opacity        ("Opacity",           Range(0.05,1.0)) = 0.86
        _AlphaFog       ("Alpha Fog Response",Range(0.5, 3.0)) = 1.25

        [Header(Layering)]
        [ToggleUI] _LayerAroundPhoton ("Layer Around Photon", Float) = 0
        // Set on the runtime copies only: 0 whole ray, 1 beyond the photon, 2 in front of it.
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
            float _Steps, _StepWorldLength, _Density, _Absorption, _Emission;
            float _Opacity, _AlphaFog;

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
                float3 ro = i.rayOrigin;
                float3 rd = normalize(i.rayDir);

                float tNear, tFar;
                rayBox(ro, rd, tNear, tFar);
                if (tNear >= tFar || tFar <= 0.0) return fixed4(0,0,0,0);

                // Layering around the photon - see the note at the top. The split is the
                // plane at the photon's view depth, pushed back by the bias.
                int maxSteps = (int)_Steps;
                if (_SplitPart > 0.5)
                {
                    float3 camFwd    = -UNITY_MATRIX_V[2].xyz;
                    float  depthPerT = dot(mul((float3x3)unity_ObjectToWorld, rd), camFwd);
                    float  split     = dot(_PhotonWorldPos.xyz - _WorldSpaceCameraPos, camFwd) + _PhotonSplitBias;
                    float  tSplit    = depthPerT > 1e-5 ? split / depthPerT : 1e20;

                    // Both parts compute the same share, so their steps add up to _Steps.
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

                // Linearise object-space position for the loop.
                float3 pBase = ro + rd * (tNear + 0.5 * stepLen);
                float3 pStep = rd * stepLen;

                float absorbFactor = _Absorption * stepLen * 2.2;

                float3 accColor = float3(0,0,0);
                float  accAlpha = 0.0;

                [loop]
                for (int s = 0; s < steps; s++)
                {
                    float3 pObj = pBase + pStep * s;            // object space [-0.5,0.5]
                    float3 uv3  = pObj + 0.5;                   // texture UV   [0,1]

                    // ONE fetch — RGB = premul colour, A = density
                    float4 baked = tex3D(_BakedTex, uv3);

                    float d = baked.a * _Density;
                    if (d < 0.001) continue;

                    float alphaStep = 1.0 - exp(-d * absorbFactor);
                    float transmit  = 1.0 - accAlpha;

                    // baked.rgb already has emission-lum baked in; scale by _Emission
                    accColor += transmit * alphaStep * baked.rgb * _Emission;
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
