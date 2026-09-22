using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ExitPoint : MonoBehaviour
{
    private void Reset()
    {
        // 편의 기능: 콜라이더를 자동으로 트리거로 설정
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (DungeonManager.Instance != null)
        {
            DungeonManager.Instance.GoToNextFloor(GetComponentInParent<StairTile>());
        }
        else
        {
            Debug.LogWarning("[ExitPoint] 씬에 DungeonManager가 없어서 층 전환을 알릴 수 없습니다.");
        }
    }
}
