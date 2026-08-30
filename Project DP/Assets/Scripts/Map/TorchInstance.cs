using UnityEngine;

/// <summary>
/// 플레이어가 설치한 횃불에 붙는 표식. 설치할 때 자동으로 추가됩니다.
///
/// 어느 벽의 어느 면에 붙었는지를 기억하는 이유: 던전을 나갔다 들어왔을 때 위치·회전만 되돌리면
/// WallObject 쪽 '이 면에 이미 설치됨' 기록이 비어 있어서, 복원된 횃불이 붙은 면에 하나 더
/// 설치할 수 있게 됩니다.
/// </summary>
public class TorchInstance : MonoBehaviour
{
    private Vector2Int wallCell;
    private Vector3Int faceKey;

    public void Initialize(Vector2Int wallCell, Vector3Int faceKey)
    {
        this.wallCell = wallCell;
        this.faceKey = faceKey;
    }

    public TorchRecord ToRecord() => new TorchRecord
    {
        position = transform.position,
        rotation = transform.rotation,
        wallCell = wallCell,
        faceKey = faceKey
    };
}
