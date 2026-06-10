using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class TrainingBotController : MonoBehaviour
{
    private enum NodeState
    {
        Success,
        Failure,
        Running
    }

    private abstract class BotNode
    {
        public abstract NodeState Tick();
    }

    private sealed class ActionNode : BotNode
    {
        private readonly System.Func<NodeState> _action;

        public ActionNode(System.Func<NodeState> action)
        {
            _action = action;
        }

        public override NodeState Tick() => _action();
    }

    private sealed class SequenceNode : BotNode
    {
        private readonly BotNode[] _children;

        public SequenceNode(params BotNode[] children)
        {
            _children = children;
        }

        public override NodeState Tick()
        {
            foreach (BotNode child in _children)
            {
                NodeState state = child.Tick();
                if (state != NodeState.Success)
                    return state;
            }

            return NodeState.Success;
        }
    }

    private sealed class SelectorNode : BotNode
    {
        private readonly BotNode[] _children;

        public SelectorNode(params BotNode[] children)
        {
            _children = children;
        }

        public override NodeState Tick()
        {
            foreach (BotNode child in _children)
            {
                NodeState state = child.Tick();
                if (state != NodeState.Failure)
                    return state;
            }

            return NodeState.Failure;
        }
    }

    [Header("Senses")]
    public float detectionRange = 34f;
    public float attackRange = 24f;
    public LayerMask lineOfSightMask = ~0;

    [Header("Movement")]
    public float patrolRadius = 18f;
    public float moveSpeed = 3.2f;
    public float strafeSpeed = 1.4f;
    public float turnSpeed = 8f;
    public float waypointReachDistance = 1.5f;

    [Header("Combat")]
    public float fireInterval = 0.45f;
    public float aimErrorDegrees = 4f;
    public float aimAngleToFire = 12f;
    public float reactionDelay = 0.35f;

    private PlayerController _player;
    private CombatSystem _combat;
    private CharacterController _controller;
    private BotNode _root;
    private PlayerController _target;
    private Vector3 _spawnPosition;
    private Vector3 _waypoint;
    private float _nextFireTime;
    private float _nextWaypointTime;
    private float _targetAcquiredTime;
    private float _nextStrafeSwitchTime;
    private int _strafeDirection = 1;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _combat = GetComponent<CombatSystem>();
        _controller = GetComponent<CharacterController>();
        _spawnPosition = transform.position;
        PickWaypoint();
        BuildBehaviorTree();
    }

    private void Update()
    {
        if (!NetworkServer.active) return;
        if (_player == null || _player.IsDead) return;

        _root.Tick();
        _player.ServerUpdateBotState(transform.position, transform.rotation, GetMotionState(), Vector2.zero);
    }

    public void ApplyDifficulty(BotDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BotDifficulty.Easy:
                detectionRange = 24f;
                attackRange = 18f;
                moveSpeed = 2.6f;
                fireInterval = 0.7f;
                aimErrorDegrees = 7f;
                aimAngleToFire = 18f;
                reactionDelay = 0.65f;
                break;
            case BotDifficulty.Hard:
                detectionRange = 42f;
                attackRange = 30f;
                moveSpeed = 3.8f;
                fireInterval = 0.28f;
                aimErrorDegrees = 2.2f;
                aimAngleToFire = 8f;
                reactionDelay = 0.2f;
                break;
            default:
                detectionRange = 34f;
                attackRange = 24f;
                moveSpeed = 3.2f;
                fireInterval = 0.45f;
                aimErrorDegrees = 4f;
                aimAngleToFire = 12f;
                reactionDelay = 0.35f;
                break;
        }
    }

    private void BuildBehaviorTree()
    {
        _root = new SelectorNode(
            new SequenceNode(
                new ActionNode(AcquireTarget),
                new ActionNode(HasLineOfSight),
                new ActionNode(IsInAttackRange),
                new ActionNode(AimAtTarget),
                new ActionNode(ShootTarget)
            ),
            new SequenceNode(
                new ActionNode(HasTarget),
                new ActionNode(ChaseTarget)
            ),
            new ActionNode(Patrol)
        );
    }

    private NodeState AcquireTarget()
    {
        PlayerController previousTarget = _target;
        _target = FindBestTarget();
        if (_target == null)
            return NodeState.Failure;

        if (_target != previousTarget)
            _targetAcquiredTime = Time.time;

        return NodeState.Success;
    }

    private NodeState HasTarget()
    {
        if (_target == null || _target.IsDead)
            return AcquireTarget();

        return NodeState.Success;
    }

    private NodeState HasLineOfSight()
    {
        if (_target == null) return NodeState.Failure;

        return CanSeeTarget(_target) ? NodeState.Success : NodeState.Failure;
    }

    private NodeState IsInAttackRange()
    {
        if (_target == null) return NodeState.Failure;

        return Vector3.Distance(transform.position, _target.transform.position) <= attackRange
            ? NodeState.Success
            : NodeState.Failure;
    }

    private NodeState AimAtTarget()
    {
        if (_target == null) return NodeState.Failure;

        Vector3 targetPoint = GetTargetPoint(_target);
        Vector3 toTarget = targetPoint - GetEyePosition();
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.01f)
            return NodeState.Success;

        Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        return NodeState.Success;
    }

    private NodeState ShootTarget()
    {
        if (_target == null) return NodeState.Failure;
        StrafeNearTarget();

        if (Time.time < _targetAcquiredTime + reactionDelay) return NodeState.Running;
        if (Time.time < _nextFireTime) return NodeState.Running;

        Vector3 targetPoint = GetTargetPoint(_target);
        Vector3 flatDirection = targetPoint - GetEyePosition();
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude > 0.01f)
        {
            float angle = Vector3.Angle(transform.forward, flatDirection.normalized);
            if (angle > aimAngleToFire)
                return NodeState.Running;
        }

        _nextFireTime = Time.time + fireInterval + Random.Range(0f, 0.15f);
        Vector3 noisyTarget = targetPoint + Random.insideUnitSphere * 0.45f;
        _combat?.ServerBotFire(noisyTarget, aimErrorDegrees);
        return NodeState.Success;
    }

    private NodeState ChaseTarget()
    {
        if (_target == null) return NodeState.Failure;

        MoveTowards(_target.transform.position);
        return NodeState.Running;
    }

    private NodeState Patrol()
    {
        if (Time.time >= _nextWaypointTime || Vector3.Distance(transform.position, _waypoint) <= waypointReachDistance)
            PickWaypoint();

        MoveTowards(_waypoint);
        return NodeState.Running;
    }

    private PlayerController FindBestTarget()
    {
        PlayerController best = null;
        float bestDistance = detectionRange;
        foreach (PlayerController candidate in FindObjectsOfType<PlayerController>())
        {
            if (candidate == null || candidate == _player || candidate.IsDead)
                continue;
            if (candidate.connectionToClient == null)
                continue;
            if (MatchManager.Instance != null &&
                MatchManager.Instance.matchMode == MatchMode.TeamDeathmatch &&
                candidate.TeamId == _player.TeamId)
                continue;

            float distance = Vector3.Distance(transform.position, candidate.transform.position);
            if (distance > bestDistance)
                continue;

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }

    private bool CanSeeTarget(PlayerController target)
    {
        Vector3 origin = GetEyePosition();
        Vector3 targetPoint = GetTargetPoint(target);
        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;
        if (distance < 0.001f) return true;

        RaycastHit[] hits = Physics.RaycastAll(origin, direction / distance, distance, lineOfSightMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            PlayerController hitPlayer = hit.collider.GetComponentInParent<PlayerController>();
            if (hitPlayer == _player)
                continue;
            if (hitPlayer == target)
                return true;

            return false;
        }

        return true;
    }

    private void MoveTowards(Vector3 point)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return;

        direction.Normalize();
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);

        Vector3 motion = direction * moveSpeed;
        motion.y = -6f;
        if (_controller != null && _controller.enabled)
            _controller.Move(motion * Time.deltaTime);
        else
            transform.position += direction * moveSpeed * Time.deltaTime;
    }

    private void StrafeNearTarget()
    {
        if (_target == null) return;

        if (Time.time >= _nextStrafeSwitchTime)
        {
            _strafeDirection = Random.value > 0.5f ? 1 : -1;
            _nextStrafeSwitchTime = Time.time + Random.Range(1.2f, 2.4f);
        }

        Vector3 right = Vector3.Cross(Vector3.up, (_target.transform.position - transform.position).normalized);
        right.y = 0f;
        if (right.sqrMagnitude < 0.001f) return;

        Vector3 motion = right.normalized * (_strafeDirection * strafeSpeed);
        motion.y = -6f;
        if (_controller != null && _controller.enabled)
            _controller.Move(motion * Time.deltaTime);
    }

    private PlayerMotionState GetMotionState()
    {
        return _controller != null && _controller.velocity.sqrMagnitude > 0.2f
            ? PlayerMotionState.Walk
            : PlayerMotionState.Idle;
    }

    private Vector3 GetEyePosition()
    {
        return transform.position + Vector3.up * 1.45f + transform.forward * 0.2f;
    }

    private Vector3 GetTargetPoint(PlayerController target)
    {
        return target.transform.position + Vector3.up * Random.Range(1.05f, 1.45f);
    }

    private void PickWaypoint()
    {
        Vector2 random = Random.insideUnitCircle * patrolRadius;
        _waypoint = _spawnPosition + new Vector3(random.x, 0f, random.y);
        _nextWaypointTime = Time.time + Random.Range(4f, 8f);
    }
}
