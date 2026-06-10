using UnityEngine;
using Mirror;

public class Bullet : NetworkBehaviour
{
    public float lifeTime = 1.2f;
    public Vector3 visualScale = new Vector3(0.08f, 0.08f, 0.22f);
    public bool ignorePlayerCollisions = true;
    public LayerMask collisionMask = ~0;

    private Rigidbody _rb;
    private NetworkConnection _owner;
    [SyncVar] private Vector3 _velocity;
    private Vector3 _previousPosition;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        ConfigureProjectile();
    }

    public override void OnStartServer()
    {
        CancelInvoke(nameof(Return));
        _previousPosition = transform.position;
        Invoke(nameof(Return), lifeTime);
    }

    public override void OnStartClient()
    {
        if (!isServer)
            ApplyVelocity();
    }

    public void Fire(Vector3 vel, NetworkConnection owner)
    {
        _owner = owner;
        _velocity = vel;
        _previousPosition = transform.position;
        ApplyVelocity();
        IgnorePlayerColliders(owner);
    }

    private void FixedUpdate()
    {
        if (!isServer) return;

        Vector3 currentPosition = transform.position;
        Vector3 delta = currentPosition - _previousPosition;
        float distance = delta.magnitude;
        if (distance > 0.001f &&
            Physics.Raycast(_previousPosition, delta / distance, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore) &&
            hit.collider.GetComponentInParent<PlayerController>() == null)
        {
            transform.position = hit.point;
            Return();
            return;
        }

        _previousPosition = currentPosition;
    }

    private void ApplyVelocity()
    {
        if (_rb != null)
            _rb.velocity = _velocity;
    }

    private void IgnorePlayerColliders(NetworkConnection owner)
    {
        if (!ignorePlayerCollisions)
            return;

        Collider[] bulletColliders = GetComponentsInChildren<Collider>();
        foreach (PlayerController player in FindObjectsOfType<PlayerController>())
        {
            if (owner != null && player.connectionToClient == owner)
                IgnoreColliders(bulletColliders, player);

            foreach (PlayerHitbox hitbox in player.GetComponentsInChildren<PlayerHitbox>(true))
            {
                Collider hitboxCollider = hitbox.GetComponent<Collider>();
                if (hitboxCollider == null) continue;

                foreach (Collider bulletCollider in bulletColliders)
                    Physics.IgnoreCollision(bulletCollider, hitboxCollider, true);
            }
        }
    }

    private static void IgnoreColliders(Collider[] bulletColliders, PlayerController player)
    {
        Collider[] playerColliders = player.GetComponentsInChildren<Collider>(true);
        foreach (Collider bulletCollider in bulletColliders)
        {
            foreach (Collider playerCollider in playerColliders)
                Physics.IgnoreCollision(bulletCollider, playerCollider, true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isServer) return;
        if (other.GetComponentInParent<PlayerController>() != null) return;

        Return();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        ConfigureProjectile();
    }

    private void ConfigureProjectile()
    {
        transform.localScale = visualScale;

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider bulletCollider in colliders)
            bulletCollider.isTrigger = true;

        _rb ??= GetComponent<Rigidbody>();
        if (_rb == null) return;

        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.useGravity = false;
    }

    [Server]
    void Return()
    {
        CancelInvoke(nameof(Return));
        NetworkServer.Destroy(gameObject);
    }
}
