#if UNITY_EDITOR
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TpsArenaBuilder
{
    private const string RootName = "TPS_Arena";

    [MenuItem("Tools/Mirror Learn/Build TPS Arena")]
    public static void BuildArena()
    {
        GameObject oldRoot = GameObject.Find(RootName);
        if (oldRoot != null)
            Object.DestroyImmediate(oldRoot);

        GameObject root = new GameObject(RootName);
        CreateFloor(root.transform);
        CreateBoundary(root.transform);
        CreateVerticalLayout(root.transform);
        CreateCover(root.transform);
        CreateSpawnPoints(root.transform);
        CreateLighting();
        PositionSceneCamera();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;
    }

    private static void CreateFloor(Transform parent)
    {
        CreateCube("Arena_Floor_Main", parent, Vector3.zero, new Vector3(72f, 0.4f, 72f), new Color(0.22f, 0.24f, 0.25f));
        CreateCube("Lane_North", parent, new Vector3(0f, 0.02f, 30f), new Vector3(46f, 0.08f, 8f), new Color(0.18f, 0.2f, 0.22f));
        CreateCube("Lane_South", parent, new Vector3(0f, 0.02f, -30f), new Vector3(46f, 0.08f, 8f), new Color(0.18f, 0.2f, 0.22f));
        CreateCube("Lane_East", parent, new Vector3(30f, 0.02f, 0f), new Vector3(8f, 0.08f, 46f), new Color(0.18f, 0.2f, 0.22f));
        CreateCube("Lane_West", parent, new Vector3(-30f, 0.02f, 0f), new Vector3(8f, 0.08f, 46f), new Color(0.18f, 0.2f, 0.22f));
    }

    private static void CreateBoundary(Transform parent)
    {
        Color wall = new Color(0.32f, 0.35f, 0.37f);
        CreateCube("Wall_North", parent, new Vector3(0f, 2f, 36f), new Vector3(72f, 4f, 1.2f), wall);
        CreateCube("Wall_South", parent, new Vector3(0f, 2f, -36f), new Vector3(72f, 4f, 1.2f), wall);
        CreateCube("Wall_East", parent, new Vector3(36f, 2f, 0f), new Vector3(1.2f, 4f, 72f), wall);
        CreateCube("Wall_West", parent, new Vector3(-36f, 2f, 0f), new Vector3(1.2f, 4f, 72f), wall);

        CreateCube("North_Backstop", parent, new Vector3(0f, 6.3f, 35.2f), new Vector3(22f, 4.6f, 1f), new Color(0.25f, 0.28f, 0.3f));
        CreateCube("South_Backstop", parent, new Vector3(0f, 6.3f, -35.2f), new Vector3(22f, 4.6f, 1f), new Color(0.25f, 0.28f, 0.3f));
    }

    private static void CreateVerticalLayout(Transform parent)
    {
        Color platform = new Color(0.28f, 0.34f, 0.36f);
        Color ramp = new Color(0.34f, 0.38f, 0.36f);

        CreateCube("Center_Platform_Low", parent, new Vector3(0f, 2.6f, 0f), new Vector3(19f, 0.8f, 19f), platform);
        CreateCube("Center_Platform_Upper", parent, new Vector3(0f, 6.2f, 0f), new Vector3(8.5f, 0.8f, 8.5f), platform);
        CreateCube("Center_Block_A", parent, new Vector3(-3.8f, 7.9f, -3.8f), new Vector3(2.2f, 3.0f, 2.2f), platform);
        CreateCube("Center_Block_B", parent, new Vector3(3.8f, 7.9f, 3.8f), new Vector3(2.2f, 3.0f, 2.2f), platform);
        CreateCube("Center_Rail_North", parent, new Vector3(0f, 6.95f, 4.6f), new Vector3(9.5f, 1.1f, 0.5f), new Color(0.2f, 0.25f, 0.27f));
        CreateCube("Center_Rail_South", parent, new Vector3(0f, 6.95f, -4.6f), new Vector3(9.5f, 1.1f, 0.5f), new Color(0.2f, 0.25f, 0.27f));

        CreateRamp("Ramp_North_Low", parent, new Vector3(0f, 1.35f, 14f), new Vector3(10f, 0.6f, 18f), 18f, ramp);
        CreateRamp("Ramp_South_Low", parent, new Vector3(0f, 1.35f, -14f), new Vector3(10f, 0.6f, 18f), -18f, ramp);
        CreateRamp("Ramp_East_Low", parent, new Vector3(14f, 1.35f, 0f), new Vector3(18f, 0.6f, 10f), 0f, ramp, 0f, 0f, -18f);
        CreateRamp("Ramp_West_Low", parent, new Vector3(-14f, 1.35f, 0f), new Vector3(18f, 0.6f, 10f), 0f, ramp, 0f, 0f, 18f);

        CreateRamp("Ramp_Upper_North", parent, new Vector3(0f, 4.45f, 7.4f), new Vector3(6f, 0.55f, 10f), 22f, ramp);
        CreateRamp("Ramp_Upper_South", parent, new Vector3(0f, 4.45f, -7.4f), new Vector3(6f, 0.55f, 10f), -22f, ramp);

        CreateCube("Bridge_NorthSouth", parent, new Vector3(0f, 6.6f, 0f), new Vector3(5f, 0.55f, 42f), platform);
        CreateCube("Bridge_EastWest", parent, new Vector3(0f, 6.6f, 0f), new Vector3(42f, 0.55f, 5f), platform);
        CreateCube("Bridge_Rail_NS_A", parent, new Vector3(-2.8f, 7.15f, 0f), new Vector3(0.35f, 1.1f, 42f), new Color(0.2f, 0.25f, 0.27f));
        CreateCube("Bridge_Rail_NS_B", parent, new Vector3(2.8f, 7.15f, 0f), new Vector3(0.35f, 1.1f, 42f), new Color(0.2f, 0.25f, 0.27f));
        CreateCube("Bridge_Rail_EW_A", parent, new Vector3(0f, 7.15f, -2.8f), new Vector3(42f, 1.1f, 0.35f), new Color(0.2f, 0.25f, 0.27f));
        CreateCube("Bridge_Rail_EW_B", parent, new Vector3(0f, 7.15f, 2.8f), new Vector3(42f, 1.1f, 0.35f), new Color(0.2f, 0.25f, 0.27f));

        CreateTower(parent, "Tower_NW", new Vector3(-26f, 2.4f, 26f), 135f);
        CreateTower(parent, "Tower_NE", new Vector3(26f, 2.4f, 26f), 225f);
        CreateTower(parent, "Tower_SW", new Vector3(-26f, 2.4f, -26f), 45f);
        CreateTower(parent, "Tower_SE", new Vector3(26f, 2.4f, -26f), 315f);
    }

    private static void CreateCover(Transform parent)
    {
        Color lowCover = new Color(0.48f, 0.45f, 0.4f);
        Color highCover = new Color(0.3f, 0.42f, 0.46f);
        Color crate = new Color(0.42f, 0.36f, 0.28f);

        CreateCube("Cover_Low_North_A", parent, new Vector3(-12f, 0.65f, 21f), new Vector3(8f, 1.3f, 1.2f), lowCover);
        CreateCube("Cover_Low_North_B", parent, new Vector3(12f, 0.65f, 21f), new Vector3(8f, 1.3f, 1.2f), lowCover);
        CreateCube("Cover_Low_South_A", parent, new Vector3(-12f, 0.65f, -21f), new Vector3(8f, 1.3f, 1.2f), lowCover);
        CreateCube("Cover_Low_South_B", parent, new Vector3(12f, 0.65f, -21f), new Vector3(8f, 1.3f, 1.2f), lowCover);

        CreateCube("Cover_Low_East_A", parent, new Vector3(21f, 0.65f, -12f), new Vector3(1.2f, 1.3f, 8f), lowCover);
        CreateCube("Cover_Low_East_B", parent, new Vector3(21f, 0.65f, 12f), new Vector3(1.2f, 1.3f, 8f), lowCover);
        CreateCube("Cover_Low_West_A", parent, new Vector3(-21f, 0.65f, -12f), new Vector3(1.2f, 1.3f, 8f), lowCover);
        CreateCube("Cover_Low_West_B", parent, new Vector3(-21f, 0.65f, 12f), new Vector3(1.2f, 1.3f, 8f), lowCover);

        CreateCube("Container_NorthWest", parent, new Vector3(-23f, 1.45f, 8f), new Vector3(4f, 2.9f, 10f), highCover);
        CreateCube("Container_SouthEast", parent, new Vector3(23f, 1.45f, -8f), new Vector3(4f, 2.9f, 10f), highCover);
        CreateCube("Container_NorthEast", parent, new Vector3(11f, 1.45f, 28f), new Vector3(10f, 2.9f, 4f), highCover);
        CreateCube("Container_SouthWest", parent, new Vector3(-11f, 1.45f, -28f), new Vector3(10f, 2.9f, 4f), highCover);
        CreateCube("Stacked_Block_North", parent, new Vector3(-18f, 4.8f, 22f), new Vector3(7f, 6.6f, 4f), highCover);
        CreateCube("Stacked_Block_South", parent, new Vector3(18f, 4.8f, -22f), new Vector3(7f, 6.6f, 4f), highCover);
        CreateCube("Catwalk_Cover_North", parent, new Vector3(8f, 7.45f, 14f), new Vector3(5f, 1.5f, 0.8f), lowCover);
        CreateCube("Catwalk_Cover_South", parent, new Vector3(-8f, 7.45f, -14f), new Vector3(5f, 1.5f, 0.8f), lowCover);
        CreateCube("Catwalk_Cover_East", parent, new Vector3(14f, 7.45f, -8f), new Vector3(0.8f, 1.5f, 5f), lowCover);
        CreateCube("Catwalk_Cover_West", parent, new Vector3(-14f, 7.45f, 8f), new Vector3(0.8f, 1.5f, 5f), lowCover);

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 position = new Vector3(Mathf.Cos(angle) * 15f, 0.7f, Mathf.Sin(angle) * 15f);
            Vector3 scale = i % 2 == 0 ? new Vector3(2.2f, 1.4f, 2.2f) : new Vector3(3.6f, 1.4f, 1.4f);
            CreateCube("Crate_Ring_" + i, parent, position, scale, crate);
        }
    }

    private static void CreateSpawnPoints(Transform parent)
    {
        GameObject group = new GameObject("SpawnPoints");
        group.transform.SetParent(parent);

        CreateSpawn(group.transform, "Spawn_NorthWest", new Vector3(-29f, 0.1f, 29f), 135f);
        CreateSpawn(group.transform, "Spawn_North", new Vector3(0f, 0.1f, 31f), 180f);
        CreateSpawn(group.transform, "Spawn_NorthEast", new Vector3(29f, 0.1f, 29f), 225f);
        CreateSpawn(group.transform, "Spawn_East", new Vector3(31f, 0.1f, 0f), 270f);
        CreateSpawn(group.transform, "Spawn_SouthEast", new Vector3(29f, 0.1f, -29f), 315f);
        CreateSpawn(group.transform, "Spawn_South", new Vector3(0f, 0.1f, -31f), 0f);
        CreateSpawn(group.transform, "Spawn_SouthWest", new Vector3(-29f, 0.1f, -29f), 45f);
        CreateSpawn(group.transform, "Spawn_West", new Vector3(-31f, 0.1f, 0f), 90f);
    }

    private static void CreateTower(Transform parent, string name, Vector3 position, float yaw)
    {
        Color platform = new Color(0.28f, 0.34f, 0.36f);
        Color wall = new Color(0.24f, 0.29f, 0.31f);

        CreateCube(name + "_Base", parent, new Vector3(position.x, 6.5f, position.z), new Vector3(8.5f, 0.8f, 8.5f), platform);
        CreateCube(name + "_Column", parent, new Vector3(position.x, 3.2f, position.z), new Vector3(3.2f, 6.4f, 3.2f), wall);
        CreateCube(name + "_BackWall", parent, new Vector3(position.x, 6.5f, position.z) + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 1.8f, -4f), new Vector3(8.5f, 2.8f, 0.8f), wall);
        CreateCube(name + "_SideWall_A", parent, new Vector3(position.x, 6.5f, position.z) + Quaternion.Euler(0f, yaw, 0f) * new Vector3(-4f, 1.8f, 0f), new Vector3(0.8f, 2.8f, 8.5f), wall);
        CreateCube(name + "_SideWall_B", parent, new Vector3(position.x, 6.5f, position.z) + Quaternion.Euler(0f, yaw, 0f) * new Vector3(4f, 1.1f, 1.5f), new Vector3(0.8f, 1.4f, 4f), wall);
    }

    private static void CreateRamp(string name, Transform parent, Vector3 position, Vector3 scale, float pitch, Color color, float xRot = 0f, float yRot = 0f, float zRot = 0f)
    {
        GameObject ramp = CreateCube(name, parent, position, scale, color);
        ramp.transform.rotation = Quaternion.Euler(xRot == 0f ? pitch : xRot, yRot, zRot);
    }

    private static void CreateSpawn(Transform parent, string name, Vector3 position, float yaw)
    {
        GameObject spawn = new GameObject(name);
        spawn.transform.SetParent(parent);
        spawn.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        spawn.AddComponent<NetworkStartPosition>();

        GameObject marker = CreateCube(name + "_Marker", spawn.transform, Vector3.zero, new Vector3(1.2f, 0.08f, 1.2f), new Color(0.1f, 0.7f, 1f));
        marker.transform.localPosition = Vector3.zero;
    }

    private static void CreateLighting()
    {
        Light directional = Object.FindObjectOfType<Light>();
        if (directional == null)
        {
            GameObject lightObject = new GameObject("Directional Light");
            directional = lightObject.AddComponent<Light>();
            directional.type = LightType.Directional;
        }

        directional.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        directional.intensity = 1.25f;

        RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.48f);
    }

    private static void PositionSceneCamera()
    {
        if (SceneView.lastActiveSceneView == null) return;

        SceneView.lastActiveSceneView.LookAt(new Vector3(0f, 4f, 0f), Quaternion.Euler(48f, 45f, 0f), 82f);
    }

    private static GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 scale, Color color)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent);
        cube.transform.position = position;
        cube.transform.localScale = scale;

        Renderer renderer = cube.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = color;
            renderer.sharedMaterial = material;
        }

        return cube;
    }
}
#endif
