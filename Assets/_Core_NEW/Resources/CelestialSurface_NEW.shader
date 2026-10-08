Shader "Custom/CelestialSurface_NEW"
{
    // A planet's or a moon's surface (CelestialBody_NEW), lit by the Sun where it really is.
    //
    // The textures are day maps only, so the rest is worked out from them:
    //   relief    a bump from the map's own brightness gradient, so craters and ridges stand
    //             up along the terminator instead of the ball reading as a flat disc
    //   oceans    picked out of the map by colour (Earth): darker, with the Sun's glint on them
    //   cities    at night, on land, warm specks in clusters (Earth)
    //   clouds    drifting weather over the ground, lit by the Sun, casting shadows (Earth).
    //             Drawn here, not on a shell above: a shell a few thousandths of the radius
    //             up fights the ground in the depth buffer at the journey's distances and
    //             flickers. At that height parallax is invisible anyway.
    //   haze      the atmosphere seen over the ground towards the limb, blue on the day side
    //   gas       giants: limb darkening, and the bands flowing, faster at the equator
    //
    // Lighting: Lambert, blended for rocky bodies with Lommel-Seeliger, the law dusty regolith
    // follows; a soft terminator, reddened on worlds with air.
    //
    // Opaque. _Color fades it as WorldSwitcher_NEW fades a world. In Resources so a build has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        _MainTex ("Day map", 2D) = "white" {}
        [NoScaleOffset] _Noise ("Noise (QuasarNoise_NEW)", 2D) = "gray" {}
        _SunPosition ("The Sun, world space (set every frame)", Vector) = (0, 0, 0, 1)
        _Exposure ("Sunlit exposure", Range(0.3, 3)) = 1.25
        _Bump ("Relief", Range(0, 6)) = 2
        _Regolith ("Dusty-surface lighting (0 Lambert, 1 Lommel-Seeliger)", Range(0, 1)) = 0
        _Night ("Night side", Color) = (0.004, 0.005, 0.009, 1)
        _Terminator ("Terminator reddening", Color) = (1.0, 0.45, 0.2, 1)
        _TerminatorAmount ("Terminator reddening amount", Range(0, 1)) = 0
        [HDR] _Haze ("Air over the ground", Color) = (0.25, 0.5, 1.0, 1)
        _HazeAmount ("Air amount", Range(0, 2)) = 0
        _Ocean ("Oceans (from the map's colour)", Range(0, 1)) = 0
        [HDR] _City ("City lights", Color) = (1.0, 0.68, 0.32, 1)
        _CityAmount ("City lights amount", Range(0, 3)) = 0
        _CloudShadow ("Cloud shadows", Range(0, 1)) = 0
        _CloudCover ("Cloud cover", Range(0, 1)) = 0.55
        _CloudSpin ("Cloud drift, radians a second", Float) = 0.01
        _CloudAmount ("Clouds", Range(0, 1)) = 0
        _CloudBrightness ("Cloud brightness", Range(0, 3)) = 1.1
        _CloudTerminator ("Clouds at the terminator", Color) = (1.0, 0.55, 0.3, 1)
        _LimbDarkening ("Limb darkening (giants)", Range(0, 1)) = 0
        _BandFlow ("Band flow, map widths a second (giants)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Celestial_NEW.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST, _MainTex_TexelSize;
            fixed4 _Color;
            float4 _SunPosition, _Night, _Terminator, _Haze, _City;
            float _Exposure, _Bump, _Regolith, _TerminatorAmount, _HazeAmount, _Ocean, _CityAmount;
            float _CloudShadow, _LimbDarkening, _BandFlow, _CloudAmount, _CloudBrightness;
            float4 _CloudTerminator;

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float3 local  : TEXCOORD1;   // object space, on the sphere
                float3 world  : TEXCOORD2;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.local = v.vertex.xyz;
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float Luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 nObj = normalize(i.local);
                float2 uv = i.uv;

                // Giants: the bands flow, the equator fastest.
                float lat = asin(clamp(nObj.y, -1.0, 1.0));
                uv.x += _BandFlow * _Time.y * (0.6 + 0.4 * cos(lat * 6.0)) * cos(lat);

                float3 albedo = tex2D(_MainTex, uv).rgb;

                // Relief from the map's brightness gradient, in the sphere's east and north.
                float2 texel = _MainTex_TexelSize.xy * 1.5;
                float hx = Luma(tex2D(_MainTex, uv + float2(texel.x, 0)).rgb) - Luma(tex2D(_MainTex, uv - float2(texel.x, 0)).rgb);
                float hy = Luma(tex2D(_MainTex, uv + float2(0, texel.y)).rgb) - Luma(tex2D(_MainTex, uv - float2(0, texel.y)).rgb);
                float3 east = normalize(cross(float3(0, 1, 0), nObj) + float3(1e-5, 0, 0));
                float3 north = cross(nObj, east);

                // Oceans, picked out by colour: blue over red and green.
                float ocean = _Ocean * saturate((albedo.b - albedo.r) * 4.0) * saturate((albedo.b - albedo.g) * 6.0 + 0.3);
                float bump = _Bump * (1.0 - ocean);
                float3 nBumped = normalize(nObj - bump * (hx * east + hy * north));

                float3 n = normalize(mul((float3x3)unity_ObjectToWorld, nBumped));
                float3 nSmooth = normalize(mul((float3x3)unity_ObjectToWorld, nObj));
                float3 l = normalize(_SunPosition.xyz - i.world);
                float3 v = normalize(_WorldSpaceCameraPos - i.world);

                float nl = dot(n, l);
                float nlSmooth = dot(nSmooth, l);
                float mu = saturate(dot(nSmooth, v));
                float mu0 = saturate(nl);

                // Lambert, or Lommel-Seeliger for dust.
                float lambert = mu0;
                float lommel = mu0 / (mu0 + mu + 1e-3) * 2.0;
                float diffuse = lerp(lambert, lommel, _Regolith);

                // A soft terminator, reddened where the light comes through air.
                float day = smoothstep(-0.08, 0.12, nlSmooth);
                float3 sun = lerp(float3(1, 1, 1), _Terminator.rgb, _TerminatorAmount * exp(-abs(nlSmooth) * 8.0));

                // Clouds' shadows: the cloud cover, a little towards the Sun.
                float shadow = 0.0;
                if (_CloudShadow > 0.0)
                {
                    float3 lObj = normalize(mul((float3x3)unity_WorldToObject, l));
                    shadow = CloudCover(normalize(nObj + lObj * 0.012)) * _CloudShadow;
                }

                // Oceans darker and deeper; the Sun's glint on them, stronger at a glance.
                albedo = lerp(albedo, albedo * float3(0.55, 0.7, 0.85), ocean * 0.6);
                float3 h = normalize(l + v);
                float fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(n, v)), 5.0);
                float glint = pow(saturate(dot(nSmooth, h)), 140.0) * 2.5 + pow(saturate(dot(nSmooth, h)), 18.0) * 0.12;
                float3 specular = sun * glint * ocean * (0.35 + fresnel) * day;

                float3 lit = albedo * sun * diffuse * _Exposure * (1.0 - shadow) + _Night.rgb * albedo;
                lit += specular * (1.0 - shadow);

                // City lights on the night side, on land, in clusters.
                if (_CityAmount > 0.0)
                {
                    float land = saturate(1.0 - ocean * 3.0) * saturate(1.0 - (Luma(albedo) - 0.55) * 6.0);
                    float clusters = smoothstep(0.58, 0.8, Triplanar(nObj * 6.0).r) * smoothstep(0.5, 0.9, Triplanar(nObj * 23.0).a);
                    float night = smoothstep(0.02, -0.18, nlSmooth);
                    lit += _City.rgb * clusters * land * night * _CityAmount;
                }

                // The clouds themselves: white by day, warm at the terminator, dark at night,
                // thinning at a glance near the limb so the edge of the world stays soft.
                if (_CloudAmount > 0.0)
                {
                    float cover = CloudCover(nObj) * _CloudAmount;
                    float cloudDay = smoothstep(-0.12, 0.25, nlSmooth);
                    float3 cloudSun = lerp(float3(1, 1, 1), _CloudTerminator.rgb, exp(-abs(nlSmooth) * 7.0));
                    float cloudLit = saturate(nlSmooth * 0.7 + 0.35);
                    float a = cover * smoothstep(0.0, 0.25, mu) * 0.92;
                    float3 cloud = cloudSun * cloudLit * cloudDay * _CloudBrightness + _Night.rgb;
                    lit = lerp(lit, cloud, a);
                }

                // The air seen over the ground towards the limb.
                float limb = pow(1.0 - mu, 2.5);
                lit = lerp(lit, lit + _Haze.rgb * day, saturate(limb * _HazeAmount));

                // Giants: darker towards the limb.
                lit *= lerp(1.0, pow(max(mu, 0.02), 0.45), _LimbDarkening);

                return fixed4(lit * _Color.rgb, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
