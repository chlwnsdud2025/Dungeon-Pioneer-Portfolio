using System;
using UnityEngine;

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
        ResetHealth();
    }

    /// <summary>
    /// 체력을 최대치로 되돌립니다. 사망 상태에서도 동작하므로 '부활'에 사용합니다.
    /// (Player가 씬마다 새로 생성되던 시절엔 Awake가 이 역할을 대신했지만,
    ///  이제는 전역 상주라 부활 시점에 명시적으로 불러줘야 합니다)
    /// </summary>
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke();
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
