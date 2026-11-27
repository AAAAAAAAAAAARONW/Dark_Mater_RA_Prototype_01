Shader "Custom/DiveSwarm_NEW"
{
    // What a dive's point resolves into: the galaxies of a cluster (and, on later gates,
    // the stars of a galaxy). One mesh, one draw — every member is a quad whose four
    // vertices all sit at its centre, and the vertex shader spreads them into a small
    // camera-facing glow there, stretched along the way it moves on screen — along its line
    // through the crowd's centre, away from it while the dive opens the crowd up, towards
    // it while a cluster being left shrinks away, and slanting round it while the crowd turns
    // as it opens — so the ones streaming past read as streaks.
    //
    // Never smaller on screen than MinPixels in radius: a member that small is drawn that big
    // and dimmer by as much, the same light over more pixels. A glow under a pixel across
    // lands on a pixel one frame and between pixels the next, and a crowd of them streaming
    // past twinkles all over.
    //
    // Scattering (_Scatter, 0 to 1): instead of the whole crowd dimming together, each member
    // goes out on its own as _Scatter passes its turn (TEXCOORD1.y), over a short while — so
    // the members are left strewn through the world the dive has opened and wink out one by
    // one.
    //
    // LATER: GALAXIES, GOLD TO BLUE (the Milky Way dive; LARC review, 25 November: "the Milky
    // Way is the only distinct galaxy, everything else looks like a hazy point of light"; "the
    // lights are gold, the galaxies as they appear are blue, so they look like they are
    // spawning: give them a lifetime, gold at first, turning blue as we go in"). Both off by
    // default, so every other crowd (the other dives, the credits) is drawn as it was.
    //   _Shapes   A member big enough on screen (_ShapeScreen.xy, a share of half the screen's
    //             height) is drawn as a small galaxy instead of a glow: a two-armed spiral seen
    //             at its own slant (its disc's normal is TEXCOORD2, fixed in the world), an
    //             elliptical, or an irregular of a few knots (TEXCOORD2.w picks, by the shares
    //             in _ShapeScreen.zw). Each holds about the light its glow had, so nothing
    //             flashes as it turns from one into the other, and is not smeared into a streak.
    //   _Life     0 to 1 through the dive: each member starts gold (_LifeFrom, or the purpler
    //             _LifeFromAccent) and turns to its own colour over _LifeWidth of it, at its own
    //             turn (TEXCOORD3.z; LayerDive_NEW puts the ones nearest the point first). The
    //             bulge of a spiral turns to _ShapeCore instead.
    //
    // Additive, queue 2990: after the cosmic web's volumes, before the photon trail —
    // the members sit inside the web and behind the light, like the dive's own glow.
    // In Resources so a build always has it: LayerDive_NEW loads it by name.

    Properties
    {
        _Alpha    ("Alpha", Range(0, 4)) = 1
        _Streak   ("Streak", Range(0, 20)) = 0
        _SizeScale("Size scale", Float) = 1
        _NearFade ("Near fade (start, end)", Vector) = (0.5, 3, 0, 0)
        // 1: members come out of the point's glow (going in). 0: there is no point in view.
        _Resolve  ("Resolve from the point", Range(0, 1)) = 1
        // The bright middle of each member: 1 a point of light, lower a soft smudge.
        _Core     ("Core", Range(0, 1)) = 1
        // Turning round the crowd's up axis as it opens (a spiral): how far it turns, in
        // radians, for every factor of e it opens by. 0: straight out.
        _Spin     ("Spin", Float) = 0
        // How far through going out one by one: 0 all there, 1 all gone.
        _Scatter  ("Scatter", Range(0, 1)) = 0
        // Galaxies (see the header). 0: every member a glow, as before.
        _Shapes   ("Galaxy shapes", Range(0, 1)) = 0
        _ShapeScreen ("Shape from x to y (share of half the screen's height); z spiral share, w + elliptical share", Vector) = (0.012, 0.03, 0.6, 0.9)
        _ShapeCore ("A spiral's bulge", Color) = (1, 0.86, 0.62, 1)
        // Lifetime (see the header). 1: every member its own colour, as before.
        _Life     ("Life, 0 gold to 1 own colour", Range(0, 1)) = 1
        _LifeWidth ("Each member's share of the life", Range(0.01, 1)) = 0.35
        _LifeFrom ("Gold", Color) = (1.05, 0.78, 0.4, 1)
        _LifeFromAccent ("Purpler gold", Color) = (1, 0.6, 0.62, 1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
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

            // The smallest a member is drawn, in pixels of radius. Its glow fades well inside
            // its quad, so this is about a pixel and a half of light across.
            #define MinPixels 2.0

            float  _Alpha, _Streak, _SizeScale, _Resolve, _Core, _Spin, _Scatter;
            float4 _NearFade;
            float  _Shapes, _Life, _LifeWidth;
            float4 _ShapeScreen, _ShapeCore, _LifeFrom, _LifeFromAccent;

            struct appdata
            {
                float4 vertex : POSITION;    // member centre, object space
                fixed4 color  : COLOR;       // member colour; alpha = its brightness
                float2 uv     : TEXCOORD0;   // quad corner, -1..1
                float2 size   : TEXCOORD1;   // x = radius in object units; y = its turn to go out (0.12..1)
                float4 disc   : TEXCOORD2;   // xyz its disc's normal, in the world; w which kind of galaxy (0..1). Zeros: none given
                float4 look   : TEXCOORD3;   // x arm phase, y arm pitch (0..1), z its turn to change colour (0..1), w 1 purpler gold
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float4 color    : TEXCOORD1;  // not COLOR: the gold is brighter than 1
                float4 axes     : TEXCOORD2;  // xy its long axis on screen, z how open (1 face on), w kind
                float4 gal      : TEXCOORD3;  // x how much a galaxy (0 a glow), y arm phase, z arm pitch, w radius in pixels
                float3 bulgeRgb : TEXCOORD4;
            };

            v2f vert(appdata v)
            {
                v2f o;

                float3 centre = UnityObjectToViewPos(v.vertex.xyz);
                float3 origin = UnityObjectToViewPos(float3(0, 0, 0));
                float  scale  = length(unity_ObjectToWorld._m00_m10_m20);

                // Its radius, never under MinPixels on screen; dimmer by the area it gained.
                float radius = v.size.x * scale * _SizeScale;
                float pixel = 2.0 * max(-centre.z, 1e-3) / (abs(unity_CameraProjection._m11) * _ScreenParams.y);
                float wide = max(radius, MinPixels * pixel);
                float spread = (radius * radius) / max(wide * wide, 1e-12);
                float2 offset = v.uv * wide;

                // Stretch along the way the member moves on screen. It moves along its line
                // through the crowd's centre, one way or the other; that line's direction on
                // screen, where the member is, is the derivative of its projection. Unlike the
                // difference of the two projections, it holds when the centre is behind the
                // camera, as a cluster being left is. Turning as well, it also moves round the
                // crowd's up axis, _Spin as far for every bit it moves out.
                float3 line3  = centre - origin;
                float3 around = mul((float3x3)UNITY_MATRIX_MV, cross(float3(0, 1, 0), v.vertex.xyz));
                float3 motion = line3 + _Spin * around;
                float2 flow   = motion.xy * (-centre.z) + centre.xy * motion.z;
                float  flen  = length(flow);
                float2 dir   = flen > 1e-6 ? flow / flen : float2(0, 1);

                // Going in, how far the member has separated from the point, on screen.
                float2 away = centre.xy / max(-centre.z, 1e-3) - origin.xy / max(-origin.z, 1e-3);
                float  len  = length(away);

                // Big enough on screen, a galaxy: its radius as a share of half the screen's height.
                float shape = _Shapes * smoothstep(_ShapeScreen.x, _ShapeScreen.y,
                                                   radius * abs(unity_CameraProjection._m11) / max(-centre.z, 1e-3));

                // A galaxy is not smeared into a streak.
                offset += dir * dot(offset, dir) * _Streak * (1.0 - shape) * lerp(1.0, saturate(len * 4.0), _Resolve);

                o.pos = mul(UNITY_MATRIX_P, float4(centre + float3(offset, 0), 1.0));
                o.uv = v.uv;

                // Members passing the camera would fill the screen: fade them out first.
                float near = saturate((-centre.z - _NearFade.x) / max(1e-3, _NearFade.y - _NearFade.x));

                // Going in, members still inside the point are part of its glow, not yet
                // resolved: they brighten as they separate from it (full by about three degrees).
                float resolved = lerp(1.0, saturate((len - 0.01) / 0.04), _Resolve);

                // Its turn to go out: over the last 0.12 of _Scatter before its own.
                float stays = 1.0 - smoothstep(v.size.y - 0.12, v.size.y, _Scatter);

                o.color = v.color;
                o.color.a *= near * resolved * spread * stays * _Alpha;

                // Gold at first, its own colour at its turn.
                float w = max(_LifeWidth, 1e-3);
                float lt = saturate((_Life - v.look.z * (1.0 - w)) / w);
                lt = lt * lt * (3.0 - 2.0 * lt);
                float3 gold = lerp(_LifeFrom.rgb, _LifeFromAccent.rgb, v.look.w);
                o.color.rgb = lerp(gold, v.color.rgb, lt);
                o.bulgeRgb = lerp(gold, _ShapeCore.rgb, lt);

                // Its disc as seen from here: which way its long axis runs on screen and how open
                // it is. The normal is the world's, so the galaxy keeps its slant as the crowd turns.
                float3 nd = dot(v.disc.xyz, v.disc.xyz) > 1e-6 ? v.disc.xyz : float3(0.0, 1.0, 0.0);
                float3 n = normalize(mul((float3x3)UNITY_MATRIX_V, nd));
                float3 toEye = normalize(-centre);
                float3 m3 = cross(n, toEye);
                float2 major = dot(m3.xy, m3.xy) > 1e-8 ? normalize(m3.xy) : float2(1.0, 0.0);
                o.axes = float4(major, max(abs(dot(n, toEye)), 0.08), v.disc.w);
                o.gal = float4(shape, v.look.x, v.look.y, wide / max(pixel, 1e-6));
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float r = length(i.uv);
                float halo = saturate(1.0 - r);
                halo *= halo;
                float core = saturate(1.0 - r * 2.5);
                core *= core;
                float3 rgb = i.color.rgb * (halo * 0.6 + core * _Core);

                if (i.gal.x > 0.001)
                {
                    // The glow's light summed over its quad, which each shape is scaled to hold
                    // (their own sums, face on, are the constants below).
                    float total = 0.314 + 0.0838 * _Core;
                    float2 q = i.uv;
                    float2 ax = i.axes.xy;
                    float2 ay = float2(-ax.y, ax.x);
                    float open = i.axes.z;
                    float kind = i.axes.w;
                    float qx = dot(q, ax), qy = dot(q, ay);
                    float3 g;

                    if (kind < _ShapeScreen.z)
                    {
                        // A spiral: two logarithmic arms on an exponential disc, at its slant,
                        // a dust lane along its middle when near edge on, and a warm bulge.
                        //
                        // Nothing finer than the screen can show: a member moves a fraction of a
                        // pixel each frame, and detail under a pixel lands on one pixel, then on
                        // the next, and twinkles (the reason glows are never under MinPixels). So
                        // the disc is never thinner than about a pixel, the arms fade to their
                        // mean light where they are closer than about two pixels apart, the dust
                        // lane shows only once it is a pixel wide, and the bulge is at least a
                        // pixel and a half across. Each keeps its light.
                        float px = max(i.gal.w, 1.0);
                        float slant = max(open, min(1.0, 4.0 / px));
                        float2 e = float2(qx, qy / slant);
                        float re = length(e);
                        float tanP = 0.2 + 0.25 * i.gal.z;
                        float th = atan2(e.y, e.x) + i.gal.y * 6.2831853;
                        float arms = pow(saturate(0.5 + 0.5 * cos(2.0 * (th - log(max(re, 0.02)) / tanP))), 2.5);
                        arms = lerp(0.34, arms, saturate((re * px * 3.14159 * tanP - 2.0) * 0.5));
                        float disc = exp(-re / 0.22) * (0.25 + 1.2 * arms) * saturate(1.0 - re);
                        disc *= 1.0 - 0.55 * (1.0 - open) * saturate(0.028 * px - 0.5) * exp(-qy * qy / 0.0016);
                        disc /= max(slant, 0.3);
                        float by = qy / max(slant, 0.6);
                        float bs = max(0.012, (1.5 / px) * (1.5 / px));
                        float bulge = exp(-(qx * qx + by * by) / bs) * (0.012 / bs);
                        g = (i.bulgeRgb * bulge * 1.3 + i.color.rgb * disc) * (total / 0.164);
                    }
                    else if (kind < _ShapeScreen.w)
                    {
                        // An elliptical: smooth, bright in the middle, flattened by its own amount.
                        float flatness = lerp(0.55, 1.0, frac(kind * 7.3));
                        float fy = qy / flatness;
                        float rr = qx * qx + fy * fy;
                        float light = (exp(-rr / 0.012) * 0.7 + exp(-rr / 0.09) * 0.45) * saturate(1.0 - sqrt(rr));
                        g = lerp(i.color.rgb, i.bulgeRgb, 0.5) * light * (total / 0.094);
                    }
                    else
                    {
                        // An irregular: a few knots of star formation in a faint haze.
                        float light = 0.35 * exp(-dot(q, q) / 0.1);
                        [unroll]
                        for (int k = 0; k < 4; k++)
                        {
                            float a = 6.2831853 * frac(kind * 13.7 + k * 0.37);
                            float d = 0.1 + 0.4 * frac(kind * 7.1 + k * 0.61);
                            float2 dq = q - float2(cos(a), sin(a)) * d;
                            float s = 0.006 + 0.012 * frac(kind * 3.3 + k * 0.29);
                            light += exp(-dot(dq, dq) / s) * (0.6 + 0.4 * frac(kind * 5.9 + k * 0.83));
                        }
                        light *= saturate(1.0 - r);
                        g = i.color.rgb * light * (total / 0.148);
                    }

                    rgb = lerp(rgb, g, i.gal.x);
                }

                return float4(rgb * i.color.a, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
