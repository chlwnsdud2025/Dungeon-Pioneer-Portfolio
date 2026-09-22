using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EntrancePoint : MonoBehaviour
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

        DungeonManager dm = DungeonManager.Instance;
        if (dm == null)
        {
            Debug.LogWarning("[EntrancePoint] 씬에 DungeonManager가 없어서 층 전환을 알릴 수 없습니다.");
            return;
        }

        // 1층 입구 = 던전 탈출(오버월드로), 2층 이상 = 한 층 위로
        StairTile stair = GetComponentInParent<StairTile>();
        if (dm.currentFloor <= 1) dm.ReturnToOverworld(stair);
        else dm.GoToPreviousFloor(stair);
    }
}
