using Mirror;
using UnityEngine;

public class CustomNetManager : NetworkManager
{
    [Header("Learning Network Settings")]
    public NetworkSyncMode syncMode = NetworkSyncMode.InputSync;
    public bool useClientSpawnPool = true;
    public int clientSpawnPoolPreloadCount = 16;
    public bool showDebugHud = true;
    public bool showGameplayHud = true;
    public NetworkSessionInfo localSession;

    [Header("Match Settings")]
    public MatchMode matchMode = MatchMode.FreeForAll;
    public int matchMinPlayersToStart = 2;
    public float matchCountdownDuration = 5f;
    public float matchDuration = 180f;
    public float matchResultDuration = 8f;
    public int matchScoreLimit = 10;
    public bool requireReadyState = true;

    [Header("Training Bots")]
    public int trainingBotCount = 3;
    public bool spawnTrainingBots;
    public BotDifficulty botDifficulty = BotDifficulty.Normal;

    private NetworkDebugHUD _debugHud;
    private GameplayHUD _gameplayHud;
    private GameMenuUI _gameMenuUi;
    private MatchManager _matchManager;

    public override void OnStartServer()
    {
        base.OnStartServer();
        EnsureMatchManager();

        NetworkServer.RegisterHandler<ChatMsg>(OnServerRecvChat);
        NetworkServer.RegisterHandler<PlayerPosMsg>(OnServerRecvPos);
        NetworkServer.RegisterHandler<HurtMsg>(OnServerRecvHurt);
        NetworkServer.RegisterHandler<PlayerReadyMsg>(OnServerRecvReady);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        EnsureDebugHud();
        EnsureGameplayHud();
        EnsureGameMenuUi();
        RegisterClientSpawnPools();

        NetworkClient.RegisterHandler<ChatMsg>(OnClientRecvChat);
        NetworkClient.RegisterHandler<MatchSnapshotMsg>(MatchManager.OnClientSnapshot);
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        EnsureMatchManager();
        base.OnServerAddPlayer(conn);

        if (conn.identity != null && MatchManager.Instance != null)
        {
            Transform spawnPoint = MatchManager.Instance.GetSpawnPoint();
            if (spawnPoint != null)
                conn.identity.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        }

        PlayerController joinedPlayer = conn.identity != null ? conn.identity.GetComponent<PlayerController>() : null;
        MatchManager.Instance?.HandlePlayerJoined(joinedPlayer);
        if (spawnTrainingBots)
            MatchManager.Instance?.EnsureTrainingBots(trainingBotCount, playerPrefab);

        Debug.Log($"New player joined, connection id: {conn.connectionId}");
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);
        MatchManager.Instance?.HandlePlayerLeft();
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        EnsureDebugHud();
        EnsureGameplayHud();
        EnsureGameMenuUi();
        Debug.Log("Client connected to server.");
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        Debug.Log("Client disconnected from server.");
    }

    private void OnServerRecvChat(NetworkConnection conn, ChatMsg msg)
    {
        Debug.Log($"Server received chat: {msg.sendName}: {msg.content}");
        NetworkServer.SendToAll(msg);
    }

    private void OnServerRecvPos(NetworkConnection conn, PlayerPosMsg msg)
    {
        Debug.Log($"Server received position: {msg.x}, {msg.y}, {msg.z}");
        NetworkServer.SendToAll(msg);
    }

    private void OnServerRecvHurt(NetworkConnection conn, HurtMsg msg)
    {
        Debug.Log($"Server received hurt request: {msg.damage}");
    }

    private void OnServerRecvReady(NetworkConnectionToClient conn, PlayerReadyMsg msg)
    {
        MatchManager.Instance?.SetPlayerReady(conn, msg.isReady);
    }

    private void OnClientRecvChat(ChatMsg msg)
    {
        Debug.Log($"Client received chat: {msg.sendName}: {msg.content}");
    }

    private void RegisterClientSpawnPools()
    {
        if (!useClientSpawnPool) return;

        foreach (GameObject prefab in spawnPrefabs)
            NetworkPrefabPool.Register(prefab, clientSpawnPoolPreloadCount);
    }

    private void EnsureDebugHud()
    {
        if (!showDebugHud || _debugHud != null) return;

        GameObject hudObject = new GameObject("NetworkDebugHUD");
        DontDestroyOnLoad(hudObject);
        _debugHud = hudObject.AddComponent<NetworkDebugHUD>();
    }

    private void EnsureGameplayHud()
    {
        if (!showGameplayHud || _gameplayHud != null) return;

        GameObject hudObject = new GameObject("GameplayHUD");
        _gameplayHud = hudObject.AddComponent<GameplayHUD>();
    }

    private void EnsureGameMenuUi()
    {
        if (_gameMenuUi != null) return;

        _gameMenuUi = FindObjectOfType<GameMenuUI>();
        if (_gameMenuUi != null) return;

        GameObject menuObject = new GameObject("GameMenuUI");
        _gameMenuUi = menuObject.AddComponent<GameMenuUI>();
    }

    private void EnsureMatchManager()
    {
        if (_matchManager != null) return;

        _matchManager = FindObjectOfType<MatchManager>();
        if (_matchManager != null)
        {
            ApplyMatchSettings(_matchManager);
            return;
        }

        GameObject matchObject = new GameObject("MatchManager");
        _matchManager = matchObject.AddComponent<MatchManager>();
        ApplyMatchSettings(_matchManager);
    }

    private void ApplyMatchSettings(MatchManager matchManager)
    {
        matchManager.matchMode = matchMode;
        matchManager.minPlayersToStart = matchMinPlayersToStart;
        matchManager.countdownDuration = matchCountdownDuration;
        matchManager.matchDuration = matchDuration;
        matchManager.resultDuration = matchResultDuration;
        matchManager.scoreLimit = matchScoreLimit;
        matchManager.requireReadyState = requireReadyState;
        matchManager.botDifficulty = botDifficulty;
    }
}
