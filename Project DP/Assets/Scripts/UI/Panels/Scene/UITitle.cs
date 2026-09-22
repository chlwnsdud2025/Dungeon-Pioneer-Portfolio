using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UITitle : UIScene
{
    [SerializeField] private Button startButton;

    [Header("게임 시작")]
    [Tooltip("시작 버튼을 누르면 이동할 지역. 저장 기능이 생기면 저장된 위치로 대체할 자리.")]
    [SerializeField] private LocationType startLocation = LocationType.Town;
    [SerializeField] private string startAnchorId = "Start";
    [Min(0f), SerializeField] private float fadeDuration = 0.4f;

    public override void Initialize()
    {
        base.Initialize();

        if (startButton == null)
        {
            Debug.LogError("[UITitle] Start Button이 연결되지 않았습니다.", this);
            return;
        }

        startButton.onClick.AddListener(StartGame);
    }

    public override void OnBeforeShow()
    {
        base.OnBeforeShow();
        if (startButton != null) startButton.interactable = true;
    }

    private void StartGame()
    {
        // 씬 로드가 끝날 때까지 연타로 두 번 이동하지 않게 잠근다.
        startButton.interactable = false;
        StartCoroutine(StartGameRoutine());
    }

    private IEnumerator StartGameRoutine()
    {
        yield return ScreenFader.Instance.FadeTo(1f, fadeDuration);
        ScreenFader.Instance.RevealAfterNextSceneLoad(fadeDuration);
        WorldManager.Travel(new LocationId(startLocation), startAnchorId);
    }
}
