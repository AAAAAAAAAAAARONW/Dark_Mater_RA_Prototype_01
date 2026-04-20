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

                float travel = tFar - tNear;

                float3 pWldNear  = mul(unity_ObjectToWorld, float4(ro + rd*tNear, 1)).xyz;
                float3 pWldFar   = mul(unity_ObjectToWorld, float4(ro + rd*tFar,  1)).xyz;
                float  worldTravel = length(pWldFar - pWldNear);

                int steps    = clamp((int)ceil(worldTravel / max(0.01,_StepWorldLength)),
                                     4, (int)_Steps);
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
