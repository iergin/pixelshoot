// Unlit URP shader that outputs the mesh VERTEX COLOR × a tint. Obi's extruded rope renderer
// bakes each particle's colour into the mesh vertex colours, so setting per-particle colours
// (bus A's colour → bus B's colour along the rope) makes the rope show a two-tone / gradient.
Shader "PixelShoot/LinkRopeVertexColor"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings   { float4 positionHCS : SV_POSITION; half4 color : COLOR; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return IN.color * _BaseColor;
            }
            ENDHLSL
        }
    }
}
