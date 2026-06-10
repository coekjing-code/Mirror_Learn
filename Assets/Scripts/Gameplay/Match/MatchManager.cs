using System.Collections.Generic;
using System.Text;
using Mirror;
using UnityEngine;

public enum MatchPhase
{
    WaitingForPlayers = 0,
    Countdown = 1,
    Playing = 2,
    Result = 3
}

public enum MatchMode
{
    FreeForAll = 0,
    TeamDeathmatch = 1
}

public enum BotDifficulty
{
    Easy = 0,
    Normal = 1,
    Hard = 2
}

public class MatchManager : MonoBehaviour
{
    public static MatchManager Instance { get; private set; }
    public static MatchSnapshotMsg ClientSnapshot { get; private set; }
    public static bool HasClientSnapshot { get; private set; }

    [Header("Match Rules")]
    public MatchMode matchMode = MatchMode.FreeForAll;
    public int minPlayersToStart = 2;
    public float countdownDuration = 5f;
    public float matchDuration = 180f;
    public float resultDuration = 8f;
    public int scoreLimit = 10;
    public bool requireReadyState = true;

    [Header("Bots")]
    public BotDifficulty botDifficulty = BotDifficulty.Normal;

    [Header("Spawns")]
    public Transform[] spawnPoints;

    private readonly Dictionary<int, int> _killsByConnectionId = new Dictionary<int, int>();
    private readonly Dictionary<int, bool> _readyByConnectionId = new Dictionary<int, bool>();
    private readonly List<GameObject> _trainingBots = new List<GameObject>();
    private int _nextSpawnIndex;
    private int _blueScore;
    private int _redScore;
    private MatchPhase _phase = MatchPhase.WaitingForPlayers;
    private float _phaseEndTime;
    private float _nextSnapshotTime;
    private string _winnerName = "";
    private int _winnerScore;

