using UnityEngine;

/// <summary>
/// 앱 전체 생명주기 동안 유지되는 싱글턴 베이스 (DontDestroyOnLoad).
/// 씬을 넘어도 살아남아야 하는 매니저(예: 전역 UIManager)에 사용합니다.
///
/// ⚠️ 주의: 이 베이스는 DontDestroyOnLoad라 '전역 상주'입니다.
///   Project-DP의 던전 씬 매니저(DungeonManager 등)처럼 '씬 전환 = 리셋'이어야 하는 대상에는
///   적합하지 않습니다. 그런 경우엔 이 베이스를 상속하지 말고 씬 로컬 싱글턴을 따로 두세요.
///
/// 사용 예: public class UIManager : MonoSingleton&lt;UIManager&gt; { ... }
/// 하위 클래스에서 Awake를 오버라이드할 땐 반드시 base.Awake()를 먼저 호출하세요.
/// </summary>
public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance { get; private set; }

    protected virtual void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this as T;
        DontDestroyOnLoad(gameObject);
    }
}
