Shader "Custom/SummitSet_NEW"
{
    // The mountains and the dome at the end of the journey (EarthSummit_NEW), at night: a
    // dim moon from one side, the glow of Los Angeles from below on the slopes and walls that
    // face it, and the city's haze thickening with distance. Opaque, one pass, no shadows.
    //
    // A rim of the city's glow round the edges (_Rim) so the dome and the ridges read against
    // the dark; and light of their own (_Emission), for the dim red of the dome's inside.
    //
    // The mountains can wear the ground's own night (_GroundMap: the close-up night map,
    // laid on by uv as it lies on the Earth below), so from above they are the same moonlit
    // land and city lights as the ground round them, not a dark patch over it (16 Nov: "a
    // black square"); the moon on their slopes still shapes them, seen from the side.
    //
    // Colour by vertex (_VertexColors on): the albedo times twice the vertex colour, so grey
    // 0.5 leaves it as it is - the Las Campanas ground's gravel and road, the enclosure's parts.
    // A grain of its own (_Grain, _GrainScale a unit of the mesh's own space): rock on the
    // ground, weathering on the walls, so nothing is a flat colour up close.
    //
    // Distances are in km (_KmToWorld, set every frame: the world grows round them on the
    // way down), so the haze is the same at any scale. In Resources so a build has it.

    Properties
    {
        _Albedo ("Albedo", Color) = (0.04, 0.04, 0.045, 1)
        _MoonDir ("Towards the moon (world)", Vector) = (0, 1, 0, 0)
        _MoonColor ("Moonlight", Color) = (0.09, 0.1, 0.13, 1)
        _GlowDir ("Towards the city's glow (world)", Vector) = (0, 0, 1, 0)
        _GlowColor ("City glow", Color) = (0.3, 0.17, 0.08, 1)
        _Ambient ("Sky light", Color) = (0.012, 0.013, 0.02, 1)
        _Rim ("Rim of city glow", Color) = (0, 0, 0, 1)
        _Emission ("Light of its own", Color) = (0, 0, 0, 1)
        [NoScaleOffset] _GroundMap ("The ground's night, by uv", 2D) = "black" {}
        _GroundOn ("Wear the ground's night", Float) = 0
        _GroundExposure ("Its brightness", Float) = 1
        _CityGlow ("Its city lights, times their own light", Float) = 1
        _HazeColor ("Haze", Color) = (0.06, 0.04, 0.035, 1)
        _HazeKm ("Haze distance, km", Float) = 25
        _HazeOn ("How much of the haze, 0 to 1 (set every frame)", Float) = 1
        _KmToWorld ("World units a km (set every frame)", Float) = 1
        _VertexColors ("Times twice the vertex colour", Float) = 0
        _MoonOnNight ("The moon on the rock under the ground's night too", Float) = 0
        _Grain ("Grain, how much", Range(0, 1)) = 0
        _GrainScale ("Grain, cycles a unit of the mesh", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Albedo, _MoonDir, _MoonColor, _GlowDir, _GlowColor, _Ambient, _HazeColor, _Rim, _Emission;
            float _HazeKm, _HazeOn, _KmToWorld, _GroundOn, _GroundExposure, _CityGlow;
            float _VertexColors, _Grain, _GrainScale, _MoonOnNight;
            sampler2D _GroundMap;

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 toCam  : TEXCOORD1;   // from the point to the camera, world
                float2 uv     : TEXCOORD2;
                float3 local  : TEXCOORD3;   // the mesh's own space, for the grain
                fixed4 colour : COLOR;
            };

            v2f vert(appdata_full v)
            {
                v2f o;
                o.uv = v.texcoord.xy;
                o.local = v.vertex.xyz;
                o.colour = v.color;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.toCam = _WorldSpaceCameraPos - mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            // Value noise, 0 to 1.
            float Noise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x), lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x), lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 albedo = _Albedo.rgb;
                if (_VertexColors > 0.5) albedo *= i.colour.rgb * 2.0;
                if (_Grain > 0.0)
                {
                    float3 q = i.local * _GrainScale;
                    float g = Noise(q) * 0.6 + Noise(q * 3.7 + 11.0) * 0.4;
                    g = lerp(g, 0.5, saturate(length(fwidth(q)) * 1.5 - 0.5));
                    albedo *= 1.0 + _Grain * (g - 0.5) * 2.0;
                }
                float moon = saturate(dot(n, normalize(_MoonDir.xyz)));
                // The glow comes from a whole sky's width below the horizon: wrapped, soft.
                float glow = saturate(dot(n, normalize(_GlowDir.xyz)) * 0.6 + 0.4);
                float3 lit = albedo * (_Ambient.rgb + _MoonColor.rgb * moon + _GlowColor.rgb * glow * glow);
                if (_GroundOn > 0.5)
                {
                    // The ground's own night, the moon on the slope shaping it.
                    float3 ground = tex2D(_GroundMap, i.uv).rgb * _GroundExposure;
                    float bright = smoothstep(0.15, 0.6, dot(ground, float3(0.299, 0.587, 0.114)));
                    ground *= 1.0 + bright * (_CityGlow - 1.0);
                    lit = ground * (0.55 + 0.9 * moon) + albedo * (_Ambient.rgb + _MoonOnNight * _MoonColor.rgb * moon);
                }
                float3 v = normalize(i.toCam);
                lit += _Rim.rgb * pow(1.0 - saturate(dot(n, v)), 3.0) + _Emission.rgb;

                float km = length(i.toCam) / max(_KmToWorld, 1e-6);
                float haze = (1.0 - exp(-km / max(_HazeKm, 1e-3))) * _HazeOn;
                return fixed4(lerp(lit, _HazeColor.rgb, haze), 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
