using System;
using UnityEngine;

/// <summary>
/// 플레이어의 돈(골드)만 관리하는 컴포넌트.
/// PlayerStat이 체력을 들고 있듯, 지갑도 전역 상주(DontDestroyOnLoad)인 Player 오브젝트의
/// 컴포넌트 상태로 존재해 씬을 넘어도 잔액이 유지된다.
///
/// 돈이 오가는 경로(상자 보상, 상점 결제 등)는 전부 AddGold/TrySpendGold로 모은다.
/// gold 필드를 직접 건드리는 코드가 없어야 OnGoldChanged가 항상 정확히 발화한다.
/// </summary>
public class PlayerWallet : MonoBehaviour
{
    [Header("골드")]
    [Tooltip("시작 소지금. 런타임 잔액도 이 필드에 그대로 담깁니다.")]
    [SerializeField, Min(0)] private int gold = 0;

    /// <summary>현재 잔액.</summary>
    public int Gold => gold;

    /// <summary>잔액 변경 시 발화. 매개변수는 '바뀐 후' 잔액. (HUD/상점 UI가 구독)</summary>
    public event Action<int> OnGoldChanged;

    /// <summary>지불 가능 여부. (상점 UI에서 구매 버튼 활성화 판정 등)</summary>
    public bool HasGold(int amount) => gold >= amount;

    /// <summary>골드 획득. 0 이하는 무시한다.</summary>
    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        gold += amount;
        OnGoldChanged?.Invoke(gold);
    }

    /// <summary>골드 지불 시도. 잔액이 모자라면 차감 없이 false. (상점 결제 등)</summary>
    public bool TrySpendGold(int amount)
    {
        if (amount <= 0 || gold < amount) return false;
        gold -= amount;
        OnGoldChanged?.Invoke(gold);
        return true;
    }
}
