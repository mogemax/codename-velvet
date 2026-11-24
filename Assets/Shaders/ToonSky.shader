// Cartoon skybox: a flat two-tone gradient (horizon -> top) with soft-edged cloud bands that drift slowly.
// The game camera looks down and barely sees above the real horizon, so _HorizonDrop moves the whole sky
// (gradient and clouds) down into the band past the end of the sea that the camera does see.
// Cheap: no textures, a few noise samples per pixel. DayNightLighting blends the colors toward night.
Shader "Velvet/Toon Sky"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.31, 0.64, 0.88, 1)
        _HorizonColor ("Horizon Color", Color) = (0.75, 0.90, 0.95, 1)
        _HorizonDrop ("Horizon Drop (moves the whole sky down into view)", Range(0, 0.5)) = 0.12
        _HorizonHeight ("Horizon Height (0-1 of the sky)", Range(0.01, 1)) = 0.12
        _HorizonSoftness ("Horizon Softness", Range(0.001, 1)) = 0.1
        _CloudColor ("Cloud Color (alpha = strength)", Color) = (1, 1, 1, 0.85)
        _CloudScale ("Cloud Scale", Float) = 0.6
        _CloudSpeed ("Cloud Speed", Float) = 0.03
        _CloudCutoff ("Cloud Cutoff", Range(0, 1)) = 0.62
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }

        Pass
        {
            Name "ToonSky"
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _HorizonDrop;
                half4 _TopColor;
                half4 _HorizonColor;
                float _HorizonHeight;
                float _HorizonSoftness;
                half4 _CloudColor;
                float _CloudScale;
                float _CloudSpeed;
                float _CloudCutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
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
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.direction);
                float y = dir.y + _HorizonDrop; // the sky's own horizon sits below the real one
                float height = saturate(y);

                // Two flat colors with a soft band between them; below the horizon stays horizon-colored.
                float toTop = smoothstep(_HorizonHeight - _HorizonSoftness * 0.5, _HorizonHeight + _HorizonSoftness * 0.5, height);
                half3 color = lerp(_HorizonColor.rgb, _TopColor.rgb, toTop);

                // Clouds on a plane above the camera: projecting by height makes them shrink toward the horizon.
                float2 p = dir.xz / (max(y, 0.0) + 0.25) * _CloudScale;
                float t = _Time.y * _CloudSpeed;
                float n = (Noise(p + float2(t, t * 0.3)) + Noise(p * 2.1 - float2(t * 0.7, 0))) * 0.5;
                float cloud = smoothstep(_CloudCutoff, _CloudCutoff + 0.03, n) * smoothstep(0.0, 0.04, y);
                color = lerp(color, _CloudColor.rgb, cloud * _CloudColor.a);

                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
