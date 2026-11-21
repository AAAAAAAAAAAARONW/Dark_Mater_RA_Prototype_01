// The body of the light at the head of its trail (TrailLook_NEW, option B): a soft round glow
// on a camera-facing quad, white-hot in the very middle. The same shader, with no core,
// draws the faint glow that trails back along the ribbon. Nothing hard-edged, no spikes.
//
// uv 0..1 across the quad; the glow is a gaussian from its middle, taken to zero at the rim.
Shader "Custom/PhotonHeadGlow_NEW"
{
    Properties
    {
        _Color       ("Color", Color) = (0.67, 0.84, 1, 1)
        _Intensity   ("Intensity", Range(0, 3)) = 0.9
        _Falloff     ("Falloff", Range(0.5, 20)) = 5
        _Core        ("White core", Range(0, 2)) = 1
        _CoreFalloff ("Core falloff", Range(5, 200)) = 60
    }
    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            float  _Intensity;
            float  _Falloff;
            float  _Core;
            float  _CoreFalloff;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float d2   = dot(i.uv - 0.5, i.uv - 0.5) * 4.0;     // 0 middle, 1 at the rim
                float rim  = smoothstep(1.0, 0.8, sqrt(d2));
                float glow = exp(-d2 * _Falloff) * rim;
                float core = exp(-d2 * _CoreFalloff) * _Core;
                float3 rgb = lerp(_Color.rgb, float3(1, 1, 1), saturate(core));
                return fixed4(rgb, saturate((glow + core) * _Intensity));
            }
            ENDCG
        }
    }
}
