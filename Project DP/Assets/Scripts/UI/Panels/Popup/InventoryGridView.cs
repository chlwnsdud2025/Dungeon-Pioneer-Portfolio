using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryGridView : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private RectTransform cellRoot;        // GridLayoutGroup 보유(배경 칸)
    [SerializeField] private RectTransform itemRoot;        // 아이템 아이콘 컨테이너
    [SerializeField] private Image cellPrefab;              // 칸 배경 1개
    [SerializeField] private InventoryItemView itemPrefab;  // 아이템 아이콘

    [Header("설정")]
    [SerializeField] private RectTransform panelRect;      // 패널 전체 크기 기준 (인스펙터에서 연결)
    private float cellSize;                                 // 패널 크기에서 자동 산출

    [SerializeField] private RectTransform highlightRoot;
    [SerializeField] private RectTransform dragLayer;
    private Camera uiCam;
    private bool built;
    private readonly List<Image> highlightPool = new();

    // ── 아이템 뷰 풀링 ──
    private readonly Queue<InventoryItemView> _viewPool = new();
    private readonly List<InventoryItemView> _activeViews = new();

    public Inventory Inventory => inventory;
    public RectTransform DragLayer => dragLayer;

    private void Awake()
    {
        var cv = GetComponentInParent<Canvas>();
        uiCam = (cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay) ? cv.worldCamera : null;
        // ★ BuildCells는 여기서 호출하지 않는다. Awake 시점엔 inventory.Grid가 아직 없을 수 있음
        //   (Inventory.Awake가 아직 안 돌았거나, 프리팹이라 참조가 준비 전일 수 있음)
        //   → 준비가 끝난 OnEnable에서 EnsureReady로 안전하게 초기화.
    }

    private void OnEnable()
    {
        if (!EnsureReady())
        {
            // 레이아웃이 아직 준비 안 됐으면 1프레임 뒤 재시도
            StartCoroutine(DelayedInit());
            return;
        }
        inventory.OnChanged += Rebuild;
        Rebuild();
    }

    private IEnumerator DelayedInit()
    {
        yield return null;                   // 1프레임 대기 — 레이아웃 계산 완료
        if (!EnsureReady()) yield break;     // 그래도 안 되면 포기
        inventory.OnChanged += Rebuild;
        Rebuild();
    }

    private void OnDisable()
    {
        StopAllCoroutines();                 // 대기 중이던 DelayedInit 정리
        if (inventory != null) inventory.OnChanged -= Rebuild;
    }

    // inventory 확보 + 최초 1회 칸 생성. 준비가 안 됐으면 false를 반환한다.
    private bool EnsureReady()
    {
        // 같은 프리팹의 Inventory를 인스펙터로 연결했으면 그대로 사용,
        // 안 했으면 부모 계층에서 자동으로 찾는다 (같은 프리팹이면 확실히 잡힘, true=비활성 포함).
        if (inventory == null) inventory = GetComponentInParent<Inventory>(true);
        if (inventory == null || inventory.Grid == null) return false;

        if (!built)
        {
            // OnEnable 시점에 RectTransform 레이아웃이 아직 계산 안 됐을 수 있으므로 강제 갱신
            Canvas.ForceUpdateCanvases();

            RectTransform pr = panelRect != null ? panelRect : (RectTransform)transform;
            if (pr.rect.width <= 0f || pr.rect.height <= 0f) return false;

            BuildCells();
            built = true;
        }
        return true;
    }

    // ── 배경 칸 만들기 (1회) ──
    private void BuildCells()
    {
        int w = inventory.Grid.Width, h = inventory.Grid.Height;

        // panelRect 기준으로 cellSize 자동 산출 (정사각형, 패널 안에 딱 맞게)
        if (panelRect == null) panelRect = (RectTransform)transform;
        Vector2 panelSize = panelRect.rect.size;
        cellSize = Mathf.Floor(Mathf.Min(panelSize.x / w, panelSize.y / h));

        Vector2 size = new Vector2(w * cellSize, h * cellSize);

        SetupTopLeft(cellRoot, size);   // 두 컨테이너를 같은 크기·좌상단 기준으로
        SetupTopLeft(itemRoot, size);
        SetupTopLeft(highlightRoot, size);

        var glg = cellRoot.GetComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(cellSize, cellSize);
        glg.spacing = Vector2.zero;
        glg.startCorner = GridLayoutGroup.Corner.UpperLeft;   // (0,0)=좌상단
        glg.startAxis = GridLayoutGroup.Axis.Horizontal;    // 오른쪽으로 채우다 다음 줄
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = w;

        foreach (Transform c in cellRoot) Destroy(c.gameObject);
        for (int i = 0; i < w * h; i++) Instantiate(cellPrefab, cellRoot);
    }

    private void SetupTopLeft(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);   // 부모 좌상단에 고정
        rt.pivot = new Vector2(0, 1);                  // 자기 좌상단 기준
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
    }

    // ── 아이템 다시 그리기 (풀링) ──
    public void Rebuild()
    {
        if (inventory == null || itemRoot == null) return;

        // 기존 활성 뷰를 전부 풀에 반납
        foreach (var v in _activeViews) ReturnView(v);
        _activeViews.Clear();

        foreach (InventoryItem it in inventory.Grid.Items)
        {
            InventoryItemView view = GetView();
            LayoutIcon(view.RectTransform, it);   // 위치/크기
            view.Setup(it, this);
            _activeViews.Add(view);
        }
    }

    private InventoryItemView GetView()
    {
        if (_viewPool.TryDequeue(out var view))
        {
            view.gameObject.SetActive(true);
            view.transform.SetParent(itemRoot, false);  // 드래그 후 dragLayer에 있을 수 있음
            return view;
        }
        return Instantiate(itemPrefab, itemRoot);
    }

    private void ReturnView(InventoryItemView view)
    {
        view.gameObject.SetActive(false);
        view.transform.SetParent(itemRoot, false);
        _viewPool.Enqueue(view);
    }

    // ── 아이콘 하나를 셀 좌표에 맞춰 배치 (에디터 프리뷰와 동일 공식, y만 뒤집음) ──
    private void LayoutIcon(RectTransform rt, InventoryItem it)
    {
        ItemData d = it.Data;
        float bw = d.GridWidth * cellSize;   // 바운딩 박스 픽셀 크기
        float bh = d.GridHeight * cellSize;

        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);   // 컨테이너 좌상단 기준
        rt.pivot = new Vector2(0.5f, 0.5f);            // 중심 기준(스케일도 중심으로)
        rt.sizeDelta = new Vector2(bw, bh) * d.iconScale;

        // 박스 중심(좌상단 기준) + 아이콘 오프셋.  uGUI는 y가 위 → 아래로 갈 땐 음수
        float cx = it.Anchor.x * cellSize + bw * 0.5f + d.iconOffset.x * cellSize;
        float cy = -(it.Anchor.y * cellSize + bh * 0.5f) - d.iconOffset.y * cellSize;
        rt.anchoredPosition = new Vector2(cx, cy);
    }

    // ── 화면 좌표 → 셀 (Step 4 드래그에서 사용) ──
    public bool ScreenToCell(Vector2 screenPos, out Vector2Int cell)
    {
        cell = default;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(itemRoot, screenPos, uiCam, out Vector2 local))
            return false;
        cell = new Vector2Int(Mathf.FloorToInt(local.x / cellSize), Mathf.FloorToInt(-local.y / cellSize));
        return inventory.Grid.InBounds(cell);
    }

    // ── 배치 하이라이트 (아이템의 '점유 칸'만 초록/빨강) ──
    public void ShowHighlight(ItemData item, Vector2Int anchor, bool valid)
    {
        ClearHighlight();
        Color col = valid ? new Color(0.3f, 1f, 0.3f, 0.35f) : new Color(1f, 0.3f, 0.3f, 0.35f);
        int i = 0;
        foreach (Vector2Int off in item.OccupiedCells())
        {
            Vector2Int c = anchor + off;

            // ★ 그리드를 벗어난 칸은 그리지 않는다. highlightRoot에는 마스크가 없어서
            //   범위 밖 좌표에 배치하면 인벤토리 패널 바깥 화면에 그대로 떠버린다.
            //   커서 위치가 아니라 '칸 단위'로 걸러야 하는 이유: grabOffset 때문에 커서가 그리드
            //   밖이어도 anchor는 유효할 수 있어서, 그때는 드롭이 성공하므로 표시도 남아야 한다.
            if (!inventory.Grid.InBounds(c)) continue;

            Image hi = GetHighlight(i++);
            RectTransform rt = hi.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(cellSize, cellSize);
            rt.anchoredPosition = new Vector2(c.x * cellSize, -c.y * cellSize);
            hi.color = col;
            hi.gameObject.SetActive(true);
        }
    }
    public void ClearHighlight()
    {
        foreach (Image hi in highlightPool) hi.gameObject.SetActive(false);
    }
    private Image GetHighlight(int index)
    {
        while (highlightPool.Count <= index)
        {
            var go = new GameObject("Highlight", typeof(Image));
            go.transform.SetParent(highlightRoot, false);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            highlightPool.Add(img);
        }
        return highlightPool[index];
    }
}