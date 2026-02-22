Shader "Custom/SunUnlitGlow"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (1,0.6,0.1,1)
        _CoreIntensity ("Core Intensity", Range(0,10)) = 2

        _RimColor ("Rim Color", Color) = (1,0.8,0.2,1)
        _RimPower ("Rim Power (higher = thinner)", Range(0.5,8)) = 2
        _RimIntensity ("Rim Intensity", Range(0,10)) = 3

        _NoiseTex ("Noise (optional)", 2D) = "white" {}
        _NoiseStrength ("Noise Strength", Range(0,2)) = 0.3
        _NoiseScale ("Noise Scale", Range(0.1,10)) = 2
        _NoiseSpeed ("Noise Speed", Range(0,5)) = 0.5

        _Alpha ("Alpha", Range(0,1)) = 1
    }

    SubShader
    {
        // Render after opaque objects; additive is common for glow.
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Cull Back
        ZWrite Off
        Blend One One   // Additive. If you want softer edges, try: Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _MainColor;
            float _CoreIntensity;

            fixed4 _RimColor;
            float _RimPower;
            float _RimIntensity;

            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;
            float _NoiseStrength;
            float _NoiseScale;
            float _NoiseSpeed;

            float _Alpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldN   : TEXCOORD1;
                float2 uv       : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldN = UnityObjectToWorldNormal(v.normal);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 N = normalize(i.worldN);
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);

                // Fresnel rim: strong at grazing angles (sphere edges)
                float fresnel = pow(1.0 - saturate(dot(N, V)), _RimPower);

                // Optional noise to modulate brightness slightly
                float2 uv = i.uv * _NoiseScale;
                uv += _Time.y * _NoiseSpeed;
                float noise = tex2D(_NoiseTex, uv).r; // 0..1
                float noiseMod = lerp(1.0, noise * 2.0, _NoiseStrength); // keep around 1

                float3 core = _MainColor.rgb * _CoreIntensity * noiseMod;
                float3 rim  = _RimColor.rgb  * (_RimIntensity * fresnel) * noiseMod;

                float3 col = core + rim;

                // Additive blending ignores alpha mostly, but keep it if you switch blend mode.
                return fixed4(col, _Alpha);
            }
            ENDCG
        }
    }
    FallBack Off
}