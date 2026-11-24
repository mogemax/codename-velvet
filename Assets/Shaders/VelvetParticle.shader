// Particles for the game feel (chips, shards, sparks, dust): vertex color × an optional soft round mask, with the same
// fog and night darkness as the sprites, so a burst sits in the world. _Glow keeps part of the color at night
// (sparks shine, dust doesn't).
Shader "Velvet/Particle"
{
    Properties
    {
        [Toggle] _Round ("Round (soft dot) instead of square", Float) = 1
        _Glow ("Glow (ignores night)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Particle"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Round;
                half _Glow;
            CBUFFER_END

            half4 _VelvetDarkness; // set by DayNightLighting

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = input.color;
                if (_Round > 0.5h)
                {
                    half distance = length(input.uv - 0.5h) * 2.0h;
                    color.a *= saturate((1.0h - distance) * 2.5h);
                }
                clip(color.a - 0.002h);
                color.rgb *= 1.0h - _VelvetDarkness.rgb * (1.0h - _Glow);
                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
