using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public abstract class UIPopup : UIPanel
{
    [Header("Dimmed Background")]
    [Tooltip("팝업 뒤 어두운 배경 버튼. 이걸 클릭하면 팝업이 닫힙니다.")]
    [SerializeField] private Button dimmed;
    [Tooltip("딤 배경 클릭으로 닫기를 허용할지 여부.")]
    [SerializeField] private bool closeOnDimmedClick = true;

    public override void Initialize()
    {
        base.Initialize();

        if (dimmed != null && closeOnDimmedClick)
            dimmed.onClick.AddListener(OnClickBack);
    }

    /// <summary>팝업을 닫습니다. (닫기 버튼 onClick 등에 연결해서 써도 됩니다.)</summary>
    public void OnClickBack()
    {
        UIManager.Instance.HidePopup().Forget();
    }

    /// <summary>
    /// ESC를 눌렀을 때 UIManager가 호출합니다. true를 반환하면 이 팝업이 입력을 소화한 것으로 보고
    /// 팝업을 닫지 않습니다. (예: 인벤토리는 컨텍스트 메뉴가 떠 있으면 그것부터 닫습니다)
    /// </summary>
    public virtual bool OnCancel() => false;
}
