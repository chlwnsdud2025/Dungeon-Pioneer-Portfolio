using UnityEngine;

public class StairTransitionState_Player : PlayerBaseState
{
    private static readonly int WalkHash = Animator.StringToHash("Walk_Player");
    private Vector3[] path;
    private int nextPoint;
    private float speed;

    public bool ReachedFoot => nextPoint >= 1;
    public bool IsFinished { get; private set; }

    public StairTransitionState_Player(PlayerController player, PlayerStateMachine stateMachine)
        : base(player, stateMachine) { }

    public void Begin(Vector3[] points, float movementSpeed)
    {
        path = points;
        nextPoint = 0;
        speed = Mathf.Max(0.01f, movementSpeed);
        IsFinished = points == null || points.Length == 0;
        stateMachine.ChangeState<StairTransitionState_Player>();
    }

    public override void Enter()
    {
        player.isInvincible = true;
        player.moveInput = Vector2.zero;
        player.verticalVelocity = 0f;
        if (player.characterController != null) player.characterController.enabled = false;
        player.animator.CrossFadeInFixedTime(WalkHash, 0.1f);
    }

    public override void Update()
    {
        if (IsFinished) return;
        Vector3 position = player.transform.position;
        Vector3 destination = path[nextPoint];
        Vector3 delta = destination - position;
        if (delta.sqrMagnitude > 0.0001f)
        {
            Vector3 heading = new Vector3(delta.x, 0f, delta.z);
            if (heading.sqrMagnitude > 0.0001f)
                player.transform.rotation = Quaternion.Slerp(player.transform.rotation,
                    Quaternion.LookRotation(heading), 15f * Time.deltaTime);
        }
        player.transform.position = Vector3.MoveTowards(position, destination, speed * Time.deltaTime);
        if ((player.transform.position - destination).sqrMagnitude < 0.0001f)
        {
            player.transform.position = destination;
            nextPoint++;
            IsFinished = nextPoint >= path.Length;
        }
    }

    public override void Exit()
    {
        if (player.characterController != null) player.characterController.enabled = true;
        player.isInvincible = false;
    }

    public override void OnAttackInput() { }
    public override void OnGuardInput() { }
    public override void OnJumpInput() { }
    public override void OnInteractInput() { }
}
