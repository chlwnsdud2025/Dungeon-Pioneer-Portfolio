using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;
using URAND = UnityEngine.Random;

public class PlayerVFXManager : MonoBehaviour
{
    [SerializeField]
    private float   _effectUpdateInterval = 0.02f;

    [SerializeField]
    private Light   _torchLightOutfacing;
    [SerializeField]
    private Light   _torchLightInFacing;

    [SerializeField]
    private Vector2 _lightMultiplierRange;
    [SerializeField]
    private Vector2 _lightRandomizationInterval;
    [SerializeField]
    private float   _lightIntensityLerpMultiplier = 2f;

    private Vector2 _lightBaseIntensity;
    private float   _lightMultiplierCursor;

    private void OnEnable()
    {
        _lightBaseIntensity[0] = _torchLightOutfacing.intensity;
        _lightBaseIntensity[1] = _torchLightInFacing.intensity;

        StartCoroutine(PlayerTorchFlickerRoutine());
    }

    private void OnDisable()
    {
        StopCoroutine(PlayerTorchFlickerRoutine());
    }

    // 던전 내의 횃불들은 컬링도 필요하며,
    // 나중에 다른 메커니즘 추가를 위해 이건 일단 플레이어만 적용
    private IEnumerator PlayerTorchFlickerRoutine()
    {
        float randomizationCursor = 0f;
        float nextRandomization = RandomizeLightCursor();

        while (true)
        {
            randomizationCursor += _effectUpdateInterval;
            if (randomizationCursor >= nextRandomization)
            {
                randomizationCursor -= nextRandomization;
                nextRandomization = RandomizeLightCursor();
                _lightMultiplierCursor = URAND.Range(_lightMultiplierRange[0], _lightMultiplierRange[1]);
            }

            _torchLightOutfacing.intensity = Mathf.Lerp(_torchLightOutfacing.intensity,
                _lightBaseIntensity[0] * _lightMultiplierCursor, _effectUpdateInterval * _lightIntensityLerpMultiplier);
            _torchLightInFacing.intensity = Mathf.Lerp(_torchLightInFacing.intensity,
                _lightBaseIntensity[1] * _lightMultiplierCursor, _effectUpdateInterval * _lightIntensityLerpMultiplier);

            yield return new WaitForSeconds(_effectUpdateInterval);
        }
    }

    private float RandomizeLightCursor()
    {
        return URAND.Range(_lightRandomizationInterval[0], _lightRandomizationInterval[1]);
    }
}
