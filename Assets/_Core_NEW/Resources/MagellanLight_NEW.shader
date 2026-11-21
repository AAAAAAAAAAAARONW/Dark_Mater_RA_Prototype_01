Shader "Custom/MagellanLight_NEW"
{
    // The starlight going through the Magellan telescope (MagellanLightPath_NEW): line
    // renderers drawn additively in their vertex colour times _Color. Where the light is
    // behind the telescope's own parts (inside the tertiary tower, through the elevation
    // bearing) it is still drawn, fainter, by _Occluded, so the whole path reads.
    //
    // Lives in Resources so a build always has it.

    Properties
    {
        [HDR] _Color ("Colour", Color) = (1.6, 1.4, 1.1, 1)
        _Occluded ("Brightness behind things", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        CGINCLUDE
        #include "UnityCG.cginc"
        half4 _Color;
        half _Occluded;
        struct appdata { float4 vertex : POSITION; half4 colour : COLOR; };
        struct v2f { float4 pos : SV_POSITION; half4 colour : COLOR; };
        v2f vert(appdata v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.colour = v.colour * _Color;
            return o;
        }
        ENDCG

        Pass
        {
            ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return half4(i.colour.rgb * i.colour.a, 1); }
            ENDCG
        }

        Pass
        {
            ZTest Greater
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return half4(i.colour.rgb * i.colour.a * _Occluded, 1); }
            ENDCG
        }
    }
}
