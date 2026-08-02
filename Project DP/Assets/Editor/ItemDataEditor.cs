using UnityEditor;
using UnityEngine;

// 두 번째 인자 true → EquipmentItemData/ConsumableItemData 등 '파생 클래스 전부'에 이 인스펙터가 적용됩니다.
[CustomEditor(typeof(ItemData), true)]
public class ItemDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ItemData item = (ItemData)target;

        // ── 상단 요약: 아이콘 프리뷰 + 이름/카테고리/중첩 ──
        EditorGUILayout.BeginHorizontal("box");
        {
            Rect iconRect = GUILayoutUtility.GetRect(72, 72, GUILayout.Width(72), GUILayout.Height(72));
            if (item.icon != null)
            {
                Texture2D tex = AssetPreview.GetAssetPreview(item.icon);
                if (tex == null) tex = item.icon.texture;
                GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.DrawRect(iconRect, new Color(0, 0, 0, 0.2f));
                GUI.Label(iconRect, "No Icon", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.BeginVertical();
            {
                string title = string.IsNullOrEmpty(item.displayName) ? item.name : item.displayName;
                GUILayout.Label(title, EditorStyles.boldLabel);
                GUILayout.Label($"카테고리: {item.Category}", EditorStyles.miniLabel);
                GUILayout.Label(item.isStackable ? $"중첩 가능 (최대 {item.maxStackSize})" : "중첩 불가",
                    EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // ── 각 SO 타입의 실제 필드 (자동으로 관련 필드만 표시됨) ──
        DrawDefaultInspector();

        // ── 규칙 보정: 중첩 불가인데 최대치가 1이 아니면 1로 고정 ──
        if (!item.isStackable && item.maxStackSize != 1)
        {
            item.maxStackSize = 1;
            EditorUtility.SetDirty(item);
        }
    }
}