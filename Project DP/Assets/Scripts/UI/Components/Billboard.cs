using UnityEngine;

/// <summary>
/// 이 오브젝트가 항상 카메라를 향하도록(빌보드) 회전시킵니다.
/// 월드 스페이스 UI(적 머리 위 체력바 등)에 붙여서 사용합니다.
///
/// - yAxisOnly = false: 카메라 평면과 완전히 평행하게 정렬 → 화면에 정면으로 보임(쿼터뷰에 자연스러움)
/// - yAxisOnly = true : 수평(Y축) 회전만 → 바가 기울지 않고 항상 수직으로 서 있음
/// </summary>
public class Billboard : MonoBehaviour
{
    [Tooltip("비우면 Camera.main을 자동으로 사용합니다.")]
    [SerializeField] private Transform targetCamera;
    [Tooltip("체크하면 수평 회전만(수직 유지), 해제하면 카메라 평면에 완전히 정렬합니다.")]
    [SerializeField] private bool yAxisOnly = false;

    private Transform _cam;
    private bool _warnedNoCamera;

    private void LateUpdate()
    {
        Transform cam = ResolveCamera();
        if (cam == null)
        {
            if (!_warnedNoCamera)
            {
                Debug.LogWarning("[Billboard] 카메라를 찾지 못했습니다. 씬의 메인 카메라에 'MainCamera' 태그가 있는지 확인하세요. (또는 Target Camera에 직접 연결)", this);
                _warnedNoCamera = true;
            }
            return;
        }

        if (yAxisOnly)
        {
            // 카메라가 바라보는 방향을 수평 성분만 남겨 정면을 맞춤 (바는 수직 유지)
            Vector3 fwd = cam.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(fwd);
        }
        else
        {
            // 카메라 회전을 그대로 복사 → 카메라 평면과 평행 (UI가 정면으로 보임)
            transform.rotation = cam.rotation;
        }
    }

    private Transform ResolveCamera()
    {
        if (targetCamera != null) return targetCamera;

        // 씬 전환 등으로 카메라가 바뀌면(_cam이 파괴돼 null) 다시 찾습니다.
        if (_cam == null)
        {
            Camera main = Camera.main;
            _cam = main != null ? main.transform : null;
        }
        return _cam;
    }
}
