// Full-screen dark UI overlay with rectangular "spotlight" holes (in 0..1 screen UV).
// Holes are fed by the SpotlightOverlay component so world targets (e.g. the conveyor
// text) stay visible while everything else dims. Put on a RawImage stretched to screen.
Shader "PixelShoot/SpotlightOverlay"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {} // RawImage writes its texture here
        _Color   ("Dark Color", Color) = (0, 0, 0, 0.8)
        _Feather ("Edge Feather", Range(0, 0.1)) = 0.012
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"
               "IgnoreProjector"="True" "PreviewType"="Plane" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "SpotlightOverlay"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_HOLES 8

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _Feather;
            CBUFFER_END

            // Set by SpotlightOverlay.cs. xMin,yMin,xMax,yMax in 0..1 screen UV.
            float4 _Holes[MAX_HOLES];
            int    _HoleCount;

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };

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
                float dark = 1.0;
                [loop]
                for (int k = 0; k < _HoleCount; k++)
                {
                    float4 h = _Holes[k];
                    float ix = smoothstep(h.x - _Feather, h.x + _Feather, IN.uv.x)
                             * (1.0 - smoothstep(h.z - _Feather, h.z + _Feather, IN.uv.x));
                    float iy = smoothstep(h.y - _Feather, h.y + _Feather, IN.uv.y)
                             * (1.0 - smoothstep(h.w - _Feather, h.w + _Feather, IN.uv.y));
                    dark *= (1.0 - ix * iy); // punch a hole
                }

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 c = _Color * IN.color * tex;
                c.a *= dark;
                return c;
            }
            ENDHLSL
        }
    }
}
