using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// UIManager가 시작 시 로드할 패널 프리팹 목록을 담는 ScriptableObject.
/// 프리팹을 Addressable로 표시한 뒤, 종류에 맞게 아래 리스트에 등록하세요.
///
/// 생성: [Assets > Create > Dungeon > UI > Registry]
/// </summary>
[CreateAssetMenu(fileName = "UIRegistry", menuName = "Dungeon/UI/Registry", order = 1)]
public class UIRegistrySO : ScriptableObject
{
    [Header("Scene UI (한 번에 하나만 활성)")]
    public List<AssetReferenceGameObject> sceneUIs = new();

    [Header("Popup UI (스택으로 쌓임)")]
    public List<AssetReferenceGameObject> popupUIs = new();
}
