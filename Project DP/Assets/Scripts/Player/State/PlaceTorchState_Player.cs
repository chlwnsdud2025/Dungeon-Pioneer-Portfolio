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

        // 개수 차감은 Inventory.UseItem이 담당한다(다중 설치여도 소모는 1개). 여기서는 설치와 모션만.

        // 1. 캐릭터를 상호작용한 메인 벽 쪽으로 회전
        Vector3 lookDir = -player.focusedHitNormal;
        lookDir.y = 0f;
        if (lookDir != Vector3.zero)
            player.transform.rotation = Quaternion.LookRotation(lookDir);

        // ★ 2. 다중 설치(Multi-Torch) 모드
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

        // 3. 모션 재생
        player.animator.CrossFadeInFixedTime(interactAnimHash, 0.1f);

        // 재고는 Inventory.UseItem이 이 직후에 1개 줄이므로, 여기서 찍는 값은 '차감 전' 개수다.
        Debug.Log($"<color=orange>횃불 설치! 남은 개수: {player.TorchStock - 1}</color>");
    }

    // 횃불의 위치를 계산하고 생성하는 공통 함수
    private void SpawnTorchOnWall(Collider wallCollider, Vector3 hitNormal)
    {
        // ★ 이 '면'에 이미 횃불이 설치됐으면 중복 설치하지 않는다. (벽 하나라도 면마다 따로 관리)
        WallObject wall = wallCollider.GetComponentInParent<WallObject>();
        if (wall != null && wall.HasTorchOnFace(hitNormal)) return;

        // ★ 함정 벽이면 횃불을 붙이지 않고 함정만 발동시킨다.
        //   (Inventory.UseItem이 어차피 1개를 차감하므로 '소모 + 미설치'가 성립)
        WallTrap wallTrap = wallCollider.GetComponentInParent<WallTrap>();
        if (wallTrap != null)
        {
            wallTrap.Activate(hitNormal);
            return;
        }

        // ★ 위치·회전은 '면 키'(6방향으로 스냅한 노멀) 기준으로 TorchInstance.GetMountPose가 정한다.
        //   복원(DungeonManager.RestoreTorches)도 같은 함수를 쓰므로, 던전을 나갔다 와도 정확히 같은
        //   자리에 다시 선다. 벽면에 밀착·직립 (예전 손횃불 모델의 0.2f 띄우기/30° 기울임은 제거).
        //   그래도 거치대와 벽 사이에 틈이 보이면 여기가 아니라 프리팹 쪽 메시 위치로 맞추는 게 맞다.
        Vector3Int faceKey = WallObject.ToFaceKey(hitNormal);
        TorchInstance.GetMountPose(wallCollider, faceKey, out Vector3 spawnPosition, out Quaternion spawnRotation);

        // ★ 현재 활성화된 장소(층)의 자식으로 부모를 지정합니다. 그래야 이 장소를 나갈 때
        //   DungeonManager가 SetActive(false)로 비활성화하면 횃불도 함께 꺼지고, 다시 돌아오면
        //   함께 다시 켜집니다 (씬 루트에 그냥 생성하면 층을 나가도 계속 남아있게 됩니다).
        Transform parentLocation = DungeonManager.Instance != null
            ? DungeonManager.Instance.CurrentLocationInstance?.transform
            : null;
        GameObject spawnedTorch = Object.Instantiate(player.torchPrefab, spawnPosition, spawnRotation, parentLocation);

        // ★ 수명을 시작하고, 던전을 나갔다 들어와도 복원할 수 있도록 어느 벽의 어느 면인지 기록해둔다.
        //   TorchInstance는 프리팹에 붙어 있는 게 정상이지만, 없는 프리팹을 연결해도 동작하도록 폴백을 둔다.
        Transform wallTransform = wall != null ? wall.transform : wallCollider.transform;
        Vector3Int wallCell = GridSystem.Instance.WorldToCell(wallTransform.position);
        TorchInstance torchInstance = spawnedTorch.GetComponent<TorchInstance>();
        if (torchInstance == null) torchInstance = spawnedTorch.AddComponent<TorchInstance>();
        torchInstance.Initialize(wall, new Vector2Int(wallCell.x, wallCell.z), faceKey, torchInstance.Lifetime);

        // ★ 이 면에 횃불이 설치됨 → 다음부터 '이 면'에는 중복 설치 방지 (다른 면엔 여전히 설치 가능)
        if (wall != null) wall.MarkTorchPlaced(hitNormal);

        // ★ 추가: 새 미니맵 구조에 이 횃불을 등록.
        //   등록된 순간부터 미니맵에 아주 작은 점으로 표시되고, 주변이 원형으로 영구히 밝게 유지됩니다.
        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.RegisterTorch(spawnedTorch.transform);
        }
    }

    public override void Update()
    {
        // 점프 중에 설치했으면 중력과 점프 관성을 그대로 받으며 떨어집니다. (땅에서는 airMomentum이 0이라 제자리)
        player.MoveCharacter(player.airMomentum * Time.deltaTime);

        timer += Time.deltaTime;

        if (timer >= placeDuration)
        {
            if (!TryResumeAirborne()) stateMachine.ChangeState<IdleState_Player>();
        }
    }

    public override void OnInteractInput() { }
    public override void OnJumpInput() { }
    public override void OnAttackInput() { }
}