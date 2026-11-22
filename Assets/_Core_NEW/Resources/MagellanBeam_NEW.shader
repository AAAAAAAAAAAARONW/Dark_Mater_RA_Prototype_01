Shader "Custom/MagellanBeam_NEW"
{
    // The starlight going through the Magellan telescope as one bundle (MagellanLightPath_NEW,
    // look Beam): a column down onto the primary, a cone up to the secondary, a narrower one
    // down to the tertiary, out to the focus. Each vertex carries in uv.x how far along the
    // light's way it is, metres; only the stretch from _Head - _Tail to _Head is lit, soft at
    // both ends, so the light is seen to run through. Brighter at the bundle's edges (seen
    // edge-on), so it reads as a volume of light, not a sheet. Drawn additively; behind the
    // telescope's parts (the tertiary tower) still drawn, fainter, by _Occluded.
    //
    // Lives in Resources so a build always has it.

    Properties
    {
        [HDR] _Color ("Colour", Color) = (0.4, 0.42, 0.48, 1)
        _Head ("Head, metres along", Float) = 0
        _Tail ("Length lit, metres", Float) = 12
        _Soft ("Soft front, metres", Float) = 1.2
        _Rim ("Brighter at the edges", Range(0, 3)) = 1.2
        _Fade ("Fade", Range(0, 1)) = 1
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
        float _Head, _Tail, _Soft, _Rim, _Fade, _Occluded;

        struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
        struct v2f
        {
            float4 pos : SV_POSITION;
            float along : TEXCOORD0;
            float3 normal : TEXCOORD1;
            float3 world : TEXCOORD2;
        };

        v2f vert(appdata v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.along = v.uv.x;
            o.normal = UnityObjectToWorldNormal(v.normal);
            o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
            return o;
        }

        half3 Beam(v2f i)
        {
            float front = saturate((_Head - i.along) / max(_Soft, 1e-3));
            float back = saturate((i.along - (_Head - _Tail)) / max(_Tail * 0.6, 1e-3));
            float lit = front * back * back;
            float3 view = normalize(_WorldSpaceCameraPos - i.world);
            float edge = 1.0 - abs(dot(view, normalize(i.normal)));
            return _Color.rgb * lit * (0.3 + _Rim * edge * edge) * _Fade;
        }
        ENDCG

        Pass
        {
            ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return half4(Beam(i), 1); }
            ENDCG
        }

        Pass
        {
            ZTest Greater
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return half4(Beam(i) * _Occluded, 1); }
            ENDCG
        }
    }
}
