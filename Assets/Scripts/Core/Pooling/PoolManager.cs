using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    private readonly Dictionary<GameObject, GameObjectPool> _pools = new Dictionary<GameObject, GameObjectPool>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, int preloadCount = 0)
    {
        if (prefab == null) return null;

        return GetOrCreatePool(prefab, preloadCount).Get(position, rotation);
    }

    public void Despawn(GameObject prefab, GameObject instance)
    {
        if (prefab == null || instance == null) return;

        GetOrCreatePool(prefab, 0).Release(instance);
    }

    public void Preload(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0) return;

        GetOrCreatePool(prefab, count);
    }

    private GameObjectPool GetOrCreatePool(GameObject prefab, int preloadCount)
    {
        if (_pools.TryGetValue(prefab, out GameObjectPool pool))
            return pool;

        Transform poolRoot = new GameObject($"{prefab.name}_Pool").transform;
        poolRoot.SetParent(transform);

        pool = new GameObjectPool(prefab, preloadCount, poolRoot);
        _pools.Add(prefab, pool);
        return pool;
    }
}
