using Cysharp.Threading.Tasks;
using UnityEngine;

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
