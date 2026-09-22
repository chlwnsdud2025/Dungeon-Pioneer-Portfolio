using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ScreenFader : MonoBehaviour
{
    private static ScreenFader instance;
    private CanvasGroup group;
    private bool revealOnSceneLoad;
    private float revealDuration = 0.4f;
    private int fadeVersion;

    public static ScreenFader Instance
    {
        get
        {
            if (instance != null) return instance;
            GameObject go = new GameObject("ScreenFader", typeof(RectTransform));
            instance = go.AddComponent<ScreenFader>();
            DontDestroyOnLoad(go);
            return instance;
        }
    }

    public bool IsOpaque => group != null && group.alpha >= 0.999f;
    public bool IsClear => group != null && group.alpha <= 0.001f;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32767;
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        GameObject cover = new GameObject("Black", typeof(RectTransform), typeof(Image));
        cover.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)cover.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = cover.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this) { SceneManager.sceneLoaded -= OnSceneLoaded; instance = null; }
    }

    public void SetOpaque()
    {
        fadeVersion++;
        group.alpha = 1f;
        group.blocksRaycasts = true;
    }

    public void RevealAfterNextSceneLoad(float duration)
    {
        revealDuration = Mathf.Max(0f, duration);
        revealOnSceneLoad = true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!revealOnSceneLoad) return;
        revealOnSceneLoad = false;
        if (PlayerManager.Instance != null && PlayerManager.Instance.Controller != null)
            PlayerManager.Instance.Controller.SetTransitionInputLocked(true);
        StartCoroutine(RevealNextFrame());
    }

    private IEnumerator RevealNextFrame()
    {
        yield return null; // PlayerManager의 앵커 배치가 끝난 다음 프레임
        yield return FadeTo(0f, revealDuration);
        if (PlayerManager.Instance != null && PlayerManager.Instance.Controller != null)
            PlayerManager.Instance.Controller.SetTransitionInputLocked(false);
    }

    public IEnumerator FadeTo(float target, float duration)
    {
        int version = ++fadeVersion;
        float start = group.alpha;
        float elapsed = 0f;
        group.blocksRaycasts = true;
        while (elapsed < duration && version == fadeVersion)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        if (version != fadeVersion) yield break;
        group.alpha = target;
        group.blocksRaycasts = target > 0f;
    }
}
