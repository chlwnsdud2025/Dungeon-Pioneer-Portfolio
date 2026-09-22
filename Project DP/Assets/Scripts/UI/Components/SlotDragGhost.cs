using UnityEngine;
using UnityEngine.UI;

public static class SlotDragGhost
{
    private static Image _image;

    /// <summary>고스트를 띄웁니다. canvasRoot는 보통 끄는 쪽의 rootCanvas.transform.</summary>
    public static void Show(Sprite sprite, Vector2 size, Vector2 screenPos, Transform canvasRoot)
    {
        if (sprite == null || canvasRoot == null) return;

        if (_image == null)
        {
            var go = new GameObject("SlotDragGhost", typeof(RectTransform), typeof(Image));
            _image = go.GetComponent<Image>();
            _image.raycastTarget = false;   // 드롭 판정이 고스트 밑으로 통과해야 함
            _image.preserveAspect = true;
        }

        // 캔버스가 바뀌었을 수 있으므로(팝업 ↔ HUD) 매번 부모를 확인하고 최상단으로 올립니다.
        Transform t = _image.transform;
        if (t.parent != canvasRoot) t.SetParent(canvasRoot, false);
        t.SetAsLastSibling();

        _image.sprite = sprite;
        _image.color = new Color(1f, 1f, 1f, 0.75f);
        _image.rectTransform.sizeDelta = size;
        _image.gameObject.SetActive(true);

        Move(screenPos);
    }

    public static void Move(Vector2 screenPos)
    {
        // 좌표 처리는 기존 InventoryItemView.OnDrag와 동일한 방식으로 맞춥니다.
        if (_image != null) _image.rectTransform.position = screenPos;
    }

    public static void Hide()
    {
        if (_image != null) _image.gameObject.SetActive(false);
    }
}
