using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// 항상 떠 있는 인게임 HUD. 세 가지를 한 화면에 묶습니다:
///  1) 미니맵     — 표시 위젯(RawImage+AspectRatioFitter)을 이 HUD가 소유하고, MinimapManager에 연결(Bind).
///                  던전에서만 보이고, 오버월드/마을에서는 숨깁니다.
///  2) 체력 바    — PlayerController.currentHealth / maxHealth 를 매 프레임 폴링
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


    [Header("Inventory")]
    [SerializeField] private GameObject inventoryPanel;


    // ── 내부 캐시 ──
    private PlayerController _player;
    private string _lastRegionLabel;



    public override void OnBeforeShow()
    {
        base.OnBeforeShow();
        // 보이기 직전에 한 번 즉시 갱신해서 첫 프레임 깜빡임을 줄임
        _player = FindPlayer();
        _lastRegionLabel = null;
        Refresh();
    }
    private void OnEnable()
    {
        if (inventoryPanel != null) inventoryPanel.SetActive(false);   // 시작은 닫힘
        if (InputManager.Instance != null)
            InputManager.Instance.OnInventoryToggle += ToggleInventory;
    }
    private void OnDisable()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.OnInventoryToggle -= ToggleInventory;
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
        if (_player == null || _player.maxHealth <= 0f) return;

        float ratio = Mathf.Clamp01(_player.currentHealth / _player.maxHealth);
        if (healthFill != null) healthFill.fillAmount = ratio;
        if (healthText != null)
            healthText.text = $"{Mathf.CeilToInt(Mathf.Max(0f, _player.currentHealth))} / {Mathf.CeilToInt(_player.maxHealth)}";
    }

    private void RefreshRegion()
    {
        RegionType? region = RegionState.Region;

        // 미니맵은 던전에서만 표시
        bool inDungeon = region == RegionType.Dungeon;
        if (minimapImage != null && minimapImage.gameObject.activeSelf != inDungeon)
            minimapImage.gameObject.SetActive(inDungeon);

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
    public void ToggleInventory()
    {
        if (inventoryPanel == null) return;
        bool open = !inventoryPanel.activeSelf;
        inventoryPanel.SetActive(open);

        Cursor.visible = open;                                     // 드래그하려면 커서 표시
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;

        if (InputManager.Instance != null)
            InputManager.Instance.SetGameplayEnabled(!open);      // 열면 이동/공격 차단
    }



    // 플레이어는 각 씬에 태그 "Player"로 존재합니다. 없으면(아직 스폰 전/씬 전환 직후) null 반환.
    private PlayerController FindPlayer()
    {
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        return go != null ? go.GetComponent<PlayerController>() : null;
    }
}
