using Mirror;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerAnimCtrl : MonoBehaviour
{
    [Header("Animation")]
    [FormerlySerializedAs("animator")]
    public Animator anim;
    public PlayerMotionCore motionCore;
    public float crossFadeDuration = 0.12f;

    [Header("Aim IK")]
    public float lookAtWeight = 1f;
    public float bodyWeight = 0.2f;
    public float headWeight = 1f;
    public float eyesWeight = 0.8f;

    [Header("Layered Actions")]
    public string upperBodyLayerName = "UpperBody";
    public string lowerBodyLayerName = "LowerBody";
    public float idleTurnMouseThreshold = 0.08f;
    public float layerCrossFadeDuration = 0.05f;

    private readonly int idleHash = Animator.StringToHash("Idle");
    private readonly int walkHash = Animator.StringToHash("Walk");
    private readonly int runHash = Animator.StringToHash("Run");
    private readonly int jumpHash = Animator.StringToHash("Jump");
    private readonly int stopHash = Animator.StringToHash("Stop");
    private readonly int crouchWalkHash = Animator.StringToHash("CrouchWalk");
    private readonly int aimingHash = Animator.StringToHash("Aiming");
    private readonly int turnLeftHash = Animator.StringToHash("TurnLeft");
    private readonly int turnRightHash = Animator.StringToHash("TurnRight");
    private readonly int fireHash = Animator.StringToHash("Fire");
    private readonly int hitHash = Animator.StringToHash("Hit");
    private readonly int dieHash = Animator.StringToHash("Die");
    private readonly int reloadingHash = Animator.StringToHash("Reloading");
    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int horizontalHash = Animator.StringToHash("Horizontal");
    private readonly int verticalHash = Animator.StringToHash("Vertical");
    private readonly int isAimingHash = Animator.StringToHash("IsAiming");
    private readonly int isMovingHash = Animator.StringToHash("IsMoving");
    private readonly int isGroundedHash = Animator.StringToHash("IsGrounded");
    private readonly int motionStateHash = Animator.StringToHash("MotionState");

    private PlayerController _playerController;
    private PlayerMotionState _lastState = (PlayerMotionState)(-1);
    private bool _lastAiming;
    private int _upperBodyLayer = -1;
    private int _lowerBodyLayer = -1;

    private void Awake()
    {
        anim ??= GetComponentInChildren<Animator>();
        motionCore ??= GetComponent<PlayerMotionCore>();
        _playerController = GetComponent<PlayerController>();

        if (anim != null)
        {
            anim.applyRootMotion = false;
            CacheLayerIndices();
        }
    }

    private void Update()
    {
        if (_playerController == null) return;

        if (_playerController.isLocalPlayer)
            UpdateAnimation(GetLocalState(), IsLocalAiming(), GetLocalMoveInput(), GetLocalAimTurnInput());
        else
            UpdateAnimation(_playerController.RemoteMotionState, _playerController.RemoteIsAiming, _playerController.RemoteMoveInput, _playerController.RemoteAimTurnInput);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (anim == null || Camera.main == null || _playerController == null || !_playerController.isLocalPlayer) return;

        anim.SetLookAtWeight(lookAtWeight, bodyWeight, headWeight, eyesWeight);
        anim.SetLookAtPosition(Camera.main.transform.position + Camera.main.transform.forward * 10f);
    }

    private void PlayIdle() => CrossFadeIfExists(idleHash);

    public void PlayFire()
    {
        if (anim == null) return;

        CacheLayerIndices();
        PlayDirectlyIfExists(fireHash, _upperBodyLayer);
    }

    public void PlayHit()
    {
        if (anim == null) return;

        CacheLayerIndices();
        PlayDirectlyIfExists(hitHash, _upperBodyLayer);
    }

    public void PlayReloading()
    {
        if (anim == null) return;

        CacheLayerIndices();
        PlayDirectlyIfExists(reloadingHash, _upperBodyLayer);
    }

    public void PlayDie()
    {
        if (anim == null) return;

        PlayDirectlyIfExists(dieHash, 0);
    }

    public void ResetToIdle()
    {
        CancelInvoke(nameof(PlayIdle));
        _lastState = (PlayerMotionState)(-1);
        _lastAiming = false;
        CrossFadeIfExists(idleHash);
    }

    private void UpdateAnimation(PlayerMotionState state, bool isAiming, Vector2 moveInput, float aimTurnInput)
    {
        if (anim == null) return;

        UpdateAnimatorParameters(state, isAiming, moveInput);
        UpdateAimIdleTurn(state, isAiming, moveInput, aimTurnInput);

        if (isAiming && HasState(aimingHash))
        {
            if (!_lastAiming)
                CrossFadeIfExists(aimingHash);

            _lastAiming = true;
            _lastState = state;
            CancelInvoke(nameof(PlayIdle));
            return;
        }

        if (state == _lastState && _lastAiming == isAiming) return;

        PlayerMotionState previousState = _lastState;
        _lastState = state;
        _lastAiming = isAiming;
        CancelInvoke(nameof(PlayIdle));

        int targetHash;
        switch (state)
        {
            case PlayerMotionState.Walk:
                targetHash = walkHash;
                break;
            case PlayerMotionState.Run:
                targetHash = runHash;
                break;
            case PlayerMotionState.Jump:
                targetHash = jumpHash;
                break;
            case PlayerMotionState.Crouch:
                targetHash = HasState(crouchWalkHash) ? crouchWalkHash : walkHash;
                break;
            default:
                if (previousState != PlayerMotionState.Idle && HasState(stopHash))
                {
                    CrossFadeIfExists(stopHash);
                    Invoke(nameof(PlayIdle), 0.2f);
                    return;
                }

                targetHash = idleHash;
                break;
        }

        CrossFadeIfExists(targetHash);
    }

    private PlayerMotionState GetLocalState()
    {
        return motionCore != null ? motionCore.MotionState : PlayerMotionState.Idle;
    }

    private bool IsLocalAiming()
    {
        return motionCore != null && motionCore.MotionData.AimInput;
    }

    private Vector2 GetLocalMoveInput()
    {
        return motionCore != null ? motionCore.GetMoveInput() : Vector2.zero;
    }

    private float GetLocalAimTurnInput()
    {
        return motionCore != null ? motionCore.MotionData.MouseLookX : 0f;
    }

    private void UpdateAnimatorParameters(PlayerMotionState state, bool isAiming, Vector2 moveInput)
    {
        if (HasParameter(speedHash, AnimatorControllerParameterType.Float))
        {
            float speed = motionCore != null ? motionCore.CurrentHorizontalSpeed : (state == PlayerMotionState.Idle ? 0f : 1f);
            anim.SetFloat(speedHash, speed);
        }

        if (HasParameter(horizontalHash, AnimatorControllerParameterType.Float))
            anim.SetFloat(horizontalHash, moveInput.x, 0.08f, Time.deltaTime);

        if (HasParameter(verticalHash, AnimatorControllerParameterType.Float))
            anim.SetFloat(verticalHash, moveInput.y, 0.08f, Time.deltaTime);

        if (HasParameter(isAimingHash, AnimatorControllerParameterType.Bool))
            anim.SetBool(isAimingHash, isAiming);

        if (HasParameter(isMovingHash, AnimatorControllerParameterType.Bool))
            anim.SetBool(isMovingHash, state == PlayerMotionState.Walk || state == PlayerMotionState.Run || state == PlayerMotionState.Crouch);

        if (HasParameter(isGroundedHash, AnimatorControllerParameterType.Bool))
            anim.SetBool(isGroundedHash, motionCore == null || motionCore.MotionData.IsGrounded);

        if (HasParameter(motionStateHash, AnimatorControllerParameterType.Int))
            anim.SetInteger(motionStateHash, (int)state);
    }

    private void UpdateAimIdleTurn(PlayerMotionState state, bool isAiming, Vector2 moveInput, float aimTurnInput)
    {
        if (!isAiming || state != PlayerMotionState.Idle || moveInput.sqrMagnitude > 0.01f) return;
        if (Mathf.Abs(aimTurnInput) < idleTurnMouseThreshold) return;

        int targetHash = aimTurnInput < 0f ? turnLeftHash : turnRightHash;
        if (!CanReplayLayerState(targetHash, _lowerBodyLayer)) return;

        CrossFadeIfExists(targetHash, _lowerBodyLayer);
    }

    private bool CanReplayLayerState(int stateHash, int layerIndex)
    {
        if (anim == null || layerIndex < 0 || !HasState(stateHash, layerIndex)) return false;

        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(layerIndex);
        if (stateInfo.shortNameHash != stateHash) return true;

        return stateInfo.normalizedTime >= 0.95f;
    }

    private void CacheLayerIndices()
    {
        if (anim == null) return;

        _upperBodyLayer = anim.GetLayerIndex(upperBodyLayerName);
        _lowerBodyLayer = anim.GetLayerIndex(lowerBodyLayerName);
    }

    private bool HasState(int stateHash) => HasState(stateHash, 0);

    private bool HasState(int stateHash, int layerIndex)
    {
        return anim != null && layerIndex >= 0 && anim.HasState(layerIndex, stateHash);
    }

    private bool HasParameter(int parameterHash, AnimatorControllerParameterType parameterType)
    {
        if (anim == null) return false;

        foreach (AnimatorControllerParameter parameter in anim.parameters)
        {
            if (parameter.nameHash == parameterHash && parameter.type == parameterType)
                return true;
        }

        return false;
    }
    private void PlayDirectlyIfExists(int stateHash, int layerIndex)
    {
        if (HasState(stateHash, layerIndex))
            anim.Play(stateHash, layerIndex);
    }
    private void CrossFadeIfExists(int stateHash)
    {
        CrossFadeIfExists(stateHash, 0, crossFadeDuration);
    }

    private void CrossFadeIfExists(int stateHash, int layerIndex)
    {
        CrossFadeIfExists(stateHash, layerIndex, layerCrossFadeDuration);
    }

    private void CrossFadeIfExists(int stateHash, int layerIndex, float duration)
    {
        if (HasState(stateHash, layerIndex))
            anim.CrossFade(stateHash, duration, layerIndex);
    }
}
