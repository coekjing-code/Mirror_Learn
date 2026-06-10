using UnityEngine;
using Mirror;

public class PlayerController : NetworkBehaviour
{
    [Header("核心模块引用")]
    public PlayerMotionCore motionCore;
    public PlayerAnimCtrl animCtrl;
    public CombatSystem combatSystem;
    public LagCompensationTarget lagCompensationTarget;
    public Health health;

    [SyncVar] private Vector3 _serverPos;
    [SyncVar] private Quaternion _serverRot;
    [SyncVar] private PlayerMotionState _serverMotionState;
    [SyncVar] private bool _hasServerState;
    [SyncVar] private int _currentHealth;
    [SyncVar] private bool _isDead;
    [SyncVar] private bool _serverIsAiming;
    [SyncVar] private Vector2 _serverMoveInput;
    [SyncVar] private float _serverAimTurnInput;
    [SyncVar] private double _respawnTime;
    [SyncVar] private int _teamId;
    [SyncVar] public string playerName;

    public static PlayerController LocalPlayerInstance;

    private const float SyncInterval = 1f / 30f;
    private const float ReconcileDistance = 1.5f;
    private const float MaxServerInputDelta = 0.1f;

    private float _nextSyncTime;
    private double _lastServerInputTime;
    private float _lastHealthFeedbackValue = -1f;

    public PlayerMotionState RemoteMotionState => _serverMotionState;
    public Vector3 ServerPosition => _serverPos;
    public Quaternion ServerRotation => _serverRot;
    public float PredictionError => Vector3.Distance(transform.position, _serverPos);
    public bool HasServerState => _hasServerState || isServer;
    public int CurrentHealth => _currentHealth;
    public bool IsDead => _isDead;
    public bool RemoteIsAiming => _serverIsAiming;
    public Vector2 RemoteMoveInput => _serverMoveInput;
    public float RemoteAimTurnInput => _serverAimTurnInput;
    public float RespawnRemaining => _isDead ? Mathf.Max(0f, (float)(_respawnTime - NetworkTime.time)) : 0f;
    public int TeamId => _teamId;

    public int maxHealth = 100;
    public float respawnDelay = 3f;

    private void Awake()
    {
        motionCore ??= GetComponent<PlayerMotionCore>();
        animCtrl ??= GetComponent<PlayerAnimCtrl>();
        combatSystem ??= GetComponent<CombatSystem>();
        health ??= GetComponent<Health>();
        health ??= gameObject.AddComponent<Health>();
        lagCompensationTarget ??= GetComponent<LagCompensationTarget>();
        lagCompensationTarget ??= gameObject.AddComponent<LagCompensationTarget>();
    }

    #region 网络生命周期
    public override void OnStartServer()
    {
        base.OnStartServer();
        _serverPos = transform.position;
        _serverRot = transform.rotation;
        _hasServerState = true;
        ServerResetHealth();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        LocalPlayerInstance = this;

        // 本地玩家初始化相机
        TPSFollowCam cam = Camera.main != null ? Camera.main.GetComponent<TPSFollowCam>() : null;
        if (cam != null)
        {
            cam.SetTarget(this);
        }

        CmdSetPlayerName("Player" + Random.Range(100, 999));

        Cursor.lockState = CursorLockMode.Locked;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        Cursor.lockState = CursorLockMode.None;
    }
    #endregion

    private void Update()
    {
        if (isLocalPlayer)
            UpdateDamageFeedback();

        if (_isDead) return;
        if (isServer && connectionToClient == null) return;

        if (isLocalPlayer)
        {
            ReconcileLocalPlayer();

            if (Time.time >= _nextSyncTime)
            {
                _nextSyncTime = Time.time + SyncInterval;
                SendMovementToServer();
            }
        }
        else
        {
            // 远程玩家：平滑同步位置和旋转
            SmoothSyncRemotePlayer();
        }
    }

    #region 网络同步逻辑
    [Command]
    private void CmdSetPlayerName(string newName)
    {
        playerName = newName;
    }

    [Server]
    public void ServerTakeDamage(int amount, NetworkConnectionToClient attacker)
    {
        if (MatchManager.Instance != null && !MatchManager.Instance.CanDealDamage()) return;
        if (MatchManager.Instance != null && MatchManager.Instance.IsFriendlyFire(attacker, this)) return;
        if (_isDead || amount <= 0) return;

        _currentHealth = Mathf.Max(0, _currentHealth - amount);
        if (_currentHealth > 0) return;

        ServerDie(attacker);
    }

