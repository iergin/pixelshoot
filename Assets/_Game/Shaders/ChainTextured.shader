// Textured line for a LineRenderer — samples a seamless chain texture, tiled along the
// line (U), transparent, tintable, with optional scroll. Pair with ChainTexture.png.
//
// LineRenderer setup: Material = this, give it width. For CONSTANT link size use the
// LineRenderer's Texture Mode = Tile (then keep _TileU = 1); or Stretch + raise _TileU.
Shader "PixelShoot/ChainTextured"
{
    Properties
    {
        [MainTexture] _BaseMap ("Chain Texture", 2D) = "white" {}
        [MainColor]   _Color   ("Tint", Color) = (1,1,1,1)
        _TileU        ("Tiling along line (Stretch mode)", Float) = 8
        _ScrollSpeed  ("Scroll speed (0 = static)", Float) = 0
        _AlphaCutoff  ("Alpha clip", Range(0,1)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "ChainTextured"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _Color;
                float  _TileU;
                float  _ScrollSpeed;
                float  _AlphaCutoff;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                float2 uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                uv.x = uv.x * _TileU + _Time.y * _ScrollSpeed;
                OUT.uv = uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 col = tex * _Color * IN.color;
                clip(col.a - _AlphaCutoff);
                return col;
            }
            ENDHLSL
        }
    }
}
