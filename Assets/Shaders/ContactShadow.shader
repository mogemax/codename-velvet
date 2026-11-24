// Soft ellipse used by ContactShadow: darkest under the center, fading to nothing at the edge, and fading into the
// fog with distance like the map.
Shader "Velvet/Contact Shadow"
{
    Properties
    {
        _Opacity ("Opacity", Range(0, 1)) = 0.55
        _Softness ("Softness (0 = hard edge)", Range(0.01, 1)) = 0.6
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Opacity;
                half _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half r = length(input.uv * 2.0 - 1.0); // 0 at the center, 1 at the edge
                half alpha = _Opacity * (1.0h - smoothstep(1.0h - _Softness, 1.0h, r));
                alpha *= MixFogColor(half3(1, 1, 1), half3(0, 0, 0), input.fogFactor).r; // 1 = clear or no fog: fades out with the map
                return half4(0, 0, 0, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
