using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 내내 하나만 쓰는 카메라. PlayerManager가 처음 한 번 프리팹(GameCamera)으로 만들고, 씬을 옮겨도 그대로 들고 갑니다.
/// 따라가기(CameraFollow)와 벽 비치기(CameraOcclusionFader)도 이 카메라에 붙어 있어 씬마다 따로 설정할 필요가 없습니다.
///
/// 씬마다 달라야 하는 것(배경, 후처리)은 그 씬의 CameraSceneSettings가 정하고, 씬이 바뀔 때 적용합니다.
/// 씬에 CameraSceneSettings가 없으면 프리팹에 정해 둔 값으로 돌아갑니다.
///
/// 타이틀처럼 자기 카메라(MainCamera 태그)를 가진 씬에서는 이 카메라와 AudioListener를 꺼서 두 카메라가 겹치지 않게 합니다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class GameCamera : MonoSingleton<GameCamera>
{
    public Camera Camera { get; private set; }
    public CameraFollow Follow { get; private set; }

    private AudioListener listener;
    private UniversalAdditionalCameraData urpData;

    // 씬에 CameraSceneSettings가 없을 때 돌아갈 프리팹 기본값
    private CameraClearFlags defaultClearFlags;
    private Color defaultBackground;
    private bool defaultPostProcessing;

    protected override void Awake()
    {
        // 나중에 생긴 중복은 곧 지워지지만, 그 전에 한 프레임이라도 그리거나 소리를 듣지 않게 바로 끕니다.
        if (Instance != null && Instance != this) gameObject.SetActive(false);

        base.Awake();                 // MonoSingleton: Instance 설정 + DontDestroyOnLoad
        if (Instance != this) return;

        Camera = GetComponent<Camera>();
        Follow = GetComponent<CameraFollow>();
        listener = GetComponent<AudioListener>();
        urpData = GetComponent<UniversalAdditionalCameraData>();

        defaultClearFlags = Camera.clearFlags;
        defaultBackground = Camera.backgroundColor;
        defaultPostProcessing = urpData != null && urpData.renderPostProcessing;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        ApplyScene(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) ApplyScene(scene);
    }

    private void ApplyScene(Scene scene)
    {
        CameraSceneSettings settings = null;
        bool sceneHasOwnCamera = false;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (settings == null) settings = root.GetComponentInChildren<CameraSceneSettings>(true);

            foreach (Camera other in root.GetComponentsInChildren<Camera>(true))
            {
                if (other != Camera && other.CompareTag("MainCamera")) sceneHasOwnCamera = true;
            }
        }

        Camera.enabled = !sceneHasOwnCamera;
        if (listener != null) listener.enabled = !sceneHasOwnCamera;

        Camera.clearFlags = settings != null ? settings.clearFlags : defaultClearFlags;
        Camera.backgroundColor = settings != null ? settings.backgroundColor : defaultBackground;
        if (urpData != null) urpData.renderPostProcessing = settings != null ? settings.postProcessing : defaultPostProcessing;
    }
}
