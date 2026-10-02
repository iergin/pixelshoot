// Procedural chain for a LineRenderer. Draws a row of interlocking oval links along the
// line (UV.x = along the line, UV.y = across the width), alternating vertical/horizontal
// so they read as chain links. Transparent — the gaps between links show through.
//
// Setup on the LineRenderer:
//   • Material = this shader.
//   • For CONSTANT link size regardless of length → Texture Mode = Tile (and tune _Tiling).
//     For a FIXED number of links over the whole line → Texture Mode = Stretch.
//   • Give the line some width so the links have room across UV.y.
Shader "PixelShoot/ChainLine"
{
    Properties
    {
        _Color        ("Chain Color", Color) = (0.75, 0.76, 0.8, 1)
        _Tiling       ("Links along the line", Float) = 8
        _Thickness    ("Link thickness", Range(0.02, 0.6)) = 0.16
        [Header(Link ovals)]
        _LongRadius   ("Long radius", Range(0.2, 0.6)) = 0.46
        _ShortRadius  ("Short radius", Range(0.1, 0.5)) = 0.28
        _Softness     ("Edge softness", Range(0.001, 0.3)) = 0.05
        _ScrollSpeed  ("Scroll speed (0 = static)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "Chain"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                float  _Tiling;
                float  _Thickness;
                float  _LongRadius;
                float  _ShortRadius;
                float  _Softness;
                float  _ScrollSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;      // LineRenderer vertex colour (respects gradient/alpha)
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            // Annulus (ring) mask for an oval of radii (rx, ry) centred at 0.
            float Ring(float2 local, float rx, float ry, float thickness, float soft)
            {
                float d = length(local / float2(rx, ry));   // 1 on the oval outline
                // distance to the outline, in oval-normalised units → band of width `thickness`
                float band = abs(d - 1.0);
                return 1.0 - smoothstep(thickness, thickness + soft, band);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float u = IN.uv.x * _Tiling + _Time.y * _ScrollSpeed;
                float v = IN.uv.y - 0.5;                 // -0.5 .. 0.5 across the width

                int base = (int)floor(u);
                float mask = 0.0;

                // Each fragment may be covered by a link and its overlapping neighbours,
                // so evaluate the 3 nearest links and take the strongest.
                [unroll]
                for (int k = -1; k <= 1; k++)
                {
                    int i = base + k;
                    float2 local = float2(u - (i + 0.5), v);   // centre link in [i, i+1]
                    // Alternate orientation: even = vertical (tall) oval, odd = horizontal (wide).
                    float parity = fmod(float(i) + 2048.0, 2.0); // 0 or 1, safe for negatives
                    float rx = parity < 0.5 ? _ShortRadius : _LongRadius;
                    float ry = parity < 0.5 ? _LongRadius  : _ShortRadius;
                    mask = max(mask, Ring(local, rx, ry, _Thickness, _Softness));
                }

                half4 col = _Color * IN.color;
                col.a *= mask;
                return col;
            }
            ENDHLSL
        }
    }
}
