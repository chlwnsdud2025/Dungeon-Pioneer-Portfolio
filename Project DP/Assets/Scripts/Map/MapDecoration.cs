using UnityEngine;

public class MapDecoration : MonoBehaviour
{
    [Tooltip("개척 완료 시 켤 장식 묶음. 프리팹에서는 켜둔 채 편집하세요 — Awake에서 자동으로 꺼집니다.")]
    [SerializeField] private GameObject decorationRoot;

    // 미니맵·맵 생성 코드가 '비활성 포함'으로 훑기 때문에, 꺼져 있어도 타일 태그가 붙어 있으면
    // 개척도 분모가 오염되고 맵 원점 보정까지 밀립니다.
    private static readonly string[] TileTags = { "Floor", "Wall", "Trap", "Entrance", "Exit" };

    private void Awake()
    {
        if (decorationRoot == null)
        {
            Debug.LogWarning($"[MapDecoration] '{name}'에 Decoration Root가 연결되지 않아 장식이 표시되지 않습니다.", this);
            return;
        }

        WarnIfTaggedAsTile();
        decorationRoot.SetActive(false);
    }

    /// <summary>장식 묶음을 켜거나 끕니다. (DungeonManager가 층 전환 직후 호출)</summary>
    public void SetRevealed(bool revealed)
    {
        if (decorationRoot != null) decorationRoot.SetActive(revealed);
    }

    /// <summary>장식에 타일 태그가 붙어 있으면 경고합니다. 값은 고치지 않고 알리기만 합니다.</summary>
    private void WarnIfTaggedAsTile()
    {
        foreach (Transform child in decorationRoot.GetComponentsInChildren<Transform>(true))
        {
            foreach (string tag in TileTags)
            {
                if (!child.CompareTag(tag)) continue;

                Debug.LogWarning(
                    $"[MapDecoration] 장식 '{child.name}'에 타일 태그 '{tag}'가 붙어 있습니다. " +
                    "미니맵 개척도와 맵 원점 보정이 이 오브젝트를 실제 타일로 착각합니다. Untagged로 바꿔주세요.",
                    child);
                break;
            }
        }
    }
}