    [Server]
    private void ServerDie(NetworkConnectionToClient attacker)
    {
        _isDead = true;
        _serverMotionState = PlayerMotionState.Idle;
        _serverMoveInput = Vector2.zero;
        _serverIsAiming = false;
        _serverAimTurnInput = 0f;
        _respawnTime = NetworkTime.time + respawnDelay;
        RpcPlayDieAnimation();
        ApplyGameplayEnabledOnServer(false);
        RpcSetGameplayEnabled(false);
        MatchManager.Instance?.RegisterKill(attacker, connectionToClient);

        if (MatchManager.Instance == null || MatchManager.Instance.IsPlaying)
            Invoke(nameof(ServerRespawn), respawnDelay);
    }

    [Server]
    public void ServerSetTeam(int teamId)
    {
        _teamId = teamId;
    }

    [Server]
    private void ServerRespawn()
    {
        if (MatchManager.Instance != null && !MatchManager.Instance.IsPlaying) return;

        ServerResetHealth();

        Transform spawnPoint = MatchManager.Instance != null ? MatchManager.Instance.GetSpawnPoint() : null;
        if (spawnPoint != null)
            ServerTeleportTo(spawnPoint.position, spawnPoint.rotation);

        _serverPos = transform.position;
        _serverRot = transform.rotation;
        _hasServerState = true;
        ApplyGameplayEnabledOnServer(true);
        RpcSetGameplayEnabled(true);
    }

    [Server]
    public void ServerRespawnForMatch()
    {
        CancelInvoke(nameof(ServerRespawn));
        ServerResetHealth();

        Transform spawnPoint = MatchManager.Instance != null ? MatchManager.Instance.GetSpawnPoint() : null;
        if (spawnPoint != null)
            ServerTeleportTo(spawnPoint.position, spawnPoint.rotation);

        _serverPos = transform.position;
        _serverRot = transform.rotation;
        _hasServerState = true;
    }

    [Server]
    public void ServerUpdateBotState(Vector3 position, Quaternion rotation, PlayerMotionState motionState, Vector2 moveInput)
    {
        _serverPos = position;
        _serverRot = rotation;
        _serverMotionState = motionState;
        _serverMoveInput = moveInput;
        _serverIsAiming = false;
        _serverAimTurnInput = 0f;
        _hasServerState = true;
    }

    [Server]
    public void ServerSetGameplayEnabled(bool enabled)
    {
        if (connectionToClient == null)
            return;

        ApplyGameplayEnabledOnServer(enabled);
        RpcSetGameplayEnabled(enabled);
    }

    [Server]
    private void ServerResetHealth()
    {
        _currentHealth = maxHealth;
        _isDead = false;
        _respawnTime = 0;
    }

    [Server]
    private void ApplyGameplayEnabledOnServer(bool enabled)
    {
        if (motionCore != null)
            motionCore.enabled = enabled;

        CharacterController characterController = GetComponent<CharacterController>();
        if (characterController != null)
            characterController.enabled = enabled;
    }

    [Server]
    private void ServerTeleportTo(Vector3 position, Quaternion rotation)
    {
        CharacterController characterController = GetComponent<CharacterController>();
        bool controllerWasEnabled = characterController != null && characterController.enabled;
        if (characterController != null)
            characterController.enabled = false;

        transform.SetPositionAndRotation(position, rotation);
        motionCore?.ResetMotion();

        if (characterController != null)
            characterController.enabled = controllerWasEnabled;

        _serverPos = position;
        _serverRot = rotation;
        _serverMotionState = PlayerMotionState.Idle;
        _serverMoveInput = Vector2.zero;
        _serverIsAiming = false;
        _serverAimTurnInput = 0f;
        _hasServerState = true;
        RpcTeleportTo(position, rotation);
    }

    [ClientRpc]
    private void RpcSetGameplayEnabled(bool enabled)
    {
        if (connectionToClient == null && isServer)
            return;

        if (combatSystem != null)
            combatSystem.enabled = enabled && isLocalPlayer;
        if (motionCore != null)
            motionCore.enabled = enabled && isLocalPlayer;

        CharacterController characterController = GetComponent<CharacterController>();
        if (characterController != null)
            characterController.enabled = enabled;
    }

    [ClientRpc]
    private void RpcTeleportTo(Vector3 position, Quaternion rotation)
    {
        CharacterController characterController = GetComponent<CharacterController>();
        bool controllerWasEnabled = characterController != null && characterController.enabled;
        if (characterController != null)
            characterController.enabled = false;

        transform.SetPositionAndRotation(position, rotation);
        motionCore?.ResetMotion();
        animCtrl?.ResetToIdle();

        if (characterController != null)
            characterController.enabled = controllerWasEnabled;
    }

    [ClientRpc]
    private void RpcPlayDieAnimation()
    {
        animCtrl ??= GetComponent<PlayerAnimCtrl>();
        animCtrl?.PlayDie();
    }

    private void UpdateDamageFeedback()
    {
        if (_lastHealthFeedbackValue < 0f)
        {
            _lastHealthFeedbackValue = _currentHealth;
            return;
        }

        if (_currentHealth < _lastHealthFeedbackValue)
            GameplayHUD.NotifyDamage();

        _lastHealthFeedbackValue = _currentHealth;
    }

