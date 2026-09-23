using TMPro;
using UnityEngine;

/// <summary>
/// 머리 위에 뜨는 월드 공간 말풍선.
///
/// 누가 왜 띄우는지는 모르고 '보여달라는 글자'만 표시합니다. NpcActor도 DialogueModule도
/// 참조하지 않으므로, 나중에 적의 경고 대사나 플레이어 혼잣말에도 그대로 쓸 수 있습니다.
///
/// EnemyHealthBar와 같은 구조입니다. 켜고 끄는 대상은 이 스크립트가 붙은 오브젝트가 아니라
/// 자식 컨테이너여야 합니다 — 자기 자신을 끄면 이 컴포넌트도 함께 꺼져 다시 켤 수 없습니다.
/// </summary>
public class SpeechBubble : MonoBehaviour
{
    [Tooltip("켜고 끌 대상(배경 + 텍스트 컨테이너). 이 스크립트가 붙은 오브젝트와 달라야 합니다.")]
    [SerializeField] private GameObject visual;
    [SerializeField] private TMP_Text bodyText;

    public bool IsShowing => visual != null && visual.activeSelf;

    private void Awake()
    {
        // ── 자주 하는 실수 자동 진단 ──
        if (visual == null)
            Debug.LogWarning("[SpeechBubble] Visual이 연결되지 않아 말풍선을 켜고 끌 수 없습니다.", this);
        else if (visual == gameObject)
            Debug.LogWarning("[SpeechBubble] Visual이 이 스크립트와 같은 오브젝트입니다. " +
                             "숨겨지면 이 컴포넌트도 함께 꺼져 다시 켤 수 없습니다. 자식 컨테이너를 지정하세요.", this);

        if (bodyText == null)
            Debug.LogWarning("[SpeechBubble] Body Text가 연결되지 않아 대사가 보이지 않습니다.", this);

        Hide();
    }

    public void Show(string text)
    {
        if (bodyText != null) bodyText.text = text;
        if (visual != null) visual.SetActive(true);
    }

    public void Hide()
    {
        if (visual != null) visual.SetActive(false);
    }
}
