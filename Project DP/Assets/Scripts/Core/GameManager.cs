using System;
using UnityEngine;

/// <summary>
/// 게임 전체의 인게임 루프(상태 흐름)를 총괄하는 싱글턴 매니저.
///
/// 흐름: Boot → GeneratingMap → Playing ⇄ Paused → (층 이동: GeneratingMap 반복) → GameOver / Victory
///
/// - "지금 어느 장소에 있는지" "장소를 어떻게 만들고 전환하는지"는 WorldManager가 담당하고,
///   GameManager는 던전 층 진행에 특화된 규칙(현재 몇 층인지, 몇 층에서 Victory인지)만 판단해서
///   WorldManager.TravelTo를 호출합니다.
/// - dungeonGenerator는 "층마다 겹치지 않는 맵을 배정"하는 런 시작 준비(PrepareNewRun)를 위해서만 참조합니다.
/// - 플레이어/몬스터 FSM은 각자 스크립트가 그대로 담당하고, GameManager에는 "결과"만 통보(이벤트 호출)하면 됩니다.
///   예) 플레이어 HP가 0이 되면 PlayerController에서 GameManager.Instance.ReportPlayerDeath() 호출
///       출구 타일을 밟으면 ExitPoint.cs가 GameManager.Instance.ReportExitReached()를 자동 호출
///       입구 타일을 밟으면 EntrancePoint.cs가 GameManager.Instance.ReportEntranceReached()를 자동 호출
/// - 마을/오버월드로 나가는 흐름이 추가되면, 그 이동은 층 카운트와 무관하므로 LocationConnector가
///   WorldManager.TravelTo를 직접 호출합니다 (GameManager를 거치지 않음).
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
        Victory         // 최종 클리어 (마지막 층 탈출)
    }

    [Header("Run 진행 상태")]
    [Tooltip("현재 몇 번째 던전 층인지. 1부터 시작")]
    public int currentFloor = 1;
    [Tooltip("이 층까지 가면 Victory 처리. 0이면 무제한(끝없이 다음 층 생성)")]
    public int maxFloor = 5;

    [Header("장소 이동 / 던전 생성 연결")]
    [Tooltip("장소(던전 층/마을/오버월드) 이동을 총괄하는 매니저.")]
    public WorldManager worldManager;
    [Tooltip("던전 층 배정(중복 없는 셔플)을 준비시키기 위해 참조합니다.")]
    public DungeonGenerator dungeonGenerator;

    [Header("현재 상태 (읽기 전용, 디버그용)")]
    [SerializeField] private GameState currentState = GameState.Boot;
    public GameState CurrentState => currentState;

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

        dungeonGenerator.PrepareNewRun(maxFloor > 0 ? maxFloor : 1);
        worldManager.ResetWorld();

        EnterDungeonFloor(currentFloor, arrivalSpawnPointId: "Entrance");
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

        EnterDungeonFloor(currentFloor, "Entrance"); // 새 층의 '입구'에 스폰
    }

    /// <summary>입구를 밟는 등, 이전 층으로 돌아갈 때 호출 (EntrancePoint.cs에서 자동 호출됨)</summary>
    public void ReportEntranceReached()
    {
        if (currentState != GameState.Playing) return;

        // 1층에서는 더 내려갈 층이 없으므로 무시
        if (currentFloor <= 1) return;

        currentFloor--;
        EnterDungeonFloor(currentFloor, "Exit"); // 이전 층의 '출구'(원래 나갔던 자리)에 스폰
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
        StartRun(); // StartRun 내부에서 기존 장소/플레이어를 전부 정리하고 다시 섞음
    }

    // ── 내부 로직 ────────────────────────────────────────────────

    private void EnterDungeonFloor(int floor, string arrivalSpawnPointId)
    {
        SetState(GameState.GeneratingMap);

        if (worldManager == null || dungeonGenerator == null)
        {
            Debug.LogError("[GameManager] WorldManager/DungeonGenerator가 연결되어 있지 않습니다! 인스펙터를 확인해주세요.");
            return;
        }

        worldManager.TravelTo(new LocationId(LocationType.Dungeon, floor), arrivalSpawnPointId);

        OnFloorStarted?.Invoke(floor);
        SetState(GameState.Playing);
    }

    private void SetState(GameState newState)
    {
        currentState = newState;
        OnStateChanged?.Invoke(currentState);
    }
}
