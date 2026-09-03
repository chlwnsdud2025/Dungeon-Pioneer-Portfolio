using System.Collections.Generic;
using UnityEngine;

public class WallObject : MonoBehaviour, IInteractable
{
    // 벽 하나라도 네 측면에 각각 1개씩 횃불이 설치될 수 있으므로, '면 단위'로 설치 여부를 관리합니다.
    // 면은 설치할 때 사용하는 표면 노멀(hitNormal) 방향으로 식별합니다.
    private readonly HashSet<Vector3Int> torchFaces = new HashSet<Vector3Int>();

    // ── 상호작용 (IInteractable) ─────────────────────────────

    // 재고 부족/중복 설치 검사는 TryPlaceTorch가 직접 하며 실패 사유도 거기서 로그로 알려줍니다.
    // 여기서 미리 걸러버리면 그 안내가 사라지므로 항상 통과시킵니다.
    public bool CanInteract(PlayerController player) => true;

    // 벽 상호작용 = 바라보는 면에 횃불 설치. 재고/중복 검사와 개수 차감은 전부 TryPlaceTorch가
    // 처리합니다. (횃불은 인벤토리 소모품이라 퀵슬롯으로 쓸 때와 같은 경로를 탄다)
    public void Interact(PlayerController player) => player.TryPlaceTorch();

    /// <summary>이 노멀 방향의 '면'에 이미 횃불이 설치됐는지.</summary>
    public bool HasTorchOnFace(Vector3 faceNormal) => torchFaces.Contains(ToFaceKey(faceNormal));

    /// <summary>이 노멀 방향의 '면'에 횃불이 설치됐다고 표시.</summary>
    public void MarkTorchPlaced(Vector3 faceNormal) => torchFaces.Add(ToFaceKey(faceNormal));

    /// <summary>
    /// 표면 노멀을 6방향(±X/±Y/±Z) 중 하나로 스냅해 '면'을 식별하는 키로 만든다.
    /// (약간 비스듬히 맞은 노멀도 가장 가까운 축 방향의 면으로 처리)
    /// 설치와 복원이 같은 규칙을 써야 해서 공개되어 있습니다.
    /// </summary>
    public static Vector3Int ToFaceKey(Vector3 normal)
    {
        float ax = Mathf.Abs(normal.x), ay = Mathf.Abs(normal.y), az = Mathf.Abs(normal.z);
        if (ax >= ay && ax >= az) return new Vector3Int(normal.x >= 0f ? 1 : -1, 0, 0);
        if (az >= ay && az >= ax) return new Vector3Int(0, 0, normal.z >= 0f ? 1 : -1);
        return new Vector3Int(0, normal.y >= 0f ? 1 : -1, 0);
    }
}