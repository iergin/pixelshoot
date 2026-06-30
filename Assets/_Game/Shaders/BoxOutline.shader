// Thin inverted-hull outline for grid boxes.
//
// The hull is grown RADIALLY from the mesh centre (positionOS direction), not along
// the normal — a cube has hard-edged (split) normals that would tear the outline at
// the corners, whereas pushing each vertex out from the centre keeps shared corner
// positions together, so the outline stays gap-free.
//
// Overlap control: the pass tests Stencil != 1. BoxStencilMask stamps 1 over every
// box footprint at an earlier render queue, so any outline pixel lying on top of a
// neighbouring box body is discarded. Result: two adjacent hit boxes do NOT draw a
// doubled line along their shared edge — only the OUTER border of the hit region is
// outlined.
Shader "PixelShoot/BoxOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width (object space)", Range(0, 0.5)) = 0.03
    }

    SubShader
    {
        // Geometry+100 → renders AFTER all box bodies and stencil masks (Geometry = 2000).
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+100" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="UniversalForward" }

            Cull Front           // inverted hull: show the back of the grown shell
            ZWrite On
            ZTest LEqual

            Stencil
            {
                Ref 1
                Comp NotEqual    // skip pixels already covered by a box body
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };
            struct Varyings { float4 positionHCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                float3 p = IN.positionOS.xyz;
                // Radial direction from the mesh centre; fall back to the normal if a
                // vertex sits exactly at the origin.
                float3 dir = (dot(p, p) > 1e-6) ? normalize(p) : IN.normalOS;
                p += dir * _OutlineWidth;

                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(p);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
