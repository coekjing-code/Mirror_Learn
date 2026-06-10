using UnityEngine;
using Mirror;
using UnityEngine.Serialization;

public class PlayerMotionCore : MonoBehaviour
{
    [Header("地面检测")]
    [FormerlySerializedAs("groundCheck")]
    public Transform groundCheckPoint;
    [FormerlySerializedAs("groundLayer")]
    public LayerMask groundLayerMask;

    [Header("角色设置")]
    public float rotationSpeed = 12f;

    public MotionData MotionData { get; private set; }
    public CharacterController CharCtrl { get; private set; }
    public float DefaultControllerHeight { get; private set; }
    public Vector3 DefaultControllerCenter { get; private set; }

    public BaseMotionState CurrentState { get; private set; }
    public IdleState IdleState { get; private set; }
    public WalkState WalkState { get; private set; }
    public RunState RunState { get; private set; }
    public JumpState JumpState { get; private set; }
    public CrouchState CrouchState { get; private set; }
    public PlayerMotionState MotionState { get; private set; } = PlayerMotionState.Idle;
    public float CurrentHorizontalSpeed { get; private set; }

    private Vector3 _targetMoveDirWorld;
    private NetworkBehaviour _networkBehaviour;
    private float _jumpBufferTimer;
    private float _lastGroundedTime;

    private void Awake()
    {
        MotionData = new MotionData();
        CharCtrl = GetComponent<CharacterController>();
        DisableOverlappingCollider();
        DefaultControllerHeight = CharCtrl.height;
        DefaultControllerCenter = CharCtrl.center;
        _networkBehaviour = GetComponent<NetworkBehaviour>();
        groundCheckPoint ??= transform.Find("GroundCheck");

        IdleState = new IdleState(this);
        WalkState = new WalkState(this);
        RunState = new RunState(this);
        JumpState = new JumpState(this);
        CrouchState = new CrouchState(this);
    }

    private void Start()
    {
        SwitchState(IdleState);
    }

    private void Update()
    {
        if (_networkBehaviour != null && !_networkBehaviour.isLocalPlayer) return;

        GetInput();
        CheckGround();
        CalculateWorldMoveDirection();

        CurrentState?.CheckSwitchState();

        if (MotionData.MoveInput.magnitude > 0.1f)
        {
            RotateTowardsMovementTarget();
            Move(_targetMoveDirWorld, GetCurrentMoveSpeed());
        }
        else
        {
            CurrentHorizontalSpeed = 0f;
            if (MotionData.AimInput)
                RotateTowardsCameraForward();
        }

        CurrentState?.UpdateState();
        ApplyGravity();
        TickJumpBuffer();
    }

    private void GetInput()
    {
        MotionData.MoveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );
        MotionData.JumpInput = Input.GetKeyDown(KeyCode.Space);
        MotionData.CrouchInput = Input.GetKey(KeyCode.LeftControl);
        MotionData.RunInput = Input.GetKey(KeyCode.LeftShift);
        MotionData.AimInput = Input.GetMouseButton(1);
        MotionData.IsAiming = MotionData.AimInput;
        MotionData.MouseLookX = Input.GetAxisRaw("Mouse X");

