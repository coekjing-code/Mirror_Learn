using Mirror;
using UnityEngine;

public class NetworkSimulationController : MonoBehaviour
{
    public bool simulationEnabled;
    public float latencyMs = 120f;
    public float jitter = 0.02f;
    public float unreliableLoss;
    public float unreliableScramble;

    private LatencySimulation _latencySimulation;
    private float _originalLatency;
    private float _originalJitter;
    private float _originalLoss;
    private float _originalScramble;

    public bool HasTransportSimulation => _latencySimulation != null;

    private void Awake()
    {
        _latencySimulation = FindObjectOfType<LatencySimulation>();
        if (_latencySimulation == null) return;

        _originalLatency = _latencySimulation.latency;
        _originalJitter = _latencySimulation.jitter;
        _originalLoss = _latencySimulation.unreliableLoss;
        _originalScramble = _latencySimulation.unreliableScramble;

        Apply();
    }

    public void Toggle()
    {
        simulationEnabled = !simulationEnabled;
        Apply();
    }

    public void IncreaseLatency()
    {
        latencyMs = Mathf.Clamp(latencyMs + 50f, 0f, 1000f);
        Apply();
    }

    public void DecreaseLatency()
    {
        latencyMs = Mathf.Clamp(latencyMs - 50f, 0f, 1000f);
        Apply();
    }

    public void Apply()
    {
        if (_latencySimulation == null) return;

        if (simulationEnabled)
        {
            _latencySimulation.latency = latencyMs;
            _latencySimulation.jitter = jitter;
            _latencySimulation.unreliableLoss = unreliableLoss;
            _latencySimulation.unreliableScramble = unreliableScramble;
        }
        else
        {
            _latencySimulation.latency = 0f;
            _latencySimulation.jitter = 0f;
            _latencySimulation.unreliableLoss = 0f;
            _latencySimulation.unreliableScramble = 0f;
        }
    }

    private void OnDestroy()
    {
        if (_latencySimulation == null) return;

        _latencySimulation.latency = _originalLatency;
        _latencySimulation.jitter = _originalJitter;
        _latencySimulation.unreliableLoss = _originalLoss;
        _latencySimulation.unreliableScramble = _originalScramble;
    }
}
