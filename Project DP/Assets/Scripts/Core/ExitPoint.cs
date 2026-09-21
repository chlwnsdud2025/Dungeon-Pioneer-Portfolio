using UnityEngine;

/// <summary>
/// 맵의 출구(Exit) 타일에 붙이는 컴포넌트.
/// 플레이어가 출구 콜라이더에 닿으면 GameManager에게 "이 층 클리어"를 알립니다.
///
/// ★ 사용법: exitPrefab 프리팹(Inspector의 MapBuilder의 exitPrefab 슬롯에 들어가는 그 프리팹)에
///   이 스크립트를 추가하고, Collider의 IsTrigger를 체크해주세요. (Reset()에서 자동으로 켜주긴 합니다)
///   플레이어 오브젝트에는 Tag가 "Player"로 설정되어 있어야 합니다.
/// </summary>
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
