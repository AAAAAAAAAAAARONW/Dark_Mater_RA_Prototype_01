Shader "Hidden/Journey/Bake Milky Way Density"
{
    Properties
    {
        [NoScaleOffset] _Volume ("Baked galaxy density (rebake after art edits)", 3D) = "" {}
        _Color ("Transition fade", Color) = (1,1,1,1)
        [NoScaleOffset] _Noise ("Interstellar structure (linear)", 2D) = "gray" {}
        [HDR] _ArmColor ("Young stars in the arms", Color) = (0.42,0.62,0.95,1)
        [HDR] _CoreColor ("Old stars in the bar", Color) = (1.15,0.79,0.43,1)
        [HDR] _HIIColor ("Sparse star-forming regions", Color) = (1.1,0.15,0.3,1)
        _Brightness ("Exposure", Range(0.1,4)) = 0.85
        _Pitch ("Spiral winding", Range(1.5,5)) = 2.3
        _ArmCount ("Principal spiral arms", Range(2,6)) = 4
        _ArmWidth ("Arm width (radius fraction)", Range(0.02,0.15)) = 0.080
        _Dust ("Leading dust lanes", Range(0,3)) = 1.35
        _Phase ("Bar and arm orientation (radians)", Float) = 0.45
        _Thickness ("Disc thickness (cube units)", Range(0.002,0.03)) = 0.0045
        _Steps ("Volume samples", Range(12,96)) = 32
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../Resources/MilkyWayDensity_NEW.cginc"
            float _SliceHeight;
            float4 frag(v2f_img i):SV_Target
            {
                float3 p=float3(i.uv.x-0.5,_SliceHeight,i.uv.y-0.5);
                float3 emission; float extinction;
                galaxy(p,emission,extinction);
                return float4(emission,extinction);
            }
            ENDCG
        }
    }
    Fallback Off
}
