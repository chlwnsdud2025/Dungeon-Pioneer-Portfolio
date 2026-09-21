using UnityEditor;
using UnityEditor.Rendering;

/// <summary>
/// Custom editor for the volumetric fog volume component.
/// </summary>
[CustomEditor(typeof(VolumetricFogVolumeComponent))]
public sealed class VolumetricFogVolumeComponentEditor : VolumeComponentEditor
{
	#region Private Attributes

	private SerializedDataParameter distance;
	private SerializedDataParameter baseHeight;
	private SerializedDataParameter maximumHeight;

	private SerializedDataParameter enableGround;
	private SerializedDataParameter groundHeight;

	private SerializedDataParameter density;
	private SerializedDataParameter attenuationDistance;
#if UNITY_2023_1_OR_NEWER
	private SerializedDataParameter enableAPVContribution;
	private SerializedDataParameter APVContributionWeight;
#endif

	private SerializedDataParameter enableMainLightContribution;
	private SerializedDataParameter anisotropy;
	private SerializedDataParameter scattering;
	private SerializedDataParameter tint;

	private SerializedDataParameter enableAdditionalLightsContribution;

	private SerializedDataParameter enableNoise3D;
	private SerializedDataParameter noise3DTexture;
	private SerializedDataParameter noise3DIntensity;
	private SerializedDataParameter noise3DTiling;
	private SerializedDataParameter noise3DScrollSpeed;
	private SerializedDataParameter noise3DPointFilter;

	private SerializedDataParameter enableCubemapNoise;
	private SerializedDataParameter cubemapNoiseTexture;
	private SerializedDataParameter cubemapNoiseIntensity;
	private SerializedDataParameter cubemapNoisePointFilter;

	private SerializedDataParameter maxSteps;
	private SerializedDataParameter blurIterations;
	private SerializedDataParameter enabled;
	
	private SerializedDataParameter renderPassEvent;

	#endregion

	#region VolumeComponentEditor Methods

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	public override void OnEnable()
	{
		PropertyFetcher<VolumetricFogVolumeComponent> pf = new PropertyFetcher<VolumetricFogVolumeComponent>(serializedObject);

		distance = Unpack(pf.Find(x => x.distance));
		baseHeight = Unpack(pf.Find(x => x.baseHeight));
		maximumHeight = Unpack(pf.Find(x => x.maximumHeight));

		enableGround = Unpack(pf.Find(x => x.enableGround));
		groundHeight = Unpack(pf.Find(x => x.groundHeight));

		density = Unpack(pf.Find(x => x.density));
		attenuationDistance = Unpack(pf.Find(x => x.attenuationDistance));
#if UNITY_2023_1_OR_NEWER
		enableAPVContribution = Unpack(pf.Find(x => x.enableAPVContribution));
		APVContributionWeight = Unpack(pf.Find(x => x.APVContributionWeight));
#endif

		enableMainLightContribution = Unpack(pf.Find(x => x.enableMainLightContribution));
		anisotropy = Unpack(pf.Find(x => x.anisotropy));
		scattering = Unpack(pf.Find(x => x.scattering));
		tint = Unpack(pf.Find(x => x.tint));

		enableAdditionalLightsContribution = Unpack(pf.Find(x => x.enableAdditionalLightsContribution));

		enableNoise3D = Unpack(pf.Find(x => x.enableNoise3D));
		noise3DTexture = Unpack(pf.Find(x => x.noise3DTexture));
		noise3DIntensity = Unpack(pf.Find(x => x.noise3DIntensity));
		noise3DTiling = Unpack(pf.Find(x => x.noise3DTiling));
		noise3DScrollSpeed = Unpack(pf.Find(x => x.noise3DScrollSpeed));
		noise3DPointFilter = Unpack(pf.Find(x => x.noise3DPointFilter));

		enableCubemapNoise = Unpack(pf.Find(x => x.enableCubemapNoise));
		cubemapNoiseTexture = Unpack(pf.Find(x => x.cubemapNoiseTexture));
		cubemapNoiseIntensity = Unpack(pf.Find(x => x.cubemapNoiseIntensity));
		cubemapNoisePointFilter = Unpack(pf.Find(x => x.cubemapNoisePointFilter));

		maxSteps = Unpack(pf.Find(x => x.maxSteps));
		blurIterations = Unpack(pf.Find(x => x.blurIterations));
		enabled = Unpack(pf.Find(x => x.enabled));
		
		renderPassEvent = Unpack(pf.Find(x => x.renderPassEvent));
	}

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	public override void OnInspectorGUI()
	{
		bool isEnabled = enabled.overrideState.boolValue && enabled.value.boolValue;

		if (!isEnabled)
		{
			PropertyField(enabled);
			return;
		}

		bool enabledGround = enableGround.overrideState.boolValue && enableGround.value.boolValue;
		bool enabledMainLightContribution = enableMainLightContribution.overrideState.boolValue && enableMainLightContribution.value.boolValue;
		bool enabledAdditionalLightsContribution = enableAdditionalLightsContribution.overrideState.boolValue && enableAdditionalLightsContribution.value.boolValue;

		PropertyField(distance);
		PropertyField(baseHeight);
		PropertyField(maximumHeight);

		PropertyField(enableGround);
		if (enabledGround)
			PropertyField(groundHeight);

		PropertyField(density);
		PropertyField(attenuationDistance);
#if UNITY_2023_1_OR_NEWER
		bool enabledAPVContribution = enableAPVContribution.overrideState.boolValue && enableAPVContribution.value.boolValue;
		PropertyField(enableAPVContribution);
		if (enabledAPVContribution)
			PropertyField(APVContributionWeight);
#endif

		PropertyField(enableMainLightContribution);
		if (enabledMainLightContribution)
		{
			PropertyField(anisotropy);
			PropertyField(scattering);
			PropertyField(tint);
		}

		PropertyField(enableAdditionalLightsContribution);

		bool enabledNoise3D = enableNoise3D.overrideState.boolValue && enableNoise3D.value.boolValue;
		PropertyField(enableNoise3D);
		if (enabledNoise3D)
		{
			PropertyField(noise3DTexture);
			PropertyField(noise3DIntensity);
			PropertyField(noise3DTiling);
			PropertyField(noise3DScrollSpeed);
			PropertyField(noise3DPointFilter);
		}

		bool enabledCubemapNoise = enableCubemapNoise.overrideState.boolValue && enableCubemapNoise.value.boolValue;
		PropertyField(enableCubemapNoise);
		if (enabledCubemapNoise)
		{
			PropertyField(cubemapNoiseTexture);
			PropertyField(cubemapNoiseIntensity);
			PropertyField(cubemapNoisePointFilter);
		}

		PropertyField(maxSteps);
		PropertyField(blurIterations);
		PropertyField(enabled);
		
		PropertyField(renderPassEvent);
	}

	#endregion
}