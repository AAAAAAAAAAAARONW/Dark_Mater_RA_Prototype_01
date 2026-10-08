Shader "Custom/QuasarJet_NEW"
{
    // A quasar's two jets (QuasarVFX_NEW), as a volume: right from the side, end on, and from
    // inside, where it is a tunnel of light.
    //
    // The mesh is a box round both jets (±1 in each axis, scaled to _Extent), drawn from its
    // back faces so it still draws with the camera inside it. Each pixel takes its ray through
    // the box and adds up the light along it: a narrow white heart, and a wider blue sheath
    // that is a hollow tube — brightest at its walls, so from inside it is a tunnel and not a
    // fog. Along the jet: brightest at the base, fading to its tip, with helical filaments
    // (QuasarNoise_NEW's ridged channel, wound round the axis), bright knots running out, and
    // after an eruption a blob of light thrown out along it.
    //
    // The ray is cut where it crosses planes square to the axis, and across each slab the
    // heart's and the sheath's light is integrated exactly — a Gaussian across the jet,
    // integrated along a straight ray, is an error function — so a jet a pixel wide seen side
    // on and a mile long seen end on both come out smooth. Near the camera the heart and the
    // sheath are left out: the heart is a line seen from outside, and from inside the sheath
    // would sit in front of everything as a haze.
    //
    // Two copies draw it: the far side of the disc before the disc (_Side -1), so the disc's
    // dust darkens it, and the near side after (_Side 1). Which side is which is worked out
    // from the camera, per pixel. The far jet is dimmer (_CounterJet): its light is beamed
    // away from us.
    //
    // Additive. _Color fades it. In Resources so a build always has it.

    Properties
    {
        _Color ("Fade (all four channels, as WorldSwitcher_NEW sets it)", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _Noise ("Noise (QuasarNoise_NEW)", 2D) = "gray" {}
        _Extent ("Half size of the box, in disc radii", Vector) = (0.23, 2.6, 0.23, 0)
        _Length ("Length of each jet, in disc radii", Float) = 2.6
        _Width ("Sheath width at the tip (of the length)", Float) = 0.035
        [HDR] _Core ("Heart", Color) = (2.6, 2.9, 3.2, 1)
        [HDR] _Sheath ("Sheath", Color) = (0.35, 0.55, 1.1, 1)
        _Side ("Which side: 1 the camera's, -1 the other", Float) = 1
        _CounterJet ("The far jet, against the near", Range(0, 1)) = 0.5
        _Hollow ("How hollow the sheath is", Range(0, 0.95)) = 0.6
        _SheathBright ("Sheath brightness", Float) = 0.5
        _HeartCut ("Heart left out near the camera, in heart widths", Float) = 40
        _StrandFloor ("Light between the filaments", Float) = 0.15
        _TwistTurns ("Filaments' turns along a jet", Float) = 1.5
        _StrandFreq ("Filament breaks along a jet", Float) = 6
        _StrandFlow ("Filaments' speed, lengths a second", Float) = 0.15
        _KnotCount ("Knots along a jet", Float) = 6
        _KnotFlow ("Knots' speed, lengths a second", Float) = 0.35
        _EruptSpeed ("Eruption blob speed, lengths a second", Float) = 0.25
        _EruptDecay ("Eruption blob fade, seconds", Float) = 2.5
        _MaxBright ("Brightest it gets", Float) = 5
        _Steps ("Slabs along a jet", Range(8, 48)) = 32
        _HoleRadius ("Black hole radius (disc radii); 0 = none", Float) = 0
        _HoleNear ("Hole gone within this far, x2 (disc radii)", Float) = 0.45
        _HoleJet ("How much of the jet shows over the hole", Range(0, 1)) = 0.12

        // Set every frame by QuasarVFX_NEW.
        _Pace ("Clock rate", Float) = 1
        _PaceOffset ("Clock offset", Float) = 0
        _Bright ("Brightness", Float) = 1
        _FlareAge ("Seconds since the eruption", Float) = 1000
        _FlareGain ("Eruption strength", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent+2" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Front

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            #define TAU 6.2831853
            #define SQRT_PI 1.7724539

            sampler2D _Noise;
            fixed4 _Color;
            float4 _Extent;
            float _Length, _Width, _Side, _CounterJet, _Hollow, _SheathBright, _HeartCut, _StrandFloor;
            float _TwistTurns, _StrandFreq, _StrandFlow, _KnotCount, _KnotFlow, _EruptSpeed, _EruptDecay;
            float _MaxBright, _Steps, _HoleRadius, _HoleNear, _HoleJet;
            float _Pace, _PaceOffset, _Bright, _FlareAge, _FlareGain;
            float4 _Core, _Sheath;

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float3 box    : TEXCOORD0;   // this back face, in the box's ±1
                float3 camera : TEXCOORD1;   // the camera, in the box's ±1
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.box = vertex.xyz;
                o.camera = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                return o;
            }

            float Sq(float x) { return x * x; }

            // erf, to about 1e-4 (Winitzki).
            float Erf(float x)
            {
                float x2 = x * x;
                float e = sqrt(1.0 - exp(-x2 * (1.2732395 + 0.147 * x2) / (1.0 + 0.147 * x2)));
                return x < 0.0 ? -e : e;
            }

            // exp(-rho^2 / sigma^2) integrated along the ray from a to b, measured from the
            // ray's closest approach to the axis (rho0^2 there; sinT the ray's angle to it).
            float Gauss(float rho02, float sinT, float a, float b, float sigma)
            {
                if (sinT * (b - a) < 1e-3 * sigma)
                {
                    float m = (a + b) * 0.5 * sinT;
                    return exp(-(rho02 + m * m) / (sigma * sigma)) * (b - a);
                }
                return exp(-rho02 / (sigma * sigma)) * (sigma / sinT) * (SQRT_PI * 0.5)
                     * (Erf(b * sinT / sigma) - Erf(a * sinT / sigma));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // The ray, in the quasar's own space (disc radii): from the camera through this
                // back face.
                float3 ro = i.camera * _Extent.xyz;
                float3 rd = normalize(i.box * _Extent.xyz - ro);

                // Where it is inside the box; from the camera, if the camera is in it.
                float3 inv = 1.0 / (abs(rd) > 1e-7 ? rd : (rd >= 0.0 ? 1e-7 : -1e-7));
                float3 t0s = (-_Extent.xyz - ro) * inv;
                float3 t1s = (_Extent.xyz - ro) * inv;
                float3 tmin = min(t0s, t1s), tmax = max(t0s, t1s);
                float tNear = max(max(tmin.x, tmin.y), max(tmin.z, 0.0));
                float tFar = min(min(tmax.x, tmax.y), tmax.z);
                if (tFar <= tNear) return 0;

                // Only this copy's side of the disc's plane: the camera's (_Side 1) or the other.
                // The whole budget of segments goes to it.
                float cameraSide = ro.y >= 0.0 ? 1.0 : -1.0;
                float wanted = cameraSide * _Side;
                float tPlane = abs(rd.y) > 1e-7 ? -ro.y / rd.y : -1.0;
                if (tPlane > tNear && tPlane < tFar)
                {
                    bool startsWanted = (ro.y + rd.y * tNear) * wanted >= 0.0;
                    if (startsWanted) tFar = tPlane; else tNear = tPlane;
                }
                else if ((ro.y + rd.y * (tNear + tFar) * 0.5) * wanted < 0.0)
                {
                    return 0;
                }

                // The ray against the axis (local Y): its angle, and where it passes closest.
                float sin2 = rd.x * rd.x + rd.z * rd.z;
                float sinT = sqrt(sin2);
                float tStar = sin2 > 1e-10 ? -(ro.x * rd.x + ro.z * rd.z) / sin2 : tNear;
                float2 closest = ro.xz + rd.xz * tStar;
                float rho02 = sin2 > 1e-10 ? dot(closest, closest) : dot(ro.xz, ro.xz);

                float T = _Time.y * _Pace + _PaceOffset;
                float L = _Length;
                float blob = _FlareGain * exp(-_FlareAge / _EruptDecay);
                float blobAt = _FlareAge * _EruptSpeed;
                float blobWidth = 0.03 + 0.04 * _FlareAge;

                // The segments are slabs of the jet between planes square to its axis — the same
                // planes for every ray, so neighbouring pixels are cut alike and nothing seams
                // where one ray leaves the box by its end and the next by its side. Side on, a ray
                // stays in one slab, where the integral across the jet is exact anyway; end on, it
                // crosses as many as there are.
                int n = (int)_Steps;
                float h = L / n;
                float A = wanted * ro.y, B = wanted * rd.y;   // the distance out along the jet: A + B t
                float yNear = A + B * tNear, yFar = A + B * tFar;
                float sLow = min(yNear, yFar) / L;
                float sHigh = min(max(yNear, yFar) / L, 1.0);
                if (sLow >= 1.0) return 0;
                int first = (int)floor(sLow * n);
                int last = min(n - 1, max(first, (int)ceil(sHigh * n) - 1));
                float3 sum = 0.0;

                [loop]
                for (int j = 0; j < 48; j++)
                {
                    int k = first + j;
                    if (k > last) break;

                    float ta = tNear, tb = tFar;
                    if (abs(B) > 1e-7)
                    {
                        float u0 = (k * h - A) / B, u1 = ((k + 1) * h - A) / B;
                        ta = max(tNear, min(u0, u1));
                        tb = min(tFar, max(u0, u1));
                    }
                    if (tb <= ta) continue;

                    // The segment's point nearest the axis stands for it.
                    float tm = clamp(tStar, ta, tb);
                    float3 p = ro + rd * tm;
                    float s = abs(p.y) / L;
                    if (s >= 1.0) continue;

                    float sigmaS = _Width * L * (0.22 + 0.78 * s);
                    float sigmaH = sigmaS * 0.16;

                    // Near the camera neither is integrated (see the top).
                    float ha = max(ta, _HeartCut * sigmaH);
                    float sa = max(ta, 0.5 * sigmaS);
                    float heart = ha < tb ? Gauss(rho02, sinT, ha - tStar, tb - tStar, sigmaH) / (sigmaH * SQRT_PI) : 0.0;
                    float sheath = sa < tb
                        ? (Gauss(rho02, sinT, sa - tStar, tb - tStar, sigmaS)
                           - _Hollow * Gauss(rho02, sinT, sa - tStar, tb - tStar, sigmaS * 0.6))
                          / (sigmaS * SQRT_PI * (1.0 - _Hollow * 0.6)) * _SheathBright
                        : 0.0;

                    // Brightest at its base, where it is launched, fading out along it to a
                    // long faint tail — not a beam of even light from end to end.
                    float fall = (0.3 + 0.7 * exp(-s / 0.22)) * pow(1.0 - s, 1.2) * smoothstep(0.0, 0.015, s);

                    // Filaments wound round the axis, and the knots running out along it,
                    // averaged over the stretch of jet the segment covers so they never alias.
                    float phi = atan2(p.z, p.x);
                    float rho = length(p.xz);
                    float strands = tex2Dlod(_Noise, float4(phi / TAU * 3.0 + s * _TwistTurns,
                                                            s * _StrandFreq - T * _StrandFlow + rho / sigmaS * 0.12, 0.0, 1.0)).b;
                    float ds = abs(rd.y) * (tb - ta) / L;
                    float x = 3.14159265 * ds * _KnotCount;
                    float filter = x > 1e-4 ? sin(x) / x : 1.0;
                    float knots = 0.5 + 0.5 * cos(TAU * (s * _KnotCount - T * _KnotFlow)) * filter;

                    // The eruption's blob, averaged over the stretch too.
                    float b = 0.0;
                    if (blob > 1e-3)
                    {
                        float lo = s - ds * 0.5 - blobAt, hi = s + ds * 0.5 - blobAt;
                        b = ds > 1e-5
                            ? (Erf(hi / blobWidth) - Erf(lo / blobWidth)) * blobWidth * (SQRT_PI * 0.5) / ds
                            : exp(-Sq((s - blobAt) / blobWidth));
                        b *= blob;
                    }

                    float side = _Side > 0.0 ? 1.0 : _CounterJet;
                    float heartLight = heart * (0.55 + 0.9 * knots + 5.0 * b);
                    float sheathLight = sheath * (_StrandFloor + 2.4 * strands * strands) * (0.7 + 0.6 * knots + 3.0 * b);
                    sum += (_Core.rgb * heartLight + _Sheath.rgb * sheathLight) * fall * side;
                }

                // Softly to a ceiling: end on, the heart adds up along the whole jet.
                float3 light = _MaxBright * (1.0 - exp(-sum / _MaxBright)) * _Bright;

                // Over the black hole's shadow the jet is held to a glimmer: looking down it, as
                // the tutorial does, the whole jet adds up over the core and would fill the hole.
                // The ray passes through the shadow where it comes within its radius of the middle.
                if (_HoleRadius > 0.0)
                {
                    float miss = length(cross(ro, rd));
                    float seen = saturate(length(ro) / max(_HoleNear, 1e-4) - 1.0) * step(0.0, -dot(ro, rd));
                    float over = 1.0 - smoothstep(_HoleRadius * 0.95, _HoleRadius * 1.1, miss);
                    light *= lerp(1.0, _HoleJet, over * seen);
                }

                // WorldSwitcher_NEW fades by scaling all four channels of _Color; additive, so
                // rgb alone is the fade.
                return fixed4(light * _Color.rgb, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
