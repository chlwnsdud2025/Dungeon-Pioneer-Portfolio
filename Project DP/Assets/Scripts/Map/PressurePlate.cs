using UnityEngine;

// 밟으면 연결된 ArrowTrap(들)을 발사시키는 발판.
// 트리거 콜라이더 위에 플레이어(또는 적)가 올라오면 연결된 함정의 Fire()를 호출합니다.
// 발판과 발사기를 분리했으므로 "발판은 바닥, 화살은 맞은편 벽에서" 같은 배치가 가능합니다.
[RequireComponent(typeof(Collider))]
public class PressurePlate : MonoBehaviour
{
    [Header("연결된 화살 함정 (여럿 가능)")]
    [Tooltip("이 발판을 밟으면 발사시킬 ArrowTrap들. 여러 개를 연결하면 동시에 일제사격합니다.")]
    public ArrowTrap[] arrowTraps;

    [Header("Trigger Settings")]
    [Tooltip("적이 밟아도 발동할지 여부. 꺼두면 플레이어만 발동합니다.")]
    public bool triggeredByEnemy = false;
    [Tooltip("한 번 밟은 뒤 다시 발동 가능해지기까지의 대기 시간(초).")]
    public float cooldown = 1f;
    [Tooltip("true면 최초 1회만 발동합니다.")]
    public bool singleUse = false;

    private float lastFireTime = -999f;
    private bool spent = false;

    private void Reset()
    {
        // 편의 기능: 콜라이더를 자동으로 트리거로 설정
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // [DEBUG] 무엇이든 발판 트리거에 들어오면 항상 기록 (이 로그가 안 뜨면 트리거 자체가 발동 안 하는 것)
        Debug.Log($"[PressurePlate] OnTriggerEnter: {other.name}", this);

        if (spent)
        {
            Debug.Log("[PressurePlate] 이미 소진됨(singleUse) → 무시", this);
            return;
        }
        if (Time.time < lastFireTime + cooldown)
        {
            Debug.Log($"[PressurePlate] 쿨다운 중({lastFireTime + cooldown - Time.time:F2}s 남음) → 무시", this);
            return;
        }

        bool isPlayer = other.GetComponent<PlayerController>() != null;
        bool isEnemy = other.GetComponent<EnemyController>() != null;
        Debug.Log($"[PressurePlate] isPlayer={isPlayer}, isEnemy={isEnemy}, triggeredByEnemy={triggeredByEnemy}", this);

        // 플레이어는 항상, 적은 옵션이 켜져 있을 때만 발동
        if (!isPlayer && !(triggeredByEnemy && isEnemy))
        {
            Debug.Log("[PressurePlate] 발동 대상 아님 → 무시 (other에 PlayerController/EnemyController가 없거나 적 발동이 꺼짐)", this);
            return;
        }

        Debug.Log("[PressurePlate] 발동 조건 충족 → FireAll() 호출", this);
        FireAll();
        lastFireTime = Time.time;
        if (singleUse) spent = true;
    }

    private void FireAll()
    {
        if (arrowTraps == null || arrowTraps.Length == 0)
        {
            Debug.LogWarning("[PressurePlate] arrowTraps가 비어 있음! 인스펙터에서 ArrowTrap을 연결했는지 확인하세요.", this);
            return;
        }

        Debug.Log($"[PressurePlate] 연결된 ArrowTrap 개수 = {arrowTraps.Length}", this);
        for (int i = 0; i < arrowTraps.Length; i++)
        {
            ArrowTrap trap = arrowTraps[i];
            if (trap == null)
            {
                Debug.LogWarning($"[PressurePlate] arrowTraps[{i}]가 null(연결 끊김) → 건너뜀", this);
                continue;
            }

            // [DEBUG] 참조가 '씬에 배치된 인스턴스'인지 '프리팹 에셋'인지 판별 (에셋이면 Awake 미실행 → 발사 안 됨)
            bool isSceneInstance = trap.gameObject.scene.IsValid();
            if (!isSceneInstance)
            {
                Debug.LogError($"[PressurePlate] arrowTraps[{i}]={trap.name} 이(가) 프리팹 에셋을 참조 중입니다! " +
                    "씬/맵에 실제로 배치된 ArrowTrap 인스턴스를 연결해야 합니다. (에셋은 Awake가 안 돌아 화살 풀이 비어 발사되지 않음)", this);
            }
            else
            {
                Debug.Log($"[PressurePlate] arrowTraps[{i}]={trap.name} (씬 인스턴스) → Fire() 호출", trap);
            }

            trap.Fire();
        }
    }
}
