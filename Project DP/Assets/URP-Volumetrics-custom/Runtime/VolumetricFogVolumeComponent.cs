using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Volume component for the volumetric fog.
/// </summary>
#if UNITY_2023_1_OR_NEWER
[VolumeComponentMenu("Custom/Volumetric Fog")]
#if UNITY_6000_0_OR_NEWER
[VolumeRequiresRendererFeatures(typeof(VolumetricFogRendererFeature))]
#endif
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
#else
[VolumeComponentMenuForRenderPipeline("Custom/Volumetric Fog", typeof(UniversalRenderPipeline))]
#endif
#if UNITY_6000_3_OR_NEWER
[DisplayInfo(name = "Volumetric Fog")]
#endif
public sealed class VolumetricFogVolumeComponent : VolumeComponent, IPostProcessComponent
{
	#region Public Attributes

	[Header("Distances")]
	[Tooltip("The maximum distance from the camera that the fog will be rendered up to.")]
	public ClampedFloatParameter distance = new ClampedFloatParameter(64.0f, 0.0f, 512.0f);
	[Tooltip("The world height at which the fog will have the density specified in the volume.")]
	public FloatParameter baseHeight = new FloatParameter(0.0f, true);
	[Tooltip("The world height at which the fog will have no density at all.")]
	public FloatParameter maximumHeight = new FloatParameter(50.0f, true);

	[Header("Ground")]
	[Tooltip("When enabled, allows to define a world height. Below it, fog will have no density at all.")]
	public BoolParameter enableGround = new BoolParameter(false, BoolParameter.DisplayType.Checkbox, true);
	[Tooltip("Below this world height, fog will have no density at all.")]
	public FloatParameter groundHeight = new FloatParameter(0.0f);

	[Header("Lighting")]
	[Tooltip("How dense is the fog.")]
	public ClampedFloatParameter density = new ClampedFloatParameter(0.2f, 0.0f, 1.0f);
	[Tooltip("Value that defines how much the fog attenuates light as distance increases. Lesser values lead to a darker image.")]
	public MinFloatParameter attenuationDistance = new MinFloatParameter(128.0f, 0.05f);
#if UNITY_2023_1_OR_NEWER
	[Tooltip("When enabled, adaptive probe volumes (APV) will be sampled to contribute to fog.")]
	public BoolParameter enableAPVContribution = new BoolParameter(false, BoolParameter.DisplayType.Checkbox, true);
	[Tooltip("A weight factor for the light coming from adaptive probe volumes (APV) when the probe volume contribution is enabled.")]
	public ClampedFloatParameter APVContributionWeight = new ClampedFloatParameter(1.0f, 0.0f, 1.0f);
#endif

	[Header("Main Light")]
	[Tooltip("Disabling this will avoid computing the main light contribution to fog, which in most cases will lead to better performance.")]
	public BoolParameter enableMainLightContribution = new BoolParameter(false, BoolParameter.DisplayType.Checkbox, true);
	[Tooltip("Higher positive values will make the fog affected by the main light to appear brighter when directly looking to it, while lower negative values will make the fog to appear brighter when looking away from it. The closer the value is closer to 1 or -1, the less the brightness will spread. Most times, positive values higher than 0 and lower than 1 should be used.")]
	public ClampedFloatParameter anisotropy = new ClampedFloatParameter(0.4f, -1.0f, 1.0f);
	[Tooltip("Higher values will make fog affected by the main light to appear brighter.")]
	public ClampedFloatParameter scattering = new ClampedFloatParameter(0.15f, 0.0f, 1.0f);
	[Tooltip("A multiplier color to tint the main light fog.")]
	public ColorParameter tint = new ColorParameter(Color.white, true, false, true);

	[Header("Additional Lights")]
	[Tooltip("Disabling this will avoid computing additional lights contribution to fog, which in most cases will lead to better performance.")]
	public BoolParameter enableAdditionalLightsContribution = new BoolParameter(false, BoolParameter.DisplayType.Checkbox, true);