    [Command(channel = Channels.Unreliable)]
    private void CmdSyncState(Vector3 pos, Quaternion rot, PlayerMotionState motionState, bool isAiming, Vector2 moveInput, float aimTurnInput)
    {
        _serverPos = pos;
        _serverRot = rot;
        _serverMotionState = motionState;
        _serverIsAiming = isAiming;
        _serverMoveInput = moveInput;
        _serverAimTurnInput = aimTurnInput;
        _hasServerState = true;
    }

    [Command(channel = Channels.Unreliable)]
    private void CmdSubmitMoveInput(Vector3 worldMoveDirection, bool runInput, bool crouchInput, bool jumpInput, bool isAiming, Vector2 moveInput, float aimTurnInput, Quaternion rotation, PlayerMotionState motionState)
    {
        if (motionCore == null) return;

        double now = NetworkTime.time;
        float deltaTime = _lastServerInputTime > 0
            ? (float)(now - _lastServerInputTime)
            : SyncInterval;

        _lastServerInputTime = now;
        deltaTime = Mathf.Clamp(deltaTime, 0f, MaxServerInputDelta);

        worldMoveDirection.y = 0f;
        if (worldMoveDirection.sqrMagnitude > 1f)
            worldMoveDirection.Normalize();

        float speed = motionCore.GetMoveSpeed(motionState, runInput, crouchInput, isAiming);
        motionCore.SimulateServerInput(worldMoveDirection, speed, deltaTime, rotation, motionState, jumpInput);

        _serverPos = transform.position;
        _serverRot = transform.rotation;
        _serverMotionState = motionState;
        _serverIsAiming = isAiming;
        _serverMoveInput = moveInput;
        _serverAimTurnInput = aimTurnInput;
        _hasServerState = true;
    }

    private void SmoothSyncRemotePlayer()
    {
        const float smoothSpeed = 12f;
        transform.position = Vector3.Lerp(transform.position, _serverPos, smoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Lerp(transform.rotation, _serverRot, smoothSpeed * Time.deltaTime);
    }

    private PlayerMotionState GetLocalMotionState()
    {
        return motionCore != null ? motionCore.MotionState : PlayerMotionState.Idle;
    }

    private void SendMovementToServer()
    {
        NetworkSyncMode syncMode = GetSyncMode();
        if (isServer)
        {
            _serverPos = transform.position;
            _serverRot = transform.rotation;
            _serverMotionState = GetLocalMotionState();
            _serverIsAiming = motionCore != null && motionCore.MotionData.AimInput;
            _serverMoveInput = motionCore != null ? motionCore.GetMoveInput() : Vector2.zero;
            _serverAimTurnInput = motionCore != null ? motionCore.MotionData.MouseLookX : 0f;
            _hasServerState = true;
            return;
        }

        if (syncMode == NetworkSyncMode.InputSync)
        {
            CmdSubmitMoveInput(
                motionCore != null ? motionCore.GetWorldMoveDirection() : Vector3.zero,
                motionCore != null && motionCore.MotionData.RunInput,
                motionCore != null && motionCore.MotionData.CrouchInput,
                ShouldSendJumpInput(),
                motionCore != null && motionCore.MotionData.AimInput,
                motionCore != null ? motionCore.GetMoveInput() : Vector2.zero,
                motionCore != null ? motionCore.MotionData.MouseLookX : 0f,
                transform.rotation,
                GetLocalMotionState()
            );
            return;
        }

        CmdSyncState(
            transform.position,
            transform.rotation,
            GetLocalMotionState(),
            motionCore != null && motionCore.MotionData.AimInput,
            motionCore != null ? motionCore.GetMoveInput() : Vector2.zero,
            motionCore != null ? motionCore.MotionData.MouseLookX : 0f
        );
    }

    private void ReconcileLocalPlayer()
    {
        if (GetSyncMode() != NetworkSyncMode.InputSync || isServer) return;
        if (!_hasServerState) return;

        float error = Vector3.Distance(transform.position, _serverPos);
        if (error > ReconcileDistance)
            transform.position = Vector3.Lerp(transform.position, _serverPos, 0.2f);
    }

    private NetworkSyncMode GetSyncMode()
    {
        return NetworkManager.singleton is CustomNetManager customNetManager
            ? customNetManager.syncMode
            : NetworkSyncMode.StateSync;
    }

    private bool ShouldSendJumpInput()
    {
        if (motionCore == null) return false;

        return motionCore.MotionData.JumpInput ||
               motionCore.HasBufferedJump ||
               (motionCore.MotionState == PlayerMotionState.Jump && motionCore.MotionData.Velocity.y > 0f);
    }
    #endregion
}
