using UnityEngine;

/// <summary>
/// 장비 장착/해제 시 3D 모델을 교체하는 비주얼 컴포넌트.
///  · 무기/방패 → handslot에 프리팹 Instantiate/Destroy
///  · 헬멧     → Head SkinnedMeshRenderer 메시+머티리얼 교체, 악세사리 on/off
///  · 갑옷     → Body/Arm/Leg SkinnedMeshRenderer 메시+머티리얼 교체
/// Player 오브젝트에 부착한다.
/// </summary>
public class EquipmentVisualizer : MonoBehaviour
{
    // ── 무기/방패 (Instantiate 방식) ──

    [Header("Hand Slots")]
    [SerializeField] private Transform weaponSlot;       // handslot.r
    [SerializeField] private Transform shieldSlot;       // handslot.l

    [Header("기본 무기/방패 프리팹")]
    [SerializeField] private GameObject defaultWeaponPrefab;
    [SerializeField] private GameObject defaultShieldPrefab;

    // ── 헬멧/갑옷 (Mesh Swap 방식) ──

    [Header("Body Part Renderers (기본 캐릭터 모델)")]
    [SerializeField] private SkinnedMeshRenderer headRenderer;
    [SerializeField] private SkinnedMeshRenderer bodyRenderer;
    [SerializeField] private SkinnedMeshRenderer armLeftRenderer;
    [SerializeField] private SkinnedMeshRenderer armRightRenderer;
    [SerializeField] private SkinnedMeshRenderer legLeftRenderer;
    [SerializeField] private SkinnedMeshRenderer legRightRenderer;

    [Header("Head Accessory (Hat/Helmet 등)")]
    [SerializeField] private SkinnedMeshRenderer headAccessoryRenderer;

    // ── Private ──

    private EquipmentHolder _holder;

    /// <summary>
    /// 현재 장착 무기의 타격 판정 콜라이더. 무기를 만들고 파괴하는 주체가 여기라 교체 때 자동으로 갱신됩니다.
    /// 무기가 없거나 프리팹에 WeaponHitbox가 없으면 null.
    /// </summary>
    public WeaponHitbox CurrentWeaponHitbox { get; private set; }

    // 무기/방패 인스턴스 추적
    private GameObject _currentWeapon;
    private GameObject _currentShield;
    private GameObject _activeWeaponPrefab;
    private GameObject _activeShieldPrefab;

    // 기본 메시/머티리얼 백업 (해제 시 복원용)
    private Mesh _defaultHeadMesh;
    private Mesh _defaultBodyMesh;
    private Mesh _defaultArmLeftMesh;
    private Mesh _defaultArmRightMesh;
    private Mesh _defaultLegLeftMesh;
    private Mesh _defaultLegRightMesh;
    private Mesh _defaultAccessoryMesh;

    private Material _defaultHeadMaterial;
    private Material _defaultBodyMaterial;
    private Material _defaultAccessoryMaterial;

    private void Awake()
    {
        _holder = GetComponent<EquipmentHolder>();

        // 기존 handslot 자식을 현재 비주얼로 인식
        if (weaponSlot != null && weaponSlot.childCount > 0)
            _currentWeapon = weaponSlot.GetChild(0).gameObject;
        if (shieldSlot != null && shieldSlot.childCount > 0)
            _currentShield = shieldSlot.GetChild(0).gameObject;

        // 기본 메시/머티리얼 백업
        CaptureDefaults();
    }

