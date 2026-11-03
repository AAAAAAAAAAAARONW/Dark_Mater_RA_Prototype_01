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
    // follows; a soft terminator, reddened on worlds with air. And a faint fill from the
    // camera's side (_Fill) — the light a photograph would add, or starlight — so that a planet
    // seen from its night side, as the journey mostly sees them with the Sun ahead, is a dim
    // disc with its markings and not a black hole in the stars.
    //
    // LATER: BRIGHTER ALL ROUND. The fill was most of the night side's light and little of the
    // day side's, and the sunlight stopped dead at the terminator: the planets read as dark,
    // with black backs. The fill is now stronger and reaches the day side too, the sunlight
    // wraps a little past the terminator, and the exposures went up a fifth.
    //
    // LATER: STEADY WHEN SMALL. Seen from afar a planet is a few pixels across, and what is
    // finer than a pixel on it twinkled as it moved across the screen: the Sun's glint on
    // Earth's oceans, a bright point under the bloom, and the city lights' specks. Both now
    // follow how big the planet is on screen (TEXCOORD3): the glint widens and dims, keeping
    // its light, until the planet is big enough to show it sharp, and the cities merge into a
    // faint glow. And a body under two pixels across — the Moon, from far off — fades out
    // rather than blink between pixels.
    //
    // LATER: EARTH'S REAL CLOUDS. Given a cloud map (_CloudMap, CelestialBody_NEW's cloudMap)
    // the cover is read from it instead of the noise, for the clouds and their shadows alike.
    // At night they are dark and lie over the city lights, as from orbit: the lights show
    // through the gaps. It replaces EarthClouds_NEW's full-screen deck in Vizlab_Journey,
    // which on the wall cost a raymarch per copy and turned the night into white fog.
    //
    // LATER: EARTH'S REAL NIGHT. Given a night map (_NightMap: NASA's Black Marble, the whole
    // Earth by moonlight with its city lights) the night side shows it as it is, in place of
    // the day map's albedo under the fill and the noise's cities: moonlit land a dark blue,
    // the cities where they are. The clouds over it are a moonlit grey (_NightCloud), not
    // black. A region map (_RegionMap, the same night at 24 times the detail, over
    // _RegionRect in degrees) comes in over the global one as the camera comes down to the
    // ground (_RegionFade, in the planet's radius over the camera's height above it), its
    // edges feathered; and round the landing point the sky is cleared (_ClearSky), so it is
    // a clear night where the light comes down.
    //
    // LATER: ON THE GROUND. Down on Mount Wilson (EarthSummit_NEW draws a finer patch of this
    // sphere with this material) the region map's texels are 460 m, and the city, seen across
    // at a glance, came out as a white slab. So where the map is bright, close up, it is
    // broken into the lights it is made of (_Lights): a point in each cell of a grid
    // _Lights.x km across, the brighter the map the more cells lit, each as bright as
    // _LightsBright; as cells come under two pixels the points give way to the map again. And
    // the ground thickens into the city's haze with distance (_GroundHaze, in km), once the
    // camera is down (_GroundHazeFrom, radius over height).
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
        _Fill ("Fill from the camera's side", Color) = (0.14, 0.15, 0.19, 1)
        _Terminator ("Terminator reddening", Color) = (1.0, 0.45, 0.2, 1)
        _TerminatorAmount ("Terminator reddening amount", Range(0, 1)) = 0
        [HDR] _Haze ("Air over the ground", Color) = (0.25, 0.5, 1.0, 1)
        _HazeAmount ("Air amount", Range(0, 2)) = 0
        _Ocean ("Oceans (from the map's colour)", Range(0, 1)) = 0
        [HDR] _City ("City lights", Color) = (1.0, 0.68, 0.32, 1)
        _CityAmount ("City lights amount", Range(0, 3)) = 0
        [NoScaleOffset] _CloudMap ("Cloud map (equirectangular, cover in red)", 2D) = "black" {}
        _CloudMapOn ("Clouds from the map, not the noise", Float) = 0
        _NightMap ("Night map (equirectangular, Black Marble)", 2D) = "black" {}
        _NightMapOn ("Night from the map", Float) = 0
        _NightExposure ("Night map exposure", Range(0, 4)) = 1
        _NightCloud ("Clouds by moonlight", Color) = (0.13, 0.135, 0.16, 1)
        [NoScaleOffset] _RegionMap ("Region night map", 2D) = "black" {}
        _RegionOn ("Region map in use", Float) = 0
        _RegionRect ("Region: west, south, east, north (degrees)", Vector) = (0, 0, 1, 1)
        _RegionFade ("Region in from / to (radius over height)", Vector) = (3, 9, 0, 0)
        _ClearSky ("Clear sky round (object-space direction)", Vector) = (0, 1, 0, 0)
        _ClearSkyCos ("Clear sky cosines: inside, outside", Vector) = (2, 1.5, 0, 0)
        _Lights ("City lights close up: cell km, point size, amount, region exposure", Vector) = (0.15, 0.22, 0, 1)
        _LightsBright ("City lights close up: each point's brightness", Range(0, 4)) = 1.6
        _GroundHaze ("Ground haze (rgb), km (w)", Vector) = (0.1, 0.06, 0.04, 30)
        _GroundHazeFrom ("Ground haze from / to (radius over height)", Vector) = (200, 800, 0, 0)
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
            float4 _SunPosition, _Night, _Terminator, _Haze, _City, _Fill;
            float _Exposure, _Bump, _Regolith, _TerminatorAmount, _HazeAmount, _Ocean, _CityAmount;
            float _CloudShadow, _LimbDarkening, _BandFlow, _CloudAmount, _CloudBrightness;
            float4 _CloudTerminator;
            sampler2D _NightMap, _RegionMap;
            float4 _NightMap_ST, _NightCloud, _RegionRect, _RegionFade;
            float _NightMapOn, _NightExposure, _RegionOn;
            float4 _Lights, _GroundHaze, _GroundHazeFrom;
            float _LightsBright;

            float3 LightHash(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            // The map's light broken into points (see LATER: ON THE GROUND): a light in a
            // cell if its hash is under the map's brightness. Between them, the map's
            // moonlit ground, dimmed to the land's own tone.
            float3 CityPoints(float3 nObj, float3 night, float weight)
            {
                float luma = dot(night, float3(0.299, 0.587, 0.114));
                float3 q = nObj * (6371.0 / _Lights.x);
                float pixel = length(fwidth(q));
                float detail = weight * _Lights.z * smoothstep(0.6, 0.3, pixel) * smoothstep(0.12, 0.35, luma);
                if (detail <= 0.0) return night;

                float3 cell = floor(q);
                float3 h = LightHash(cell);
                float3 at = 0.2 + 0.6 * LightHash(cell + 19.0);
                float size = max(_Lights.y, pixel * 0.7);
                float spot = saturate(1.0 - length(frac(q) - at) / size);
                float lit = step(h.x, saturate(luma * 1.2));
                float3 hue = night / max(luma, 0.05);
                float3 ground = hue * 0.04;
                float3 points = ground + hue * spot * spot * lit * _LightsBright * (0.6 + 0.8 * h.y);
                return lerp(night, points, detail);
            }

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float3 local  : TEXCOORD1;   // object space, on the sphere
                float3 world  : TEXCOORD2;
                float  pixels : TEXCOORD3;   // the planet's radius on screen, in pixels
                float  close  : TEXCOORD4;   // the planet's radius over the camera's height above it
                float  km     : TEXCOORD5;   // km a world unit, Earth's radius standing for 6371
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.local = v.vertex.xyz;
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 centre = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float radius = length(mul((float3x3)unity_ObjectToWorld, v.vertex.xyz));
                o.pixels = radius / max(length(centre - _WorldSpaceCameraPos), 1e-6)
                         * abs(unity_CameraProjection._m11) * _ScreenParams.y * 0.5;
                o.close = radius / max(length(centre - _WorldSpaceCameraPos) - radius, 1e-5);
                o.km = 6371.0 / max(radius, 1e-6);
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

                // Lambert, wrapped a little past the terminator, or Lommel-Seeliger for dust.
                float lambert = saturate((nl + 0.12) / 1.12);
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
                // As sharp as the planet's size on screen lets it be, its light kept.
                float nh = saturate(dot(nSmooth, h));
                float sharp = clamp(i.pixels * i.pixels / 2.25, 4.0, 140.0);
                float broad = min(sharp, 18.0);
                float glint = pow(nh, sharp) * 2.5 * (sharp / 140.0) + pow(nh, broad) * 0.12 * (broad / 18.0);
                float3 specular = sun * glint * ocean * (0.35 + fresnel) * day;

                // The fill: brightest face on, falling off towards the limb; on the night side
                // most, but on the day side too, a little.
                float fill = (0.45 + 0.55 * mu) * (1.0 - 0.45 * day);
                float3 dark = (_Night.rgb + _Fill.rgb * fill) * albedo;

                // The night from the map, where there is one: on the night side it is the
                // night side, moonlit land, cities and all (see LATER, above).
                float nightSide = smoothstep(0.06, -0.14, nlSmooth);
                if (_NightMapOn > 0.5)
                {
                    float3 night = tex2D(_NightMap, TRANSFORM_TEX(i.uv, _NightMap)).rgb;
                    if (_RegionOn > 0.5)
                    {
                        // Longitude and latitude, as the maps lay them out on PlanetSphere_NEW.
                        float lon = frac(atan2(-nObj.x, -nObj.z) / 6.2831853) * 360.0 - 180.0;
                        float lat = degrees(asin(clamp(nObj.y, -1.0, 1.0)));
                        float2 r = float2((lon - _RegionRect.x) / (_RegionRect.z - _RegionRect.x),
                                          (lat - _RegionRect.y) / (_RegionRect.w - _RegionRect.y));
                        float2 edge = saturate(min(r, 1.0 - r) / 0.12);
                        float w = edge.x * edge.y * smoothstep(_RegionFade.x, _RegionFade.y, i.close);
                        night = lerp(night, tex2D(_RegionMap, saturate(r)).rgb * _Lights.w, w);
                        night = CityPoints(nObj, night, w);
                    }
                    dark = lerp(dark, night * _NightExposure, nightSide);
                }

                float3 lit = albedo * sun * diffuse * _Exposure * (1.0 - shadow) + dark;
                lit += specular * (1.0 - shadow);

                // City lights on the night side, on land, in clusters. Not with a night map:
                // it has the real ones.
                if (_CityAmount > 0.0 && _NightMapOn < 0.5)
                {
                    float land = saturate(1.0 - ocean * 3.0) * saturate(1.0 - (Luma(albedo) - 0.55) * 6.0);
                    float clusters = smoothstep(0.58, 0.8, Triplanar(nObj * 6.0).r) * smoothstep(0.5, 0.9, Triplanar(nObj * 23.0).a);
                    // Too small to show them, a faint glow where they would be.
                    clusters = lerp(0.04, clusters, smoothstep(25.0, 80.0, i.pixels));
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
                    float3 cloud = cloudSun * cloudLit * cloudDay * _CloudBrightness + _Night.rgb + _Fill.rgb * fill * 0.8;
                    cloud += _NightCloud.rgb * nightSide * _NightMapOn;
                    lit = lerp(lit, cloud, a);
                }

                // The air seen over the ground towards the limb.
                float limb = pow(1.0 - mu, 2.5);
                lit = lerp(lit, lit + _Haze.rgb * day, saturate(limb * _HazeAmount));

                // Giants: darker towards the limb.
                lit *= lerp(1.0, pow(max(mu, 0.02), 0.45), _LimbDarkening);

                // Down on the ground, the city's haze with distance.
                float hazeOn = smoothstep(_GroundHazeFrom.x, _GroundHazeFrom.y, i.close);
                if (hazeOn > 0.0)
                {
                    float km = length(i.world - _WorldSpaceCameraPos) * i.km;
                    lit = lerp(lit, _GroundHaze.rgb, (1.0 - exp(-km / max(_GroundHaze.w, 1e-3))) * hazeOn);
                }

                // Under two pixels across, gone rather than blinking between pixels.
                lit *= smoothstep(0.6, 1.2, i.pixels);

                return fixed4(lit * _Color.rgb, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
