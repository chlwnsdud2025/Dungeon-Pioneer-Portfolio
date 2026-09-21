// This shader multiplies a cubemap texture onto the volumetric fog in the final screen output.
// It is assignable from the volumetric fog renderer feature, so it can be replaced with a
// customized copy. Custom shaders must contain a pass named "VolumetricFogCubemapNoise" and
// should keep the same uniform names to stay compatible with the render pass.
Shader "Hidden/VolumetricFogCubemapNoise"
{
    Properties
    {
        _CubemapNoiseTexture ("Cubemap Noise", Cube) = "grey" {}
        _CubemapNoiseIntensity ("Intensity", Range(0.0, 1.0)) = 1.0
        _CubemapNoisePointFilter ("Point Filter", Float) = 0.0
    }

    SubShader
    {
        Tags
        { 
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "VolumetricFogCubemapNoise"

            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM

            #include "./VolumetricFogCubemapNoise.hlsl"

            #pragma target 4.5

            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                return CubemapNoiseComposition(input.texcoord);
            }

            ENDHLSL
        }
    }

    SubShader
    {
        Tags
        { 
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "VolumetricFogCubemapNoise"

            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM

            #include "./VolumetricFogCubemapNoise.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                return CubemapNoiseComposition(input.texcoord);
            }

            ENDHLSL
        }
    }

    Fallback Off
}