using System;
using UnityEngine;

/// <summary>
/// 플레이어의 수치(체력, 공격력, 방어력 등)만 관리하는 컴포넌트.
/// PlayerController는 이 컴포넌트를 참조해서 스탯을 읽고,
/// EquipmentHolder는 장비 변경 시 보너스를 기록한다.
/// </summary>
public class PlayerStat : MonoBehaviour
{
    // ── 기본 스탯 (인스펙터에서 설정) ──
    [Header("체력")]
    public float maxHealth = 100f;
    [HideInInspector] public float currentHealth;

    [Header("공격")]
    public float attackDamage = 5f;

    [Header("방어")]
    [Range(0, 180)] public float blockAngle = 120f;

    // ── 장비 보너스 (EquipmentHolder가 장착/해제 시 재계산해서 기록) ──
    [HideInInspector] public float equipBonusAttack;
    [HideInInspector] public float equipBonusDefense;
    [HideInInspector] public float equipBonusBlockChance;

    // ── 최종 스탯 (기본 + 장비 보너스) ──
    public float FinalAttackDamage => attackDamage + equipBonusAttack;
    public float FinalDefense      => equipBonusDefense;
    public float FinalBlockChance  => equipBonusBlockChance;
    public float FinalBlockAngle   => blockAngle;

    public bool IsDead => currentHealth <= 0f;

    // ── 이벤트 ──
    public event Action OnHealthChanged;
    public event Action OnDeath;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    /// <summary>체력 회복. 사망 상태면 무시.</summary>
    public void Heal(float amount)
    {
        if (amount <= 0f || IsDead) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke();
    }

    /// <summary>데미지 적용. 방어력 차감 후 최소 1 데미지 보장. 사망 시 OnDeath 발생.</summary>
    public void ApplyDamage(float rawDamage)
    {
        if (IsDead) return;
        float finalDamage = Mathf.Max(1f, rawDamage - FinalDefense);
        currentHealth -= finalDamage;
        OnHealthChanged?.Invoke();

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            OnDeath?.Invoke();
        }
    }

    /// <summary>방어력 무시 데미지 (넉백 등). 사망 시 OnDeath 발생.</summary>
    public void ApplyRawDamage(float damage)
    {
        if (IsDead) return;
        currentHealth -= damage;
        OnHealthChanged?.Invoke();

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            OnDeath?.Invoke();
        }
    }
}
