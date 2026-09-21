using System;
using UnityEngine;

/// <summary>
/// 인벤토리의 '외부 창구'(MonoBehaviour). 순수 모델(InventoryGrid)을 소유하고,
/// 게임플레이가 쓰기 좋은 API(획득/사용/이동/조회)와 상위 이벤트를 제공한다.
/// </summary>
public class Inventory : MonoBehaviour
{
    [Header("그리드 크기 (칸)")]
    [SerializeField] private int width = 10;
    [SerializeField] private int height = 6;

    public InventoryGrid Grid { get; private set; }   // 읽기 접근 (View/드래그가 참조)

    // ── 상위 이벤트 (UI/사운드/획득 연출 등이 구독) ──
    public event Action OnChanged;                    // 무엇이든 바뀜 → UI 다시 그림
    public event Action<ItemData, int> OnItemAdded;   // 획득
    public event Action<ItemData> OnItemUsed;         // 사용

    private void Awake()
    {
        Grid = new InventoryGrid(width, height);
        Grid.OnChanged += HandleGridChanged;          // 모델 변경 → 상위로 중계
    }

    private void OnDestroy()
    {
        if (Grid != null) Grid.OnChanged -= HandleGridChanged;   // 구독 해제(누수 방지)
    }

    private void HandleGridChanged() => OnChanged?.Invoke();

    // ── 획득 ──
    /// <summary>아이템 추가(중첩 병합 + 자동 배치). 자리가 모자라 다 못 넣으면 false.</summary>
    public bool AddItem(ItemData item, int count = 1)
    {
        if (item == null || count <= 0) return false;
        bool fullyAdded = Grid.TryAutoAdd(item, count);
        OnItemAdded?.Invoke(item, count);
        return fullyAdded;
    }

    // ── 사용 (소모품만) ──
    /// <summary>소모품 1개 사용: 효과 실행 → 개수 1 감소 → 0이면 제거.</summary>
    public bool UseItem(InventoryItem it, PlayerController user)
    {
        if (it == null) return false;

        if (it.Data is ConsumableItemData consumable)
        {
            if (!consumable.CanUse(user)) return false;   // 조건 미충족 → 개수를 차감하지 않는다

            consumable.Use(user);        // 폴리모피즘: 물약이면 회복, 폭탄이면 폭발...
            Grid.Consume(it, 1);         // 개수 감소/제거 + 알림
            OnItemUsed?.Invoke(it.Data);
            return true;
        }
        return false;                    // 장비는 여기서 사용 X (장착은 후속 단계)
    }

    /// <summary>
    /// ItemData로 지정한 소모품을 재고에서 1개 찾아 사용한다. (퀵슬롯용)
    /// 퀵슬롯은 스택이 아니라 ItemData만 기억하므로, 같은 아이템의 첫 스택을 찾아 기존 경로에 위임한다.
    /// </summary>
    public bool UseItem(ItemData data, PlayerController user)
    {
        if (data == null) return false;

        foreach (InventoryItem it in Grid.Items)
            if (ItemData.IsSame(it.Data, data)) return UseItem(it, user);   // 찾는 즉시 반환 — 순회 중 목록이 바뀌지 않음

        return false;   // 재고 없음
    }

    // ── 이동 (드래그용) ──
    public bool MoveItem(InventoryItem it, Vector2Int anchor) => Grid.Place(it, anchor);

    // ── 제거 (버리기 등) ──
    public void RemoveItem(InventoryItem it) => Grid.Remove(it);

    // ── 조회 (퀘스트/조합/구매 조건 등) ──
    public int CountOf(ItemData item)
    {
        int total = 0;
        foreach (InventoryItem it in Grid.Items)
            if (ItemData.IsSame(it.Data, item)) total += it.Count;
        return total;
    }
    public bool Has(ItemData item, int count = 1) => CountOf(item) >= count;
}