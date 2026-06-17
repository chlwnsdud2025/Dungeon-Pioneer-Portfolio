using NUnit.Framework.Interfaces;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    // [핵심] 상태 머신
    public PlayerStateMachine stateMachine { get; private set; }

    [Header("Grid System")]
    public float tileSize = 1f;

    [Header("Movement System")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;

    [Header("Input System (Double Tap)")]
    public float doubleTapThreshold = 0.5f;
    private float lastMovePressTime = 0f;
    private bool isMoveKeyPressed = false;

    [Header("Jump & Gravity Settings")]
    public float jumpForce = 5f;        // 점프 뛰는 힘 (높이)
    public float gravity = -9.81f;      // 기본 중력
    public float gravityMultiplier = 2f;// 중력 가속도 배수
    [HideInInspector] public float verticalVelocity;

    [HideInInspector] public bool isRunning = false;
    [HideInInspector] public Vector2 moveInput;

    [Header("Components")]
    public Animator animator;
    public CharacterController characterController;

    [Header("Camera")]
    public Transform cameraTransform;

    private PlayerInputActions controls;

    private void Awake()
    {
        controls = new PlayerInputActions();

        stateMachine = new PlayerStateMachine();
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();


        stateMachine.AddState(new IdleState_Player(this, stateMachine));

        stateMachine.AddState(new WalkState_Player(this, stateMachine));

        stateMachine.AddState(new RunState_Player(this, stateMachine));
        stateMachine.AddState(new JumpState_Player(this, stateMachine));

        controls.PlayerControll.Jump.performed += ctx => stateMachine.CurrentState.OnJumpInput();

        //controls.Player.Interact.performed += ctx => stateMachine.CurrentState.OnInteractInput();
    }

    private void Start()
    {
        stateMachine.Initialize<IdleState_Player>();
    }

    private void Update()
    {
        HandleMovementInput();
        stateMachine.Update();
    }

    private void FixedUpdate()
    {
        stateMachine.FixedUpdate();
    }

    private void HandleMovementInput()
    {
        Vector2 currentMoveInput = controls.PlayerControll.Movement.ReadValue<Vector2>();

        if (currentMoveInput != Vector2.zero && !isMoveKeyPressed)
        {
            isMoveKeyPressed = true;

            if (Time.time - lastMovePressTime <= doubleTapThreshold)
            {
                isRunning = true;
            }
            else
            {
                isRunning = false;
            }
            lastMovePressTime = Time.time;
        }
        else if (currentMoveInput == Vector2.zero && isMoveKeyPressed)
        {
            isMoveKeyPressed = false;
            isRunning = false;
        }

        moveInput = currentMoveInput;
    }

    public void MoveCharacter(Vector3 motion)
    {
        // 1. 바닥에 닿아있을 때 수직 속도 초기화 (땅에 확실히 붙여주기 위해 -2f 적용)
        if (characterController.isGrounded)
        {
            if (verticalVelocity < 0.0f)
            {
                verticalVelocity = -2f;
            }
        }
        else
        {
            // 2. 공중에 있을 때는 중력을 계속 누적하여 낙하시킴
            verticalVelocity += gravity * gravityMultiplier * Time.deltaTime;
        }

        // 3. 인자로 받은 수평 이동 벡터(motion)에 수직 이동(verticalVelocity)을 합침
        motion.y = verticalVelocity * Time.deltaTime;

        // 4. 최종 이동 실행
        characterController.Move(motion);
    }
    // 현재 플레이어가 밟고 있는 타일의 좌표를 반환하는 함수
    public Vector2Int GetCurrentGridPosition()
    {
        int x = Mathf.RoundToInt(transform.position.x / tileSize);
        int z = Mathf.RoundToInt(transform.position.z / tileSize);
        return new Vector2Int(x, z);
    }

    private void OnEnable() { controls.Enable(); }
    private void OnDisable() { controls.Disable(); }
}