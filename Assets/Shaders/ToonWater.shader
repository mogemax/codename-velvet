// Cartoon water for URP: flat colors only, no lighting, reflections or refraction.
// Color goes from shallow to deep by how much ground lies under the surface (scene depth texture),
// with banded ripples and a foam line where the water meets the shore. Without a depth texture
// (e.g. the Mobile quality level) it renders as deep water without foam.
Shader "Velvet/Toon Water"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.36, 0.86, 0.88, 0.75)
        _DeepColor ("Deep Color", Color) = (0.12, 0.40, 0.66, 0.95)
        _DepthRange ("Depth For Deep Color", Float) = 15
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamDistance ("Foam Width", Float) = 2.5
        _RippleColor ("Ripple Color (alpha = strength)", Color) = (1, 1, 1, 0.35)
        _RippleScale ("Ripple Scale", Float) = 0.1
        _RippleSpeed ("Ripple Speed", Float) = 0.15
        _RippleCutoff ("Ripple Cutoff", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ToonWater"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthRange;
                half4 _FoamColor;
                float _FoamDistance;
                half4 _RippleColor;
                float _RippleScale;
                float _RippleSpeed;
                float _RippleCutoff;
            CBUFFER_END

            half4 _VelvetDarkness;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // Smooth value noise in [0, 1].
            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Eye-space distance from the surface to whatever is behind it.
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float waterDepth = max(0.0, sceneDepth - input.screenPos.w);

                half4 color = lerp(_ShallowColor, _DeepColor, saturate(waterDepth / _DepthRange));

                // Two noise layers drifting in different directions, cut into flat bands.
                float2 p = input.positionWS.xz * _RippleScale;
                float t = _Time.y * _RippleSpeed;
                float n = (Noise(p + float2(t, t * 0.6)) + Noise(p * 1.7 - float2(t * 0.8, t))) * 0.5;
                color.rgb = lerp(color.rgb, _RippleColor.rgb, step(_RippleCutoff, n) * _RippleColor.a);

                // Foam line that breathes with time and is broken up by the same noise.
                float foamWidth = _FoamDistance * (0.75 + 0.25 * sin(_Time.y * 2.0 + n * 6.2832));
                color = lerp(color, _FoamColor, step(waterDepth, foamWidth) * _FoamColor.a);

                color.rgb *= 1.0h - _VelvetDarkness.rgb; // night, set by DayNightLighting (unset = 0 = no change)
                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
