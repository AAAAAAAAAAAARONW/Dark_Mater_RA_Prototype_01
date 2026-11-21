Shader "Custom/SunSurface_NEW"
{
    // The Sun's disc (SunLook_NEW), drawn after NASA's SDO pictures of it in extreme
    // ultraviolet — the orange Sun of the 304 Å channel — but not slavishly:
    //
    //   network        the chromosphere's mottling: bright along the edges of convection
    //                  cells, darker in their middles, at two sizes. A slow flow field drags it
    //                  about (_Flow), so the surface moves like gas rather than a turning map.
    //                  Faded to its mean as the cells come under a pixel, so a far Sun does not
    //                  shimmer.
    //   warmth         a broad, slow brightness variation over that, so the disc is not even.
    //   filaments      a few thin dark threads lying across the disc: cool gas held up above it.
    //   active regions bright patches in the activity belts (_Regions, world space, from
    //                  SunLook_NEW, which turns them with the Sun), where the network brightens
    //                  and goes over the bloom. Each flares now and then (_RegionFlare).
    //   limb           even across the disc, brighter towards the edge — limb brightening, as
    //                  in the ultraviolet: the line of sight there runs through more glowing gas.
    //                  (21 Nov: it was limb darkening, the white-light rule; it put a bright
    //                  spot in the middle, as if the Sun were a ball lit from the front.)
    //
    // 21 Nov, later: redder in the middle (_CentreRed), as in NASA's simulations — the disc
    // deep red-orange, going orange and yellow only towards the limb and in the active regions;
    // the edge a warm orange rather than white under the bloom (_RimColour). The network finer
    // and softer (it read as crazed glaze), the filaments thinner and fewer (one read as a worm).
    //
    // Colours run from _Cool (the darkest gas) to _Hot; only the active regions and flares go
    // past 1, so the bloom picks out those and leaves the disc its texture.
    //
    // Opaque, so planets crossing it hide it properly; the glow, the fuzzy edge and the loops
    // round it are SunGlow_NEW. _Color fades it as WorldSwitcher_NEW fades a world.
    // In Resources so a build has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        _Cool ("Darkest gas", Color) = (0.42, 0.1, 0.02, 1)
        _Hot ("Brightest gas", Color) = (1, 0.6, 0.18, 1)
        _Brightness ("Brightness", Range(0.3, 2)) = 1
        _Network ("Network mottling", Range(0, 1)) = 0.8
        _CellScale ("Cells across the radius", Range(4, 60)) = 14
        _Flow ("Surface flow", Range(0, 1)) = 0.5
        _Filaments ("Dark filaments", Range(0, 1)) = 0.6
        _Active ("Active regions", Range(0, 1)) = 1
        _ActiveGlow ("Active regions' glow (over 1, for the bloom)", Range(0, 4)) = 1.2
        _LimbBright ("Brighter towards the limb", Range(0, 2)) = 0.9
        _RimColour ("Colour towards the limb", Color) = (1, 0.42, 0.1, 1)
        _CentreRed ("Redder towards the middle", Range(0, 1)) = 0.6
        _Turn ("How far the Sun has turned, radians (set by script)", Float) = 0
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

            #define REGIONS 6

            fixed4 _Color, _Cool, _Hot, _RimColour;
            float _CentreRed;
            float _Brightness, _Network, _CellScale, _Flow, _Filaments, _Active, _ActiveGlow, _LimbBright, _Turn;
            float4 _Regions[REGIONS];       // xyz: direction from the centre, world space; w: size
            float _RegionFlare[REGIONS];    // 0 to 1

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float3 local : TEXCOORD0;
                float3 world : TEXCOORD1;
                float3 n     : TEXCOORD2;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.local = v.vertex.xyz;
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.n = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float3 Hash3(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            float Hash1(float3 p) { return Hash3(p).x; }

            // Smooth value noise, 0 to 1.
            float Noise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash1(i), Hash1(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash1(i + float3(0, 1, 0)), Hash1(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash1(i + float3(0, 0, 1)), Hash1(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash1(i + float3(0, 1, 1)), Hash1(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            // How near the edge between two cells: 1 on it, 0 in a cell's middle.
            float Network(float3 p, float t)
            {
                float3 i = floor(p), f = frac(p);
                float f1 = 8.0, f2 = 8.0;
                for (int z = -1; z <= 1; z++)
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float3 b = float3(x, y, z);
                    float3 o = 0.5 + 0.45 * sin(t + 6.2832 * Hash3(i + b));
                    float d = length(b + o - f);
                    if (d < f1) { f2 = f1; f1 = d; }
                    else if (d < f2) f2 = d;
                }
                return 1.0 - smoothstep(0.0, 0.45, f2 - f1);
            }

            // The same turn as SunLook_NEW.Turn: about the Sun's own axis.
            float3 Turn(float3 v, float a)
            {
                float c = cos(a), s = sin(a);
                return float3(c * v.x + s * v.z, v.y, -s * v.x + c * v.z);
            }

            // 1 at a region's heart, falling off over about `size` (chord length).
            float Region(float3 n, float3 centre, float size)
            {
                float3 e = (n - centre) / size;
                return exp(-dot(e, e));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 nObj = normalize(i.local);
                float3 nWorld = normalize(i.n);
                float3 v = normalize(_WorldSpaceCameraPos - i.world);
                float mu = saturate(dot(nWorld, v));
                float t = _Time.y;

                // The surface turns with the Sun; everything on it turns with it.
                float3 s = Turn(nObj, -_Turn);

                // The flow: a slow field drags the network about, a cell or so at most.
                float3 p = s * _CellScale * 1.6;
                float3 q = p * 0.18 + t * 0.02;
                p += _Flow * 1.6 * (float3(Noise(q), Noise(q + 31.7), Noise(q + 63.1)) - 0.5);

                // Network, at two sizes, faded to its mean as the cells come under a pixel.
                float seen = 1.0 - smoothstep(0.3, 0.8, length(fwidth(s * _CellScale * 1.6)));
                float net = 0.5;
                if (seen > 0.0)
                {
                    float churn = 0.15 + 0.35 * _Flow;
                    float n1 = Network(p, t * churn);
                    float n2 = Network(p * 2.4 + 17.0, t * churn * 1.4);
                    net = lerp(0.5, n1 * 0.55 + n2 * 0.45, seen);
                }

                // Broad warmth, and the dark filaments.
                float3 w = s + _Flow * 0.05 * (float3(Noise(q * 0.5), Noise(q * 0.5 + 7.0), 0.5) - 0.5);
                float warmth = Noise(w * 2.2 + t * 0.01) * 0.7 + Noise(w * 5.0 - t * 0.015) * 0.3;
                float thread = abs(Noise(w * 5.0 + 7.0) - 0.5);
                float filament = (1.0 - smoothstep(0.0, 0.018, thread)) * smoothstep(0.62, 0.75, Noise(w * 1.4 + 3.0));

                // Active regions, and their flares.
                float active = 0.0, flare = 0.0;
                [unroll]
                for (int k = 0; k < REGIONS; k++)
                {
                    float3 dir = _Regions[k].xyz;
                    float size = _Regions[k].w;
                    active += Region(nWorld, dir, size);
                    flare += _RegionFlare[k] * Region(nWorld, dir, size * 0.45);
                }
                active = saturate(active) * _Active;

                float heat = 0.22 + 0.42 * lerp(0.5, net, _Network) + 0.36 * warmth;
                heat *= 1.0 - 0.6 * filament * _Filaments;
                heat = saturate(heat + active * (0.35 + 0.5 * net));

                float3 c = lerp(_Cool.rgb, _Hot.rgb, heat);

                // The active regions' knots go over 1, for the bloom; a flare, more.
                c += _Hot.rgb * active * net * _ActiveGlow;
                c += float3(1.0, 0.85, 0.6) * flare * (0.6 + 0.4 * net) * 3.0;

                // Redder towards the middle, but for the active regions.
                c *= lerp(float3(1, 1, 1), float3(1.0, 0.5, 0.32), _CentreRed * pow(mu, 1.2) * (1.0 - 0.7 * active));

                // Brighter towards the limb, in a warm orange.
                c *= lerp(0.85, 1.0, mu);
                c += _RimColour.rgb * _LimbBright * pow(1.0 - mu, 2.5) * (0.7 + 0.3 * net);

                c *= _Brightness;
                return fixed4(c * _Color.rgb * _Color.a, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
