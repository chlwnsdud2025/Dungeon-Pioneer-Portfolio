using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 아이템 호버 시 이름/설명/스탯을 보여주는 툴팁.
/// 인벤토리 프리팹 안에 하나만 두고, static Instance로 접근한다.
/// InventoryItemView의 IPointerEnter/Exit/MoveHandler가 호출한다.
/// </summary>
public class ItemTooltipView : MonoBehaviour
{
    [Header("텍스트")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descText;
    [SerializeField] private TMP_Text statText;

    [Header("레이아웃")]
    [SerializeField] private RectTransform tooltipRect;     // 툴팁 최상위 RectTransform
    [SerializeField] private CanvasGroup canvasGroup;       // 선택: 페이드 등에 활용 가능

    [Header("설정")]
    [Tooltip("마우스 커서에서 살짝 떨어뜨릴 오프셋 (픽셀)")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(16f, -16f);

    private Canvas _rootCanvas;
    private RectTransform _canvasRect;

    /// <summary>InventoryItemView가 접근하는 전역 인스턴스. UIInventoryPopup이 설정한다.</summary>
    public static ItemTooltipView Instance { get; set; }

    private void Awake()
    {
        if (tooltipRect == null) tooltipRect = (RectTransform)transform;

        // 부모 Canvas를 찾아 화면 클램핑에 사용
        _rootCanvas = GetComponentInParent<Canvas>();
        if (_rootCanvas != null)
        {
            // rootCanvas가 중첩 Canvas일 수 있으므로 최상위 루트를 사용
            _rootCanvas = _rootCanvas.rootCanvas;
            _canvasRect = _rootCanvas.GetComponent<RectTransform>();
        }

        // 시작 시 숨김
        gameObject.SetActive(false);
    }

    /// <summary>아이템 정보를 채우고 툴팁을 표시한다.</summary>
    public void Show(ItemData data, Vector2 screenPos)
    {
        if (data == null) return;

        // 텍스트 채우기
        if (nameText != null) nameText.text = data.displayName;
        if (descText != null)
        {
            bool hasDesc = !string.IsNullOrEmpty(data.description);
            descText.gameObject.SetActive(hasDesc);
            if (hasDesc) descText.text = data.description;
        }
        if (statText != null)
        {
            string stats = BuildStatText(data);
            bool hasStats = !string.IsNullOrEmpty(stats);
            statText.gameObject.SetActive(hasStats);
            if (hasStats) statText.text = stats;
        }

        gameObject.SetActive(true);

        // ContentSizeFitter가 크기를 다시 계산하도록 강제 갱신
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);

        UpdatePosition(screenPos);
    }

    /// <summary>마우스 위치에 맞춰 툴팁 위치를 갱신한다.</summary>
    public void UpdatePosition(Vector2 screenPos)
    {
        if (!gameObject.activeSelf) return;

        Vector2 pos = screenPos + cursorOffset;

        // 화면 밖으로 나가지 않도록 클램핑
        if (_canvasRect != null)
        {
            Vector2 tooltipSize = tooltipRect.sizeDelta;
            float scaleFactor = _rootCanvas.scaleFactor;

            // 스크린 좌표 기준 툴팁 경계 체크
            float rightEdge = pos.x + tooltipSize.x * scaleFactor;
            float bottomEdge = pos.y - tooltipSize.y * scaleFactor;

            // 오른쪽 초과 → 왼쪽으로 표시
            if (rightEdge > Screen.width)
                pos.x = screenPos.x - cursorOffset.x - tooltipSize.x * scaleFactor;

            // 아래쪽 초과 → 위로 표시
            if (bottomEdge < 0f)
                pos.y = screenPos.y - cursorOffset.y + tooltipSize.y * scaleFactor;
        }

        tooltipRect.position = pos;
    }

    /// <summary>툴팁을 숨긴다.</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // ── 아이템 타입별 스탯 문자열 ──
    private static string BuildStatText(ItemData data)
    {
        return data switch
        {
            WeaponItemData w  => $"공격력 +{w.attack}\n공격 속도 {w.attackSpeed:F1}",
            ArmorItemData a   => $"방어력 +{a.defense}",
            HelmetItemData h  => $"방어력 +{h.defense}",
            ShieldItemData s  => $"방어력 +{s.defense}\n막기 확률 +{s.blockChance:F0}%",
            PotionItemData p  => $"회복량 {p.healAmount:F0}",
            BuffItemData b    => $"효과 {b.amount:F0}  지속 {b.duration:F0}초",
            _                 => string.Empty
        };
    }
}
