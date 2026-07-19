using System.Collections.Generic;
using UnityEngine;

// '화살 발사 함정'. PressurePlate(발판)가 밟히면 Fire()가 호출되어,
// 오브젝트 풀에서 화살을 꺼내 이 오브젝트의 정면(z축 앞쪽, transform.forward)으로 직선 발사합니다.
// 발사 방향은 맵 제작 시 이 오브젝트를 회전시켜 조절합니다.
// (RollingRockTrap과 동일한 풀 구조이며, 타이머 대신 발판 신호로 발사한다는 점만 다릅니다.)
public class ArrowTrap : MonoBehaviour
{
    [Header("Arrow")]
    [Tooltip("발사할 화살 프리팹(Arrow 컴포넌트 보유).")]
    public Arrow arrowPrefab;
    [Tooltip("화살 속도")]
    public float arrowSpeed = 12f;

    [Header("Spawn")]
    [Tooltip("화살이 나오는 위치(비우면 이 오브젝트의 위치). 방향은 항상 이 오브젝트의 z축 앞쪽입니다.")]
    public Transform spawnPoint;

    [Header("Object Pool")]
    [Tooltip("미리 만들어둘 화살 개수")]
    public int poolSize = 5;

    private readonly List<Arrow> pool = new List<Arrow>();

    private void Awake()
    {
        if (arrowPrefab == null)
        {
            Debug.LogError("[ArrowTrap] arrowPrefab이 비어있습니다. 화살 프리팹을 연결해주세요.", this);
            enabled = false;
            return;
        }

        // 풀 미리 생성 (이 오브젝트의 자식으로 두어 장소를 나갈 때 함께 정리되도록 함)
        for (int i = 0; i < poolSize; i++)
        {
            Arrow arrow = Instantiate(arrowPrefab, transform);
            arrow.gameObject.SetActive(false);
            pool.Add(arrow);
        }

        // [DEBUG] 이 로그가 뜨는 ArrowTrap만 실제로 발사 가능. 발판이 참조하는 트랩에서 이 로그가 안 뜨면 프리팹 에셋을 참조 중.
        Debug.Log($"[ArrowTrap] Awake 완료: 화살 풀 {pool.Count}개 생성", this);
    }

    /// <summary>PressurePlate가 밟혔을 때 호출됩니다. 화살 하나를 발사합니다.</summary>
    public void Fire()
    {
        // [DEBUG] Fire가 호출됐는지, 풀 상태는 어떤지 기록. pool=0이면 Awake 미실행(프리팹 에셋 참조 등).
        Debug.Log($"[ArrowTrap] Fire() 호출. pool={pool.Count}, arrowPrefab={(arrowPrefab != null ? arrowPrefab.name : "null")}", this);

        Arrow arrow = GetAvailableArrow();
        if (arrow == null)
        {
            Debug.LogWarning("[ArrowTrap] 사용 가능한 화살이 없음(pool이 비었거나 전부 사용 중) → 발사 안 됨", this);
            return; // 풀에 여유가 없으면 이번 발사는 건너뜀
        }

        Transform origin = spawnPoint != null ? spawnPoint : transform;
        Vector3 dir = transform.forward; // z축 앞쪽 (오브젝트 회전으로 방향 조절)

        arrow.transform.SetPositionAndRotation(origin.position, Quaternion.LookRotation(dir));
        arrow.gameObject.SetActive(true);
        arrow.Launch(dir, arrowSpeed, this);

        Debug.Log($"[ArrowTrap] 화살 발사! 위치={origin.position}, 방향={dir}, 속도={arrowSpeed}", this);
    }

    private Arrow GetAvailableArrow()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (!pool[i].gameObject.activeSelf) return pool[i];
        }
        return null; // (원하면 여기서 풀을 동적으로 늘리도록 확장 가능)
    }

    public void ReturnToPool(Arrow arrow)
    {
        arrow.gameObject.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        // 발사 방향/발사 지점 시각화
        Transform origin = spawnPoint != null ? spawnPoint : transform;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin.position, origin.position + transform.forward * 3f);
        Gizmos.DrawSphere(origin.position, 0.12f);
    }
}
