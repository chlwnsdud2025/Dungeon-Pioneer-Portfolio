using UnityEngine;

/// <summary>
/// 장비 장착/해제 시 3D 모델을 handslot에 교체하는 비주얼 컴포넌트.
/// EquipmentHolder.OnEquipmentChanged를 구독하여 무기·방패를 Instantiate/Destroy한다.
/// Player 오브젝트에 부착한다.
/// 프리팹 자체의 localPosition/Rotation/Scale을 그대로 사용한다.
/// </summary>
public class EquipmentVisualizer : MonoBehaviour
{
    [Header("Hand Slots (Rig_Medium 본 하위)")]
    [Tooltip("오른손 무기 슬롯 — Rig_Medium > … > hand.r > handslot.r")]
    [SerializeField] private Transform weaponSlot;

    [Tooltip("왼손 방패 슬롯 — Rig_Medium > … > hand.l > handslot.l")]
    [SerializeField] private Transform shieldSlot;

    [Header("기본 장비 프리팹 (비장착 시 표시)")]
    [SerializeField] private GameObject defaultWeaponPrefab;
    [SerializeField] private GameObject defaultShieldPrefab;

    private EquipmentHolder _holder;
    private GameObject _currentWeapon;
    private GameObject _currentShield;

    // 현재 표시 중인 프리팹을 추적 — 같은 프리팹이면 재생성하지 않음
    private GameObject _activeWeaponPrefab;
    private GameObject _activeShieldPrefab;

    private void Awake()
    {
        _holder = GetComponent<EquipmentHolder>();

        // 프리팹에 미리 배치된 기본 장비(sword_A, shield_A 등)를 현재 비주얼로 인식
        if (weaponSlot != null && weaponSlot.childCount > 0)
            _currentWeapon = weaponSlot.GetChild(0).gameObject;
        if (shieldSlot != null && shieldSlot.childCount > 0)
            _currentShield = shieldSlot.GetChild(0).gameObject;
    }

    private void Start()
    {
        // 게임 시작 시 기본 장비 비주얼로 초기화
        RefreshVisuals();
    }

    private void OnEnable()
    {
        if (_holder != null)
            _holder.OnEquipmentChanged += RefreshVisuals;
    }

    private void OnDisable()
    {
        if (_holder != null)
            _holder.OnEquipmentChanged -= RefreshVisuals;
    }

    // ── 비주얼 갱신 ──

    /// <summary>장비 슬롯 전체를 현재 장착 상태에 맞게 갱신한다.</summary>
    private void RefreshVisuals()
    {
        RefreshWeapon();
        RefreshShield();
    }

    private void RefreshWeapon()
    {
        if (weaponSlot == null) return;

        var equipped = _holder.GetEquipped(EquipmentSlot.Weapon);
        GameObject prefab = (equipped?.Data is WeaponItemData w && w.equipPrefab != null)
            ? w.equipPrefab
            : defaultWeaponPrefab;

        SpawnInSlot(weaponSlot, prefab, ref _currentWeapon, ref _activeWeaponPrefab);
    }

    private void RefreshShield()
    {
        if (shieldSlot == null) return;

        var equipped = _holder.GetEquipped(EquipmentSlot.Shield);
        GameObject prefab = (equipped?.Data is ShieldItemData s && s.equipPrefab != null)
            ? s.equipPrefab
            : defaultShieldPrefab;

        SpawnInSlot(shieldSlot, prefab, ref _currentShield, ref _activeShieldPrefab);
    }

    /// <summary>
    /// handslot 하위의 기존 오브젝트를 제거하고 새 프리팹을 생성한다.
    /// 프리팹에 설정된 localPosition/Rotation/Scale을 그대로 유지한다.
    /// 이미 같은 프리팹이 표시 중이면 아무것도 하지 않는다.
    /// </summary>
    private void SpawnInSlot(Transform slot, GameObject prefab,
                             ref GameObject currentInstance, ref GameObject activePrefab)
    {
        // 같은 프리팹이면 교체 불필요
        if (prefab == activePrefab && currentInstance != null)
            return;

        // 기존 오브젝트 제거
        if (currentInstance != null)
            Destroy(currentInstance);

        currentInstance = null;
        activePrefab = null;

        // 새 프리팹 생성 — worldPositionStays: false로 프리팹의 로컬 Transform을 그대로 사용
        if (prefab != null)
        {
            currentInstance = Instantiate(prefab, slot, false);
            activePrefab = prefab;
        }
    }
}
