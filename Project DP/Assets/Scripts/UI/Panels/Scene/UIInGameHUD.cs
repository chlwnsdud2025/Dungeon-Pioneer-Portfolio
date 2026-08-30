using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 항상 떠 있는 인게임 HUD. 세 가지를 한 화면에 묶습니다:
///  1) 미니맵     — 표시 위젯(RawImage+AspectRatioFitter)을 이 HUD가 소유하고, MinimapManager에 연결(Bind).
///                  던전에서만 보이고, 오버월드/마을에서는 숨깁니다.
///  2) 체력 바    — PlayerStat.currentHealth / maxHealth 를 매 프레임 폴링
///  3) 지역 표시  — RegionState.Region 기준: 던전이면 "n층", 오버월드/마을이면 각각의 라벨
///
/// 이 HUD는 전역 UIManager가 로드해 상시 표시되며 씬을 넘어도 유지됩니다.
/// MinimapManager는 던전 씬마다 새로 생기므로, 인스턴스가 바뀌면 자동으로 다시 연결합니다.
/// </summary>
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

    [Header("Region / Floor")]
    [Tooltip("현재 지역을 표시하는 텍스트. 던전이면 층, 그 외엔 지역 이름이 나옵니다.")]
    [SerializeField] private TMP_Text regionText;
    [Tooltip("던전일 때 층 표시 형식. {0}에 층 번호가 들어갑니다.")]
    [SerializeField] private string floorFormat = "{0}층";
    [SerializeField] private string overworldLabel = "Overworld";
    [SerializeField] private string townLabel = "Town";

    [Header("Torch")]
    [Tooltip("남은 횃불 개수 텍스트. 인벤토리의 횃불 아이템 재고를 그대로 보여줍니다.")]
    [SerializeField] private TMP_Text torchCountText;
    [Tooltip("개수 표시 형식. {0}에 개수가 들어갑니다.")]
    [SerializeField] private string torchCountFormat = "{0}";

    [Header("Quick Slot (단축키 1~4)")]
    [Tooltip("퀵슬롯 바. Player의 QuickSlotHolder와 인벤토리에 이 HUD가 연결해줍니다.")]
    [SerializeField] private UIQuickSlotBar quickSlotBar;


    // ── 내부 캐시 ──
    private PlayerController _player;
    private string _lastRegionLabel;
    private int _lastTorchStock = -1;



    public override void OnBeforeShow()
    {
        base.OnBeforeShow();
        // 보이기 직전에 한 번 즉시 갱신해서 첫 프레임 깜빡임을 줄임
        _player = FindPlayer();
        _lastRegionLabel = null;
        _lastTorchStock = -1;
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
        RefreshHealth();
        RefreshRegion();
        RefreshTorch();
        EnsurePlayerBindings();
    }

    // 횃불은 인벤토리 소모품이라 개수가 곧 인벤토리 재고입니다.
    // 설치(F키)로도, 퀵슬롯으로도 줄어들기 때문에 어느 쪽으로 써도 여기에 반영됩니다.
    private void RefreshTorch()
    {
        if (torchCountText == null) return;

        // 인벤토리 연결 전이면 0. EnsurePlayerBindings가 곧 연결하고, 그때 값이 채워집니다.
        int stock = _player != null ? _player.TorchStock : 0;

        // 값이 바뀔 때만 갱신 (매 프레임 TMP 텍스트를 건드리지 않도록)
        if (stock == _lastTorchStock) return;

        _lastTorchStock = stock;
        torchCountText.text = string.Format(torchCountFormat, stock);
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

    // 미니맵: MinimapManager가 그리는 '현재 층' 텍스처를 매 프레임 그대로 가져와 표시합니다.
    // 층을 오가면 MinimapManager.CurrentTexture가 바뀌므로 자동으로 최신 층 미니맵이 보입니다.
    private void RefreshMinimap()
    {
        if (minimapImage == null) return;

        MinimapManager mm = MinimapManager.Instance;
        Texture tex = mm != null ? mm.CurrentTexture : null;

        if (minimapImage.texture != tex) minimapImage.texture = tex;
        if (minimapFitter != null && tex != null) minimapFitter.aspectRatio = mm.CurrentAspect;
    }

    private void RefreshHealth()
    {
        if (_player == null) _player = FindPlayer();
        if (_player == null || _player.stat == null || _player.stat.maxHealth <= 0f) return;

        float ratio = Mathf.Clamp01(_player.stat.currentHealth / _player.stat.maxHealth);
        if (healthFill != null) healthFill.fillAmount = ratio;
        if (healthText != null)
            healthText.text = $"{Mathf.CeilToInt(Mathf.Max(0f, _player.stat.currentHealth))} / {Mathf.CeilToInt(_player.stat.maxHealth)}";
    }

    private void RefreshRegion()
    {
        RegionType? region = RegionState.Region;

        // 현재 지역에 미니맵 소스가 있을 때만 표시합니다.
        bool showMinimap = HasMinimapFor(region);
        if (minimapImage != null && minimapImage.gameObject.activeSelf != showMinimap)
            minimapImage.gameObject.SetActive(showMinimap);

        if (regionText == null) return;

        string label;
        if (region == RegionType.Dungeon)
        {
            int floor = DungeonManager.Instance != null ? DungeonManager.Instance.currentFloor : 1;
            label = string.Format(floorFormat, floor);
        }
        else if (region == RegionType.Town)
        {
            label = townLabel;
        }
        else if (region == RegionType.Overworld)
        {
            label = overworldLabel;
        }
        else
        {
            label = string.Empty; // 아직 지역 제공자가 등록되지 않음
        }

        // 지역/층은 자주 안 바뀌므로 값이 변할 때만 갱신
        if (label != _lastRegionLabel)
        {
            _lastRegionLabel = label;
            regionText.text = label;
        }
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
