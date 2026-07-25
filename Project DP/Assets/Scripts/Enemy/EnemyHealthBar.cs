using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 적 머리 위에 떠서 체력을 표시하는 바.
/// 적 프리팹의 자식(월드 스페이스 Canvas)으로 두고, localPosition을 머리 위 높이만큼 올려둡니다.
/// 카메라 정렬은 같은 오브젝트의 Billboard가 담당하고, 이 스크립트는 체력만 반영합니다.
///
/// 체력은 EnemyController에서 매 프레임 폴링합니다. (별도 이벤트 없이도 즉시 반영)
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    [Tooltip("비우면 부모 계층에서 EnemyController를 자동으로 찾습니다.")]
    [SerializeField] private EnemyController enemy;
    [Tooltip("Image Type을 'Filled'(Horizontal)로 설정한 체력 게이지.")]
    [SerializeField] private Image fill;
    [Tooltip("표시/숨김할 대상(배경+게이지 컨테이너). 비우면 항상 표시합니다.")]
    [SerializeField] private GameObject barVisual;

    [Header("Options")]
    [Tooltip("체력이 가득 찼을 때는 숨깁니다(피해를 입어야 표시).")]
    [SerializeField] private bool hideWhenFull = true;

    private void Awake()
    {
        if (enemy == null) enemy = GetComponentInParent<EnemyController>();

        // ── 자주 하는 실수 자동 진단 ──
        if (enemy == null)
            Debug.LogWarning("[EnemyHealthBar] EnemyController를 찾지 못했습니다. 이 체력바가 적(EnemyController) 오브젝트의 '자식'으로 있는지 확인하세요.", this);
        if (fill == null)
            Debug.LogWarning("[EnemyHealthBar] Fill(Image)이 연결되지 않았습니다. 인스펙터에서 게이지 Image를 연결하세요.", this);
        if (barVisual == gameObject)
            Debug.LogWarning("[EnemyHealthBar] Bar Visual이 이 스크립트와 같은 오브젝트입니다. 숨겨지면 스크립트(LateUpdate)도 멈춰 다시 못 켜집니다. 자식 컨테이너를 지정하세요.", this);
    }

    private void LateUpdate()
    {
        if (enemy == null || enemy.maxHealth <= 0f)
        {
            SetVisible(false);
            return;
        }

        float ratio = Mathf.Clamp01(enemy.currentHealth / enemy.maxHealth);
        if (fill != null) fill.fillAmount = ratio;

        // 가득 찼거나(옵션) 죽었으면 숨기고, 그 외엔 표시
        bool visible = ratio > 0f && !(hideWhenFull && ratio >= 1f);
        SetVisible(visible);
    }

    private void SetVisible(bool visible)
    {
        if (barVisual != null && barVisual.activeSelf != visible)
            barVisual.SetActive(visible);
    }
}
