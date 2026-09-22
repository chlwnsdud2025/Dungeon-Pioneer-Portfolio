using System.Collections.Generic;
using UnityEngine;

public class RollingRockTrap : MonoBehaviour
{
    [Header("Rock")]
    [Tooltip("굴릴 돌 프리팹(RollingRock 컴포넌트 보유).")]
    public RollingRock rockPrefab;
    [Tooltip("돌이 굴러가는 속도")]
    public float rockSpeed = 6f;

    [Header("Spawn")]
    [Tooltip("돌 스폰 간격(초)")]
    public float spawnInterval = 2f;
    [Tooltip("첫 스폰까지의 추가 시작 딜레이(초). 여러 타일의 스폰 타이밍을 어긋나게 할 때 유용.")]
    public float startDelay = 0f;
    [Tooltip("돌이 나오는 위치(비우면 이 타일의 위치). 방향은 항상 타일의 z축 앞쪽입니다.")]
    public Transform spawnPoint;

    [Header("Object Pool")]
    [Tooltip("미리 만들어둘 돌 개수")]
    public int poolSize = 5;

    private readonly List<RollingRock> pool = new List<RollingRock>();
    private float timer;

    private void Awake()
    {
        if (rockPrefab == null)
        {
            Debug.LogError("[RollingRockTrap] rockPrefab이 비어있습니다. 돌 프리팹을 연결해주세요.", this);
            enabled = false;
            return;
        }

        // 풀 미리 생성 (이 타일의 자식으로 두어 장소를 나갈 때 함께 비활성화/정리되도록 함)
        for (int i = 0; i < poolSize; i++)
        {
            RollingRock rock = Instantiate(rockPrefab, transform);
            rock.gameObject.SetActive(false);
            pool.Add(rock);
        }

        timer = -startDelay; // 시작 딜레이만큼 첫 스폰을 늦춘다
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnRock();
        }
    }

    private void SpawnRock()
    {
        RollingRock rock = GetAvailableRock();
        if (rock == null) return; // 풀에 여유가 없으면 이번 스폰은 건너뜀

        Transform origin = spawnPoint != null ? spawnPoint : transform;
        Vector3 dir = transform.forward; // z축 앞쪽 (타일 회전으로 방향 조절)

        rock.transform.SetPositionAndRotation(origin.position, Quaternion.LookRotation(dir));
        rock.gameObject.SetActive(true);
        rock.Launch(dir, rockSpeed, this);
    }

    private RollingRock GetAvailableRock()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (!pool[i].gameObject.activeSelf) return pool[i];
        }
        return null; // (원하면 여기서 풀을 동적으로 늘리도록 확장 가능)
    }

    public void ReturnToPool(RollingRock rock)
    {
        rock.gameObject.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        // 굴러갈 방향/스폰 지점 시각화
        Transform origin = spawnPoint != null ? spawnPoint : transform;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin.position, origin.position + transform.forward * 3f);
        Gizmos.DrawSphere(origin.position, 0.15f);
    }
}
