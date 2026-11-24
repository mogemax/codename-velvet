// Sprite shader that sits the 2D characters and props in the 3D world. Same inputs as URP's Sprite-Unlit-Default
// (skinning, instancing, flip, renderer color), plus:
// - a cast shadow: the sprite drawn again, flattened onto the ground along the sun's direction (URP does not put
//   SpriteRenderers in the shadow map, so this is how a sprite casts). Skinned characters animate it for free;
// - receives the sun's shadows (a character under a cliff darkens like the ground);
// - takes the sun's hue (warm at dusk, blue at night) and the global night darkness;
// - fades into the fog like the map;
// - optional wind sway (more at the top than at the base), per material.
// Globals (set by DayNightLighting; unset = 0 = no effect):
//   _VelvetDarkness, _VelvetSunTint, _VelvetShadowStrength, _VelvetCastShadow.
// Per renderer (set by ContactShadow): _ShadowGround = (ground height, 1 = set). Unset = the renderer's own pivot.
Shader "Velvet/Sprite"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0
        _WindStrength ("Wind Strength (shear at the top)", Range(0, 0.3)) = 0
        _WindSpeed ("Wind Speed", Float) = 1.5
        _ShadowMaxLength ("Cast Shadow Max Length (× height)", Range(0, 4)) = 1.6
        [HideInInspector] _ShadowGround ("Shadow Ground (height, set)", Vector) = (0, 0, 0, 0)

        // Legacy properties. They're here so that materials using this shader can gracefully fallback to the legacy sprite shader.
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _WindStrength;
            half _WindSpeed;
            half _ShadowMaxLength;
            float4 _ShadowGround;
        CBUFFER_END

        // Globals, outside the material: one value for every sprite.
        half4 _VelvetDarkness;      // what night takes away, per channel
        half _VelvetSunTint;        // how much of the sun's hue sprites take (0-1)
        half _VelvetShadowStrength; // how dark a sprite gets in full shadow (0-1)
        half _VelvetCastShadow;     // opacity of the cast shadow (0 = none)

        struct Attributes
        {
            COMMON_2D_INPUTS
            half4 color : COLOR;
            UNITY_SKINNED_VERTEX_INPUTS
        };

        // Skinning, flip and wind: the sprite's final shape in object space.
        float3 SpritePositionOS(inout Attributes input)
        {
            UNITY_SKINNED_VERTEX_COMPUTE(input);
            SetUpSpriteInstanceProperties();
            float3 position = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

            // Each object sways with its own phase (from its world position); the base stays put.
            float phase = dot(UNITY_MATRIX_M._m03_m23, float2(0.37, 0.71));
            position.x += sin(_Time.y * _WindSpeed + phase) * _WindStrength * max(position.y, 0.0);
            return position;
        }
    ENDHLSL

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        // 1. Cast shadow, drawn first (under the sprite). Stencil bit 2 = a shadow is already there (a skinned
        //    character's limbs or two shadows overlapping never darken twice); bit 4 = a sprite is already there (a
        //    shadow never covers a character drawn before it). It draws only where neither bit is set.
        Pass
        {
            Name "CastShadow"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1
            Stencil
            {
                Ref 2
                ReadMask 6
                WriteMask 2
                Comp Greater
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #pragma multi_compile_fog

            static const float Lift = 0.06; // above the ground, against z-fighting

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                half alpha : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = TransformObjectToWorld(SpritePositionOS(input));
                float groundY = _ShadowGround.y > 0.5 ? _ShadowGround.x : UNITY_MATRIX_M._m13;

                // Slide each point down the sun's direction until it reaches the ground, capped in length.
                float3 toGround = -_MainLightPosition.xyz; // the direction the light travels
                float height = max(positionWS.y - groundY, 0.0);
                float2 slide = toGround.xz / max(-toGround.y, 0.05) * height;
                float slideLength = length(slide);
                float maxLength = height * _ShadowMaxLength;
                if (slideLength > maxLength) slide *= maxLength / max(slideLength, 0.0001);

                float3 shadowWS = float3(positionWS.x + slide.x, groundY + Lift, positionWS.z + slide.y);
                output.positionCS = TransformWorldToHClip(shadowWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.uv = input.uv;
                // No shadow when the sun is below the horizon.
                output.alpha = input.color.a * _Color.a * unity_SpriteColor.a * _VelvetCastShadow * step(0.05, -toGround.y);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half coverage = step(0.5h, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a);
                clip(coverage * input.alpha - 0.001h);
                half visibility = MixFogColor(half3(1, 1, 1), half3(0, 0, 0), input.fogFactor).r; // 1 = clear or no fog
                return half4(0, 0, 0, input.alpha * visibility);
            }
            ENDHLSL
        }

        // 2. The sprite itself.
        Pass
        {
            Name "Sprite"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            Cull Off
            ZWrite [_ZWrite]
            Stencil
            {
                Ref 4
                WriteMask 4
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionWS = TransformObjectToWorld(SpritePositionOS(input));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(color.a - 0.01h); // empty pixels must not mark the stencil

                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, half4(1, 1, 1, 1));
                half3 hue = sun.color / max(max(sun.color.r, sun.color.g), max(sun.color.b, 0.001h));
                color.rgb *= lerp(half3(1, 1, 1), hue, _VelvetSunTint);
                color.rgb *= lerp(1.0h - _VelvetShadowStrength, 1.0h, sun.shadowAttenuation);
                color.rgb *= 1.0h - _VelvetDarkness.rgb;

                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
