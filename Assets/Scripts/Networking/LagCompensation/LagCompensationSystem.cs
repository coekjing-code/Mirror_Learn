using Mirror;
using UnityEngine;

public static class LagCompensationSystem
{
    public static bool RaycastHitboxes(NetworkConnection owner, double clientFireTime, Vector3 rayOrigin, Vector3 rayDirection, float maxDistance, out LagCompensationTarget.HitResult bestHit)
    {
        bestHit = default;
        bool hasHit = false;
        float bestDistance = maxDistance;

        LagCompensationTarget[] targets = Object.FindObjectsOfType<LagCompensationTarget>();
        foreach (LagCompensationTarget target in targets)
        {
            if (target == null || target.OwnerConnection == owner)
                continue;

            if (!target.RaycastHitboxesHistory(clientFireTime, rayOrigin, rayDirection, maxDistance, out LagCompensationTarget.HitResult candidate))
                continue;
            if (candidate.distance > bestDistance)
                continue;

            hasHit = true;
            bestDistance = candidate.distance;
            bestHit = candidate;
        }

        return hasHit;
    }

    public static bool RaycastPlayers(NetworkConnection owner, double clientFireTime, Vector3 rayOrigin, Vector3 rayDirection, float maxDistance, out LagCompensationTarget hitTarget, out Vector3 hitPoint)
    {
        hitTarget = null;
        hitPoint = Vector3.zero;

        LagCompensationTarget[] targets = Object.FindObjectsOfType<LagCompensationTarget>();
        foreach (LagCompensationTarget target in targets)
        {
            if (target == null || target.OwnerConnection == owner)
                continue;

            if (!target.RaycastHistory(clientFireTime, rayOrigin, rayDirection, maxDistance, out Vector3 candidateHitPoint))
                continue;

            hitTarget = target;
            hitPoint = candidateHitPoint;
            return true;
        }

        return false;
    }
}
