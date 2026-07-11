using System;
using UnityEngine;

/// <summary>
/// 게임 전체의 인게임 루프(상태 흐름)를 총괄하는 싱글턴 매니저.
///
/// 흐름: Boot → GeneratingMap → Playing ⇄ Paused → (다음 층: GeneratingMap 반복) → GameOver / Victory
///
/// - 실제 맵 생성(던전 미로/이후 필드맵)은 DungeonGenerator(혹은 이후 만들 FieldGenerator)가 담당하고,
///   GameManager는 "언제 생성을 시작할지 / 언제 다음 층으로 넘어갈지 / 언제 게임오버인지"만 결정합니다.
/// - 플레이어/몬스터 FSM은 각자 스크립트가 그대로 담당하고, GameManager에는 "결과"만 통보(이벤트 호출)하면 됩니다.
///   예) 플레이어 HP가 0이 되면 PlayerController에서 GameManager.Instance.ReportPlayerDeath() 호출
///       출구 타일을 밟으면 ExitPoint.cs가 GameManager.Instance.ReportExitReached()를 자동 호출
/// - 필드 맵을 나중에 추가할 때는 fieldGenerator 참조를 하나 더 두고,
///   GenerateCurrentFloor()에서 층수/모드에 따라 dungeonGenerator/fieldGenerator 중 하나를 선택하도록 확장하면 됩니다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState
    {
        Boot,           // 씬 로드 직후
        GeneratingMap,  // 맵(던전/필드) 생성 중
        Playing,        // 정상 플레이 중
        Paused,         // 일시정지
        GameOver,       // 플레이어 사망
        Victory         // 최종 클리어 (예: 마지막 층 탈출)
    }

    [Header("Run 진행 상태")]
    [Tooltip("현재 몇 번째 던전/층인지. 1부터 시작")]
    public int currentFloor = 1;
    [Tooltip("이 층까지 가면 Victory 처리. 0이면 무제한(끝없이 다음 층 생성)")]
    public int maxFloor = 0;

    [Header("맵 생성기 연결")]
    [Tooltip("던전(미로형) 맵 생성기. 씬의 DungeonGenerator를 연결하세요.")]
    public DungeonGenerator dungeonGenerator;
    // ★ 확장 지점: 필드 맵 생성기를 추가하게 되면 여기에 참조를 추가하고,
    //   GenerateCurrentFloor()에서 currentFloor나 별도 모드 값에 따라 분기하면 됩니다.
    // public FieldGenerator fieldGenerator;

    [Header("현재 상태 (읽기 전용, 디버그용)")]
    [SerializeField] private GameState currentState = GameState.Boot;
    public GameState CurrentState => currentState;

    [Header("현재 런타임 참조 (읽기 전용)")]
    public GameObject currentMapInstance;
    public GameObject currentPlayer;

    // ── 이벤트: UI/사운드/이펙트 등이 구독해서 반응하도록 ──────────
    public event Action<GameState> OnStateChanged;
    public event Action<int> OnFloorStarted;   // 새 층 시작 시 (파라미터: floor 번호)
    public event Action OnPlayerDied;
    public event Action OnGameCleared;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 씬 전환 후에도 게임 진행 상태(현재 층 등)를 유지하고 싶다면 주석 해제
        // DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        StartRun();
    }

    // ── 외부에서 호출할 진입점들 ─────────────────────────────────

    /// <summary>새 게임(런)을 처음부터 시작</summary>
    public void StartRun()
    {
        Time.timeScale = 1f;
        currentFloor = 1;
        GenerateCurrentFloor();
    }

    /// <summary>출구를 밟는 등, 현재 층 클리어 시 호출 (ExitPoint.cs에서 자동 호출됨)</summary>
    public void ReportExitReached()
    {
        if (currentState != GameState.Playing) return;

        currentFloor++;

        if (maxFloor > 0 && currentFloor > maxFloor)
        {
            SetState(GameState.Victory);
            OnGameCleared?.Invoke();
            return;
        }

        GenerateCurrentFloor();
    }

    public void ReportEntranceReached()
    {
        if (currentState != GameState.Playing) return;

        // 1층에서는 더 내려갈 층이 없으므로 무시
        if (currentFloor <= 1) return;

        currentFloor--;
        GenerateCurrentFloor();
    }

    /// <summary>플레이어 사망 시 PlayerController 등에서 호출</summary>
    public void ReportPlayerDeath()
    {
        if (currentState == GameState.GameOver) return;

        SetState(GameState.GameOver);
        OnPlayerDied?.Invoke();
        Time.timeScale = 0f;
    }

    public void PauseGame()
    {
        if (currentState != GameState.Playing) return;
        SetState(GameState.Paused);
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        if (currentState != GameState.Paused) return;
        SetState(GameState.Playing);
        Time.timeScale = 1f;
    }

    public void RestartRun()
    {
        Time.timeScale = 1f;
        CleanupCurrentMap();
        StartRun();
    }

    // ── 내부 로직 ────────────────────────────────────────────────

    private void GenerateCurrentFloor()
    {
        SetState(GameState.GeneratingMap);
        CleanupCurrentMap();

        if (dungeonGenerator == null)
        {
            Debug.LogError("[GameManager] DungeonGenerator가 연결되어 있지 않습니다! 인스펙터를 확인해주세요.");
            return;
        }

        dungeonGenerator.GenerateDungeonAndSpawnPlayer();

        // DungeonGenerator가 생성 직후 기록해둔 참조를 그대로 받아옴
        currentMapInstance = dungeonGenerator.LastSpawnedMap;
        currentPlayer = dungeonGenerator.LastSpawnedPlayer;

        OnFloorStarted?.Invoke(currentFloor);
        SetState(GameState.Playing);
    }

    private void CleanupCurrentMap()
    {
        if (currentMapInstance != null) Destroy(currentMapInstance);
        if (currentPlayer != null) Destroy(currentPlayer);
        currentMapInstance = null;
        currentPlayer = null;
    }

    private void SetState(GameState newState)
    {
        currentState = newState;
        OnStateChanged?.Invoke(currentState);
    }
}