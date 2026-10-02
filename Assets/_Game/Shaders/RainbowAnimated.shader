// Animated palette gradient — scrolls through THIS GAME'S colours (not a generic HSV
// rainbow), so it matches the art. Assign to a material and drop on any mesh.
//
// The palette below is the vivid subset of the game palette, ordered to loop nicely
// (red→orange→yellow→green→teal→blue→purple→pink→red). To use different / more colours,
// edit the _Palette array and PAL_COUNT. Full game palette (add any you want):
//   3F4658 FFF1C4 FFFFFF 181B24 FF5E57 A61E3C FF8C1A F7EA3A B7F13D 31D167
//   149255 1ED8D8 2F7BFF 1C3FAE 8A4DFF 5627A8 FF5DB1 A8642A E0A44B A8B0BC
Shader "PixelShoot/RainbowAnimated"
{
    Properties
    {
        _Speed        ("Scroll Speed", Float) = 0.4
        _Scale        ("Band Scale (how many bands)", Float) = 1.0
        _Direction    ("Band Direction (object space)", Vector) = (1, 1, 0, 0)
        _Saturation   ("Saturation", Range(0, 1)) = 1
        _Value        ("Brightness", Range(0, 3)) = 1
        _SrgbToLinear ("sRGB→Linear (1 for Linear projects)", Range(0, 1)) = 1
        _FresnelPower ("Rim Power (0 = off)", Range(0, 8)) = 2
        _FresnelBoost ("Rim Boost", Range(0, 3)) = 0.4
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "PaletteUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define PAL_COUNT 14

            // Game colours (sRGB 0..1), ordered for a smooth spectral loop.
            static const float3 _Palette[PAL_COUNT] =
            {
                float3(1.0000, 0.3686, 0.3412), // FF5E57 coral red
                float3(0.6510, 0.1176, 0.2353), // A61E3C deep red
                float3(1.0000, 0.5490, 0.1020), // FF8C1A orange
                float3(0.8784, 0.6431, 0.2941), // E0A44B amber
                float3(0.9686, 0.9176, 0.2275), // F7EA3A yellow
                float3(0.7176, 0.9451, 0.2392), // B7F13D lime
                float3(0.1922, 0.8196, 0.4039), // 31D167 green
                float3(0.0784, 0.5725, 0.3333), // 149255 dark green
                float3(0.1176, 0.8471, 0.8471), // 1ED8D8 teal
                float3(0.1843, 0.4824, 1.0000), // 2F7BFF blue
                float3(0.1098, 0.2471, 0.6824), // 1C3FAE indigo
                float3(0.5412, 0.3020, 1.0000), // 8A4DFF purple
                float3(0.3373, 0.1529, 0.6588), // 5627A8 violet
                float3(1.0000, 0.3647, 0.6941)  // FF5DB1 pink
            };

            CBUFFER_START(UnityPerMaterial)
                float  _Speed;
                float  _Scale;
                float4 _Direction;
                float  _Saturation;
                float  _Value;
                float  _SrgbToLinear;
                float  _FresnelPower;
                float  _FresnelBoost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
            };

            // Smoothly sample the palette at t (wraps around).
            float3 SamplePalette(float t)
            {
                t = frac(t) * PAL_COUNT;
                int i = (int)floor(t);
                float f = t - i;
                float3 a = _Palette[i % PAL_COUNT];
                float3 b = _Palette[(i + 1) % PAL_COUNT];
                return lerp(a, b, smoothstep(0.0, 1.0, f));
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = p.positionCS;
                OUT.positionWS  = p.positionWS;
                OUT.positionOS  = IN.positionOS.xyz;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(_Direction.xyz + float3(1e-5, 0, 0));
                float coord = dot(IN.positionOS, dir) * _Scale + _Time.y * _Speed;
                float3 rgb = SamplePalette(coord);

                // Match the on-screen hex in Linear colour space.
                rgb = lerp(rgb, pow(saturate(rgb), 2.2), _SrgbToLinear);

                // Saturation toward grey, then brightness.
                float grey = dot(rgb, float3(0.333, 0.333, 0.334));
                rgb = lerp(grey.xxx, rgb, _Saturation) * _Value;

                // Fresnel rim highlight.
                if (_FresnelPower > 0)
                {
                    float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - IN.positionWS);
                    float f = pow(1.0 - saturate(dot(normalize(IN.normalWS), viewDir)), _FresnelPower);
                    rgb += f * _FresnelBoost;
                }

                return half4(rgb, 1);
            }
            ENDHLSL
        }
    }
}
