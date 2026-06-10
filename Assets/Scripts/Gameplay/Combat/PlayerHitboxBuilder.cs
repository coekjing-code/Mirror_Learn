using UnityEngine;

public class PlayerHitboxBuilder : MonoBehaviour
{
    public bool buildOnAwake = true;
    public bool rebuildExisting = false;

    private PlayerController _owner;

    private void Awake()
    {
        _owner = GetComponent<PlayerController>();
        if (buildOnAwake)
            EnsureHitboxes();
    }

    public void EnsureHitboxes()
    {
        _owner ??= GetComponent<PlayerController>();
        if (_owner == null) return;
        if (!rebuildExisting && GetComponentsInChildren<PlayerHitbox>(true).Length > 0) return;

        Transform root = transform.Find("Hitboxes");
        if (root == null)
        {
            GameObject rootObject = new GameObject("Hitboxes");
            rootObject.transform.SetParent(transform, false);
            root = rootObject.transform;
        }

        if (rebuildExisting)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
        }

        Create(root, "Head", PlayerHitGroup.Head, new Vector3(0f, 1.72f, 0f), new Vector3(0.34f, 0.28f, 0.32f), 2f);
        Create(root, "Chest", PlayerHitGroup.Chest, new Vector3(0f, 1.28f, 0f), new Vector3(0.72f, 0.52f, 0.38f), 1f);
        Create(root, "Pelvis", PlayerHitGroup.Pelvis, new Vector3(0f, 0.86f, 0f), new Vector3(0.58f, 0.34f, 0.36f), 1f);
        Create(root, "LeftArm", PlayerHitGroup.Arm, new Vector3(-0.52f, 1.22f, 0f), new Vector3(0.22f, 0.62f, 0.24f), 0.75f);
        Create(root, "RightArm", PlayerHitGroup.Arm, new Vector3(0.52f, 1.22f, 0f), new Vector3(0.22f, 0.62f, 0.24f), 0.75f);
        Create(root, "LeftLeg", PlayerHitGroup.Leg, new Vector3(-0.18f, 0.42f, 0f), new Vector3(0.24f, 0.78f, 0.26f), 0.75f);
        Create(root, "RightLeg", PlayerHitGroup.Leg, new Vector3(0.18f, 0.42f, 0f), new Vector3(0.24f, 0.78f, 0.26f), 0.75f);
    }

    private void Create(Transform root, string name, PlayerHitGroup group, Vector3 localPosition, Vector3 size, float multiplier)
    {
        GameObject hitboxObject = new GameObject(name);
        hitboxObject.transform.SetParent(root, false);
        hitboxObject.transform.localPosition = localPosition;
        hitboxObject.transform.localRotation = Quaternion.identity;

        hitboxObject.AddComponent<BoxCollider>();
        PlayerHitbox hitbox = hitboxObject.AddComponent<PlayerHitbox>();
        hitbox.Configure(_owner, group, size, multiplier);
    }
}
