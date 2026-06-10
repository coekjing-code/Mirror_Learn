using UnityEngine;

public enum PlayerHitGroup
{
    Head,
    Chest,
    Pelvis,
    Arm,
    Leg
}

[RequireComponent(typeof(BoxCollider))]
public class PlayerHitbox : MonoBehaviour
{
    public PlayerHitGroup hitGroup = PlayerHitGroup.Chest;
    public float damageMultiplier = 1f;
    public Vector3 size = Vector3.one * 0.5f;

    public PlayerController Owner { get; private set; }

    private BoxCollider _collider;

    public Vector3 WorldCenter => transform.position;
    public Quaternion WorldRotation => transform.rotation;
    public Vector3 WorldSize => Vector3.Scale(size, transform.lossyScale);

    private void Awake()
    {
        Owner = GetComponentInParent<PlayerController>();
        EnsureCollider();
    }

    public void Configure(PlayerController owner, PlayerHitGroup group, Vector3 boxSize, float multiplier)
    {
        Owner = owner;
        hitGroup = group;
        size = boxSize;
        damageMultiplier = multiplier;
        EnsureCollider();
    }

    private void EnsureCollider()
    {
        if (_collider == null)
            _collider = GetComponent<BoxCollider>();

        if (_collider == null)
            _collider = gameObject.AddComponent<BoxCollider>();

        if (_collider == null)
            return;

        _collider.isTrigger = true;
        _collider.size = size;
        _collider.center = Vector3.zero;
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            EnsureCollider();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = GetGizmoColor();
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = oldMatrix;
    }

    private Color GetGizmoColor()
    {
        switch (hitGroup)
        {
            case PlayerHitGroup.Head:
                return Color.red;
            case PlayerHitGroup.Chest:
                return Color.yellow;
            case PlayerHitGroup.Pelvis:
                return Color.cyan;
            case PlayerHitGroup.Arm:
                return Color.magenta;
            case PlayerHitGroup.Leg:
                return Color.green;
            default:
                return Color.white;
        }
    }
}