    private void Start()
    {
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

    // ── 기본값 백업 ──

    private void CaptureDefaults()
    {
        if (headRenderer != null)
        {
            _defaultHeadMesh = headRenderer.sharedMesh;
            _defaultHeadMaterial = headRenderer.sharedMaterial;
        }
        if (bodyRenderer != null)
        {
            _defaultBodyMesh = bodyRenderer.sharedMesh;
            _defaultBodyMaterial = bodyRenderer.sharedMaterial;
        }
        if (armLeftRenderer != null)  _defaultArmLeftMesh  = armLeftRenderer.sharedMesh;
        if (armRightRenderer != null) _defaultArmRightMesh = armRightRenderer.sharedMesh;
        if (legLeftRenderer != null)  _defaultLegLeftMesh  = legLeftRenderer.sharedMesh;
        if (legRightRenderer != null) _defaultLegRightMesh = legRightRenderer.sharedMesh;

        if (headAccessoryRenderer != null)
        {
            _defaultAccessoryMesh = headAccessoryRenderer.sharedMesh;
            _defaultAccessoryMaterial = headAccessoryRenderer.sharedMaterial;
        }
    }

    // ── 전체 갱신 ──

    private void RefreshVisuals()
    {
        RefreshWeapon();
        RefreshShield();
        RefreshHelmet();
        RefreshArmor();
    }

    // ── 무기 (Instantiate) ──

    private void RefreshWeapon()
    {
        if (weaponSlot == null) return;

        var equipped = _holder.GetEquipped(EquipmentSlot.Weapon);
        GameObject prefab = (equipped?.Data is WeaponItemData w && w.equipPrefab != null)
            ? w.equipPrefab
            : defaultWeaponPrefab;

        SpawnInSlot(weaponSlot, prefab, ref _currentWeapon, ref _activeWeaponPrefab);

        CurrentWeaponHitbox = _currentWeapon != null
            ? _currentWeapon.GetComponentInChildren<WeaponHitbox>(true)
            : null;

        if (_currentWeapon != null && CurrentWeaponHitbox == null)
            Debug.LogWarning($"[EquipmentVisualizer] '{_currentWeapon.name}' 무기 프리팹에 WeaponHitbox가 없어 " +
                             "공격이 적중하지 않습니다. 칼날에 트리거 콜라이더 + WeaponHitbox를 추가해주세요.", this);
    }

    // ── 방패 (Instantiate) ──

    private void RefreshShield()
    {
        if (shieldSlot == null) return;

        var equipped = _holder.GetEquipped(EquipmentSlot.Shield);
        GameObject prefab = (equipped?.Data is ShieldItemData s && s.equipPrefab != null)
            ? s.equipPrefab
            : defaultShieldPrefab;

        SpawnInSlot(shieldSlot, prefab, ref _currentShield, ref _activeShieldPrefab);
    }

    private void SpawnInSlot(Transform slot, GameObject prefab,
                             ref GameObject currentInstance, ref GameObject activePrefab)
    {
        if (prefab == activePrefab && currentInstance != null)
            return;

        if (currentInstance != null)
            Destroy(currentInstance);

        currentInstance = null;
        activePrefab = null;

        if (prefab != null)
        {
            currentInstance = Instantiate(prefab, slot, false);
            activePrefab = prefab;
        }
    }

    // ── 헬멧 (Mesh Swap) ──

    private void RefreshHelmet()
    {
        if (headRenderer == null) return;

        var equipped = _holder.GetEquipped(EquipmentSlot.Helmet);

        if (equipped?.Data is HelmetItemData helmet)
        {
            // 머리 메시 교체
            if (helmet.headMesh != null)
            {
                headRenderer.sharedMesh = helmet.headMesh;
                if (helmet.material != null)
                    headRenderer.sharedMaterial = helmet.material;
            }

            // 악세사리 (Hat → Helmet 등)
            if (headAccessoryRenderer != null)
            {
                if (helmet.accessoryMesh != null)
                {
                    headAccessoryRenderer.sharedMesh = helmet.accessoryMesh;
                    if (helmet.material != null)
                        headAccessoryRenderer.sharedMaterial = helmet.material;
                    headAccessoryRenderer.gameObject.SetActive(true);
                }
                else
                {
                    // 악세사리 메시가 없으면 숨김
                    headAccessoryRenderer.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            // 헬멧 미장착 → 기본 복원
            headRenderer.sharedMesh = _defaultHeadMesh;
            headRenderer.sharedMaterial = _defaultHeadMaterial;

            if (headAccessoryRenderer != null)
            {
                headAccessoryRenderer.sharedMesh = _defaultAccessoryMesh;
                headAccessoryRenderer.sharedMaterial = _defaultAccessoryMaterial;
                headAccessoryRenderer.gameObject.SetActive(true);
            }
        }
    }

    // ── 갑옷 (Mesh Swap) ──

    private void RefreshArmor()
    {
        if (bodyRenderer == null) return;

        var equipped = _holder.GetEquipped(EquipmentSlot.Armor);

        if (equipped?.Data is ArmorItemData armor)
        {
            SwapRenderer(bodyRenderer,     armor.bodyMesh,     armor.material, _defaultBodyMesh,     _defaultBodyMaterial);
            SwapRenderer(armLeftRenderer,  armor.armLeftMesh,  armor.material, _defaultArmLeftMesh,  _defaultBodyMaterial);
            SwapRenderer(armRightRenderer, armor.armRightMesh, armor.material, _defaultArmRightMesh, _defaultBodyMaterial);
            SwapRenderer(legLeftRenderer,  armor.legLeftMesh,  armor.material, _defaultLegLeftMesh,  _defaultBodyMaterial);
            SwapRenderer(legRightRenderer, armor.legRightMesh, armor.material, _defaultLegRightMesh, _defaultBodyMaterial);
        }
        else
        {
            // 갑옷 미장착 → 기본 복원
            SwapRenderer(bodyRenderer,     null, null, _defaultBodyMesh,     _defaultBodyMaterial);
            SwapRenderer(armLeftRenderer,  null, null, _defaultArmLeftMesh,  _defaultBodyMaterial);
            SwapRenderer(armRightRenderer, null, null, _defaultArmRightMesh, _defaultBodyMaterial);
            SwapRenderer(legLeftRenderer,  null, null, _defaultLegLeftMesh,  _defaultBodyMaterial);
            SwapRenderer(legRightRenderer, null, null, _defaultLegRightMesh, _defaultBodyMaterial);
        }
    }

    /// <summary>
    /// SkinnedMeshRenderer의 메시와 머티리얼을 교체한다.
    /// newMesh가 null이면 기본값(fallback)으로 복원한다.
    /// </summary>
    private static void SwapRenderer(SkinnedMeshRenderer renderer,
                                     Mesh newMesh, Material newMat,
                                     Mesh fallbackMesh, Material fallbackMat)
    {
        if (renderer == null) return;

        renderer.sharedMesh     = newMesh != null ? newMesh : fallbackMesh;
        renderer.sharedMaterial = newMat  != null ? newMat  : fallbackMat;
    }
}
