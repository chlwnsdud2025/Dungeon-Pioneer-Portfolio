using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class UIManager : MonoSingleton<UIManager>
{
    [Header("Roots (Canvas 하위의 부모 Transform)")]
    [Tooltip("Scene UI들이 붙을 부모.")]
    [SerializeField] private Transform sceneRoot;
    [Tooltip("Popup UI들이 붙을 부모. (Scene보다 위에 그려지도록 계층상 아래에 두세요)")]
    [SerializeField] private Transform popupRoot;

    [Header("Registry")]
    [SerializeField] private UIRegistrySO registry;

    private readonly Dictionary<Type, UIPanel> _panels = new();
    private readonly Stack<UIPopup> _popupStack = new();

    private UIScene _currentScene;

    public bool IsInitialized { get; private set; }
    public UIScene CurrentScene => _currentScene;
    public UIPopup CurrentPopup => _popupStack.Count > 0 ? _popupStack.Peek() : null;

    private void Start()
    {
        // 중복으로 파괴되는 인스턴스면 초기화하지 않음 (MonoSingleton.Awake가 이미 Destroy 처리)
        if (Instance != this) return;

        StartupAsync().Forget();
    }

    // 어떤 Scene UI를 띄울지(타이틀/HUD)는 씬 도착 시 WorldManager가 정합니다.
    private async UniTaskVoid StartupAsync()
    {
        await InitializeAsync();

        // ESC 라우팅. InputManager와 초기화 순서가 얽혀 있어 OnEnable이 아니라 초기화가 끝난 뒤에 겁니다.
        if (InputManager.Instance != null)
            InputManager.Instance.OnUICancel += HandleCancel;
        else
            Debug.LogWarning("[UIManager] InputManager가 없어 ESC로 UI를 닫을 수 없습니다. " +
                             "먼저 로드되는 씬에 InputManager를 배치해주세요.");
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        if (InputManager.Instance != null)
            InputManager.Instance.OnUICancel -= HandleCancel;
    }

    /// <summary>Registry의 모든 패널을 로드/등록합니다. (Start에서 자동 호출)</summary>
    public async UniTask InitializeAsync()
    {
        if (IsInitialized) return;

        if (registry == null)
        {
            Debug.LogError("[UIManager] UIRegistrySO가 연결되지 않았습니다. 인스펙터를 확인해주세요.");
            return;
        }

        var tasks = new List<UniTask>();

        foreach (var reference in registry.sceneUIs)
            tasks.Add(LoadAndRegisterPanel(reference, sceneRoot));

        foreach (var reference in registry.popupUIs)
            tasks.Add(LoadAndRegisterPanel(reference, popupRoot));

        await UniTask.WhenAll(tasks);

        IsInitialized = true;
    }

    private async UniTask LoadAndRegisterPanel(AssetReferenceGameObject reference, Transform root)
    {
        var go = await reference.InstantiateAsync(root).ToUniTask();
        var panel = go.GetComponent<UIPanel>();

        if (panel == null)
        {
            Debug.LogError($"[UIManager] 프리팹에 UIPanel 컴포넌트가 없습니다: {go.name}");
            return;
        }

        panel.Initialize();
        panel.gameObject.SetActive(false);
        _panels[panel.GetType()] = panel; // 같은 타입 중복 등록 시 마지막 것으로 덮어씀
    }

    // ── Scene UI ─────────────────────────────────────────────

    /// <summary>Scene UI로 전환합니다. 이전 Scene UI는 자동으로 숨겨집니다.</summary>
    public async UniTask<T> ShowScene<T>() where T : UIScene
    {
        if (!TryGetPanel<T>(out var target)) return null;
        if (_currentScene == target) return target;

        if (_currentScene != null)
        {
            await _currentScene.Hide();
            _currentScene.OnAfterHide();
        }

        _currentScene = target;
        _currentScene.OnBeforeShow();
        await _currentScene.Show();

        return target;
    }

    // ── Popup UI ─────────────────────────────────────────────

    /// <summary>팝업을 스택 위에 띄웁니다. 이미 떠 있으면 그대로 반환.</summary>
    public async UniTask<T> ShowPopup<T>() where T : UIPopup
    {
        if (!TryGetPanel<T>(out var target)) return null;
        if (_popupStack.Contains(target)) return target;

        target.OnBeforeShow();
        await target.Show();
        _popupStack.Push(target);

        return target;
    }

    /// <summary>가장 위 팝업을 닫습니다.</summary>
    public async UniTask HidePopup(bool instant = false)
    {
        if (_popupStack.Count == 0) return;

        var popup = _popupStack.Pop();
        await popup.Hide(instant);
        popup.OnAfterHide();
    }

    /// <summary>열려 있는 모든 팝업을 닫습니다.</summary>
    public async UniTask HideAllPopups(bool instant = true)
    {
        while (_popupStack.Count > 0)
            await HidePopup(instant);
    }

    /// <summary>
    /// ESC 처리. 맨 위 팝업에 먼저 물어보고(OnCancel), 팝업이 소화하지 않으면 그 팝업을 닫습니다.
    /// 열린 팝업이 없으면 아무 일도 하지 않습니다.
    /// </summary>
    private void HandleCancel()
    {
        UIPopup popup = CurrentPopup;
        if (popup == null) return;

        if (popup.OnCancel()) return;   // 팝업이 자체적으로 처리 (예: 컨텍스트 메뉴만 닫기)

        HidePopup().Forget();
    }

    // ── 조회 ─────────────────────────────────────────────────

    public T GetPanel<T>() where T : UIPanel
    {
        _panels.TryGetValue(typeof(T), out var panel);
        return panel as T;
    }

    private bool TryGetPanel<T>(out T panel) where T : UIPanel
    {
        if (_panels.TryGetValue(typeof(T), out var found))
        {
            panel = found as T;
            return true;
        }

        Debug.LogError($"[UIManager] 패널을 찾을 수 없습니다: {typeof(T)} (Registry 등록 / 초기화 완료 여부 확인)");
        panel = null;
        return false;
    }
}
