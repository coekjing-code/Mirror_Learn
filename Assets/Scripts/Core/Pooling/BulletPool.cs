using UnityEngine;

public class BulletPool : MonoBehaviour
{
    public static BulletPool Instance;
    public GameObject bulletPrefab;
    public int preloadCount = 16;

    void Awake()
    {
        Instance = this;

        if (PoolManager.Instance != null && bulletPrefab != null)
            PoolManager.Instance.Preload(bulletPrefab, preloadCount);
    }

    public GameObject GetBullet()
    {
        if (PoolManager.Instance == null || bulletPrefab == null) return null;

        return PoolManager.Instance.Spawn(bulletPrefab, Vector3.zero, Quaternion.identity, preloadCount);
    }

    public void ReturnBullet(GameObject bullet)
    {
        if (PoolManager.Instance == null || bulletPrefab == null) return;

        PoolManager.Instance.Despawn(bulletPrefab, bullet);
    }
}
