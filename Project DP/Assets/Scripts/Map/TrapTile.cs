using UnityEngine;

public class TrapTile : MonoBehaviour
{
    [Header("Trap Settings")]
    public float trapDamage = 15f;
    public float triggerCooldown = 1.5f; // 한 번 발동 후 다시 발동하기까지의 대기 시간

    // ★ 추가: 가시 연출 설정
    [Header("Spike Animation")]
    [Tooltip("obstacle 하위에 넣은 가시 오브젝트. 이 오브젝트의 로컬 Y만 움직입니다.")]
    public Transform spikeObject;
    public float spikeRaisedY = 0f;      // 데미지 주는 순간(튀어나옴)
    public float spikeLoweredY = -1.7f;  // 숨어있을 때(바닥 아래)
    [Tooltip("빠르게 튀어나오는 시간(초). 내려가는 시간은 쿨타임에서 자동 계산됩니다.")]
    public float raiseDuration = 0.08f;

    private float lastTriggerTime = -999f;
    private bool isFilled = false;

    // ★ 추가: 가시가 올라가는 중(true) / 내려가는 중(false)
    private bool spikeGoingUp = false;

    // ★ 추가: 시작 시 가시를 내려간 상태로 초기화
    private void Awake()
    {
        if (spikeObject != null)
        {
            Vector3 lp = spikeObject.localPosition;
            spikeObject.localPosition = new Vector3(lp.x, spikeLoweredY, lp.z);
        }
    }

    // ★ 추가: 가시 위/아래 이동 처리 (PushableObject와 동일한 Update 기반 방식)
    private void Update()
    {
        if (spikeObject == null) return;

        // 올릴 땐 빠르게, 내릴 땐 쿨타임에 맞춰 천천히
        float targetY = spikeGoingUp ? spikeRaisedY : spikeLoweredY;
        float duration = spikeGoingUp
            ? raiseDuration
            : Mathf.Max(0.01f, triggerCooldown - raiseDuration); // 쿨타임이 끝날 즈음 완전히 내려오도록
        float speed = Mathf.Abs(spikeRaisedY - spikeLoweredY) / duration;

        Vector3 lp = spikeObject.localPosition;
        float newY = Mathf.MoveTowards(lp.y, targetY, speed * Time.deltaTime);
        spikeObject.localPosition = new Vector3(lp.x, newY, lp.z);

        // 다 올라왔으면 곧바로 '천천히 내려가기' 단계로 전환
        if (spikeGoingUp && Mathf.Approximately(newY, spikeRaisedY))
        {
            spikeGoingUp = false;
        }
    }

    // Enter 대신 Stay를 사용하여, 함정 위에 머무르는 동안 쿨타임마다 계속 데미지를 줍니다.
    private void OnTriggerStay(Collider other)
    {
        if (isFilled) return;

        // 쿨타임 중이면 작동하지 않음
        if (Time.time < lastTriggerTime + triggerCooldown) return;

        bool isTriggered = false;

        // 1. 플레이어가 밟고 있는지 확인
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamage(trapDamage, this.transform);
            isTriggered = true;
        }

        // 2. 몬스터가 밟고 있는지 확인 (플레이어가 아니었다면)
        if (!isTriggered)
        {
            EnemyController enemy = other.GetComponent<EnemyController>();
            if (enemy != null)
            {
                enemy.TakeDamage(trapDamage);
                isTriggered = true;
            }
        }

        // 누군가 함정을 밟아서 발동되었다면 쿨타임 갱신 및 연출 실행
        if (isTriggered)
        {
            lastTriggerTime = Time.time;
            PlayTrapEffect();
        }
    }

    // ★ 변경: 데미지 주는 순간 가시를 빠르게 올림 (이후 Update에서 쿨타임 동안 천천히 내려감)
    private void PlayTrapEffect()
    {
        spikeGoingUp = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PushableObject>() != null)
        {
            isFilled = true;
            Debug.Log("함정이 밀려온 오브젝트로 메워졌습니다!");
            // ★ 추가: 메워지면 가시를 완전히 내려서 더 이상 튀어나오지 않게 함
            spikeGoingUp = false;
        }
    }
}