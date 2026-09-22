using System.Collections.Generic;
using UnityEngine;

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
        // Debug.Log($"[ArrowTrap] Awake 완료: 화살 풀 {pool.Count}개 생성", this);
    }

    /// <summary>PressurePlate가 밟혔을 때 호출됩니다. 화살 하나를 발사합니다.</summary>
    public bool Fire(PressurePlate source = null)
    {
        Arrow arrow = GetAvailableArrow();
        if (arrow == null) return false;

        Transform origin = spawnPoint != null ? spawnPoint : transform;
        Vector3 dir = transform.forward;

        arrow.transform.SetPositionAndRotation(origin.position, Quaternion.LookRotation(dir));
        arrow.gameObject.SetActive(true);
        arrow.Launch(dir, arrowSpeed, this, source);
        return true;
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
