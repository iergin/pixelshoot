// Animated rainbow — moving HSV bands across the surface with an optional Fresnel rim.
// Unlit + emissive-style so it glows on its own. Assign this shader to a material and drop
// it on any mesh (e.g. a sphere).
Shader "PixelShoot/RainbowAnimated"
{
    Properties
    {
        _Speed        ("Scroll Speed", Float) = 0.4
        _Scale        ("Band Scale (how many bands)", Float) = 1.5
        _Direction    ("Band Direction (object space)", Vector) = (1, 1, 0, 0)
        _Saturation   ("Saturation", Range(0, 1)) = 1
        _Value        ("Brightness", Range(0, 3)) = 1
        _FresnelPower ("Rim Power (0 = off)", Range(0, 8)) = 2
        _FresnelBoost ("Rim Boost", Range(0, 3)) = 0.4
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "RainbowUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float  _Speed;
                float  _Scale;
                float4 _Direction;
                float  _Saturation;
                float  _Value;
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

            // Hue (0..1) → RGB, no saturation/value (that's applied after).
            float3 Hue2RGB(float h)
            {
                h = frac(h);
                return saturate(float3(abs(h * 6 - 3) - 1,
                                       2 - abs(h * 6 - 2),
                                       2 - abs(h * 6 - 4)));
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
                // Hue scrolls along the band direction over time (_Time.y = seconds).
                float hue = dot(IN.positionOS, dir) * _Scale + _Time.y * _Speed;
                float3 rgb = Hue2RGB(hue);

                // Saturation: fade toward grey. Then scale brightness.
                float grey = dot(rgb, float3(0.333, 0.333, 0.334));
                rgb = lerp(grey.xxx, rgb, _Saturation) * _Value;

                // Fresnel rim highlight for a glossy sphere feel.
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