    public MatchPhase Phase => _phase;
    public bool IsPlaying => _phase == MatchPhase.Playing;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RefreshSpawnPoints();
    }

    private void Update()
    {
        if (!NetworkServer.active) return;

        UpdateServerMatchState();
        BroadcastSnapshotThrottled();
    }

    public Transform GetSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            RefreshSpawnPoints();
        if (spawnPoints == null || spawnPoints.Length == 0)
            return null;

        Transform spawnPoint = spawnPoints[_nextSpawnIndex % spawnPoints.Length];
        _nextSpawnIndex++;
        return spawnPoint;
    }

    [Server]
    public void RegisterKill(NetworkConnectionToClient attacker, NetworkConnectionToClient victim)
    {
        if (!IsPlaying) return;
        if (attacker == null || attacker == victim) return;

        int connectionId = attacker.connectionId;
        _killsByConnectionId.TryGetValue(connectionId, out int kills);
        _killsByConnectionId[connectionId] = kills + 1;

        if (matchMode == MatchMode.TeamDeathmatch)
        {
            int teamId = GetTeamId(attacker);
            if (teamId == 1)
                _blueScore++;
            else if (teamId == 2)
                _redScore++;
        }

        Debug.Log($"Kill registered. attacker={connectionId}, kills={_killsByConnectionId[connectionId]}");

        if (ShouldEndByScore(connectionId))
            EnterResult(GetWinnerName(), GetLeadingScore());

        BroadcastSnapshot();
    }

    [Server]
    public bool IsFriendlyFire(NetworkConnectionToClient attacker, PlayerController victim)
    {
        if (matchMode != MatchMode.TeamDeathmatch) return false;
        if (attacker == null || victim == null) return false;

        int attackerTeam = GetTeamId(attacker);
        return attackerTeam > 0 && attackerTeam == victim.TeamId;
    }

    public int GetKills(int connectionId)
    {
        return _killsByConnectionId.TryGetValue(connectionId, out int kills) ? kills : 0;
    }

    [Server]
    public void EnsureTrainingBots(int count, GameObject playerPrefab)
    {
        if (playerPrefab == null || count <= 0) return;
        if (_trainingBots.Count >= count) return;

        while (_trainingBots.Count < count)
        {
            Transform spawnPoint = GetSpawnPoint();
            Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
            GameObject bot = Instantiate(playerPrefab, position, rotation);
            bot.name = "TrainingBot_" + _trainingBots.Count;

            PlayerController player = bot.GetComponent<PlayerController>();
            if (player != null)
            {
                player.playerName = "Bot " + (_trainingBots.Count + 1);
                player.ServerSetTeam(GetLeastPopulatedTeam());
            }

            TrainingBotController botController = bot.GetComponent<TrainingBotController>();
            if (botController == null)
                botController = bot.AddComponent<TrainingBotController>();
            botController.ApplyDifficulty(botDifficulty);

            NetworkServer.Spawn(bot);
            _trainingBots.Add(bot);
        }
    }

    [Server]
    public bool CanDealDamage()
    {
        return IsPlaying;
    }

    [Server]
    public void HandlePlayerJoined(PlayerController joinedPlayer)
    {
        if (joinedPlayer != null)
        {
            joinedPlayer.ServerSetTeam(matchMode == MatchMode.TeamDeathmatch ? GetLeastPopulatedTeam() : 0);
            if (joinedPlayer.connectionToClient != null)
                _readyByConnectionId[joinedPlayer.connectionToClient.connectionId] = !requireReadyState;
        }

        if (_phase == MatchPhase.WaitingForPlayers)
        {
            SetAllPlayersGameplay(false);
            TryStartCountdown();
        }
        else if (_phase == MatchPhase.Countdown || _phase == MatchPhase.Result)
        {
            SetAllPlayersGameplay(false);
        }
        else if (_phase == MatchPhase.Playing)
        {
            if (joinedPlayer != null)
            {
                joinedPlayer.ServerRespawnForMatch();
                joinedPlayer.ServerSetGameplayEnabled(true);
            }
        }

        BroadcastSnapshot();
    }

    [Server]
    public void HandlePlayerLeft()
    {
        RemoveMissingReadyEntries();

        if (GetConnectedPlayerCount() < minPlayersToStart && _phase != MatchPhase.WaitingForPlayers)
            EnterWaitingForPlayers();

        BroadcastSnapshot();
    }

    [Server]
    public void SetPlayerReady(NetworkConnectionToClient conn, bool isReady)
    {
        if (conn == null) return;

        _readyByConnectionId[conn.connectionId] = isReady;
        if (_phase == MatchPhase.WaitingForPlayers)
            TryStartCountdown();

        BroadcastSnapshot();
    }

    [Client]
    public static void OnClientSnapshot(MatchSnapshotMsg msg)
    {
        ClientSnapshot = msg;
        HasClientSnapshot = true;
    }

    private void UpdateServerMatchState()
    {
        switch (_phase)
        {
            case MatchPhase.WaitingForPlayers:
                TryStartCountdown();
                break;
            case MatchPhase.Countdown:
                if (GetConnectedPlayerCount() < minPlayersToStart)
                {
                    EnterWaitingForPlayers();
                    return;
                }

                if (!AreEnoughPlayersReady())
                {
                    EnterWaitingForPlayers();
                    return;
                }

                if (Time.time >= _phaseEndTime)
                    EnterPlaying();
                break;
            case MatchPhase.Playing:
                if (Time.time >= _phaseEndTime)
                    EnterResult(GetWinnerName(), GetLeadingScore());
                break;
            case MatchPhase.Result:
                if (Time.time >= _phaseEndTime)
                    EnterWaitingForPlayers();
                break;
        }
    }

    private void TryStartCountdown()
    {
        if (GetConnectedPlayerCount() >= minPlayersToStart && AreEnoughPlayersReady())
            EnterCountdown();
    }

    private void EnterWaitingForPlayers()
    {
        _phase = MatchPhase.WaitingForPlayers;
        _phaseEndTime = 0f;
        _winnerName = "";
        _winnerScore = 0;
        _blueScore = 0;
        _redScore = 0;
        SetAllPlayersGameplay(false);
    }

    private void EnterCountdown()
    {
        _phase = MatchPhase.Countdown;
        _phaseEndTime = Time.time + countdownDuration;
        _winnerName = "";
        _winnerScore = 0;
        _blueScore = 0;
        _redScore = 0;
        SetAllPlayersGameplay(false);
    }

    private void EnterPlaying()
    {
        _phase = MatchPhase.Playing;
        _phaseEndTime = Time.time + matchDuration;
        _killsByConnectionId.Clear();
        _winnerName = "";
        _winnerScore = 0;
        _blueScore = 0;
        _redScore = 0;
        RespawnAllPlayers();
        SetAllPlayersGameplay(true);
        BroadcastSnapshot();
    }

    private void EnterResult(string winnerName, int winnerScore)
    {
        _phase = MatchPhase.Result;
        _phaseEndTime = Time.time + resultDuration;
        _winnerName = winnerName;
        _winnerScore = winnerScore;
        SetAllPlayersGameplay(false);
        BroadcastSnapshot();
    }

    private void RespawnAllPlayers()
    {
        foreach (PlayerController player in FindObjectsOfType<PlayerController>())
            player.ServerRespawnForMatch();
    }

    private void SetAllPlayersGameplay(bool enabled)
    {
        foreach (PlayerController player in FindObjectsOfType<PlayerController>())
            player.ServerSetGameplayEnabled(enabled);
    }

    private int GetConnectedPlayerCount()
    {
        int count = 0;
        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            if (conn != null && conn.identity != null)
                count++;
        }

        return count;
    }

    private bool AreEnoughPlayersReady()
    {
        if (!requireReadyState)
            return true;

        int connected = GetConnectedPlayerCount();
        if (connected < minPlayersToStart)
            return false;

        return GetReadyPlayerCount() >= connected;
    }

    private int GetReadyPlayerCount()
    {
        int count = 0;
        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            if (conn == null || conn.identity == null)
                continue;

            if (_readyByConnectionId.TryGetValue(conn.connectionId, out bool ready) && ready)
                count++;
        }

        return count;
    }

    private void RemoveMissingReadyEntries()
    {
        List<int> staleIds = new List<int>();
        foreach (int connectionId in _readyByConnectionId.Keys)
        {
            if (!NetworkServer.connections.ContainsKey(connectionId))
                staleIds.Add(connectionId);
        }

        foreach (int connectionId in staleIds)
            _readyByConnectionId.Remove(connectionId);
    }

    private bool ShouldEndByScore(int attackerConnectionId)
    {
        if (matchMode == MatchMode.TeamDeathmatch)
            return _blueScore >= scoreLimit || _redScore >= scoreLimit;

        return _killsByConnectionId.TryGetValue(attackerConnectionId, out int kills) && kills >= scoreLimit;
    }

    private int GetTeamId(NetworkConnectionToClient conn)
    {
        PlayerController player = conn != null && conn.identity != null
            ? conn.identity.GetComponent<PlayerController>()
            : null;

        return player != null ? player.TeamId : 0;
    }

    private int GetLeastPopulatedTeam()
    {
        if (matchMode != MatchMode.TeamDeathmatch)
            return 0;

        int blue = 0;
        int red = 0;
        foreach (PlayerController player in FindObjectsOfType<PlayerController>())
        {
            if (player.TeamId == 1)
                blue++;
            else if (player.TeamId == 2)
                red++;
        }

        return blue <= red ? 1 : 2;
    }

    private string GetTeamName(int teamId)
    {
        switch (teamId)
        {
            case 1:
                return "BLUE";
            case 2:
                return "RED";
            default:
                return "FFA";
        }
    }

    private void BroadcastSnapshotThrottled()
    {
        if (Time.time < _nextSnapshotTime) return;

        _nextSnapshotTime = Time.time + 0.25f;
        BroadcastSnapshot();
    }

    private void BroadcastSnapshot()
    {
        if (!NetworkServer.active) return;

        NetworkServer.SendToAll(BuildSnapshot());
    }

    private MatchSnapshotMsg BuildSnapshot()
    {
        int leadingScore = _phase == MatchPhase.Result ? _winnerScore : GetLeadingScore();
        string leaderName = _phase == MatchPhase.Result ? _winnerName : GetWinnerName();

        return new MatchSnapshotMsg
        {
            phase = (int)_phase,
            mode = (int)matchMode,
            phaseTimeRemaining = GetPhaseTimeRemaining(),
            connectedPlayers = GetConnectedPlayerCount(),
            minPlayersToStart = minPlayersToStart,
            readyPlayers = GetReadyPlayerCount(),
            readyRequired = requireReadyState,
            scoreLimit = scoreLimit,
            leaderName = leaderName,
            leadingScore = leadingScore,
            blueScore = _blueScore,
            redScore = _redScore,
            resultTitle = GetResultTitle(),
            scoreboard = BuildScoreboard()
        };
    }

    private float GetPhaseTimeRemaining()
    {
        if (_phase == MatchPhase.WaitingForPlayers)
            return 0f;

        return Mathf.Max(0f, _phaseEndTime - Time.time);
    }

    private int GetLeadingScore()
    {
        if (matchMode == MatchMode.TeamDeathmatch)
            return Mathf.Max(_blueScore, _redScore);

        int best = 0;
        foreach (int score in _killsByConnectionId.Values)
            best = Mathf.Max(best, score);

        return best;
    }

    private string GetWinnerName()
    {
        if (matchMode == MatchMode.TeamDeathmatch)
        {
            if (_blueScore == _redScore)
                return "DRAW";

            return _blueScore > _redScore ? "BLUE TEAM" : "RED TEAM";
        }

        int bestScore = -1;
        string bestName = "";
        foreach (KeyValuePair<int, int> entry in _killsByConnectionId)
        {
            if (entry.Value <= bestScore) continue;

            bestScore = entry.Value;
            bestName = GetPlayerName(entry.Key);
        }

        return bestName;
    }

    private string GetResultTitle()
    {
        if (_phase != MatchPhase.Result)
            return "";

        if (matchMode == MatchMode.TeamDeathmatch)
            return $"{GetWinnerName()} WINS";

        return string.IsNullOrWhiteSpace(_winnerName) ? "DRAW" : $"{_winnerName} WINS";
    }

    private string BuildScoreboard()
    {
        StringBuilder builder = new StringBuilder();
        if (matchMode == MatchMode.TeamDeathmatch)
        {
            builder.Append("BLUE:");
            builder.Append(_blueScore);
            builder.AppendLine();
            builder.Append("RED:");
            builder.Append(_redScore);
            builder.AppendLine();
        }

        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            if (conn == null || conn.identity == null) continue;

            string name = GetPlayerName(conn);
            int kills = GetKills(conn.connectionId);
            builder.Append(name);
            if (matchMode == MatchMode.TeamDeathmatch)
            {
                builder.Append(" [");
                builder.Append(GetTeamName(GetTeamId(conn)));
                builder.Append(']');
            }
            builder.Append(':');
            builder.Append(kills);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private string GetPlayerName(NetworkConnectionToClient conn)
    {
        return conn != null ? GetPlayerName(conn.connectionId) : "";
    }

    private string GetPlayerName(int connectionId)
    {
        if (!NetworkServer.connections.TryGetValue(connectionId, out NetworkConnectionToClient conn))
            return $"Player {connectionId}";

        PlayerController player = conn.identity != null ? conn.identity.GetComponent<PlayerController>() : null;
        if (player != null && !string.IsNullOrWhiteSpace(player.playerName))
            return player.playerName;

        return $"Player {connectionId}";
    }

    private void RefreshSpawnPoints()
    {
        NetworkStartPosition[] networkStartPositions = FindObjectsOfType<NetworkStartPosition>();
        if (networkStartPositions.Length == 0)
        {
            GameObject fallback = GameObject.Find("SpawnPoint");
            spawnPoints = fallback != null ? new[] { fallback.transform } : new[] { transform };
            return;
        }

        spawnPoints = new Transform[networkStartPositions.Length];
        for (int i = 0; i < networkStartPositions.Length; i++)
            spawnPoints[i] = networkStartPositions[i].transform;
    }
}
