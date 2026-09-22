using UnityEngine;
using UnityEngine.UI;

public class TorchGauge : MonoBehaviour
{
    [Tooltip("비우면 부모 계층에서 TorchInstance를 자동으로 찾습니다.")]
    [SerializeField] private TorchInstance torch;
    [Tooltip("Image Type을 'Filled'(Horizontal, Origin Left)로 설정한 게이지. 시간이 지나면 오른쪽부터 줄어듭니다.")]
    [SerializeField] private Image fill;
    [Tooltip("표시/숨김할 대상(배경+게이지 컨테이너). 비우면 항상 표시합니다.")]
    [SerializeField] private GameObject barVisual;

    [Header("Options")]
    [Tooltip("0보다 크면 플레이어가 이 거리(월드 단위) 안에 있을 때만 표시합니다. 0이면 항상 표시.")]
    [Min(0f), SerializeField] private float showWithinDistance = 0f;
    [Tooltip("게이지가 가득 찼을 때 색. 줄어들수록 Low Color로 바뀝니다. 둘을 같게 두면 색 변화 없음.")]
    [SerializeField] private Color fullColor = new Color(0.93f, 0.13f, 0.13f, 1f);
    [SerializeField] private Color lowColor = new Color(0.93f, 0.13f, 0.13f, 1f);

    private void Awake()
    {
        if (torch == null) torch = GetComponentInParent<TorchInstance>();

        // ── 자주 하는 실수 자동 진단 ──
        if (torch == null)
            Debug.LogWarning("[TorchGauge] TorchInstance를 찾지 못했습니다. 이 게이지가 횃불 프리팹의 '자식'으로 있는지 확인하세요.", this);
        if (fill == null)
            Debug.LogWarning("[TorchGauge] Fill(Image)이 연결되지 않았습니다. 인스펙터에서 게이지 Image를 연결하세요.", this);
        if (barVisual == gameObject)
            Debug.LogWarning("[TorchGauge] Bar Visual이 이 스크립트와 같은 오브젝트입니다. 숨겨지면 스크립트(LateUpdate)도 멈춰 다시 못 켭니다. 자식 컨테이너를 지정하세요.", this);
    }

    private void LateUpdate()
    {
        if (torch == null)
        {
            SetVisible(false);
            return;
        }

        float ratio = torch.RemainingRatio;
        if (fill != null)
        {
            fill.fillAmount = ratio;
            fill.color = Color.Lerp(lowColor, fullColor, ratio);
        }

        SetVisible(ratio > 0f && IsPlayerNearby());
    }

    private bool IsPlayerNearby()
    {
        if (showWithinDistance <= 0f) return true;

        GameObject player = PlayerManager.Instance != null ? PlayerManager.Instance.Player : null;
        if (player == null) return false;

        float sqrDistance = (player.transform.position - transform.position).sqrMagnitude;
        return sqrDistance <= showWithinDistance * showWithinDistance;
    }

    private void SetVisible(bool visible)
    {
        if (barVisual != null && barVisual.activeSelf != visible)
            barVisual.SetActive(visible);
    }
}
