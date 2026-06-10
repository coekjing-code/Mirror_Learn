using Mirror;
using UnityEngine;

public class NetworkDebugHUD : MonoBehaviour
{
    public bool show = true;
    public KeyCode toggleKey = KeyCode.F3;
    public KeyCode simulationToggleKey = KeyCode.F4;

    private const int Width = 410;
    private NetworkSimulationController _simulation;

    private void Awake()
    {
        _simulation = GetComponent<NetworkSimulationController>();
        if (_simulation == null)
            _simulation = gameObject.AddComponent<NetworkSimulationController>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            show = !show;

        if (Input.GetKeyDown(simulationToggleKey))
            _simulation.Toggle();
    }

    private void OnGUI()
    {
        if (!show) return;

        PlayerController player = PlayerController.LocalPlayerInstance;
        NetworkSyncMode syncMode = GetSyncMode();

        GUILayout.BeginArea(new Rect(12, 12, Width, 340), GUI.skin.box);
        GUILayout.Label("<b>Network Debug</b>");
        GUILayout.Space(4);

        GUILayout.Label($"Mode: {syncMode}");
        GUILayout.Label($"Transport: {(Transport.active != null ? Transport.active.GetType().Name : "None")}");
        GUILayout.Label($"Client Active: {NetworkClient.active}");
        GUILayout.Label($"Server Active: {NetworkServer.active}");
        GUILayout.Label($"RTT: {NetworkTime.rtt * 1000f:F0} ms");

        GUILayout.Space(6);
        DrawSimulationControls();

        GUILayout.Space(6);

        if (player == null)
        {
            GUILayout.Label("Local Player: none");
        }
        else
        {
            Vector3 localPos = player.transform.position;
            Vector3 serverPos = player.ServerPosition;

            GUILayout.Label($"Player: {player.playerName}");
            GUILayout.Label($"Motion: {player.RemoteMotionState}");
            GUILayout.Label($"Health: {player.CurrentHealth}");
            GUILayout.Label($"Dead: {player.IsDead}");
            GUILayout.Label($"Local Pos: {FormatVector(localPos)}");
            GUILayout.Label($"Server Pos: {(player.HasServerState ? FormatVector(serverPos) : "waiting")}");
            GUILayout.Label($"Prediction Error: {(player.HasServerState ? player.PredictionError.ToString("F3") : "waiting")}");
        }

        GUILayout.Space(6);
        GUILayout.Label($"{toggleKey}: toggle HUD | {simulationToggleKey}: toggle simulation");
        GUILayout.EndArea();
    }

    private void DrawSimulationControls()
    {
        GUILayout.Label("<b>Latency Simulation</b>");

        if (_simulation == null || !_simulation.HasTransportSimulation)
        {
            GUILayout.Label("Transport: LatencySimulation not found");
            GUILayout.Label("Add LatencySimulation wrapping KCP to test real transport delay.");
            return;
        }

        GUILayout.Label($"Enabled: {_simulation.simulationEnabled}");
        GUILayout.Label($"Latency: {_simulation.latencyMs:F0} ms");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-50 ms"))
            _simulation.DecreaseLatency();
        if (GUILayout.Button("+50 ms"))
            _simulation.IncreaseLatency();
        if (GUILayout.Button(_simulation.simulationEnabled ? "Disable" : "Enable"))
            _simulation.Toggle();
        GUILayout.EndHorizontal();
    }

    private NetworkSyncMode GetSyncMode()
    {
        return NetworkManager.singleton is CustomNetManager customNetManager
            ? customNetManager.syncMode
            : NetworkSyncMode.StateSync;
    }

    private static string FormatVector(Vector3 value)
    {
        return $"{value.x:F2}, {value.y:F2}, {value.z:F2}";
    }
}