        if (MotionData.JumpInput)
            _jumpBufferTimer = MotionData.JumpBufferTime;
    }

    private void CalculateWorldMoveDirection()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            _targetMoveDirWorld = transform.forward * MotionData.MoveInput.y + transform.right * MotionData.MoveInput.x;
            _targetMoveDirWorld.Normalize();
            return;
        }

        Vector3 camForward = cam.transform.forward;
        Vector3 camRight = cam.transform.right;
        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();

        _targetMoveDirWorld = (camForward * MotionData.MoveInput.y + camRight * MotionData.MoveInput.x).normalized;
    }

    private void RotateTowardsMovementTarget()
    {
        if (MotionData.AimInput)
        {
            RotateTowardsCameraForward();
            return;
        }

        RotateTowardsMoveDirection();
    }

    private void RotateTowardsMoveDirection()
    {
        if (_targetMoveDirWorld.magnitude < 0.1f) return;

        Quaternion targetRot = Quaternion.LookRotation(_targetMoveDirWorld);
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            targetRot,
            rotationSpeed * Time.deltaTime
        );
    }

    private void RotateTowardsCameraForward()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 aimForward = cam.transform.forward;
        aimForward.y = 0f;
        if (aimForward.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(aimForward.normalized);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }

    public Vector3 GetWorldMoveDirection() => _targetMoveDirWorld;
    public Vector2 GetMoveInput() => MotionData.MoveInput.sqrMagnitude > 1f ? MotionData.MoveInput.normalized : MotionData.MoveInput;
    public bool HasBufferedJump => _jumpBufferTimer > 0f;
    public bool CanJump => MotionData.IsGrounded || Time.time - _lastGroundedTime <= MotionData.CoyoteTime;

    public void ConsumeJumpBuffer()
    {
        _jumpBufferTimer = 0f;
        MotionData.JumpInput = false;
    }

    public void StartJump()
    {
        ConsumeJumpBuffer();
        MotionData.IsGrounded = false;
        MotionData.Velocity.y = Mathf.Sqrt(MotionData.JumpForce * -2f * MotionData.Gravity);
    }

    public void ResetMotion()
    {
        MotionData.Velocity = Vector3.zero;
        MotionData.MoveInput = Vector2.zero;
        MotionData.JumpInput = false;
        MotionData.RunInput = false;
        MotionData.CrouchInput = false;
        MotionData.AimInput = false;
        MotionData.IsAiming = false;
        CurrentHorizontalSpeed = 0f;
        ConsumeJumpBuffer();
        SwitchState(IdleState);
    }

    public float GetPredictedMoveSpeed() => GetCurrentMoveSpeed();

    public void Move(Vector3 worldDirection, float speed)
    {
        if (worldDirection.sqrMagnitude < 0.0001f || speed <= 0f) return;
        if (CharCtrl == null || !CharCtrl.enabled) return;

        Vector3 horizontalMove = worldDirection.normalized * speed * Time.deltaTime;
        CurrentHorizontalSpeed = speed;
        CharCtrl.Move(horizontalMove);
    }

    private float GetCurrentMoveSpeed()
    {
        if (MotionData.CrouchInput && MotionData.IsGrounded)
            return MotionData.CrouchSpeed;
        if (MotionData.AimInput)
            return MotionData.WalkSpeed;
        if (MotionData.RunInput)
            return MotionData.RunSpeed;
        if (CurrentState is JumpState)
            return MotionData.RunSpeed * 0.7f;
        return MotionData.WalkSpeed;
    }

    public float GetMoveSpeed(PlayerMotionState motionState, bool runInput, bool crouchInput)
    {
        if (crouchInput || motionState == PlayerMotionState.Crouch)
            return MotionData.CrouchSpeed;
        if (runInput || motionState == PlayerMotionState.Run)
            return MotionData.RunSpeed;
        if (motionState == PlayerMotionState.Jump)
            return MotionData.RunSpeed * 0.7f;
        if (motionState == PlayerMotionState.Idle)
            return 0f;

        return MotionData.WalkSpeed;
    }

    public float GetMoveSpeed(PlayerMotionState motionState, bool runInput, bool crouchInput, bool aimInput)
    {
        if (aimInput && !crouchInput)
            return MotionData.WalkSpeed;

        return GetMoveSpeed(motionState, runInput, crouchInput);
    }

    public void SimulateServerInput(Vector3 worldDirection, float speed, float deltaTime, Quaternion rotation, PlayerMotionState motionState, bool jumpInput)
    {
        CheckGround();
        if (jumpInput && MotionData.IsGrounded)
        {
            MotionData.IsGrounded = false;
            MotionData.Velocity.y = Mathf.Sqrt(MotionData.JumpForce * -2f * MotionData.Gravity);
        }

        MotionState = motionState;
        CurrentHorizontalSpeed = speed;
        transform.rotation = rotation;

        if (worldDirection.sqrMagnitude > 0.0001f && speed > 0f)
        {
            Vector3 horizontalMove = worldDirection.normalized * speed * deltaTime;
            CharCtrl.Move(horizontalMove);
        }

        if (MotionData.IsGrounded && MotionData.Velocity.y < 0f)
            MotionData.Velocity.y = -1f;

        MotionData.Velocity.y += MotionData.Gravity * deltaTime;
        CharCtrl.Move(MotionData.Velocity * deltaTime);
    }

    private void CheckGround()
    {
        if (groundCheckPoint == null)
        {
            MotionData.IsGrounded = CharCtrl.isGrounded;
            if (MotionData.IsGrounded)
                _lastGroundedTime = Time.time;
            return;
        }

        if (MotionData.Velocity.y > 0.05f)
        {
            MotionData.IsGrounded = false;
            return;
        }

        bool sphereGrounded = groundLayerMask.value != 0
            ? Physics.CheckSphere(groundCheckPoint.position, MotionData.GroundCheckRadius, groundLayerMask, QueryTriggerInteraction.Ignore)
            : Physics.CheckSphere(groundCheckPoint.position, MotionData.GroundCheckRadius, ~0, QueryTriggerInteraction.Ignore);

        MotionData.IsGrounded = CharCtrl.isGrounded || sphereGrounded;
        if (MotionData.IsGrounded)
            _lastGroundedTime = Time.time;

        if (MotionData.IsGrounded && MotionData.Velocity.y < 0)
            MotionData.Velocity.y = -1f;
    }

    private void ApplyGravity()
    {
        if (CharCtrl == null || !CharCtrl.enabled) return;

        MotionData.Velocity.y += MotionData.Gravity * Time.deltaTime;
        CharCtrl.Move(MotionData.Velocity * Time.deltaTime);
    }

    private void TickJumpBuffer()
    {
        if (_jumpBufferTimer <= 0f) return;

        _jumpBufferTimer -= Time.deltaTime;
        if (_jumpBufferTimer <= 0f)
            MotionData.JumpInput = false;
    }

    public void SwitchState(BaseMotionState newState)
    {
        if (CurrentState == newState) return;

        CurrentState?.ExitState();
        CurrentState = newState;
        MotionState = GetMotionState(newState);
        CurrentState.EnterState();
    }

    private PlayerMotionState GetMotionState(BaseMotionState state)
    {
        if (state is WalkState) return PlayerMotionState.Walk;
        if (state is RunState) return PlayerMotionState.Run;
        if (state is JumpState) return PlayerMotionState.Jump;
        if (state is CrouchState) return PlayerMotionState.Crouch;
        return PlayerMotionState.Idle;
    }

    private void DisableOverlappingCollider()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            if (collider is CharacterController) continue;
            collider.enabled = false;
        }
    }
}
