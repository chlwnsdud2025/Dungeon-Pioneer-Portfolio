using UnityEngine;

public class TrapTile : MonoBehaviour
{
    [Header("Trap Settings")]
    public float trapDamage = 15f;
    public float triggerCooldown = 1.5f; // 한 번 발동 후 다시 발동하기까지의 대기 시간

    private float lastTriggerTime = -999f;

    // Enter 대신 Stay를 사용하여, 함정 위에 머무르는 동안 쿨타임마다 계속 데미지를 줍니다.
    private void OnTriggerStay(Collider other)
    {
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

    private void PlayTrapEffect()
    {
        Debug.Log("함정");
        // TODO: 가시가 튀어나오는 애니메이션이나 소리(AudioSource) 추가
    }
}