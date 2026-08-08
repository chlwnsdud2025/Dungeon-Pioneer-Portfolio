using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemData), true)]
public class ItemDataEditor : Editor
{
    private enum EditMode { Cells, Image }
    private EditMode mode = EditMode.Cells;
    private const float Cell = 40f;

    public override void OnInspectorGUI()
    {
        ItemData item = (ItemData)target;
        EnsureMaskSize(item);

        DrawCombinedEditor(item);      // ★ 이미지 + 그리드 통합 편집

        EditorGUILayout.Space();
        DrawDefaultInspector();

        if (!item.isStackable && item.maxStackSize != 1) { item.maxStackSize = 1; EditorUtility.SetDirty(item); }
    }

    private void EnsureMaskSize(ItemData item)
    {
        int need = item.GridWidth * item.GridHeight;
        if (item.shapeMask == null || item.shapeMask.Length != need)
        {
            var m = new bool[need];
            for (int i = 0; i < need; i++) m[i] = true;   // 크기 바뀌면 전체 사각형으로
            item.shapeMask = m; EditorUtility.SetDirty(item);
        }
    }

    private void DrawCombinedEditor(ItemData item)
    {
        int w = item.GridWidth, h = item.GridHeight;
        float pw = w * Cell, ph = h * Cell;

        EditorGUILayout.Space(4);
        mode = (EditMode)GUILayout.Toolbar((int)mode, new[] { "칸 편집", "이미지 맞추기" });

        Rect area = GUILayoutUtility.GetRect(pw, ph, GUILayout.Width(pw), GUILayout.Height(ph));
        Event e = Event.current;

        GUI.BeginClip(area);   // 이미지가 박스를 넘어가도 이 영역 안으로 클립
        {
            EditorGUI.DrawRect(new Rect(0, 0, pw, ph), new Color(0.12f, 0.12f, 0.12f, 1f));

            // 점유 칸 하이라이트 (이미지 아래)
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (item.IsCellOccupied(x, y))
                        EditorGUI.DrawRect(new Rect(x * Cell, y * Cell, Cell, Cell), new Color(0.2f, 0.6f, 1f, 0.18f));

            // 아이콘 (배율/오프셋 적용, 실제 스프라이트 영역 정확히)
            if (item.icon != null)
            {
                Vector2 size = new Vector2(pw, ph) * item.iconScale;
                Vector2 center = new Vector2(pw, ph) * 0.5f + item.iconOffset * Cell;
                DrawSprite(new Rect(center - size * 0.5f, size), item.icon);
            }

            // 격자선 (이미지 위)
            Color line = new Color(1, 1, 1, 0.45f);
            for (int x = 0; x <= w; x++) EditorGUI.DrawRect(new Rect(x * Cell, 0, 1, ph), line);
            for (int y = 0; y <= h; y++) EditorGUI.DrawRect(new Rect(0, y * Cell, pw, 1), line);

            // ── 입력 (좌표는 clip 로컬) ──
            Vector2 mp = e.mousePosition;
            bool inside = mp.x >= 0 && mp.x < pw && mp.y >= 0 && mp.y < ph;

            if (mode == EditMode.Cells && e.type == EventType.MouseDown && e.button == 0 && inside)
            {
                int x = Mathf.FloorToInt(mp.x / Cell), y = Mathf.FloorToInt(mp.y / Cell);
                Undo.RecordObject(item, "Toggle Cell");
                item.shapeMask[y * w + x] = !item.shapeMask[y * w + x];
                EditorUtility.SetDirty(item); e.Use(); Repaint();
            }
            else if (mode == EditMode.Image)
            {
                if (e.type == EventType.MouseDrag && e.button == 0 && inside)
                {
                    Undo.RecordObject(item, "Move Icon");
                    item.iconOffset += e.delta / Cell;
                    EditorUtility.SetDirty(item); e.Use(); Repaint();
                }
                if (e.type == EventType.ScrollWheel && inside)
                {
                    Undo.RecordObject(item, "Scale Icon");
                    item.iconScale = Mathf.Clamp(item.iconScale - e.delta.y * 0.03f, 0.1f, 5f);
                    EditorUtility.SetDirty(item); e.Use(); Repaint();
                }
            }
        }
        GUI.EndClip();

        // ── 정밀 조절 필드 ──
        EditorGUI.BeginChangeCheck();
        float scale = EditorGUILayout.Slider("이미지 배율", item.iconScale, 0.1f, 5f);
        Vector2 off = EditorGUILayout.Vector2Field("이미지 오프셋(칸)", item.iconOffset);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(item, "Adjust Icon");
            item.iconScale = scale; item.iconOffset = off; EditorUtility.SetDirty(item);
        }
        if (GUILayout.Button("이미지 리셋"))
        {
            Undo.RecordObject(item, "Reset Icon");
            item.iconOffset = Vector2.zero; item.iconScale = 1f; EditorUtility.SetDirty(item);
        }

        EditorGUILayout.HelpBox(mode == EditMode.Cells
            ? "칸을 클릭해 점유를 켜고/끕니다 (L자 등 모양)."
            : "드래그로 이미지 이동, 스크롤로 크기 조절 → 칸에 맞추세요.", MessageType.Info);
    }

    // 스프라이트를 '해당 영역만' 정확히 그림 (아틀라스에 있어도 OK), 비율 유지
    private void DrawSprite(Rect rect, Sprite sprite)
    {
        Texture2D tex = sprite.texture;
        Rect tr = sprite.textureRect;
        Rect uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);

        float sa = tr.width / tr.height, ra = rect.width / rect.height;   // 비율 유지 fit
        Rect draw = (sa > ra)
            ? new Rect(rect.x, rect.y + (rect.height - rect.width / sa) * 0.5f, rect.width, rect.width / sa)
            : new Rect(rect.x + (rect.width - rect.height * sa) * 0.5f, rect.y, rect.height * sa, rect.height);

        GUI.DrawTextureWithTexCoords(draw, tex, uv);
    }
}