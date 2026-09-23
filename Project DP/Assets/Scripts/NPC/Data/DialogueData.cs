using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대화 한 편. 여러 NPC가 같은 애셋을 공유할 수 있습니다.
///
/// 말풍선은 머리 위에 뜨므로 화자 이름을 따로 두지 않습니다.
/// 분기 대화는 노드 그래프와 전용 에디터가 있어야 쓸 만해서, 실제로 필요해질 때 붙입니다.
/// </summary>
[CreateAssetMenu(fileName = "Dialogue_", menuName = "NPC/Dialogue")]
public class DialogueData : ScriptableObject
{
    [Tooltip("한 줄씩 순서대로 말풍선에 표시됩니다. F를 누를 때마다 다음 줄로 넘어갑니다.")]
    [SerializeField, TextArea(2, 4)] private List<string> lines = new List<string>();

    public IReadOnlyList<string> Lines => lines;
    public int Count => lines.Count;
}
