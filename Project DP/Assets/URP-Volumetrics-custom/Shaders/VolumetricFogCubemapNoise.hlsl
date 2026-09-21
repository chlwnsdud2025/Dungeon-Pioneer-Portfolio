#ifndef VOLUMETRIC_FOG_CUBEMAP_NOISE_INCLUDED
#define VOLUMETRIC_FOG_CUBEMAP_NOISE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "./DepthAwareUpsample.hlsl"

TEXTURE2D_X(_VolumetricFogTexture);
TEXTURECUBE(_CubemapNoiseTexture);
SAMPLER(sampler_BlitTexture);

// Inline sampler states. Their names define their behaviour (filter + wrap mode), so the
// cubemap texture asset import settings do not matter here. Note that inline sampler states
// are not supported on some platforms (e.g. OpenGL ES), where the texture's own sampler is used instead.
SAMPLER(cubemapnoise_point_repeat_sampler);
SAMPLER(cubemapnoise_linear_repeat_sampler);

float _CubemapNoiseIntensity;
float _CubemapNoisePointFilter;

// Computes the world space direction used to sample the cubemap for the given fragment.
float3 GetCubemapNoiseSampleDirection(float2 uv)
{
    UNITY_BRANCH
    if (unity_OrthoParams.w <= 0)
    {
        // Perspective: direction from the camera through this fragment.
        // Any depth along the ray reconstructs a point on the same ray, so an arbitrary value works.
        float3 posWS = ComputeWorldSpacePosition(uv, 0.5, UNITY_MATRIX_I_VP);

        return normalize(posWS - GetCameraPositionWS());
    }
    else
    {
        // Orthographic: fake a per fragment direction, similarly to what the volumetric fog
        // does to be able to use the phase function in orthographic mode.
        float depth = LinearEyeDepthOrthographic(SampleSceneDepth(uv));

        float4x4 viewMatrix = UNITY_MATRIX_V;
        float2 ndc = uv * 2.0 - 1.0;

        float3 fwdOffset = normalize(-viewMatrix[2].xyz) * depth;
        float3 rightOffset = normalize(viewMatrix[0].xyz) * (ndc.x * unity_OrthoParams.x);
        float3 upOffset = normalize(viewMatrix[1].xyz) * (ndc.y * unity_OrthoParams.y);

        return normalize(fwdOffset + rightOffset + upOffset);
    }
}

// ---------------------------------------------------------------------------------------------
// CUSTOMIZABLE: this function defines how the cubemap behaves. Modify it (or better, duplicate
// this shader outside of the package and assign it in the volumetric fog renderer feature) to
// customize things such as rotating the sample direction over time, scrolling, using a single
// channel, remapping the noise range, etc.
// Returns the multiplier that will be applied to the volumetric fog in the final screen output.
// ---------------------------------------------------------------------------------------------
float3 SampleCubemapNoise(float3 sampleDirection, float2 uv)
{
    float3 noise;

    UNITY_BRANCH
    if (_CubemapNoisePointFilter > 0.5)
        noise = SAMPLE_TEXTURECUBE_LOD(_CubemapNoiseTexture, cubemapnoise_point_repeat_sampler, sampleDirection, 0).rgb;
    else
        noise = SAMPLE_TEXTURECUBE_LOD(_CubemapNoiseTexture, cubemapnoise_linear_repeat_sampler, sampleDirection, 0).rgb;

    return lerp(float3(1.0, 1.0, 1.0), noise, _CubemapNoiseIntensity);
}

// Multiplies the cubemap noise onto the volumetric fog contribution of the final screen output.
// The camera color at this point is: sceneColor * fogTransmittance + fogInscattering.
// The fog inscattering is recovered by upsampling the fog texture with the exact same depth aware
// upsample used at composition, so only the fog is modulated and the rest of the scene is untouched.
float4 CubemapNoiseComposition(float2 uv)
{
    float4 volumetricFog = DepthAwareUpsample(uv, _VolumetricFogTexture);
    float4 cameraColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);

    float3 sampleDirection = GetCubemapNoiseSampleDirection(uv);
    float3 noise = SampleCubemapNoise(sampleDirection, uv);

    float3 sceneColorNoFog = max(cameraColor.rgb - volumetricFog.rgb, 0.0);

    return float4(sceneColorNoFog + volumetricFog.rgb * noise, cameraColor.a);
}

#endif