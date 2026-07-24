using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카메라와 플레이어 사이를 가리는 벽을 감지해 반투명 처리합니다.
/// CameraFollow와 같은 오브젝트(메인 카메라)에 부착하세요.
/// 벽 오브젝트에는 아무 것도 미리 붙일 필요가 없습니다(FadeableWall을 자동 부착).
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraOcclusionFader : MonoBehaviour
{
    [Header("추적 대상")]
    [Tooltip("비워두면 같은 오브젝트의 CameraFollow.target을 자동 사용합니다.")]
    public Transform target;
    [Tooltip("발밑이 아니라 몸통 기준으로 가림 판정 (PlayerPositionSender와 동일한 +1.5)")]
    public float targetHeightOffset = 1.5f;

    [Header("가림 감지")]
    [Tooltip("가림 판정에 사용할 벽 태그")]
    public string wallTag = "Wall";
    [Tooltip("레이 대신 굵은 구로 쏴서 살짝 빗겨난 벽까지 잡습니다.")]
    public float castRadius = 0.4f;
    [Tooltip("이 레이어의 콜라이더만 검사(성능). Wall 전용 레이어 권장, 없으면 Everything으로 두세요.")]
    public LayerMask detectionMask = ~0;

    [Header("페이드")]
    [Range(0f, 1f)] public float fadedAlpha = 0.25f; // 가릴 때 목표 투명도
    public float fadeSpeed = 8f;                     // 알파 변화 속도

    private CameraFollow camFollow;
    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private readonly HashSet<FadeableWall> lastOccluders = new HashSet<FadeableWall>();
    private readonly HashSet<FadeableWall> currentOccluders = new HashSet<FadeableWall>();

    private void Awake() => camFollow = GetComponent<CameraFollow>();

    private Transform ResolveTarget()
    {
        if (target != null) return target;
        return camFollow != null ? camFollow.target : null;
    }

    // ★ CameraFollow가 LateUpdate에서 카메라를 옮긴 '뒤'에 판정해야 정확합니다.
    //   (Script Execution Order에서 이 스크립트를 CameraFollow보다 뒤로 두면 완벽하고,
    //    안 해도 Lerp 추적이라 1프레임 오차는 사실상 보이지 않습니다.)
    private void LateUpdate()
    {
        Transform t = ResolveTarget();
        if (t == null) return;

        Vector3 origin = transform.position;
        Vector3 playerPoint = t.position + Vector3.up * targetHeightOffset;
        Vector3 dir = playerPoint - origin;
        float dist = dir.magnitude;
        if (dist < 0.01f) return;
        dir /= dist;

        currentOccluders.Clear();

        int count = Physics.SphereCastNonAlloc(
            origin, castRadius, dir, hitBuffer, dist, detectionMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Transform hitT = hitBuffer[i].transform;
            if (!hitT.CompareTag(wallTag)) continue;

            FadeableWall wall = hitT.GetComponent<FadeableWall>();
            if (wall == null) wall = hitT.gameObject.AddComponent<FadeableWall>();

            wall.SetTargetAlpha(fadedAlpha, fadeSpeed); // 투명하게
            currentOccluders.Add(wall);
        }

        // 지난 프레임엔 가렸지만 지금은 아닌 벽 → 다시 불투명하게
        lastOccluders.ExceptWith(currentOccluders); // 남은 것 = 방금 빠진 벽
        foreach (FadeableWall wall in lastOccluders)
            if (wall != null) wall.SetTargetAlpha(1f, fadeSpeed);

        // 다음 프레임 비교용으로 현재 집합을 저장
        lastOccluders.Clear();
        lastOccluders.UnionWith(currentOccluders);
    }
}