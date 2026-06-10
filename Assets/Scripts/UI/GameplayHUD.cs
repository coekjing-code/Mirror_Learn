using UnityEngine;
using Mirror;

public class GameplayHUD : MonoBehaviour
{
    public bool showReticle = true;
    public bool showHealth = true;
    public bool showMovementMode = true;
    public bool showMatchStatus = true;
    public Color reticleColor = new Color(1f, 0.08f, 0.04f, 0.9f);
    public Color healthBackColor = new Color(0f, 0f, 0f, 0.55f);
    public Color healthFillColor = new Color(0.15f, 0.9f, 0.35f, 0.9f);

    private GUIStyle _labelStyle;
    private GUIStyle _largeLabelStyle;
    private GUIStyle _scoreboardStyle;
    private static float _hitMarkerEndTime;
    private static bool _criticalHitMarker;
    private static float _damageFeedbackEndTime;
    private bool _localReady;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!NetworkClient.isConnected)
        {
            _localReady = false;
            return;
        }

        if (Input.GetKeyDown(KeyCode.F5) && MatchManager.HasClientSnapshot)
        {
            MatchSnapshotMsg snapshot = MatchManager.ClientSnapshot;
            if ((MatchPhase)snapshot.phase == MatchPhase.WaitingForPlayers && snapshot.readyRequired)
            {
                _localReady = !_localReady;
                NetworkClient.Send(new PlayerReadyMsg { isReady = _localReady });
            }
        }
    }

    private void OnGUI()
    {
        PlayerController player = PlayerController.LocalPlayerInstance;
        if (player == null) return;

        if (showReticle)
            DrawReticle(player.RemoteIsAiming || IsLocalAiming(player));

        DrawHitMarker();
        DrawDamageFeedback();

        if (showHealth)
            DrawHealth(player);

        DrawAmmo(player);
        DrawDeathState(player);

        if (showMovementMode && IsLocalAiming(player))
            DrawMovementMode();

        if (showMatchStatus)
            DrawMatchStatus();
    }

    private void DrawReticle(bool isAiming)
    {
        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        float length = isAiming ? 8f : 12f;
        float gap = isAiming ? 4f : 7f;
        float thickness = isAiming ? 2f : 3f;

        Color oldColor = GUI.color;
        GUI.color = reticleColor;
        GUI.DrawTexture(new Rect(centerX - gap - length, centerY - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerX + gap, centerY - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerX - thickness * 0.5f, centerY - gap - length, thickness, length), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerX - thickness * 0.5f, centerY + gap, thickness, length), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerX - 1f, centerY - 1f, 2f, 2f), Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    public static void NotifyHitMarker(bool isCritical)
    {
        _criticalHitMarker = isCritical;
        _hitMarkerEndTime = Time.time + 0.18f;
    }

    public static void NotifyDamage()
    {
        _damageFeedbackEndTime = Time.time + 0.35f;
    }

    private void DrawHitMarker()
    {
        if (Time.time > _hitMarkerEndTime) return;

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        float alpha = Mathf.Clamp01((_hitMarkerEndTime - Time.time) / 0.18f);
        float size = _criticalHitMarker ? 14f : 10f;
        float thickness = _criticalHitMarker ? 3f : 2f;

        Color oldColor = GUI.color;
        GUI.color = _criticalHitMarker ? new Color(1f, 0.15f, 0.05f, alpha) : new Color(1f, 1f, 1f, alpha);
        GUI.DrawTexture(new Rect(centerX - size, centerY - size, size, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerX, centerY - size, size, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerX - size, centerY + size, size, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerX, centerY + size, size, thickness), Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private void DrawDamageFeedback()
    {
        if (Time.time > _damageFeedbackEndTime) return;

        float alpha = Mathf.Clamp01((_damageFeedbackEndTime - Time.time) / 0.35f) * 0.38f;
        Color oldColor = GUI.color;
        GUI.color = new Color(1f, 0f, 0f, alpha);
        float thickness = 28f;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0f, Screen.height - thickness, Screen.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0f, 0f, thickness, Screen.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(Screen.width - thickness, 0f, thickness, Screen.height), Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private void DrawHealth(PlayerController player)
    {
        const float width = 220f;
        const float height = 22f;
        Rect backRect = new Rect(24f, Screen.height - 52f, width, height);
        float healthPercent = player.maxHealth > 0 ? Mathf.Clamp01((float)player.CurrentHealth / player.maxHealth) : 0f;

        Color oldColor = GUI.color;
        GUI.color = healthBackColor;
        GUI.DrawTexture(backRect, Texture2D.whiteTexture);

        GUI.color = healthFillColor;
        GUI.DrawTexture(new Rect(backRect.x, backRect.y, backRect.width * healthPercent, backRect.height), Texture2D.whiteTexture);

        EnsureLabelStyle();
        GUI.color = Color.white;
        GUI.Label(backRect, $"{player.CurrentHealth}/{player.maxHealth}", _labelStyle);
        GUI.color = oldColor;
    }

    private void DrawAmmo(PlayerController player)
    {
        CombatSystem combat = player.combatSystem;
        if (combat == null) return;

        EnsureLabelStyle();

        Rect rect = new Rect(Screen.width - 202f, Screen.height - 72f, 178f, 44f);
        Color oldColor = GUI.color;
        GUI.color = healthBackColor;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        string ammoText = combat.IsReloading
            ? "RELOADING"
            : $"{combat.CurrentAmmo}/{combat.MagazineSize}  {combat.CurrentReserveAmmo}";

        GUI.Label(new Rect(rect.x, rect.y + 2f, rect.width, 22f), ammoText, _largeLabelStyle);
        GUI.Label(new Rect(rect.x, rect.y + 23f, rect.width, 18f), $"B  {combat.FireModeLabel}", _labelStyle);
        GUI.color = oldColor;
    }

    private void DrawDeathState(PlayerController player)
    {
        if (!player.IsDead) return;

        EnsureLabelStyle();
        Rect rect = new Rect(Screen.width * 0.5f - 160f, Screen.height * 0.5f - 44f, 320f, 88f);
        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x, rect.y + 12f, rect.width, 30f), "ELIMINATED", _largeLabelStyle);
        GUI.Label(new Rect(rect.x, rect.y + 48f, rect.width, 24f), $"RESPAWN IN {Mathf.CeilToInt(player.RespawnRemaining)}", _labelStyle);
        GUI.color = oldColor;
    }

    private bool IsLocalAiming(PlayerController player)
    {
        return player.motionCore != null && player.motionCore.MotionData.AimInput;
    }

    private void DrawMovementMode()
    {
        EnsureLabelStyle();

        Rect rect = new Rect(24f, Screen.height - 82f, 92f, 22f);
        Color oldColor = GUI.color;
        GUI.color = healthBackColor;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(rect, "AIM WALK", _labelStyle);
        GUI.color = oldColor;
    }

    private void DrawMatchStatus()
    {
        if (!MatchManager.HasClientSnapshot) return;

        MatchSnapshotMsg snapshot = MatchManager.ClientSnapshot;
        EnsureLabelStyle();

        Rect statusRect = new Rect(Screen.width * 0.5f - 170f, 18f, 340f, 48f);
        Color oldColor = GUI.color;
        GUI.color = healthBackColor;
        GUI.DrawTexture(statusRect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(statusRect, GetMatchStatusText(snapshot), _largeLabelStyle);

        DrawScoreboard(snapshot);
        DrawResultPanel(snapshot);
        GUI.color = oldColor;
    }

    private void DrawScoreboard(MatchSnapshotMsg snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.scoreboard)) return;

        Rect boardRect = new Rect(Screen.width - 194f, 18f, 170f, 112f);
        GUI.color = healthBackColor;
        GUI.DrawTexture(boardRect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(boardRect.x + 10f, boardRect.y + 8f, boardRect.width - 20f, boardRect.height - 16f), "SCORE\n" + snapshot.scoreboard, _scoreboardStyle);
    }

    private string GetMatchStatusText(MatchSnapshotMsg snapshot)
    {
        MatchPhase phase = (MatchPhase)snapshot.phase;
        MatchMode mode = (MatchMode)snapshot.mode;
        int seconds = Mathf.CeilToInt(snapshot.phaseTimeRemaining);
        string modeLabel = mode == MatchMode.TeamDeathmatch ? "TDM" : "FFA";

        switch (phase)
        {
            case MatchPhase.WaitingForPlayers:
                if (snapshot.readyRequired)
                    return $"{modeLabel} READY {snapshot.readyPlayers}/{snapshot.connectedPlayers}  F5 {(_localReady ? "UNREADY" : "READY")}";

                return $"{modeLabel} WAITING {snapshot.connectedPlayers}/{snapshot.minPlayersToStart}";
            case MatchPhase.Countdown:
                return $"STARTING IN {seconds}";
            case MatchPhase.Playing:
                if (mode == MatchMode.TeamDeathmatch)
                    return $"{FormatTime(seconds)}  BLUE {snapshot.blueScore}/{snapshot.scoreLimit}  RED {snapshot.redScore}/{snapshot.scoreLimit}";

                return $"{FormatTime(seconds)}  LEADER {snapshot.leaderName} {snapshot.leadingScore}/{snapshot.scoreLimit}";
            case MatchPhase.Result:
                return $"{snapshot.resultTitle}  {seconds}";
            default:
                return "";
        }
    }

    private void DrawResultPanel(MatchSnapshotMsg snapshot)
    {
        if ((MatchPhase)snapshot.phase != MatchPhase.Result) return;

        EnsureLabelStyle();
        Rect rect = new Rect(Screen.width * 0.5f - 210f, Screen.height * 0.5f - 88f, 420f, 176f);
        GUI.color = new Color(0f, 0f, 0f, 0.78f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x, rect.y + 18f, rect.width, 32f), snapshot.resultTitle, _largeLabelStyle);
        GUI.Label(new Rect(rect.x + 24f, rect.y + 62f, rect.width - 48f, 88f), snapshot.scoreboard, _scoreboardStyle);
    }

    private string FormatTime(int totalSeconds)
    {
        int minutes = Mathf.Max(0, totalSeconds) / 60;
        int seconds = Mathf.Max(0, totalSeconds) % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    private void EnsureLabelStyle()
    {
        if (_labelStyle != null) return;

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13,
            fontStyle = FontStyle.Bold
        };

        _largeLabelStyle = new GUIStyle(_labelStyle)
        {
            fontSize = 16
        };

        _scoreboardStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
    }
}
