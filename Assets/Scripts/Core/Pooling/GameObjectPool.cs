using System.Collections.Generic;
using UnityEngine;

public class GameObjectPool
{
    private readonly GameObject _prefab;
    private readonly Transform _parent;
    private readonly Queue<GameObject> _inactiveObjects = new Queue<GameObject>();

    public GameObjectPool(GameObject prefab, int preloadCount, Transform parent = null)
    {
        _prefab = prefab;
        _parent = parent;

        Preload(preloadCount);
    }

    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject instance = _inactiveObjects.Count > 0
            ? _inactiveObjects.Dequeue()
            : Object.Instantiate(_prefab, _parent);

        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);

        foreach (IPoolable poolable in instance.GetComponentsInChildren<IPoolable>())
            poolable.OnSpawnedFromPool();

        return instance;
    }

    public void Release(GameObject instance)
    {
        if (instance == null) return;

        foreach (IPoolable poolable in instance.GetComponentsInChildren<IPoolable>())
            poolable.OnReturnedToPool();

        instance.SetActive(false);
        if (_parent != null)
            instance.transform.SetParent(_parent);

        _inactiveObjects.Enqueue(instance);
    }

    private void Preload(int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject instance = Object.Instantiate(_prefab, _parent);
            instance.SetActive(false);
            _inactiveObjects.Enqueue(instance);
        }
    }
}
