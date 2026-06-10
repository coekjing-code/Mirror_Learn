using Mirror;

// 1.玩家聊天消息
public struct ChatMsg : NetworkMessage
{
    public string sendName;
    public string content;
}

// 2.玩家位置同步消息
public struct PlayerPosMsg : NetworkMessage
{
    public float x;
    public float y;
    public float z;
}

// 3.扣血请求消息
public struct HurtMsg : NetworkMessage
{
    public int damage;
}

public struct MatchSnapshotMsg : NetworkMessage
{
    public int phase;
    public int mode;
    public float phaseTimeRemaining;
    public int connectedPlayers;
    public int minPlayersToStart;
    public int readyPlayers;
    public bool readyRequired;
    public int scoreLimit;
    public string leaderName;
    public int leadingScore;
    public int blueScore;
    public int redScore;
    public string resultTitle;
    public string scoreboard;
}

public struct PlayerReadyMsg : NetworkMessage
{
    public bool isReady;
}
