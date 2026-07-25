using System.Collections.Generic;
using UnityEngine;

public class WallObject : MonoBehaviour
{
    // 벽 하나라도 네 측면에 각각 1개씩 횃불이 설치될 수 있으므로, '면 단위'로 설치 여부를 관리합니다.
    // 면은 설치할 때 사용하는 표면 노멀(hitNormal) 방향으로 식별합니다.
    private readonly HashSet<Vector3Int> torchFaces = new HashSet<Vector3Int>();

    /// <summary>이 노멀 방향의 '면'에 이미 횃불이 설치됐는지.</summary>
    public bool HasTorchOnFace(Vector3 faceNormal) => torchFaces.Contains(ToFaceKey(faceNormal));

    /// <summary>이 노멀 방향의 '면'에 횃불이 설치됐다고 표시.</summary>
    public void MarkTorchPlaced(Vector3 faceNormal) => torchFaces.Add(ToFaceKey(faceNormal));

    // 표면 노멀을 6방향(±X/±Y/±Z) 중 하나로 스냅해 '면'을 식별하는 키로 만든다.
    // (약간 비스듬히 맞은 노멀도 가장 가까운 축 방향의 면으로 처리)
    private static Vector3Int ToFaceKey(Vector3 normal)
    {
        float ax = Mathf.Abs(normal.x), ay = Mathf.Abs(normal.y), az = Mathf.Abs(normal.z);
        if (ax >= ay && ax >= az) return new Vector3Int(normal.x >= 0f ? 1 : -1, 0, 0);
        if (az >= ay && az >= ax) return new Vector3Int(0, 0, normal.z >= 0f ? 1 : -1);
        return new Vector3Int(0, normal.y >= 0f ? 1 : -1, 0);
    }
}