using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIInGameHUD : UIScene
{
    [Header("Minimap (이 HUD가 소유 → MinimapManager에 연결. 던전에서만 표시)")]
    [SerializeField] private RawImage minimapImage;
    [SerializeField] private AspectRatioFitter minimapFitter;

    [Header("Health Bar")]
    [Tooltip("Image Type을 'Filled'로 설정한 체력 게이지. fillAmount(0~1)로 채웁니다.")]
    [SerializeField] private Image healthFill;
    [Tooltip("(선택) '80 / 100' 같은 수치 텍스트.")]
    [SerializeField] private TMP_Text healthText;

    [Header("MP Bar")]
    [Tooltip("Image Type을 'Filled'로 설정한 마나 게이지. fillAmount(0~1)로 채웁니다.")]
    [SerializeField] private Image manaFill;
    [Tooltip("(선택) '40 / 100' 같은 수치 텍스트.")]
    [SerializeField] private TMP_Text manaText;

    [Header("Exploration (현재 층 개척도)")]
    [Tooltip("현재 층을 얼마나 개척했는지 보여주는 텍스트. 던전에서만 표시됩니다.")]
    [SerializeField] private TMP_Text explorationText;
    [Tooltip("진행 중 표시 형식. {0}에 정수 퍼센트가 들어갑니다.")]
    [SerializeField] private string explorationFormat = "개척도 {0}%";
    [Tooltip("임계값을 넘겼을 때 표시할 문구.")]
    [SerializeField] private string explorationCompleteLabel = "개척 완료";

    [Header("Gold")]
    [Tooltip("보유 골드 텍스트. PlayerWallet의 잔액을 그대로 보여줍니다.")]
    [SerializeField] private TMP_Text goldText;
    [Tooltip("골드 표시 형식. {0}에 잔액이 들어갑니다.")]
    [SerializeField] private string goldFormat = "{0}";

    [Header("Quick Slot (단축키 1~4)")]
    [Tooltip("퀵슬롯 바. Player의 QuickSlotHolder와 인벤토리에 이 HUD가 연결해줍니다.")]
    [SerializeField] private UIQuickSlotBar quickSlotBar;


    // ── 내부 캐시 ──
    private PlayerController _player;
    private string _lastExplorationLabel;
    private int _lastGold = -1;



    public override void OnBeforeShow()
    {
        base.OnBeforeShow();
        // 보이기 직전에 한 번 즉시 갱신해서 첫 프레임 깜빡임을 줄임
        _player = FindPlayer();
        _lastExplorationLabel = null;
        _lastGold = -1;
        Refresh();
    }
    private void OnEnable()
    {
        if (InputManager.Instance == null) return;

        InputManager.Instance.OnInventoryToggle += ToggleInventory;
        InputManager.Instance.OnQuickSlotUsed += UseQuickSlot;
    }
    private void OnDisable()
    {
        if (InputManager.Instance == null) return;

        InputManager.Instance.OnInventoryToggle -= ToggleInventory;
        InputManager.Instance.OnQuickSlotUsed -= UseQuickSlot;
    }


    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        RefreshMinimap();
        RefreshStatus();
        RefreshExploration();
        RefreshGold();
        EnsurePlayerBindings();
    }

    // 현재 층의 개척도. 층마다 따로 집계되므로 층을 옮기면 그 층의 값이 자동으로 나옵니다.
    private void RefreshExploration()
    {
        if (explorationText == null) return;

        // 미니맵과 같은 기준으로 표시/숨김을 판단합니다. 던전을 나가면 MinimapManager.Instance가
        // 파괴된 객체라 fake-null로 걸러져 자동으로 숨겨집니다.
        MinimapManager mm = MinimapManager.Instance;
        bool show = RegionState.Region == RegionType.Dungeon && mm != null && mm.HasCurrentLocation;

        if (explorationText.gameObject.activeSelf != show) explorationText.gameObject.SetActive(show);
        if (!show)
        {
            _lastExplorationLabel = null;
            return;
        }

        // 버림을 쓰는 이유: 반올림하면 아직 개척이 안 끝났는데 100%로 보입니다.
        string label = mm.IsCurrentExplored
            ? explorationCompleteLabel
            : string.Format(explorationFormat, Mathf.FloorToInt(mm.CurrentExploration * 100f));

        if (label == _lastExplorationLabel) return;

        _lastExplorationLabel = label;
        explorationText.text = label;
    }

    // 골드는 Player의 PlayerWallet이 들고 있습니다. 상자 보상·상점 결제 등
    // 어느 경로로 잔액이 바뀌어도 여기에 반영됩니다.
    private void RefreshGold()
    {
        if (goldText == null) return;

        // 플레이어/지갑 연결 전이면 0. 준비되면 실제 잔액이 채워집니다.
        int gold = (_player != null && _player.wallet != null) ? _player.wallet.Gold : 0;

        // 값이 바뀔 때만 갱신 (매 프레임 TMP 텍스트를 건드리지 않도록)
        if (gold == _lastGold) return;

        _lastGold = gold;
        goldText.text = string.Format(goldFormat, gold);
    }

    /// <summary>
    /// 플레이어 쪽 컴포넌트들을 인벤토리에 연결합니다. (퀵슬롯 바, QuickSlotHolder, 그리고 횃불 소모에
    /// 인벤토리가 필요한 PlayerController)
    ///
    /// PlayerManager와 UIManager의 초기화 시점이 서로 달라(UI는 비동기 로드) 준비되는 순간이 제각각이므로,
    /// _player를 지연 조회하는 기존 방식과 똑같이 준비될 때까지 매 프레임 시도합니다.
    ///
    /// 인벤토리 팝업이 아니라 HUD가 바인딩을 맡는 이유: EquipmentHolder처럼 팝업이 열릴 때 Init하면
    /// 인벤토리를 한 번도 열지 않은 상태에서는 단축키도, 횃불 설치도 동작하지 않습니다.
    /// </summary>
    private void EnsurePlayerBindings()
    {
        if (_player == null) return;
        if (_player.Inventory != null && (quickSlotBar == null || quickSlotBar.IsBound)) return;

        UIInventoryPopup popup = UIManager.Instance != null ? UIManager.Instance.GetPanel<UIInventoryPopup>() : null;
        Inventory inventory = popup != null ? popup.Inventory : null;
        if (inventory == null) return;

        if (_player.Inventory == null) _player.BindInventory(inventory);

        if (quickSlotBar == null || quickSlotBar.IsBound) return;

        QuickSlotHolder holder = _player.GetComponent<QuickSlotHolder>();
        if (holder == null) return;

        if (!holder.IsInitialized) holder.Init(inventory);
        quickSlotBar.Bind(holder, inventory, _player);
    }

    private void UseQuickSlot(int index)
    {
        if (quickSlotBar != null) quickSlotBar.Use(index);
    }

    // 미니맵: MinimapManager가 그린 텍스처를 HUD RawImage에 표시합니다.
    // MinimapManager는 전역 HUD에 하나만 존재하며, 던전 씬의 중복 Canvas는 사용하지 않습니다.
    private void RefreshMinimap()
    {
        if (minimapImage == null) return;

        // 현재 지역에 미니맵 소스가 있을 때만 표시합니다.
        bool show = HasMinimapFor(RegionState.Region);
        if (minimapImage.gameObject.activeSelf != show) minimapImage.gameObject.SetActive(show);
        if (!show) return;

        MinimapManager mm = MinimapManager.Instance;
        Texture tex = mm.CurrentTexture;
        if (minimapImage.texture != tex) minimapImage.texture = tex;
        if (minimapFitter != null && tex != null) minimapFitter.aspectRatio = mm.CurrentAspect;
    }

    // 체력·마나 게이지. 둘 다 PlayerStat의 현재값/최대값을 그대로 보여줍니다.
    private void RefreshStatus()
    {
        if (_player == null) _player = FindPlayer();
        if (_player == null || _player.stat == null) return;

        PlayerStat stat = _player.stat;
        ApplyGauge(healthFill, healthText, stat.currentHealth, stat.maxHealth);
        ApplyGauge(manaFill, manaText, stat.currentMana, stat.maxMana);
    }

    private static void ApplyGauge(Image fill, TMP_Text text, float current, float max)
    {
        if (max <= 0f) return;

        if (fill != null) fill.fillAmount = Mathf.Clamp01(current / max);
        if (text != null) text.text = $"{Mathf.CeilToInt(Mathf.Max(0f, current))} / {Mathf.CeilToInt(max)}";
    }

    /// <summary>
    /// 이 지역에 표시할 미니맵이 있는지. 지금은 던전(MinimapManager)만 미니맵을 갖고 있습니다.
    /// 오버월드/마을 미니맵이 생기면 여기에 조건을 추가하세요.
    /// </summary>
    private static bool HasMinimapFor(RegionType? region)
    {
        return region == RegionType.Dungeon && MinimapManager.Instance != null;
    }

    private void ToggleInventory()
    {
        var uiMgr = UIManager.Instance;
        if (uiMgr == null) return;

        var popup = uiMgr.GetPanel<UIInventoryPopup>();
        if (popup != null && popup.gameObject.activeSelf)
            uiMgr.HidePopup().Forget();
        else
            uiMgr.ShowPopup<UIInventoryPopup>().Forget();
    }



    // 플레이어는 PlayerManager가 전역으로 하나만 들고 있습니다.
    // (씬 전환 중에는 잠시 비활성이라 태그 검색으로는 못 찾으므로 PlayerManager를 먼저 봅니다)
    private PlayerController FindPlayer()
    {
        if (PlayerManager.Instance != null && PlayerManager.Instance.Controller != null)
            return PlayerManager.Instance.Controller;

        GameObject go = GameObject.FindGameObjectWithTag("Player");
        return go != null ? go.GetComponent<PlayerController>() : null;
    }
}
