using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;

public class VFXManagerOverworld : MonoBehaviour
{
    [SerializeField]
    private Light _directionalLight;
    [SerializeField]
    private Volume _nightOverrideVolume;

    [SerializeField]
    private float _lightingUpdateInterval = 0.05f;
    [SerializeField]
    private float _lightingDayLength = 30f;
    [SerializeField]
    private Vector3 _directionalEulerStart;
    [SerializeField]
    private Vector3 _directionalEulerEnd;

    [SerializeField]
    private Gradient _staticLightGradient;
    [SerializeField]
    private AnimationCurve _volumeOverrideCurve;

    [SerializeField]
    private AnimationCurve _directionalLightIntensityCurve;
    [SerializeField]
    private Gradient _directionalLightColorGradient;

    private void OnEnable()
    {
        StartCoroutine(UpdateLighting());
    }

    private void OnDisable()
    {
        StopCoroutine(UpdateLighting());
    }

    private IEnumerator UpdateLighting()
    {
        float elapsedTime = 0f;
        float evolution;

        Quaternion directionQuaternionStart = Quaternion.Euler(_directionalEulerStart);
        Quaternion directionQuaternionEnd = Quaternion.Euler(_directionalEulerEnd);

        while (true)
        {
            elapsedTime += _lightingUpdateInterval;
            if (elapsedTime > _lightingDayLength) elapsedTime -= _lightingDayLength;

            evolution = elapsedTime / _lightingDayLength;

            _nightOverrideVolume.weight = _volumeOverrideCurve.Evaluate(evolution);
            RenderSettings.ambientLight = _staticLightGradient.Evaluate(evolution);

            _directionalLight.intensity = _directionalLightIntensityCurve.Evaluate(evolution);
            _directionalLight.color = _directionalLightColorGradient.Evaluate(evolution);

            _directionalLight.transform.rotation = Quaternion.Lerp(directionQuaternionStart, directionQuaternionEnd, evolution);

            yield return new WaitForSeconds(_lightingUpdateInterval);
        }
    }
}
