using UnityEngine;

public class PlaceTorchState_Player : PlayerBaseState
{
    private readonly int interactAnimHash = Animator.StringToHash("Interact_Torch");

    private float timer = 0f;
    private float placeDuration = 0.5f;

    public PlaceTorchState_Player(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

    public override void Enter()
    {
        timer = 0f;

        // 1. 횃불 개수는 1개만 차감
        player.torchCount--;

        // 2. 캐릭터를 상호작용한 메인 벽 쪽으로 회전
        Vector3 lookDir = -player.focusedHitNormal;
        lookDir.y = 0f;
        if (lookDir != Vector3.zero)
            player.transform.rotation = Quaternion.LookRotation(lookDir);

        // ★ 3. 다중 설치(Multi-Torch) 모드
        if (player.enableMultiTorch)
        {
            // (1) 상호작용한 벽의 중심 좌표 가져오기
            Collider interactedWall = player.focusedObject.GetComponent<Collider>();
            Vector3 wallCenter = interactedWall.bounds.center;

            // (2) 벽면(hitNormal) 방향으로 1칸 이동하여 '기준이 되는 빈 공간(바닥)'의 중심 좌표 구하기
            Vector3 floorCenter = wallCenter + (player.focusedHitNormal * player.tileSize);

            // (3) 그 빈 공간을 중심으로 4방향(동, 서, 남, 북) 검사
            Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

            foreach (Vector3 dir in directions)
            {
                // 검사할 주변 타일의 중심 좌표 (기준 바닥 + 방향 * 타일사이즈)
                Vector3 targetWallPos = floorCenter + (dir * player.tileSize);

                // 해당 좌표에 벽이 있는지 겹침 판정 (반경은 타일의 절반 크기인 0.4f 정도로 좁게 설정)
                Collider[] hitColliders = Physics.OverlapSphere(targetWallPos, player.tileSize * 0.4f, player.interactableLayer);

                foreach (Collider col in hitColliders)
                {
                    if (col.GetComponent<WallObject>() != null)
                    {
                        // 벽이 발견되었다면, 벽이 '기준 바닥'을 바라보는 방향은 우리가 검사한 방향(dir)의 반대(-dir)가 됩니다.
                        Vector3 normalTowardsFloor = -dir;

                        SpawnTorchOnWall(col, normalTowardsFloor);
                        break; // 해당 칸에서 벽을 하나 찾았으면 중복 방지를 위해 바로 다음 방향으로 넘어감
                    }
                }
            }
        }
        else
        {
            // 기능이 꺼져있다면 기존처럼 쳐다본 벽 1개에만 설치
            Collider wallCollider = player.focusedObject.GetComponent<Collider>();
            SpawnTorchOnWall(wallCollider, player.focusedHitNormal);
        }

        // 4. 모션 재생 및 제자리 고정
        player.animator.CrossFadeInFixedTime(interactAnimHash, 0.1f);
        player.MoveCharacter(Vector3.zero);

        Debug.Log($"<color=orange>다중 횃불 설치 완료! 남은 개수: {player.torchCount}</color>");
    }

    // 횃불의 위치를 계산하고 생성하는 공통 함수
    private void SpawnTorchOnWall(Collider wallCollider, Vector3 hitNormal)
    {
        // ★ 함정 벽이면 횃불을 붙이지 않고 함정만 발동시킨다.
        //   (횃불 개수는 Enter()에서 이미 1개 차감되었으므로 '소모 + 미설치'가 성립)
        WallTrap wallTrap = wallCollider.GetComponentInParent<WallTrap>();
        if (wallTrap != null)
        {
            wallTrap.Activate(hitNormal);
            return;
        }

        Vector3 wallCenter = wallCollider.bounds.center;
        Vector3 wallExtents = wallCollider.bounds.extents;

        Vector3 surfaceOffset = new Vector3(
            hitNormal.x * wallExtents.x,
            hitNormal.y * wallExtents.y,
            hitNormal.z * wallExtents.z
        );

        // 벽면에서 살짝 띄우기 (0.2f)
        Vector3 spawnPosition = wallCenter + surfaceOffset + (hitNormal * 0.2f);

        // 벽면을 향하게 한 뒤 비스듬하게 꺾기
        Quaternion baseRotation = Quaternion.LookRotation(hitNormal);
        Quaternion spawnRotation = baseRotation * Quaternion.Euler(30f, 0f, 0f);

        // ★ 현재 활성화된 장소(층)의 자식으로 부모를 지정합니다. 그래야 이 장소를 나갈 때
        //   WorldManager가 SetActive(false)로 비활성화하면 횃불도 함께 꺼지고, 다시 돌아오면
        //   함께 다시 켜집니다 (씬 루트에 그냥 생성하면 층을 나가도 계속 남아있게 됩니다).
        Transform parentLocation = WorldManager.Instance != null
            ? WorldManager.Instance.CurrentLocationInstance?.transform
            : null;
        GameObject spawnedTorch = Object.Instantiate(player.torchPrefab, spawnPosition, spawnRotation, parentLocation);

        // ★ 추가: 새 미니맵 구조에 이 횃불을 등록.
        //   등록된 순간부터 미니맵에 아주 작은 점으로 표시되고, 주변이 원형으로 영구히 밝게 유지됩니다.
        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.RegisterTorch(spawnedTorch.transform);
        }
    }

    public override void Update()
    {
        timer += Time.deltaTime;

        if (timer >= placeDuration)
        {
            stateMachine.ChangeState<IdleState_Player>();
        }
    }

    public override void OnInteractInput() { }
    public override void OnJumpInput() { }
    public override void OnAttackInput() { }
}