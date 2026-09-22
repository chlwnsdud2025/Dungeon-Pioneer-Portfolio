using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerManager : MonoSingleton<PlayerManager>
{
    [Header("Player")]
    [Tooltip("앱 전체에서 단 하나만 생성될 플레이어 프리팹. (Assets/Prefabs/Player/Player.prefab)")]
    [SerializeField] private GameObject playerPrefab;

    [Header("첫 스폰")]
    [Tooltip("게임을 이 씬에서 바로 시작했을 때 플레이어를 놓을 SpawnAnchor의 id. 없으면 배치를 건너뜁니다.")]
    [SerializeField] private string defaultAnchorId = "Start";

    /// <summary>전역에 하나뿐인 플레이어 오브젝트. 아직 생성 전이면 null.</summary>
    public GameObject Player { get; private set; }
    public PlayerController Controller { get; private set; }
    public PlayerStat Stat { get; private set; }

    private bool hasPlaced; // 이번 씬에서 이미 배치했는지 (Start의 기본 배치와 중복 방지)

    protected override void Awake()
    {
        base.Awake();                 // MonoSingleton: Instance 설정 + DontDestroyOnLoad
        if (Instance != this) return; // 중복 인스턴스면 초기화 스킵 (곧 Destroy됨)

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        if (Instance != this) return;

        EnsurePlayer();

        // 씬 전환으로 들어온 경우엔 HandleSceneLoaded가 이미 배치를 끝냈습니다.
        if (hasPlaced) return;

        // 게임을 이 씬에서 바로 시작한 경우 = 기본 앵커에 배치.
        // 앵커가 없는 씬(던전 씬 단독 실행 등)이면 그 씬의 매니저가 배치하도록 놔둡니다.
        if (SpawnAnchor.TryGetPose(defaultAnchorId, out Vector3 position, out Quaternion rotation))
            PlaceAt(position, rotation);
    }

    // ── 씬 전환 ──────────────────────────────────────────────────

    /// <summary>
    /// 씬 로드 직전 호출. 전환 중 플레이어가 허공에서 낙하하거나 트리거를 잘못 밟지 않도록 꺼둡니다.
    /// 도착 지점에 배치될 때 PlaceAt이 다시 켭니다. (SceneTransition.Go가 자동으로 호출)
    /// </summary>
    public void PrepareForSceneChange()
    {
        hasPlaced = false;
        if (Player != null) Player.SetActive(false);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string anchorId = SceneTransition.ConsumePendingAnchorId();

        // 앵커 요청이 없으면 이 씬의 매니저가 직접 배치한다는 뜻입니다 (던전 씬 = DungeonManager).
        if (string.IsNullOrEmpty(anchorId)) return;

        EnsurePlayer();
        if (Player == null) return;

        if (SpawnAnchor.TryGetPose(anchorId, out Vector3 position, out Quaternion rotation))
        {
            PlaceAt(position, rotation);
            return;
        }

        Debug.LogError($"[PlayerManager] '{scene.name}' 씬에서 SpawnAnchor '{anchorId}'를 찾지 못했습니다. " +
                       "씬에 앵커를 배치해주세요. 일단 원래 자리에 플레이어를 되살립니다.");
        PlaceAt(Player.transform.position, Player.transform.rotation);
    }

    // ── 배치 ─────────────────────────────────────────────────────

    /// <summary>회전은 유지한 채 위치만 옮깁니다. (던전 층 간 이동처럼 '씬 안'에서 옮길 때)</summary>
    public void PlaceAt(Vector3 position)
    {
        EnsurePlayer();
        PlaceAt(position, Player != null ? Player.transform.rotation : Quaternion.identity);
    }

    /// <summary>
    /// 플레이어를 지정한 위치/방향에 배치하는 유일한 진입점.
    /// 부활 처리 → 위치 이동 → 활성화 → 상태 정리 → 카메라 연결 순서가 중요합니다.
    /// </summary>
    public void PlaceAt(Vector3 position, Quaternion rotation)
    {
        EnsurePlayer();
        if (Player == null) return;

        // ① 부활은 상태머신을 Idle로 되돌리기 '전에' 끝나야 합니다. (체력 0인 채로 살아나는 것 방지)
        if (SceneTransition.ConsumeRevive() && Stat != null) Stat.ResetHealth();

        // ② CharacterController가 켜진 채로 position을 직접 바꾸면 충돌 보정이 끼어들어 위치가 튑니다.
        CharacterController cc = Player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        Player.transform.SetPositionAndRotation(position, rotation);

        // ③ 씬 전환 동안 꺼두었던 플레이어를 다시 켭니다. (OnEnable에서 입력을 다시 구독)
        if (!Player.activeSelf) Player.SetActive(true);

        if (cc != null) cc.enabled = true;

        // ④ 이전 씬에서 남은 낙하 속도 / 무적 / 사망 상태를 정리
        if (Controller != null) Controller.PrepareForSceneArrival();

        BindCamera();
        hasPlaced = true;
    }

    /// <summary>
    /// 씬마다 존재하는 Main Camera의 CameraFollow를 플레이어에 다시 연결합니다.
    /// (CameraOcclusionFader는 자기 target이 비어 있으면 CameraFollow.target을 쓰므로 함께 해결됩니다)
    /// </summary>
    public void BindCamera()
    {
        if (Player == null) return;

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[PlayerManager] MainCamera 태그를 가진 카메라가 없어 카메라를 연결하지 못했습니다.");
            return;
        }

        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow == null)
        {
            Debug.LogWarning("[PlayerManager] Main Camera에 CameraFollow가 없어 카메라를 연결하지 못했습니다.");
            return;
        }

        follow.target = Player.transform;
        // 첫 프레임에 이전 위치에서 Lerp로 날아오는 것을 막기 위해 즉시 스냅합니다.
        cam.transform.position = Player.transform.position + follow.offset;
    }

    // ── 생성 ─────────────────────────────────────────────────────

    /// <summary>
    /// Player가 아직 없으면 생성합니다. 스크립트 실행 순서(PlayerManager.Start vs DungeonManager.Start)에
    /// 의존하지 않도록, 플레이어가 필요한 시점마다 호출합니다.
    /// </summary>
    private void EnsurePlayer()
    {
        if (Player != null) return;

        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerManager] playerPrefab이 연결되지 않았습니다. 인스펙터를 확인해주세요.", this);
            return;
        }

        Player = Instantiate(playerPrefab);
        Player.name = playerPrefab.name; // 하이어라키에서 찾기 쉽도록 "(Clone)" 제거
        DontDestroyOnLoad(Player);

        Controller = Player.GetComponent<PlayerController>();
        Stat = Player.GetComponent<PlayerStat>();

        if (Controller == null)
            Debug.LogError("[PlayerManager] playerPrefab에 PlayerController가 없습니다.", this);
    }
}
