using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "UIRegistry", menuName = "Dungeon/UI/Registry", order = 1)]
public class UIRegistrySO : ScriptableObject
{
    [Header("Scene UI (한 번에 하나만 활성)")]
    public List<AssetReferenceGameObject> sceneUIs = new();

    [Header("Popup UI (스택으로 쌓임)")]
    public List<AssetReferenceGameObject> popupUIs = new();
}