	[Header("3D Noise")]
	[Tooltip("When enabled, a 3D noise texture is sampled at each raymarch step to modulate the fog density. Only the red channel of the texture is used.")]
	public BoolParameter enableNoise3D = new BoolParameter(false, BoolParameter.DisplayType.Checkbox, true);
	[Tooltip("The 3D texture whose red channel multiplies the fog density.")]
	public TextureParameter noise3DTexture = new TextureParameter(null);
	[Tooltip("How much the 3D noise affects the fog density. At 0 the density is left untouched, at 1 it is fully multiplied by the noise.")]
	public ClampedFloatParameter noise3DIntensity = new ClampedFloatParameter(1.0f, 0.0f, 1.0f);
	[Tooltip("World space tiling of the 3D noise. The sampled coordinate is: worldPosition * tiling + offset, so higher values repeat the texture more often.")]
	public Vector3Parameter noise3DTiling = new Vector3Parameter(new Vector3(0.1f, 0.1f, 0.1f));
	[Tooltip("Scrolls the 3D noise over time to simulate wind. The fog pattern drifts along this direction. Expressed in texture repetitions per second, so world speed also depends on the tiling: worldUnitsPerSecond = scrollSpeed / tiling.")]
	public Vector3Parameter noise3DScrollSpeed = new Vector3Parameter(Vector3.zero);
	[Tooltip("When enabled, the 3D noise is sampled with point (nearest) filtering instead of trilinear filtering.")]
	public BoolParameter noise3DPointFilter = new BoolParameter(false, BoolParameter.DisplayType.Checkbox, true);

	[Header("Cubemap Noise")]
	[Tooltip("When enabled, a cubemap texture is multiplied onto the volumetric fog in the final screen output, sampled along the view direction of each fragment.")]
	public BoolParameter enableCubemapNoise = new BoolParameter(false, BoolParameter.DisplayType.Checkbox, true);
	[Tooltip("The cubemap texture that is multiplied onto the volumetric fog.")]
	public TextureParameter cubemapNoiseTexture = new TextureParameter(null);
	[Tooltip("How much the cubemap noise affects the volumetric fog. At 0 the fog is left untouched, at 1 the fog is fully multiplied by the cubemap.")]
	public ClampedFloatParameter cubemapNoiseIntensity = new ClampedFloatParameter(1.0f, 0.0f, 1.0f);
	[Tooltip("When enabled, the cubemap is sampled with point (nearest) filtering instead of bilinear filtering.")]
	public BoolParameter cubemapNoisePointFilter = new BoolParameter(false, BoolParameter.DisplayType.Checkbox, true);

	[Header("Performance & Quality")]
	[Tooltip("Raymarching steps. Greater values will increase the fog quality at the expense of performance.")]
	public ClampedIntParameter maxSteps = new ClampedIntParameter(128, 8, 256);
	[Tooltip("The number of times that the fog texture will be blurred. Higher values lead to softer volumetric god rays at the cost of some performance.")]
	public ClampedIntParameter blurIterations = new ClampedIntParameter(2, 1, 4);
	[Tooltip("Disabling this will completely remove any feature from the volumetric fog from being rendered at all.")]
	public BoolParameter enabled = new BoolParameter(false, BoolParameter.DisplayType.Checkbox, true);

	[Header("Render Pass Event")]
	[Tooltip("The URP render pass event to render the volumetric fog.")]
	public VolumetricFogRenderPassEventParameter renderPassEvent = new VolumetricFogRenderPassEventParameter(VolumetricFogRenderPass.DefaultVolumetricFogRenderPassEvent);

	#endregion

	#region Initialization Methods

#if !UNITY_6000_3_OR_NEWER

	public VolumetricFogVolumeComponent() : base()
	{
		displayName = "Volumetric Fog";
	}

#endif

	#endregion

	#region Volume Component Methods

	private void OnValidate()
	{
		maximumHeight.overrideState = baseHeight.overrideState;
		maximumHeight.value = Mathf.Max(baseHeight.value, maximumHeight.value);
		baseHeight.value = Mathf.Min(baseHeight.value, maximumHeight.value);
	}

	#endregion

	#region IPostProcessComponent Methods

#if !UNITY_2023_1_OR_NEWER

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	/// <returns></returns>
	public bool IsTileCompatible()
	{
		return true;
	}

#endif

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	/// <returns></returns>
	public bool IsActive()
	{
		return enabled.value && distance.value > 0.0f && groundHeight.value < maximumHeight.value && density.value > 0.0f;
	}

	#endregion
}