using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 모든 UI 화면의 공통 부모. 표시(Show)/숨김(Hide)과 생명주기 훅을 제공합니다.
/// 실제 화면은 이 클래스를 직접 상속하기보다 UIScene(한 번에 하나) 또는 UIPopup(스택)을 상속해 구현합니다.
///
/// 생명주기 순서:
///   [표시] OnBeforeShow → Show( gameObject 활성 → OnShowAnimation → OnShow )
///   [숨김] Hide( OnHideAnimation → OnHide → gameObject 비활성 ) → OnAfterHide
///   (OnBeforeShow / OnAfterHide 호출은 UIManager가 담당)
/// </summary>
public abstract class UIPanel : MonoBehaviour
{
    /// <summary>UIManager가 패널을 로드한 직후 1회 호출. 버튼 리스너 등록 등 초기 세팅에 사용.</summary>
    public virtual void Initialize() { }

    public async UniTask Show()
    {
        gameObject.SetActive(true);
        await OnShowAnimation();
        await OnShow();
    }

    public async UniTask Hide(bool instant = false)
    {
        if (!instant) await OnHideAnimation();
        await OnHide();
        gameObject.SetActive(false);
    }

    // 필요한 패널만 오버라이드해서 애니메이션/로직을 채웁니다. (기본은 즉시 완료)
    protected virtual UniTask OnShowAnimation() => UniTask.CompletedTask;
    protected virtual UniTask OnShow() => UniTask.CompletedTask;
    protected virtual UniTask OnHideAnimation() => UniTask.CompletedTask;
    protected virtual UniTask OnHide() => UniTask.CompletedTask;

    /// <summary>표시 직전(활성화 전) 호출. 데이터 초기화/상태 리셋에 사용.</summary>
    public virtual void OnBeforeShow() { }
    /// <summary>완전히 숨겨진 직후 호출. 정리 작업에 사용.</summary>
    public virtual void OnAfterHide() { }
}
