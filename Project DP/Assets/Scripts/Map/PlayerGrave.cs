using UnityEngine;

/// <summary>
/// 플레이어 사망 시 설치되는 묘비. F키 상호작용으로 제거할 수 있습니다.
/// DungeonManager가 런타임에 생성·복원하며, MapSnapshot.graves에 기록됩니다.
/// </summary>
public class PlayerGrave : MonoBehaviour
{
    public enum Variant
    {
        A = 0,
        B = 1
    }

    private Vector2Int cell;
    private Variant variant;

    public Vector2Int Cell => cell;
    public Variant GraveVariant => variant;

    public void Initialize(Vector2Int cell, Variant variant)
    {
        this.cell = cell;
        this.variant = variant;
    }

    public GraveRecord ToRecord() => new GraveRecord
    {
        cell = cell,
        variant = (int)variant,
        position = transform.position,
        rotation = transform.rotation
    };

    /// <summary>F키 상호작용. 추후 유품/스킬 회수·이펙트는 여기에 연결할 수 있습니다.</summary>
    public void Interact()
    {
        if (MinimapManager.Instance != null)
            MinimapManager.Instance.UnregisterGrave(transform);

        if (DungeonManager.Instance != null)
            DungeonManager.Instance.RemoveGraveRecord(cell);

        Destroy(gameObject);
    }
}
