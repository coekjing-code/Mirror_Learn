using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class LagCompensationTarget : MonoBehaviour
{
    public struct HitResult
    {
        public LagCompensationTarget target;
        public PlayerHitbox hitbox;
        public Vector3 point;
        public float distance;
        public float damageMultiplier;
    }

    private struct HitboxSnapshot
    {
        public PlayerHitbox hitbox;
        public Vector3 center;
        public Quaternion rotation;
        public Vector3 size;
        public float damageMultiplier;
    }

    private struct Snapshot
    {
        public double time;
        public Vector3 position;
        public float radius;
        public HitboxSnapshot[] hitboxes;
    }

    public float hitRadius = 0.6f;
    public float historyDuration = 1.0f;
    public float captureInterval = 0.05f;

    private readonly List<Snapshot> _history = new List<Snapshot>();
    private double _nextCaptureTime;
    private NetworkIdentity _networkIdentity;
    private PlayerHitboxBuilder _hitboxBuilder;

    public NetworkConnectionToClient OwnerConnection => _networkIdentity != null ? _networkIdentity.connectionToClient : null;

    private void Awake()
    {
        _networkIdentity = GetComponent<NetworkIdentity>();
        _hitboxBuilder = GetComponent<PlayerHitboxBuilder>();
        _hitboxBuilder ??= gameObject.AddComponent<PlayerHitboxBuilder>();
        _hitboxBuilder.EnsureHitboxes();
    }

    private void Update()
    {
        if (!NetworkServer.active) return;
        if (_networkIdentity == null || !_networkIdentity.isServer) return;

        double now = NetworkTime.time;
        if (now < _nextCaptureTime) return;

        _nextCaptureTime = now + captureInterval;
        Capture(now);
        Trim(now);
    }

    public bool RaycastHistory(double targetTime, Vector3 rayOrigin, Vector3 rayDirection, float maxDistance, out Vector3 hitPoint)
    {
        Snapshot snapshot = GetSnapshot(targetTime);
        return RaySphere(rayOrigin, rayDirection.normalized, snapshot.position, snapshot.radius, maxDistance, out hitPoint);
    }

    public bool RaycastHitboxesHistory(double targetTime, Vector3 rayOrigin, Vector3 rayDirection, float maxDistance, out HitResult hitResult)
    {
        hitResult = default;
        Snapshot snapshot = GetSnapshot(targetTime);
        if (snapshot.hitboxes == null || snapshot.hitboxes.Length == 0)
        {
            if (!RaySphere(rayOrigin, rayDirection.normalized, snapshot.position, snapshot.radius, maxDistance, out Vector3 fallbackPoint))
                return false;

            hitResult = new HitResult
            {
                target = this,
                point = fallbackPoint,
                distance = Vector3.Distance(rayOrigin, fallbackPoint),
                damageMultiplier = 1f
            };
            return true;
        }

        bool hasHit = false;
        float bestDistance = maxDistance;
        Vector3 direction = rayDirection.normalized;
        foreach (HitboxSnapshot hitbox in snapshot.hitboxes)
        {
            if (!RayBox(rayOrigin, direction, hitbox.center, hitbox.rotation, hitbox.size, maxDistance, out float distance))
                continue;
            if (distance > bestDistance)
                continue;

            hasHit = true;
            bestDistance = distance;
            hitResult = new HitResult
            {
                target = this,
                hitbox = hitbox.hitbox,
                point = rayOrigin + direction * distance,
                distance = distance,
                damageMultiplier = hitbox.damageMultiplier
            };
        }

        return hasHit;
    }

    private void Capture(double time)
    {
        _history.Add(new Snapshot
        {
            time = time,
            position = transform.position,
            radius = hitRadius,
            hitboxes = CaptureHitboxes()
        });
    }

    private HitboxSnapshot[] CaptureHitboxes()
    {
        PlayerHitbox[] hitboxes = GetComponentsInChildren<PlayerHitbox>(true);
        HitboxSnapshot[] snapshots = new HitboxSnapshot[hitboxes.Length];
        for (int i = 0; i < hitboxes.Length; i++)
        {
            PlayerHitbox hitbox = hitboxes[i];
            snapshots[i] = new HitboxSnapshot
            {
                hitbox = hitbox,
                center = hitbox.WorldCenter,
                rotation = hitbox.WorldRotation,
                size = hitbox.WorldSize,
                damageMultiplier = hitbox.damageMultiplier
            };
        }

        return snapshots;
    }

    private void Trim(double now)
    {
        double minTime = now - historyDuration;
        while (_history.Count > 0 && _history[0].time < minTime)
            _history.RemoveAt(0);
    }

    private Snapshot GetSnapshot(double targetTime)
    {
        if (_history.Count == 0)
        {
            return new Snapshot
            {
                time = NetworkTime.time,
                position = transform.position,
                radius = hitRadius,
                hitboxes = CaptureHitboxes()
            };
        }

        Snapshot closest = _history[0];
        double closestDistance = System.Math.Abs(targetTime - closest.time);

        for (int i = 1; i < _history.Count; i++)
        {
            double distance = System.Math.Abs(targetTime - _history[i].time);
            if (distance >= closestDistance) continue;

            closest = _history[i];
            closestDistance = distance;
        }

        return closest;
    }

    private static bool RaySphere(Vector3 origin, Vector3 direction, Vector3 center, float radius, float maxDistance, out Vector3 hitPoint)
    {
        Vector3 toCenter = center - origin;
        float projection = Vector3.Dot(toCenter, direction);
        projection = Mathf.Clamp(projection, 0f, maxDistance);

        hitPoint = origin + direction * projection;
        float sqrDistance = (center - hitPoint).sqrMagnitude;
        return sqrDistance <= radius * radius;
    }

    private static bool RayBox(Vector3 origin, Vector3 direction, Vector3 center, Quaternion rotation, Vector3 size, float maxDistance, out float distance)
    {
        distance = 0f;
        Quaternion inverseRotation = Quaternion.Inverse(rotation);
        Vector3 localOrigin = inverseRotation * (origin - center);
        Vector3 localDirection = inverseRotation * direction;
        Vector3 half = size * 0.5f;

        float tMin = 0f;
        float tMax = maxDistance;

        if (!IntersectSlab(localOrigin.x, localDirection.x, -half.x, half.x, ref tMin, ref tMax)) return false;
        if (!IntersectSlab(localOrigin.y, localDirection.y, -half.y, half.y, ref tMin, ref tMax)) return false;
        if (!IntersectSlab(localOrigin.z, localDirection.z, -half.z, half.z, ref tMin, ref tMax)) return false;

        distance = tMin;
        return distance >= 0f && distance <= maxDistance;
    }

    private static bool IntersectSlab(float origin, float direction, float min, float max, ref float tMin, ref float tMax)
    {
        if (Mathf.Abs(direction) < 0.0001f)
            return origin >= min && origin <= max;

        float invDirection = 1f / direction;
        float t1 = (min - origin) * invDirection;
        float t2 = (max - origin) * invDirection;
        if (t1 > t2)
        {
            float temp = t1;
            t1 = t2;
            t2 = temp;
        }

        tMin = Mathf.Max(tMin, t1);
        tMax = Mathf.Min(tMax, t2);
        return tMin <= tMax;
    }
}
