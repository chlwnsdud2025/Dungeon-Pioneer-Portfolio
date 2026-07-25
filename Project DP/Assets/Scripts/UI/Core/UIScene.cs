/// <summary>
/// 한 번에 하나만 활성화되는 '화면' 단위 UI (예: 인게임 HUD, 타이틀).
/// UIManager.ShowScene&lt;T&gt;()로 전환하면 이전 Scene UI는 자동으로 숨겨집니다.
/// </summary>
public abstract class UIScene : UIPanel { }
