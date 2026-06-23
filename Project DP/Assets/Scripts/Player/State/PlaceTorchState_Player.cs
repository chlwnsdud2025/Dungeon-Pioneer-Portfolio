using UnityEngine;

public class PlaceTorchState_Player : PlayerBaseState
{
    // 임시로 상자 여는 모션이나 범용 상호작용 모션을 사용합니다.
    private readonly int interactAnimHash = Animator.StringToHash("Interact_Torch");

    private float timer = 0f;
    private float placeDuration = 0.5f; // 설치 모션 길이

    public PlaceTorchState_Player(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

    public override void Enter()
    {
        timer = 0f;

        // 1. 횃불 개수 감소
        player.torchCount--;

        // 2. 캐릭터를 벽 쪽으로 자연스럽게 회전
        // (벽의 법선 방향(hitNormal)의 반대 방향이 벽면을 정면으로 바라보는 방향입니다)
        Vector3 lookDir = -player.focusedHitNormal;
        lookDir.y = 0f;
        if (lookDir != Vector3.zero)
            player.transform.rotation = Quaternion.LookRotation(lookDir);

        // 3. 횃불 설치 위치 계산 (해당 큐브의 표면 중앙에 맞추기) ★

        // 3-1. 벽 오브젝트의 콜라이더에서 '정중앙(center)'과 '크기의 절반(extents)' 정보를 가져옵니다.
        Collider wallCollider = player.focusedObject.GetComponent<Collider>();
        Vector3 wallCenter = wallCollider.bounds.center;
        Vector3 wallExtents = wallCollider.bounds.extents;

        // 3-2. 벽이 바라보는 방향(Normal) 쪽에 해당하는 면의 중심점을 구합니다.
        Vector3 surfaceOffset = new Vector3(
            player.focusedHitNormal.x * wallExtents.x,
            player.focusedHitNormal.y * wallExtents.y,
            player.focusedHitNormal.z * wallExtents.z
        );

        // 3-3. 벽 중앙 표면에서 횃불이 파묻히지 않게 수직 방향(Normal)으로 살짝(0.2f) 띄워줍니다.
        Vector3 spawnPosition = wallCenter + surfaceOffset + (player.focusedHitNormal * 0.2f);

        // ★ 수정된 부분: 횃불의 등짝이 벽면(Normal)을 향하게 한 뒤, 비스듬하게 기울입니다.
        Quaternion baseRotation = Quaternion.LookRotation(player.focusedHitNormal);

        // 로컬 X축을 기준으로 30도 기울입니다. 
        // (프리팹 모델의 기본 축에 따라 30 대신 -30, 혹은 X가 아닌 Z축을 변경해야 할 수도 있습니다)
        Quaternion spawnRotation = baseRotation * Quaternion.Euler(30f, 0f, 0f);

        // 4. 프리팹 생성 (벽면 정중앙 스냅 및 기울기 적용)
        Object.Instantiate(player.torchPrefab, spawnPosition, spawnRotation);

        // 5. 모션 재생 및 제자리 고정
        player.animator.CrossFadeInFixedTime(interactAnimHash, 0.1f);
        player.MoveCharacter(Vector3.zero);

        Debug.Log($"<color=orange>벽 중앙에 비스듬히 횃불 설치 완료! 남은 개수: {player.torchCount}</color>");
    }

    public override void Update()
    {
        timer += Time.deltaTime;

        if (timer >= placeDuration)
        {
            stateMachine.ChangeState<IdleState_Player>();
        }
    }

    // 설치 도중 캔슬 방지
    public override void OnInteractInput() { }
    public override void OnJumpInput() { }
    public override void OnAttackInput() { }
}