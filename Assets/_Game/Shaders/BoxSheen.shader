// Screen-space diagonal "shine" sweep for grid boxes — the same bottom-left → top-right
// gloss band used on UI, projected in SCREEN space so the whole grid reads as ONE
// continuous light wave (no seams between boxes) and it always travels along the
// screen diagonal regardless of camera.
//
// Driven by two GLOBAL floats so every box stays in sync (set them from
// GridSheenController):
//   _SweepPos       0..1 position of the band along the diagonal (animated)
//   _SweepIntensity overall brightness (0 = invisible)
//
// Additive (Blend One One): it only ADDS light, never darkens the box colour. Put it
// on a child that is enabled only while the box is Hit, so only painted cells shimmer.
Shader "PixelShoot/BoxSheen"
{
    Properties
    {
        _SheenColor ("Sheen Color", Color) = (1, 1, 1, 1)
        _BandWidth  ("Band Width (screen diagonal frac)", Range(0.01, 1)) = 0.15
        _Softness   ("Edge Softness", Range(0.5, 8)) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }

        Pass
        {
            Name "Sheen"
            Tags { "LightMode"="UniversalForward" }

            Blend One One        // additive
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1        // sit cleanly on top of the box face

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Per-material (SRP Batcher compatible).
            CBUFFER_START(UnityPerMaterial)
                half4 _SheenColor;
                float _BandWidth;
                float _Softness;
            CBUFFER_END

            // GLOBAL (set via Shader.SetGlobalFloat) — declared OUTSIDE the CBUFFER.
            float _SweepPos;
            float _SweepIntensity;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPos   : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs v = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = v.positionCS;
                OUT.screenPos   = v.positionNDC; // ComputeScreenPos result (xy/w → 0..1)
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.screenPos.xy / max(IN.screenPos.w, 1e-5);
                // Diagonal coordinate: 0 at bottom-left, 1 at top-right.
                float d = (uv.x + uv.y) * 0.5;

                // Soft band centred on _SweepPos.
                float dist = abs(d - _SweepPos);
                float band = saturate(1.0 - dist / max(_BandWidth, 1e-4));
                band = pow(band, _Softness);

                half3 col = _SheenColor.rgb * band * _SweepIntensity;
                return half4(col, 1.0); // alpha unused under Blend One One
            }
            ENDHLSL
        }
    }
}
