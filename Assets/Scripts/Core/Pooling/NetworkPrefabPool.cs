using System.Collections.Generic;
using Mirror;
using UnityEngine;

public static class NetworkPrefabPool
{
    private sealed class Entry
    {
        public readonly GameObject Prefab;
        public readonly Queue<GameObject> InactiveObjects = new Queue<GameObject>();
        public readonly Transform Root;

        public Entry(GameObject prefab, int preloadCount)
        {
            Prefab = prefab;
            Root = new GameObject($"{prefab.name}_NetworkPool").transform;
            Object.DontDestroyOnLoad(Root.gameObject);

            for (int i = 0; i < preloadCount; i++)
                InactiveObjects.Enqueue(CreateInactiveInstance());
        }

        public GameObject Spawn(SpawnMessage message)
        {
            GameObject instance = InactiveObjects.Count > 0
                ? InactiveObjects.Dequeue()
                : CreateInactiveInstance();

            instance.transform.SetPositionAndRotation(message.position, message.rotation);
            instance.transform.localScale = message.scale;
            instance.SetActive(true);
            return instance;
        }

        public void Unspawn(GameObject instance)
        {
            if (instance == null) return;

            instance.SetActive(false);
            instance.transform.SetParent(Root);
            InactiveObjects.Enqueue(instance);
        }

        private GameObject CreateInactiveInstance()
        {
            GameObject instance = Object.Instantiate(Prefab, Root);
            instance.SetActive(false);
            return instance;
        }
    }

    private static readonly Dictionary<uint, Entry> Entries = new Dictionary<uint, Entry>();

    public static void Register(GameObject prefab, int preloadCount)
    {
        if (prefab == null) return;

        if (!prefab.TryGetComponent(out NetworkIdentity identity))
        {
            Debug.LogError($"NetworkPrefabPool: {prefab.name} has no NetworkIdentity.");
            return;
        }

        uint assetId = identity.assetId;
        if (assetId == 0)
        {
            Debug.LogError($"NetworkPrefabPool: {prefab.name} has an empty assetId.");
            return;
        }

        NetworkClient.UnregisterPrefab(prefab);

        if (!Entries.TryGetValue(assetId, out Entry entry))
        {
            entry = new Entry(prefab, preloadCount);
            Entries.Add(assetId, entry);
        }

        NetworkClient.RegisterPrefab(prefab, entry.Spawn, entry.Unspawn);
    }
}
