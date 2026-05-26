Shader "Custom/CubeGradient_URP"
{
    // -----------------------------------------------------------------
    // ONE shader that handles ANY color.
    // You set _BaseColor per cube (via MaterialPropertyBlock for 30x30
    // grids) and the shader derives a lighter top + darker bottom
    // automatically.
    // -----------------------------------------------------------------
    Properties
    {
        [Header(Color)]
        [MainColor] _BaseColor      ("Base Color", Color) = (0.3, 0.55, 0.85, 1)

        [Header(Gradient)]
        _LightAmount                ("Top Lighten Amount", Range(0, 1)) = 0.40
        _DarkAmount                 ("Bottom Darken Amount", Range(0, 1)) = 0.30
        _GradientPower              ("Gradient Falloff (1 = linear)", Range(0.2, 3)) = 1.0

        [Header(Shading)]
        _AmbientFloor               ("Ambient Floor", Range(0, 1)) = 0.55
        _DirectionalBoost           ("Light Contrast", Range(0, 1)) = 0.30
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing      // 900 cubes -> 1 draw call

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float  gradT      : TEXCOORD1;   // 0 = bottom, 1 = top
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _LightAmount;
                float  _DarkAmount;
                float  _GradientPower;
                float  _AmbientFloor;
                float  _DirectionalBoost;
            CBUFFER_END

            // RGB <-> HSV so we don't desaturate when lightening
            float3 RGBtoHSV(float3 c)
            {
                float4 K = float4(0, -1.0/3.0, 2.0/3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)),
                              d / (q.x + e),
                              q.x);
            }
            float3 HSVtoRGB(float3 c)
            {
                float4 K = float4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            // Lighten / darken while preserving hue (HSV value channel)
            float3 Lighten(float3 rgb, float amt)
            {
                float3 hsv = RGBtoHSV(rgb);
                hsv.z = saturate(hsv.z + amt * (1 - hsv.z));
                hsv.y = saturate(hsv.y - amt * 0.25);
                return HSVtoRGB(hsv);
            }
            float3 Darken(float3 rgb, float amt)
            {
                float3 hsv = RGBtoHSV(rgb);
                hsv.z = saturate(hsv.z * (1 - amt));
                hsv.y = saturate(hsv.y + amt * 0.10);
                return HSVtoRGB(hsv);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = vp.positionCS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);

                // Gradient based on OBJECT-space Y so every cube shades
                // the same regardless of its world position in the grid.
                // Object space is in [-1, 1] for a typical unit cube, so
                // remap to [0, 1] and apply a tunable falloff curve.
                float t = saturate(IN.positionOS.y * 0.5 + 0.5);
                OUT.gradT = pow(t, _GradientPower);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // Build top and bottom variants of the base color
                float3 lightCol = Lighten(_BaseColor.rgb, _LightAmount);
                float3 darkCol  = Darken (_BaseColor.rgb, _DarkAmount);
                float3 grad     = lerp(darkCol, lightCol, IN.gradT);

                // Subtle directional shading on top so silhouette reads as 3D.
                // Sample the main directional light from the URP scene.
                Light mainLight  = GetMainLight();
                float3 N         = normalize(IN.normalWS);
                float  ndotl     = saturate(dot(N, mainLight.direction));
                float  shade     = _AmbientFloor + _DirectionalBoost * ndotl;

                return half4(grad * shade, 1.0);
            }
            ENDHLSL
        }
    }
}
