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
    // Textures of its own (_DetailOn, 22 Nov): a colour and a normal map laid on in the mesh's own
    // space from three sides (triplanar), at _DetailScale repeats a unit and again at
    // _DetailFar of that, so neither tiling shows; on slopes steeper than _SlopeFrom a second
    // pair (_Slope). The colour multiplies the albedo (times _DetailGain, so a texture of
    // middling brightness leaves it about as it was), the normal shapes the moon on it.
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
        [NoScaleOffset] _Detail ("Detail colour", 2D) = "gray" {}
        [NoScaleOffset] [Normal] _DetailNormal ("Detail normal", 2D) = "bump" {}
        [NoScaleOffset] _Slope ("Steep slopes: colour", 2D) = "gray" {}
        [NoScaleOffset] [Normal] _SlopeNormal ("Steep slopes: normal", 2D) = "bump" {}
        _DetailOn ("Detail textures", Float) = 0
        _SlopeOn ("Slope textures", Float) = 0
        _DetailScale ("Detail, repeats a unit of the mesh", Float) = 1
        _DetailFar ("Detail, a second larger repeat, times the first", Float) = 0.12
        _DetailGain ("Detail colour, times", Float) = 2
        _DetailStrength ("Detail colour, how much", Range(0, 1)) = 1
        _SlopeScale ("Slope textures, times the detail's repeat", Float) = 0.5
        _SlopeGain ("Slope colour, times", Float) = 2
        _SlopeFrom ("Steep below this much up", Range(0, 1)) = 0.82
        _BumpStrength ("Normal maps, how much", Range(0, 2)) = 1
        _MacroOn ("Broad patches of the slope texture everywhere", Float) = 0
        _MacroScale ("Their repeat, times the detail's", Float) = 0.04
        _MacroMix ("How much of them", Range(0, 1)) = 0.45
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
            sampler2D _GroundMap, _Detail, _DetailNormal, _Slope, _SlopeNormal;
            float _DetailOn, _SlopeOn, _DetailScale, _DetailFar, _DetailGain, _DetailStrength;
            float _SlopeScale, _SlopeGain, _SlopeFrom, _BumpStrength, _MacroOn, _MacroScale, _MacroMix;

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 toCam  : TEXCOORD1;   // from the point to the camera, world
                float2 uv     : TEXCOORD2;
                float3 local  : TEXCOORD3;   // the mesh's own space, for the grain
                float3 localN : TEXCOORD4;   // the mesh's own normal, for the textures
                fixed4 colour : COLOR;
            };

            v2f vert(appdata_full v)
            {
                v2f o;
                o.uv = v.texcoord.xy;
                o.local = v.vertex.xyz;
                o.localN = v.normal;
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

            // A texture from three sides, weighted by the normal.
            float3 Tri(sampler2D t, float3 p, float3 w)
            {
                return tex2D(t, p.zy).rgb * w.x + tex2D(t, p.xz).rgb * w.y + tex2D(t, p.xy).rgb * w.z;
            }

            // A normal map from three sides, blended onto the mesh's normal (UDN), own space.
            float3 TriNormal(sampler2D t, float3 p, float3 w, float3 on)
            {
                float3 x = UnpackNormal(tex2D(t, p.zy)), y = UnpackNormal(tex2D(t, p.xz)), z = UnpackNormal(tex2D(t, p.xy));
                x = float3(x.xy * _BumpStrength + on.zy, on.x);
                y = float3(y.xy * _BumpStrength + on.xz, on.y);
                z = float3(z.xy * _BumpStrength + on.xy, on.z);
                return normalize(x.zyx * w.x + y.xzy * w.y + z.xyz * w.z);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 albedo = _Albedo.rgb;
                float detailLight = 1.0;
                if (_DetailOn > 0.5)
                {
                    float3 on = normalize(i.localN);
                    float3 w = pow(abs(on), 4.0);
                    w /= w.x + w.y + w.z;
                    float3 p = i.local * _DetailScale;
                    float3 c = (Tri(_Detail, p, w) * 0.6 + Tri(_Detail, p * _DetailFar + 0.37, w) * 0.4) * _DetailGain;
                    float3 bent = TriNormal(_DetailNormal, p, w, on);
                    if (_MacroOn > 0.5)
                    {
                        // Rock in broad patches, where a slow noise says so.
                        float3 pm = p * _MacroScale;
                        float patch = smoothstep(0.35, 0.75, Tri(_Slope, pm * 0.37 + 1.9, w).g);
                        c = lerp(c, Tri(_Slope, pm, w) * _SlopeGain, _MacroMix * (0.4 + 0.6 * patch));
                    }
                    if (_SlopeOn > 0.5)
                    {
                        float steep = smoothstep(_SlopeFrom, _SlopeFrom - 0.18, on.y);
                        float3 ps = p * _SlopeScale;
                        c = lerp(c, Tri(_Slope, ps, w) * _SlopeGain, steep);
                        bent = normalize(lerp(bent, TriNormal(_SlopeNormal, ps, w, on), steep));
                    }
                    c = lerp(1.0, c, _DetailStrength);
                    albedo *= c;
                    detailLight = dot(c, float3(0.299, 0.587, 0.114));
                    n = normalize(UnityObjectToWorldNormal(bent));
                }
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
                    float3 ground = tex2D(_GroundMap, i.uv).rgb * _GroundExposure * detailLight;
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
