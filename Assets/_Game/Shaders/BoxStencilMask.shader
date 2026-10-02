// Invisible pass that stamps stencil = 1 over a box's footprint.
// It draws NO colour (ColorMask 0) but writes depth + stencil, so the BoxOutline
// shader can later skip any outline pixel that sits on top of a box body.
//
// Render queue is Geometry (2000): every box's mask is stamped BEFORE any outline
// (which lives at a higher queue), so when outlines draw, all neighbour footprints
// are already in the stencil buffer and the shared seams get clipped away.
Shader "PixelShoot/BoxStencilMask"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "StencilMask"
            Tags { "LightMode"="UniversalForward" }

            ColorMask 0          // write nothing visible
            ZWrite On
            Cull Back

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace     // mark this pixel as "box body"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target { return half4(0, 0, 0, 0); }
            ENDHLSL
        }
    }
}
