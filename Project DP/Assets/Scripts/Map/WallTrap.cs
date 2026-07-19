using UnityEngine;

// 벽 타일에 부착하는 전용 함정. (WallObject와 같은 GameObject에 함께 부착)
// 플레이어가 이 벽에 '횃불 설치' 상호작용을 하면 PlaceTorchState_Player.SpawnTorchOnWall()에서
// Activate()가 호출되어, 재활용한 가시(Spike) 오브젝트가 바닥 함정(TrapTile)과 똑같이
// Y축으로 빠르게 올라왔다가(0) 천천히 내려갑니다(-1.7).
// 데미지는 지정한 트리거 콜라이더(damageTrigger)를 가시가 올라와 있는 동안에만 켜서 판정합니다.
// 이때 횃불은 소모되지만(개수 차감) 벽에는 설치되지 않습니다.
public class WallTrap : MonoBehaviour
{
    [Header("Spike (재활용하는 가시 오브젝트)")]
    public Transform spikeObject;
    public float spikeRaisedY = 0f;      // 올라왔을 때(공격) 로컬 Y
    public float spikeLoweredY = -1.7f;  // 숨어있을 때 로컬 Y
    [Tooltip("빠르게 올라오는 시간(초)")]
    public float raiseDuration = 0.08f;
    [Tooltip("천천히 다시 내려가는 시간(초)")]
    public float lowerDuration = 1.2f;

    [Header("Damage")]
    public float trapDamage = 25f;
    [Tooltip("가시가 올라와 있는 동안에만 활성화되는 트리거 콜라이더. WallTrap과 같은 오브젝트에 두는 것을 권장합니다.")]
    public Collider damageTrigger;

    [Header("Options")]
    [Tooltip("true면 최초 1회만 발동하고 이후에는 작동하지 않습니다.")]
    public bool singleUse = false;

    private bool goingUp = false;       // 올라가는 중(true) / 내려가는 중(false)
    private bool armed = false;         // 현재 발동(가시 이동) 중인지
    private bool dealtThisShot = false; // 이번 발동에서 이미 데미지를 줬는지
    private bool spent = false;         // singleUse용: 이미 소진되었는지

    private void Awake()
    {
        // 시작 시 가시는 내려가 있고, 데미지 판정은 꺼둔다.
        if (spikeObject != null)
        {
            Vector3 lp = spikeObject.localPosition;
            spikeObject.localPosition = new Vector3(lp.x, spikeLoweredY, lp.z);
        }
        if (damageTrigger != null) damageTrigger.enabled = false;
    }

    // PlaceTorchState_Player.SpawnTorchOnWall()에서 호출됩니다.
    // (outwardNormal은 호출부 호환을 위해 유지하며, Y축 이동 방식에서는 사용하지 않습니다.)
    public void Activate(Vector3 outwardNormal)
    {
        if (spikeObject == null || spent) return;

        armed = true;
        goingUp = true;
        dealtThisShot = false;
        if (damageTrigger != null) damageTrigger.enabled = true; // 올라오는 즉시 판정 ON
        if (singleUse) spent = true;
    }

    private void Update()
    {
        if (!armed || spikeObject == null) return;

        // 올릴 땐 빠르게, 내릴 땐 천천히
        float targetY  = goingUp ? spikeRaisedY : spikeLoweredY;
        float duration = goingUp ? raiseDuration : lowerDuration;
        float speed    = Mathf.Abs(spikeRaisedY - spikeLoweredY) / Mathf.Max(0.01f, duration);

        Vector3 lp = spikeObject.localPosition;
        float newY = Mathf.MoveTowards(lp.y, targetY, speed * Time.deltaTime);
        spikeObject.localPosition = new Vector3(lp.x, newY, lp.z);

        if (goingUp)
        {
            // 다 올라오면 곧바로 '천천히 내려가기' 단계로 전환
            if (Mathf.Approximately(newY, spikeRaisedY))
                goingUp = false;
        }
        else
        {
            // 다 내려가면 발동 종료 + 판정 OFF
            if (Mathf.Approximately(newY, spikeLoweredY))
            {
                armed = false;
                if (damageTrigger != null) damageTrigger.enabled = false;
            }
        }
    }

    // damageTrigger가 이 오브젝트에 있을 때(권장), 가시가 올라와 판정이 켜진 동안 접촉을 감지합니다.
    private void OnTriggerStay(Collider other)
    {
        if (!armed || dealtThisShot) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamage(trapDamage, transform);
            dealtThisShot = true;
            return;
        }

        EnemyController enemy = other.GetComponent<EnemyController>();
        if (enemy != null)
        {
            enemy.TakeDamage(trapDamage);
            dealtThisShot = true;
        }
    }
}
