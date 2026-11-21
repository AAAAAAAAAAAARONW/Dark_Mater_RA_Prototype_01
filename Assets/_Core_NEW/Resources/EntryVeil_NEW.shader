Shader "Custom/EntryVeil_NEW"
{
    // The air the camera falls through on the way down to the summit (EarthSummit_NEW): a
    // sphere round the camera, drawn inside-out over everything solid, in the night air's colours -
    // deep blue overhead, warmer below where the ground's lights glow up through it - with
    // wisps of thin cloud that stream past as the camera drops (_Scroll goes with height).
    // _Alpha is set every frame from the camera's height: none high up, thickest through the
    // middle of the air, clear again a few km over the ground.
    //
    // Lives in Resources so a build always has it.

    Properties
    {
        [NoScaleOffset] _Noise ("Noise (QuasarNoise_NEW)", 2D) = "gray" {}
        _Up ("Up at the summit (world)", Vector) = (0, 1, 0, 0)
        _High ("Air overhead", Color) = (0.05, 0.08, 0.17, 1)
        _Low ("Air below, over the lights", Color) = (0.22, 0.13, 0.08, 1)
        _Alpha ("How thick", Range(0, 1)) = 0
        _Scroll ("Wisps' drift (set with height)", Float) = 0
        _Wisps ("Wisps", Range(0, 1)) = 0.6
    }

    SubShader
    {
        // Before the transparent queue: over the ground, the mountains and the sky, but the
        // light and its trail (Transparent and after) still drawn over it - it is what we follow.
        Tags { "Queue"="Transparent-100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Front

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Celestial_NEW.cginc"

            float4 _Up, _High, _Low;
            float _Alpha, _Scroll, _Wisps;

            struct v2f { float4 pos : SV_POSITION; float3 world : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.world - _WorldSpaceCameraPos);
                float e = dot(d, _Up.xyz);
                float3 colour = lerp(_Low.rgb, _High.rgb, smoothstep(-0.4, 0.5, e));

                // Thin cloud streaming up past the camera as it falls.
                float n = Triplanar(d * 2.2 + _Up.xyz * _Scroll).r * 0.65 + Triplanar(d * 5.3 - _Up.xyz * _Scroll * 1.7).g * 0.35;
                float wisp = smoothstep(0.42, 0.78, n) * _Wisps;
                colour += _Low.rgb * wisp * 0.8 + 0.04 * wisp;

                return fixed4(colour, saturate(_Alpha * (0.75 + 0.5 * wisp)));
            }
            ENDCG
        }
    }

    FallBack Off
}
