Shader "Custom/GalaxyField_NEW"
{
    // The galaxies of a cosmic web: the only part of it the eye could see. Thousands of soft
    // points of starlight in ONE mesh, one draw call, baked by GalaxyFieldBuilder_NEW from the
    // web's own density (the Houdini Texture3Ds), so they sit where the web is thickest: crowded
    // and bright at the nodes, sparse and faint along the threads.
    //
    // Each galaxy is four vertices at the same centre; the vertex shader opens them into a quad
    // facing the camera's POSITION, not the view plane, so in VR a galaxy does not swing round
    // when the head turns.
    //
    //   POSITION  the galaxy's centre (in the object's space)
    //   TEXCOORD0 the corner, -1..1
    //   TEXCOORD1 x size (object units), y brightness
    //   COLOR     its colour
    //
    // Far away a galaxy is held at a minimum size on screen (_MinAngle) and dimmed in step, so
    // it stays a point of light instead of flickering below a pixel. Close up it fades out
    // (_NearFade) rather than filling the view as a flat disc.
    //
    // Additive. _Color is the world's fade (WorldSwitcher_NEW). In Resources so a build has it.

    Properties
    {
        _Color ("Fade (as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        _Fade ("Show (DarkMatterToggleTest_NEW cross-fades it with the web)", Range(0, 1)) = 1
        _Brightness ("Brightness", Range(0, 8)) = 1.5
        _Sharpness ("Core sharpness", Range(2, 40)) = 14
        _HaloAmount ("Soft halo round the core", Range(0, 1)) = 0.25
        _MinAngle ("Smallest size on screen (radians)", Range(0, 0.02)) = 0.0025
        _NearFade ("Fade out nearer than x sizes, gone at y", Vector) = (8, 2, 0, 0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Brightness, _Sharpness, _HaloAmount, _MinAngle, _Fade;
            float4 _NearFade;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 corner : TEXCOORD0;
                float2 data : TEXCOORD1;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 corner : TEXCOORD0;
                float3 tint : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 centre = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
                float scale = length(unity_ObjectToWorld._m00_m10_m20);
                float size = v.data.x * scale;

                float3 toCam = _WorldSpaceCameraPos - centre;
                float dist = max(length(toCam), 1e-4);
                float3 f = toCam / dist;
                float3 upRef = abs(f.y) > 0.99 ? float3(1, 0, 0) : float3(0, 1, 0);
                float3 right = normalize(cross(upRef, f));
                float3 up = cross(f, right);

                // Held at a minimum size on screen, dimmed by as much as it was enlarged.
                float shown = max(size, dist * _MinAngle);
                float bright = v.data.y * (size / shown);

                // Close up, gone.
                bright *= saturate((dist / size - _NearFade.y) / max(_NearFade.x - _NearFade.y, 1e-3));

                float3 world = centre + (right * v.corner.x + up * v.corner.y) * shown;
                o.pos = mul(UNITY_MATRIX_VP, float4(world, 1.0));
                o.corner = v.corner;
                o.tint = v.color.rgb * bright;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float r2 = dot(i.corner, i.corner);
                if (r2 >= 1.0) return 0;
                float core = exp(-r2 * _Sharpness);
                float halo = exp(-r2 * 3.0) * _HaloAmount * (1.0 - r2);
                float3 c = i.tint * (core + halo) * _Brightness * _Fade * _Color.rgb;
                return fixed4(c, 0);
            }
            ENDCG
        }
    }

    FallBack Off
}
