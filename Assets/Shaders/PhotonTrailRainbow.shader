Shader "Custom/PhotonTrail"
{
    Properties
    {
        // Base
        _Color ("Color", Color) = (1,1,1,1)
        [NoScaleOffset] _MainTex ("Texture (optional)", 2D) = "white" {}

        // Spectrum
        [Toggle] _UseSpectrum ("Use Spectrum Gradient", Float) = 1
        _SpectrumTex ("Spectrum Texture", 2D) = "white" {}
        _SpectrumOffset ("Spectrum UV Offset", Range(0,1)) = 0
        _SpectrumScale  ("Spectrum UV Scale",  Range(0.1, 3)) = 1

        // IR / UV glow extras
        _IRGlowStrength  ("IR Glow Strength",  Range(0, 5)) = 1.2
        _UVGlowStrength  ("UV Glow Strength",  Range(0, 5)) = 1.8
        _IRGlowColor     ("IR Glow Color",  Color) = (0.6, 0.05, 0.0, 1)
        _UVGlowColor     ("UV Glow Color",  Color) = (0.45, 0.0, 0.9, 1)

        // Visible band highlight — added for the tutorial's "this is the light your eyes
        // can see" moment. Brightens only the wavelengths between the two edges.
        // Highlight 0 (the default) changes nothing, so existing materials look the same.
        _VisibleBandStart ("Visible Band Start",     Range(0, 1)) = 0.15
        _VisibleBandEnd   ("Visible Band End",       Range(0, 1)) = 0.85
        _VisibleHighlight ("Visible Band Highlight", Range(0, 3)) = 0

        // Glow
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 1.31
        _GlowFalloff   ("Glow Falloff",   Range(0.1, 10)) = 5.29

        // Emission
        _EmissionColor    ("Emission Color",    Color) = (0.3, 0.8, 1, 1)
        _EmissionStrength ("Emission Strength", Range(0, 5)) = 2.2
        _EmissionSoftness ("Emission Softness", Range(0.1, 5)) = 1.4

        // Edge Fade
        _EdgeFadeWidth  ("Edge Fade Width",  Range(0, 0.5)) = 0.15
        _EdgeFadePower  ("Edge Fade Power",  Range(0.1, 5)) = 2.0
        _EdgeSoftness   ("Edge Softness",    Range(0.1, 5)) = 1.3
        _EndFadeWidth   ("End Fade Width",   Range(0, 0.5)) = 0.12

        // Head indicator
        [Toggle] _ShowHead ("Show Head", Float) = 1
        _HeadWidth      ("Head Width",      Range(0, 0.5)) = 0.04
        _HeadColor      ("Head Color",      Color) = (1, 1, 1, 1)
        _HeadBrightness ("Head Brightness", Range(0, 3)) = 1.8

        // Lyman-alpha absorption lines
        [Toggle] _UseAbsorptionLine ("Use Absorption Lines", Float) = 0
        _AbsorptionLineTex ("Absorption Line Texture", 2D) = "black" {}
        _AbsorptionLineStrength ("Absorption Line Strength", Range(0, 1)) = 1.0
        _AbsorptionLineWidth ("Line Softness", Range(0.5, 4.0)) = 1.5

        // Widens the dark line ACROSS THE RIBBON ONLY — added for the tutorial, where
        // the ribbon is a fifth as wide on screen as the spectrum bar and a line that
        // reads as six pixels on the bar is one on the light.
        // Spread 0 (the default) changes nothing, so existing materials look the same.
        _AbsorptionLineSpread ("Absorption Line Spread", Range(0, 0.06)) = 0

        // 0 keeps the buffer's soft falloff; 1 cuts each line as a solid band with a clean
        // edge. 0 (the default) changes nothing, so existing materials look the same.
        _AbsorptionLineHardness ("Absorption Line Hardness", Range(0, 1)) = 0

        // How BLACK an absorbed line is, 0 to 1. The ribbon is additive, so on its own an
        // absorbed wavelength only stops adding light — the line shows whatever is behind
        // the trail, which reads as a hole, not as black. Above 0, the first pass paints the
        // line black over the background before the ribbon is added. 0 (the default) draws
        // nothing, so existing materials look the same.
        _AbsorptionBlack ("Absorption Line Black", Range(0, 1)) = 0
        _AbsorbThroughGlow ("Absorption Cuts Glow", Range(0, 1)) = 0

        // A bright rim either side of every absorbed line, so a thin black line still reads
        // against a dim ribbon. Width is in the same wavelength units as the spread.
        // Width 0 (the default) draws nothing, so existing materials look the same.
        _AbsorptionOutlineWidth    ("Absorption Outline Width",    Range(0, 0.03)) = 0
        _AbsorptionOutlineStrength ("Absorption Outline Strength", Range(0, 4)) = 1
        _AbsorptionOutlineColor    ("Absorption Outline Color",    Color) = (1, 0.91, 0.63, 1)
        // Limit the outline to one line: only within Range of Center (both in the same
        // wavelength units as the line texture). Range 0 (the default) outlines every line.
        _AbsorptionOutlineCenter   ("Absorption Outline Center",   Range(0, 1)) = 0.5
        _AbsorptionOutlineRange    ("Absorption Outline Range",    Range(0, 0.5)) = 0

        // ── Look (TrailLook_NEW drives these in the journey; 0 everywhere else = unchanged) ──
        // Tube: the cross-section lit like a round beam (brightness ~ sqrt(1 - x^2)) with a soft
        // gaussian edge, instead of a flat band with a fade at its rim.
        _Tube ("Tube Profile", Range(0, 1)) = 0
        // Head cap edge: 0 = the old hard clip, 1 = a soft round end.
        _HeadSoft ("Head Cap Softness", Range(0, 1)) = 0
        // Flow: a faint brightness ripple anchored in the world and running back along the
        // trail. _TrailLength and _FlowDistance are written every frame by TrailLook_NEW.
        _FlowAmount     ("Flow Amount",               Range(0, 0.5)) = 0
        _FlowWavelength ("Flow Wavelength (world)",   Float) = 5
        _FlowSpeed      ("Flow Speed",                Float) = 2.2
        _TrailLength    ("Trail Length (set by script)",  Float) = 1
        _FlowDistance   ("Distance Flown (set by script)", Float) = 0

        // Beam: the mesh is a round tube (TrailFacing_NEW) and uv.y, the spectrum coordinate, is
        // worked out per vertex from the view: where this point of the tube sits, left to right,
        // across the beam as seen on screen. 0 = the usual ribbon, uv.y from the mesh.
        _Beam     ("Beam (set by script)", Float) = 0
        // Beam brightness. The beam does not use the ribbon's emission, centre glow or UV/IR
        // edge glow: tuned for a flat band, they piled up into a white blob down the middle
        // and a wide violet stripe. It is the spectrum, times how thick the light is.
        _BeamBrightness ("Beam Brightness", Range(0, 4)) = 1.0
        // Share of the trail's length (from the head) over which the beam fades in, so its
        // tip is soft rather than a hard polygon. Set by TrailFacing_NEW from the width.
        _BeamHeadFade ("Beam Head Fade (set by script)", Float) = 0
        _BeamSide ("Beam Side (set by script)", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0

        // Soft beam (Vizlab_Journey, TrailLook_NEW): a soft rainbow-like light on a camera-facing
        // ribbon. Colours from _SoftPalette (CIE-based, laid on the same wavelength axis as the
        // spectrum texture, brightness in alpha), blurred a little across; no rim (it fades to
        // nothing at both edges); slow drifts of brightness along the flight; narrows into the
        // head over _SoftConverge world units; fades out close to the camera. 0 = unchanged.
        _Soft           ("Soft Beam (set by script)", Float) = 0
        _SoftPalette    ("Soft Beam Palette (set by script)", 2D) = "black" {}
        _SoftBrightness ("Soft Beam Brightness", Range(0, 4)) = 1.9
        // Values from the Photon Beam Tuner page (2026-11-22).
        _SoftFlux       ("Soft Beam Flux (set by script)", 2D) = "white" {}
        _SoftFluxAmount ("Soft Beam: follow the real flux", Range(0, 1)) = 1
        _SoftMargin     ("Soft Beam: far-UV margin (set by script)", Float) = 0.1
        _SoftDisperse   ("Soft Beam: white light spreads over (set by script)", Float) = 5
        _SoftWhite      ("Soft Beam: white at the head", Range(0, 1)) = 1
        _SoftWhiteCover ("Soft Beam: white across the whole width", Range(0, 1)) = 1
        _SoftWarm       ("Soft Beam: head warmth (0 white, 1 warm yellow)", Range(0, 1)) = 0.3
        _SoftHeadBright ("Soft Beam: head brightness", Range(0.2, 2)) = 0.75
        _SoftRound      ("Soft Beam: head start width", Range(0, 1)) = 0
        _SoftHeadFade   ("Soft Beam: head fade-in (set by script)", Float) = 0
        _SoftGas        ("Soft Beam: gas texture amount", Range(0, 0.6)) = 0.04
        _SoftGasScale   ("Soft Beam: gas scale", Range(0.05, 3)) = 0.3
        _SoftGasSpeed   ("Soft Beam: gas speed", Range(0, 2)) = 0.4
        _SoftWisp       ("Soft Beam: edge spread", Range(0, 1)) = 0.5
        _SoftBleed      ("Soft Beam: colour bleed", Range(0, 1)) = 0.5
        _SoftTail       ("Soft Beam: tail fades out", Range(0, 1.5)) = 0.3
        // Tutorial (PC_Tutorial): colours from the spectrum texture the tutorial already paints,
        // and its teaching effects (band unfolding, visible flash, UV/IR glow, line outline),
        // driven by the properties TutorialTrailBands_NEW already writes. 0 in the journey.
        _SoftUseSpecTex ("Soft Beam: colours from the spectrum texture (set by script)", Float) = 0
        _SoftTeach      ("Soft Beam: tutorial teaching effects (set by script)", Float) = 0
        _SoftSaturation ("Soft Beam: saturation", Range(0, 2)) = 1.3
        _SoftEdge       ("Soft Beam: edge softness", Range(0.02, 0.45)) = 0.19
        _SoftConverge   ("Soft Beam Converge (set by script)", Float) = 1.5
        _SoftDistance   ("Soft Beam Distance (set by script)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull [_Cull]

        // ── Pass 1: the absorbed wavelengths, painted black ──────────────────────────
        //
        // WHY A SECOND PASS. The ribbon below is additive — Blend SrcAlpha One — which can
        // only ever ADD light to the frame. An absorbed wavelength there contributes
        // nothing, so the "black" line was really a gap showing whatever was behind the
        // trail: the quasar, the nebula, the stars. It read as transparent, not as black.
        //
        // Additive blending cannot darken, so this pass does it: ordinary alpha blending,
        // black, only where there is absorption, and nothing at all anywhere else. It runs
        // first, so the ribbon is then added on top — and at an absorbed wavelength the
        // ribbon adds nothing, so the black stays black.
        //
        // The math is the ribbon's own, line for line: the same wavelength coordinate, the
        // same spread and hardness, the same edge, tail and head fades, the same vertex
        // alpha. The black stripe therefore lands exactly in the gap the ribbon leaves and
        // fades out where the ribbon fades out, instead of drawing a black line across
        // empty space past the end of the trail.
        //
        // _AbsorptionBlack 0 (the default) returns alpha 0 everywhere: the pass draws
        // nothing and every existing material looks exactly as it did.
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex   vertBlack
            #pragma fragment fragBlack
            #pragma target   3.0

            #include "UnityCG.cginc"

            float _Beam;
            float _BeamSide;
            float _Soft;

            // Beam mode: the spectrum coordinate from the view. Across the tube as seen on screen
            // is (trail direction x direction to the eye); this vertex's place on it is its
            // normal along that, -1..1, so uv.y runs 0..1 from one side to the other.
            float BeamV(float4 vertex, float3 normal, float4 tangent)
            {
                float3 wp    = mul(unity_ObjectToWorld, vertex).xyz;
                float3 n     = UnityObjectToWorldNormal(normal);
                float3 t     = UnityObjectToWorldDir(tangent.xyz);
                float3 toEye = normalize(_WorldSpaceCameraPos - wp);
                float3 side  = cross(t, toEye);
                float  sl    = length(side);
                side = sl > 1e-4 ? side / sl : float3(0, 1, 0);
                return saturate(0.5 + 0.5 * _BeamSide * dot(n, side));
            }

            float4    _MainTex_ST;
            float     _SpectrumOffset;
            float     _SpectrumScale;

            float     _EdgeFadeWidth;
            float     _EdgeFadePower;
            float     _EdgeSoftness;
            float     _EndFadeWidth;

            float     _ShowHead;
            float     _HeadWidth;

            float     _UseAbsorptionLine;
            sampler2D _AbsorptionLineTex;
            float     _AbsorptionLineStrength;
            float     _AbsorptionLineSpread;
            float     _AbsorptionLineHardness;
            float     _AbsorptionBlack;
            float     _Tube;
            float     _HeadSoft;

            struct appdataBlack
            {
                float4 vertex  : POSITION;
                float2 uv      : TEXCOORD0;
                float4 color   : COLOR;
                float3 normal  : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2fBlack
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                float4 color : COLOR;
            };

            v2fBlack vertBlack(appdataBlack v)
            {
                v2fBlack o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = TRANSFORM_TEX(v.uv, _MainTex);
                if (_Beam > 0.5) o.uv.y = BeamV(v.vertex, v.normal, v.tangent);
                o.color = v.color;
                return o;
            }

            fixed4 fragBlack(v2fBlack i) : SV_Target
            {
                if (_Soft > 0.5 || _AbsorptionBlack <= 0.0001 || _UseAbsorptionLine < 0.5) return fixed4(0, 0, 0, 0);

                float2 uv = i.uv;
                float specT = frac(((1.0 - uv.y) + _SpectrumOffset) * _SpectrumScale);

                // Absorption — identical to the ribbon pass.
                float lineStrength = tex2Dlod(_AbsorptionLineTex, float4(specT, 0.5, 0, 0)).r;

                if (_AbsorptionLineSpread > 0.0001)
                {
                    float tapStep = _AbsorptionLineSpread * 0.125;

                    for (int tap = 1; tap <= 8; tap++)
                    {
                        float d = tapStep * tap;
                        lineStrength = max(lineStrength,
                                           tex2Dlod(_AbsorptionLineTex, float4(specT - d, 0.5, 0, 0)).r);
                        lineStrength = max(lineStrength,
                                           tex2Dlod(_AbsorptionLineTex, float4(specT + d, 0.5, 0, 0)).r);
                    }
                }

                float solid = smoothstep(0.06, 0.22, lineStrength);
                lineStrength = lerp(lineStrength, solid, _AbsorptionLineHardness);

                float absorp = saturate(lineStrength * _AbsorptionLineStrength);

                // The ribbon's footprint — identical fades, so the black ends where it ends.
                float edgeY    = min(uv.y, 1.0 - uv.y);
                float edgeFade = saturate(edgeY / max(_EdgeFadeWidth, 0.0001));
                edgeFade = pow(edgeFade, _EdgeFadePower);
                edgeFade = smoothstep(0, _EdgeSoftness * 0.2, edgeFade);
                {
                    float c = abs(uv.y - 0.5) * 2.0;
                    edgeFade = lerp(edgeFade, exp(-pow(c, 2.4) * 2.4) * smoothstep(1.0, 0.85, c), _Tube);
                    if (_Beam > 0.5)
                    {
                        float thick = sqrt(saturate(1.0 - c * c));
                        edgeFade = thick * smoothstep(0.0, 0.35, thick);
                    }
                }

                float endFade = saturate((1.0 - uv.x) / max(_EndFadeWidth, 0.0001));

                float headClip = 1.0;
                if (_ShowHead > 0.5)
                {
                    float headLocal = saturate(1.0 - uv.x / max(_HeadWidth, 0.0001));
                    float centreY   = abs(uv.y - 0.5) * 2.0;
                    float dist      = sqrt(headLocal * headLocal + centreY * centreY);
                    headClip = saturate((1.0 - dist) * lerp(40.0, 2.5, _HeadSoft * saturate(headLocal * 8.0)));   // soft only inside the cap
                }

                float alpha = absorp * edgeFade * endFade * headClip * i.color.a * _AbsorptionBlack;

                return fixed4(0, 0, 0, saturate(alpha));
            }
            ENDCG
        }

        // ── Pass 2: the ribbon itself, additive — unchanged ──────────────────────────
        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "UnityCG.cginc"

            float _Beam;
            float _BeamSide;
            float _Soft;

            // Beam mode: the spectrum coordinate from the view. Across the tube as seen on screen
            // is (trail direction x direction to the eye); this vertex's place on it is its
            // normal along that, -1..1, so uv.y runs 0..1 from one side to the other.
            float BeamV(float4 vertex, float3 normal, float4 tangent)
            {
                float3 wp    = mul(unity_ObjectToWorld, vertex).xyz;
                float3 n     = UnityObjectToWorldNormal(normal);
                float3 t     = UnityObjectToWorldDir(tangent.xyz);
                float3 toEye = normalize(_WorldSpaceCameraPos - wp);
                float3 side  = cross(t, toEye);
                float  sl    = length(side);
                side = sl > 1e-4 ? side / sl : float3(0, 1, 0);
                return saturate(0.5 + 0.5 * _BeamSide * dot(n, side));
            }

            sampler2D _MainTex;
            float4    _MainTex_ST;
            sampler2D _SpectrumTex;

            float4 _Color;
            float  _UseSpectrum;
            float  _SpectrumOffset;
            float  _SpectrumScale;

            float4 _IRGlowColor;
            float4 _UVGlowColor;
            float  _IRGlowStrength;
            float  _UVGlowStrength;

            float  _VisibleBandStart;
            float  _VisibleBandEnd;
            float  _VisibleHighlight;

            float  _GlowIntensity;
            float  _GlowFalloff;

            float4 _EmissionColor;
            float  _EmissionStrength;
            float  _EmissionSoftness;

            float  _EdgeFadeWidth;
            float  _EdgeFadePower;
            float  _EdgeSoftness;
            float  _EndFadeWidth;

            float  _ShowHead;
            float  _HeadWidth;
            float4 _HeadColor;
            float  _HeadBrightness;

            float  _UseAbsorptionLine;
            sampler2D _AbsorptionLineTex;
            float  _AbsorptionLineStrength;
            float  _AbsorptionLineWidth;
            float  _AbsorptionLineSpread;
            float  _AbsorptionLineHardness;
            float  _AbsorbThroughGlow;
            float  _AbsorptionOutlineWidth;
            float  _AbsorptionOutlineStrength;
            float4 _AbsorptionOutlineColor;
            float  _AbsorptionOutlineCenter;
            float  _AbsorptionOutlineRange;
            float  _Tube;
            float  _HeadSoft;
            float  _FlowAmount;
            float  _FlowWavelength;
            float  _FlowSpeed;
            float  _TrailLength;
            float  _FlowDistance;
            float  _BeamBrightness;
            sampler2D _SoftPalette;
            float  _SoftBrightness;
            float  _SoftConverge;
            float  _SoftDistance;
            sampler2D _SoftFlux;
            float  _SoftFluxAmount;
            float  _SoftMargin;
            float  _SoftDisperse;
            float  _SoftWhite;
            float  _SoftSaturation;
            float  _SoftEdge;
            float  _SoftWhiteCover, _SoftWarm, _SoftHeadBright, _SoftRound, _SoftHeadFade;
            float  _SoftGas, _SoftGasScale, _SoftGasSpeed, _SoftWisp, _SoftBleed, _SoftTail;
            float  _SoftUseSpecTex, _SoftTeach;

            float SoftHash(float3 p) { return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453); }
            float SoftNoise(float3 p)
            {
                float3 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(SoftHash(i), SoftHash(i + float3(1, 0, 0)), f.x), lerp(SoftHash(i + float3(0, 1, 0)), SoftHash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(SoftHash(i + float3(0, 0, 1)), SoftHash(i + float3(1, 0, 1)), f.x), lerp(SoftHash(i + float3(0, 1, 1)), SoftHash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }
            // Two octaves of soft gas, fixed to where the light has been, drifting slowly.
            float SoftGasAt(float s, float across)
            {
                float3 q = float3(s * _SoftGasScale, across * _SoftGasScale * 1.6, _Time.y * _SoftGasSpeed * 0.3);
                return SoftNoise(q) * 0.65 + SoftNoise(q * 2.3 + 7.0) * 0.35;
            }

            // The quasar's flux now, 0..1, at spectrum-texture place st (bar axis after the margin).
            float SoftFluxAt(float st)
            {
                return tex2Dlod(_SoftFlux, float4(saturate((st - _SoftMargin) / max(1.0 - _SoftMargin, 1e-3)), 0.5, 0, 0)).r;
            }
            float  _BeamHeadFade;

            // The tube's edge: a gaussian, taken to exactly zero at the rim.
            float TubeEdge(float c) { return exp(-pow(c, 2.4) * 2.4) * smoothstep(1.0, 0.85, c); }

            struct appdata
            {
                float4 vertex  : POSITION;
                float2 uv      : TEXCOORD0;
                float4 color   : COLOR;
                float3 normal  : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float4 pos     : SV_POSITION;
                float2 uv      : TEXCOORD0;
                float4 color   : COLOR;
                float  camDist : TEXCOORD1;
            };

            float hash(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            float smoothNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(hash(i),               hash(i + float2(1,0)), f.x),
                    lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), f.x),
                    f.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = TRANSFORM_TEX(v.uv, _MainTex);
                if (_Beam > 0.5) o.uv.y = BeamV(v.vertex, v.normal, v.tangent);
                o.color = v.color;
                o.camDist = distance(_WorldSpaceCameraPos, mul(unity_ObjectToWorld, v.vertex).xyz);
                return o;
            }

            // The spectrum-texture coordinate for place t across the ribbon (mirroring included).
            float SpecT(float t) { return frac(((1.0 - t) + _SpectrumOffset) * _SpectrumScale); }

            fixed4 SoftBeam(v2f i)
            {
                float2 uv = i.uv;
                float fromHead = uv.x * _TrailLength;
                float s = _SoftDistance - fromHead;                                // fixed in the world
                // From the head's start width out to the full width along a quarter circle: no crease.
                float cx = saturate(fromHead / max(_SoftConverge, 0.001));
                float spread = max(lerp(_SoftRound, 1.0, sqrt(cx * (2.0 - cx))), 0.03);
                float v = (uv.y - 0.5) / spread + 0.5;                             // place across the light
                float gasN = SoftGasAt(s, (uv.y - 0.5) * spread);
                // The edge moves with the gas a little, so the outline is not a perfect curve.
                float vE = v + (gasN - 0.5) * _SoftWisp * 0.22;
                if (vE <= 0.0 || vE >= 1.0) return fixed4(0, 0, 0, 0);
                v = saturate(v);

                // Colour, blurred across, more at the edges and towards the tail; brightness from the real flux.
                float blur = 0.012 * (1.0 + _SoftBleed * (abs(v - 0.5) * 4.0 + uv.x * 4.0));
                float3 col = 0; float wsum = 0;
                for (int k = -3; k <= 3; k++)
                {
                    float w = exp(-(k * k) / 4.5);
                    float  st = SpecT(saturate(v + k * blur));
                    float4 p  = _SoftUseSpecTex > 0.5 ? tex2Dlod(_SpectrumTex, float4(st, 0.5, 0, 0))
                                                      : tex2Dlod(_SoftPalette, float4(st, 0.5, 0, 0));
                    float  fb = lerp(1.0, 0.3 + 0.95 * SoftFluxAt(st), _SoftFluxAmount * step(_SoftMargin, st));
                    col += p.rgb * p.a * fb * w; wsum += w;
                }
                col /= wsum;

                // Absorbed wavelengths give no light: soft dark gaps, as on the bar.
                float ab = 0;
                if (_UseAbsorptionLine > 0.5)
                {
                    float st = SpecT(v), d = max(_AbsorptionLineSpread * 0.5, 0.002);
                    ab = max(tex2Dlod(_AbsorptionLineTex, float4(st, 0.5, 0, 0)).r,
                             0.6 * max(tex2Dlod(_AbsorptionLineTex, float4(st - d, 0.5, 0, 0)).r,
                                       tex2Dlod(_AbsorptionLineTex, float4(st + d, 0.5, 0, 0)).r));
                    ab = lerp(ab, smoothstep(0.06, 0.22, ab), _AbsorptionLineHardness);
                    ab = saturate(ab * _AbsorptionLineStrength);
                }

                // The tutorial's teaching effects, as the ribbon draws them: UV/IR glow at the edges
                // (D6 redden), the visible band flashing (D2/D3), a rim of light round a line (D5).
                float3 teach = 0;
                if (_SoftTeach > 0.5)
                {
                    float st = SpecT(v);
                    float uvF = pow(saturate((0.18 - st) * 6.0), 2.0), irF = pow(saturate((st - 0.82) * 6.0), 2.0);
                    // Half the ribbon's strength: on the soft beam's open edges it read as a white veil.
                    col += (_UVGlowColor.rgb * uvF * _UVGlowStrength + _IRGlowColor.rgb * irF * _IRGlowStrength) * 0.15;
                    float vis = smoothstep(_VisibleBandStart - 0.004, _VisibleBandStart + 0.004, st)
                              * (1.0 - smoothstep(_VisibleBandEnd - 0.004, _VisibleBandEnd + 0.004, st));
                    float4 sc = tex2Dlod(_SpectrumTex, float4(st, 0.5, 0, 0));
                    float3 hue = sc.rgb / max(max(sc.r, max(sc.g, sc.b)), 0.001);
                    teach += hue * vis * _VisibleHighlight * 0.6 * (1.0 - ab);
                    if (_UseAbsorptionLine > 0.5 && _AbsorptionOutlineWidth > 0.0001)
                    {
                        float nearAbs = 0;
                        for (int o = 1; o <= 4; o++)
                        {
                            float od = _AbsorptionOutlineWidth * o * 0.25;
                            nearAbs = max(nearAbs, tex2Dlod(_AbsorptionLineTex, float4(st - od, 0.5, 0, 0)).r);
                            nearAbs = max(nearAbs, tex2Dlod(_AbsorptionLineTex, float4(st + od, 0.5, 0, 0)).r);
                        }
                        nearAbs = lerp(nearAbs, smoothstep(0.06, 0.22, nearAbs), _AbsorptionLineHardness);
                        float outline = saturate(nearAbs - ab) * _AbsorptionOutlineStrength;
                        if (_AbsorptionOutlineRange > 0.0001)
                            outline *= 1.0 - smoothstep(_AbsorptionOutlineRange * 0.5, _AbsorptionOutlineRange, abs(st - _AbsorptionOutlineCenter));
                        teach += _AbsorptionOutlineColor.rgb * outline;
                    }
                }

                float lum = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(lum.xxx, col, _SoftSaturation);
                // White (to warm) light at the head, giving way to the colours towards the camera.
                float dh = 1.0 - smoothstep(0.0, max(_SoftDisperse, 1e-3), fromHead);
                dh *= dh;
                float centre = 1.0 - abs(v - 0.5) * 2.0;
                float cover  = lerp(0.35 + 0.65 * centre, 1.0, _SoftWhiteCover);
                float3 headCol = lerp(float3(1, 1, 1), float3(1.0, 0.84, 0.58), _SoftWarm);
                col = lerp(col, headCol * lerp(0.55 + 0.45 * centre, 0.9, _SoftWhiteCover) * 0.85, dh * _SoftWhite * cover);

                float edge   = smoothstep(0.0, _SoftEdge, vE) * smoothstep(1.0, 1.0 - _SoftEdge, vE);   // no rim
                float c      = abs(uv.y - 0.5) * 2.0;
                float body   = lerp(0.65, 1.0, sqrt(saturate(1.0 - c * c)));     // a round beam
                float drift  = 0.8 + 0.2 * sin(s * 0.31416 + _Time.y * 0.35) * sin(s * 0.10472 - _Time.y * 0.21 + 1.3);
                float gasB   = max(1.0 + _SoftGas * (gasN * 2.0 - 1.0) * 1.4, 0.0);
                float fade   = pow(saturate(i.color.a), 1.1 + _SoftTail) * smoothstep(0.3, 1.4, i.camDist);
                float headIn = _SoftHeadFade > 1e-4 ? smoothstep(0.0, _SoftHeadFade, fromHead) : 1.0;
                float hb     = lerp(1.0, _SoftHeadBright, dh);
                return fixed4((col * (1.0 - ab) + teach) * edge * body * drift * gasB * fade * headIn * hb * _SoftBrightness, 1.0);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (_Soft > 0.5) return SoftBeam(i);
                float2 uv = i.uv;
                // Unity TrailRenderer UV convention:
                //   uv.x = along the LENGTH  (0 = head, 1 = tail)
                //   uv.y = across the WIDTH  (0 = one edge, 1 = other edge)
                //
                // Spectrum across WIDTH (uv.y):
                //   uv.y = 0 → UV (transparent violet)   ← REVERSED from before
                //   uv.y = 1 → IR (transparent red)

                // ── 1. Spectrum across WIDTH (uv.y), UV→IR direction ──────
                // Flip uv.y so uv.y=0 → IR end of texture, uv.y=1 → UV end
                // Texture is generated UV→IR (t=0=UV), so we invert here
                float specT = frac(((1.0 - uv.y) + _SpectrumOffset) * _SpectrumScale);
                // specT=0 → UV end, specT=1 → IR end
                float4 specColor = tex2D(_SpectrumTex, float2(specT, 0.5));
                float4 baseColor = lerp(_Color * i.color, specColor * i.color, _UseSpectrum);

                // UV glow at top edge (uv.y=1, specT=0), IR glow at bottom edge (uv.y=0, specT=1)
                float uvFactor = pow(saturate((0.18 - specT) * 6.0), 2.0);
                float irFactor = pow(saturate((specT - 0.82) * 6.0), 2.0);
                baseColor.rgb += _UVGlowColor.rgb * uvFactor * _UVGlowStrength * _UseSpectrum * 0.3;
                baseColor.rgb += _IRGlowColor.rgb * irFactor * _IRGlowStrength * _UseSpectrum * 0.3;

                // ── 1b. Visible band highlight ────────────────────────────
                // Same wavelength coordinate (specT) as the colours and the absorption
                // lines, so the edges land exactly on the band PhotonSpectrumTrail paints.
                // A very narrow smoothstep only to anti-alias the edge. Applied at the end,
                // in step 9 — see there for why.
                float visMask = smoothstep(_VisibleBandStart - 0.004, _VisibleBandStart + 0.004, specT)
                              * (1.0 - smoothstep(_VisibleBandEnd - 0.004, _VisibleBandEnd + 0.004, specT));

                // ── 2. Texture sample ─────────────────────────────────────
                float4 texSample = tex2D(_MainTex, uv);
                baseColor *= texSample;

                // ── 3. Glow — brightest at centre of width ────────────────
                float centreY  = abs(uv.y - 0.5) * 2.0;
                float glowMask = pow(saturate(1.0 - centreY), _GlowFalloff);
                float glow     = glowMask * _GlowIntensity;

                // ── 4. Soft edge fade across WIDTH ────────────────────────
                float edgeY    = min(uv.y, 1.0 - uv.y);
                float edgeFade = saturate(edgeY / max(_EdgeFadeWidth, 0.0001));
                edgeFade = pow(edgeFade, _EdgeFadePower);
                edgeFade = smoothstep(0, _EdgeSoftness * 0.2, edgeFade);
                edgeFade = lerp(edgeFade, TubeEdge(centreY), _Tube);

                // ── 5. Tail fade along LENGTH (uv.x) ─────────────────────
                float endFade = saturate((1.0 - uv.x) / max(_EndFadeWidth, 0.0001));

                // ── 6. Head — shader-driven semicircle cap ────────────────
                // Entirely in UV space — no geometry needed, looks round at any speed.
                //
                // The cap is a semicircle: pixels inside the circle are kept,
                // pixels outside are clipped. Center is at (uv.x=0, uv.y=0.5).
                //
                //   headLocal = 1 at the very tip, 0 at the end of the cap region
                //   centreY   = 0 at trail centre, 1 at edges
                //   dist      = distance from tip in UV space
                //   anything outside dist=1 gets clipped → semicircle shape
                float headGlow  = 0.0;
                float headAlpha = 1.0;
                float headClip  = 1.0;
                if (_ShowHead > 0.5)
                {
                    float headLocal  = saturate(1.0 - uv.x / max(_HeadWidth, 0.0001));
                    float centreY    = abs(uv.y - 0.5) * 2.0; // 0=centre, 1=edge

                    // Circular distance from tip — pure UV math, no geometry dependency
                    float dist = sqrt(headLocal * headLocal + centreY * centreY);

                    // Clip pixels outside the circle — sharp but anti-aliased edge
                    headClip = saturate((1.0 - dist) * lerp(40.0, 2.5, _HeadSoft * saturate(headLocal * 8.0)));   // soft only inside the cap

                    // Bright white glow inside the cap
                    headGlow = headLocal * _HeadBrightness * headClip;

                    // Alpha boost so cap reads as solid
                    headAlpha = 1.0 + headLocal * headClip * 1.5;
                }

                // ── 7. Emission ───────────────────────────────────────────
                float emitMask = pow(glowMask, _EmissionSoftness);
                float4 emitCol = lerp(_EmissionColor, baseColor, max(_UseSpectrum, 0.8));
                float3 emission = emitCol.rgb * emitMask * _EmissionStrength;

                // ── 8. Lyman-alpha absorption lines ───────────────────────
                // Sample the absorption texture at this pixel's wavelength (specT).
                // The texture stores per-wavelength absorption strength in the red channel.
                // Lines are parallel to color bands — same wavelength darkened all along
                // the trail length simultaneously.
                float absorp = 0.0;
                if (_UseAbsorptionLine > 0.5)
                {
                    float lineStrength = tex2D(_AbsorptionLineTex, float2(specT, 0.5)).r;

                    // SPREAD WIDENS THE LINE WITHOUT MOVING IT. Four extra taps either
                    // side of this pixel's wavelength, keeping whichever is darkest, so a
                    // line one texel wide is painted across a band of the ribbon centred
                    // on the wavelength it actually belongs to.
                    //
                    // The absorption buffer is shared with the spectrum bar, and this
                    // does NOT touch it: the bar keeps the sharp dip it can afford at 720
                    // pixels wide, and the ribbon — a fifth of that on screen — gets a
                    // stripe thick enough to be a stripe. Same wavelength, same drift,
                    // two scales.
                    //
                    // Max rather than a blur, because absorption is light that is gone.
                    // Averaging would make a narrow deep line into a wide shallow one and
                    // lose exactly what has to be visible.
                    if (_AbsorptionLineSpread > 0.0001)
                    {
                        // Not named `step`: that is an HLSL intrinsic, and shadowing it
                        // compiles on one platform and argues on the next.
                        //
                        // EIGHT TAPS A SIDE, NOT FOUR. With four, the taps sat further apart
                        // than a line is wide, so the max picked up the line's peak at each
                        // tap and its dim edges in between — one absorption was drawn as a
                        // row of thin dark ridges, which reads as several lines. Eight puts
                        // the taps closer together than the line is wide and the band
                        // comes out continuous.
                        float tapStep = _AbsorptionLineSpread * 0.125;

                        // tex2Dlod, not tex2D: a sample inside flow control has no
                        // derivatives, and some compilers refuse it outright. The buffer
                        // is one texel tall with no mip chain, so asking for level 0
                        // explicitly costs nothing and is what was wanted anyway.
                        for (int tap = 1; tap <= 8; tap++)
                        {
                            float d = tapStep * tap;
                            lineStrength = max(lineStrength,
                                               tex2Dlod(_AbsorptionLineTex, float4(specT - d, 0.5, 0, 0)).r);
                            lineStrength = max(lineStrength,
                                               tex2Dlod(_AbsorptionLineTex, float4(specT + d, 0.5, 0, 0)).r);
                        }
                    }

                    // HARDNESS TURNS A SOFT DIP INTO A SOLID BAND. The buffer stores each
                    // line with a soft falloff, which the bar needs — it is a curve. On the
                    // ribbon that falloff reads as a faint smudge that fades into the
                    // colours either side. Above a small threshold this snaps to fully
                    // absorbed, so the line is one solid black stripe with a clean edge.
                    // 0 (the default) leaves every existing material as it was.
                    float solid = smoothstep(0.06, 0.22, lineStrength);
                    lineStrength = lerp(lineStrength, solid, _AbsorptionLineHardness);

                    absorp = lineStrength * _AbsorptionLineStrength;
                }

                // ── 8b. Outline ─────────────────────────────────────────
                // Light just outside a line: the strongest absorption within the outline
                // width either side, minus the absorption here. Zero inside the line and
                // far from it, so it is a rim and nothing else.
                float outline = 0.0;
                if (_UseAbsorptionLine > 0.5 && _AbsorptionOutlineWidth > 0.0001)
                {
                    float nearAbs = 0.0;
                    for (int o = 1; o <= 4; o++)
                    {
                        float d = _AbsorptionOutlineWidth * o * 0.25;
                        nearAbs = max(nearAbs, tex2Dlod(_AbsorptionLineTex, float4(specT - d, 0.5, 0, 0)).r);
                        nearAbs = max(nearAbs, tex2Dlod(_AbsorptionLineTex, float4(specT + d, 0.5, 0, 0)).r);
                    }
                    nearAbs = lerp(nearAbs, smoothstep(0.06, 0.22, nearAbs), _AbsorptionLineHardness);
                    outline = saturate(nearAbs - absorp) * _AbsorptionOutlineStrength;

                    if (_AbsorptionOutlineRange > 0.0001)
                        outline *= 1.0 - smoothstep(_AbsorptionOutlineRange * 0.5, _AbsorptionOutlineRange,
                                                    abs(specT - _AbsorptionOutlineCenter));
                }

                // Darken the color at absorbed wavelengths — absorption removes light
                float4 absorbedColor = baseColor * (1.0 - absorp);

                // ── 9. Assemble ───────────────────────────────────────────
                float4 col = absorbedColor;
                // _AbsorbThroughGlow 0 (default, every existing material): emission and glow are
                // added on top of the absorbed colour, which fills lines back in wherever the
                // ribbon is bright. 1: they are cut by the absorption too, so an absorbed
                // wavelength stays dark however bright the ribbon is there.
                float keep = 1.0 - absorp * _AbsorbThroughGlow;
                col.rgb   += emission * keep;
                col.rgb   *= (1.0 + glow * 0.5 * keep);
                col.rgb   += _HeadColor.rgb * headGlow;

                // Visible band highlight. ADDITIVE and at EQUAL BRIGHTNESS per hue, and
                // applied after the centre glow. Multiplying the colour instead made the
                // flash scale with how bright each colour already was, and the centre of
                // the ribbon — where the glow and emission peak, and where cyan and green
                // sit — outshone red and violet so far that only the middle appeared to
                // flash. Normalising each colour to its brightest channel lifts every
                // visible wavelength by the same amount in its own hue. Absorbed
                // wavelengths are not lifted, so lines stay dark through the flash.
                float huePeak = max(specColor.r, max(specColor.g, specColor.b));
                float3 hue    = specColor.rgb / max(huePeak, 0.001);
                col.rgb      += hue * visMask * _VisibleHighlight * 0.6 * (1.0 - absorp);

                // The outline is added light, so it shows on the dimmest part of the ribbon.
                col.rgb += _AbsorptionOutlineColor.rgb * outline;
                col.a    = max(col.a, saturate(outline));

                // Tube: lit like a round beam, brightest down the middle.
                col.rgb *= lerp(1.0, 0.45 + 0.55 * sqrt(saturate(1.0 - centreY * centreY)), _Tube);   // never brighter than before, only darker towards the rim

                // Flow: a ripple fixed to where the light has been (distance flown minus how far
                // back along the trail this pixel is), drifting back. Multiplies, so an absorbed
                // wavelength stays as dark as it was.
                float flowAlpha = 1.0;
                float flowMul   = 1.0;
                if (_FlowAmount > 0.0001)
                {
                    float s = _FlowDistance - uv.x * _TrailLength;
                    float r = sin(s * 6.28318 / max(_FlowWavelength, 0.01) + _Time.y * _FlowSpeed);
                    flowMul   = 1.0 + _FlowAmount * r;
                    col.rgb  *= flowMul;
                    flowAlpha = 1.0 - 0.545 * _FlowAmount * (1.0 - r);
                }

                // Beam: the spectrum at this pixel's place across the beam, times how much light
                // the sight line passes through there (a round tube seen side on: sqrt(1 - x^2),
                // x being the place across). Absorbed wavelengths give nothing. Nothing else.
                if (_Beam > 0.5)
                {
                    float  thick = sqrt(saturate(1.0 - centreY * centreY));
                    float3 bc    = specColor.rgb * i.color.rgb * (1.0 - absorp) * _BeamBrightness;
                    bc += hue * visMask * _VisibleHighlight * 0.6 * (1.0 - absorp);
                    bc += _AbsorptionOutlineColor.rgb * outline;
                    bc *= flowMul;
                    float tip = _BeamHeadFade > 0.0001 ? smoothstep(0.0, _BeamHeadFade, uv.x) : 1.0;
                    // baseColor.a carries the spectrum texture's alpha: UV and IR are dim by design.
                    float ba = thick * smoothstep(0.0, 0.35, thick) * baseColor.a * endFade * flowAlpha * tip;
                    return fixed4(bc, saturate(max(ba, saturate(outline) * thick)));
                }

                // headClip cuts the rectangle corners into a semicircle shape
                float alpha = col.a
                            * flowAlpha
                            * edgeFade
                            * endFade
                            * headAlpha
                            * headClip;

                col.a = saturate(alpha);
                return col;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
