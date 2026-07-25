using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene UI 위에 스택으로 쌓이는 팝업. 어두운 배경(dimmed)을 클릭하면 닫히도록 옵션을 제공합니다.
/// UIManager.ShowPopup&lt;T&gt;() / HidePopup()으로 제어합니다.
/// </summary>
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
}
