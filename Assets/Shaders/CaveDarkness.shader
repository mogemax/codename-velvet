// The dark inside of a cave mouth: a soft-edged patch of darkness, opaque in the middle and fading out towards its
// edges (where the rock frames it). Unlit: as dark by day as by night. Fades into the fog like the map.
// Goes on a quad standing in the opening, behind whatever stands in it; give its renderer the sorting order of its
// depth (DynamicSortingOrder.OrderForZ) so sprites in front draw over it.
Shader "Velvet/Cave Darkness"
{
    Properties
    {
        _Color ("Color", Color) = (0.02, 0.015, 0.015, 1)
        _Solid ("Solid Core (0-1 of the radius)", Range(0, 1)) = 0.55
        _Softness ("Edge Softness", Range(0.01, 1)) = 0.45
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "CaveDarkness"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Solid;
                half _Softness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fogFactor : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half radius = length((input.uv - 0.5h) * 2.0h); // 0 in the middle, 1 at the edge's midpoints
                half alpha = 1.0h - smoothstep(_Solid, _Solid + _Softness, radius);
                half3 color = MixFog(_Color.rgb, input.fogFactor);
                return half4(color, alpha * _Color.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
