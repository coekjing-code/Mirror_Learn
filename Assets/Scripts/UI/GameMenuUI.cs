using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class GameMenuUI : MonoBehaviour
{
    public Color panelColor = new Color(0.04f, 0.05f, 0.06f, 0.92f);
    public Color buttonColor = new Color(0.12f, 0.16f, 0.2f, 0.95f);
    public Color textColor = Color.white;

    private Canvas _canvas;
    private GameObject _mainPanel;
    private GameObject _pausePanel;
    private CustomNetManager _netManager;
    private int _defaultMinPlayersToStart = 2;
    private int _selectedBotCount = 3;
    private BotDifficulty _selectedBotDifficulty = BotDifficulty.Normal;
    private bool _pauseOpen;
    private Text _botCountLabel;
    private Text _botDifficultyLabel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAtStartup()
    {
        if (FindObjectOfType<GameMenuUI>() != null) return;

        GameObject menuObject = new GameObject("GameMenuUI");
        menuObject.AddComponent<GameMenuUI>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        _netManager = GetNetManager();
        if (_netManager != null)
            _defaultMinPlayersToStart = _netManager.matchMinPlayersToStart;

        DisableLegacyMenus();
        BuildCanvas();
        ShowMainMenu();
    }

    private void Update()
    {
        bool networkActive = NetworkManager.singleton != null && NetworkManager.singleton.isNetworkActive;
        if (!networkActive)
        {
            if (!_mainPanel.activeSelf)
                ShowMainMenu();
            return;
        }

        if (_mainPanel.activeSelf)
            _mainPanel.SetActive(false);

        if (Input.GetKeyDown(KeyCode.Escape))
            SetPauseOpen(!_pauseOpen);
    }

    private void StartTraining()
    {
        _netManager = GetNetManager();
        if (_netManager != null)
        {
            _netManager.matchMinPlayersToStart = 1;
            _netManager.spawnTrainingBots = true;
            _netManager.trainingBotCount = _selectedBotCount;
            _netManager.botDifficulty = _selectedBotDifficulty;
            _netManager.matchMode = MatchMode.FreeForAll;
            _netManager.requireReadyState = false;
        }

        StartHostInternal();
    }

    private void StartHostFreeForAll()
    {
        StartHost(MatchMode.FreeForAll);
    }

    private void StartHostTeamDeathmatch()
    {
        StartHost(MatchMode.TeamDeathmatch);
    }

    private void StartHost(MatchMode mode)
    {
        if (NetworkManager.singleton == null || NetworkManager.singleton.isNetworkActive) return;

        _netManager = GetNetManager();
        if (_netManager != null)
        {
            _netManager.matchMinPlayersToStart = _selectedBotCount > 0 ? 1 : _defaultMinPlayersToStart;
            _netManager.spawnTrainingBots = _selectedBotCount > 0;
            _netManager.trainingBotCount = _selectedBotCount;
            _netManager.matchMode = mode;
            _netManager.requireReadyState = true;
            _netManager.botDifficulty = _selectedBotDifficulty;
        }

        StartHostInternal();
    }

    private void StartHostInternal()
    {
        if (NetworkManager.singleton == null || NetworkManager.singleton.isNetworkActive) return;

        NetworkManager.singleton.StartHost();
        _mainPanel.SetActive(false);
        LockCursor();
    }

    private void JoinGame()
    {
        if (NetworkManager.singleton == null || NetworkManager.singleton.isNetworkActive) return;

        NetworkManager.singleton.StartClient();
        _mainPanel.SetActive(false);
        LockCursor();
    }

    private void ResumeGame()
    {
        SetPauseOpen(false);
    }

    private void ExitToMenu()
    {
        StopNetwork();
        SetPauseOpen(false);
        ShowMainMenu();
    }

    private void QuitGame()
    {
        StopNetwork();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void StopNetwork()
    {
        if (NetworkManager.singleton == null || !NetworkManager.singleton.isNetworkActive) return;

        if (NetworkServer.active && NetworkClient.isConnected)
            NetworkManager.singleton.StopHost();
        else if (NetworkServer.active)
            NetworkManager.singleton.StopServer();
        else
            NetworkManager.singleton.StopClient();

        _netManager = GetNetManager();
        if (_netManager != null)
        {
            _netManager.matchMinPlayersToStart = _defaultMinPlayersToStart;
            _netManager.spawnTrainingBots = false;
            _netManager.requireReadyState = true;
        }
    }

    private void ShowMainMenu()
    {
        _mainPanel.SetActive(true);
        _pausePanel.SetActive(false);
        _pauseOpen = false;
        UnlockCursor();
    }

    private void SetPauseOpen(bool open)
    {
        _pauseOpen = open;
        _pausePanel.SetActive(open);

        if (open)
            UnlockCursor();
        else
            LockCursor();
    }

    private void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("GameMenuCanvas");
        canvasObject.transform.SetParent(transform, false);
        _canvas = canvasObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();

        _mainPanel = CreatePanel("MainMenu", new Vector2(460f, 500f));
        AddTitle(_mainPanel.transform, "MIRROR TPS");
        AddButton(_mainPanel.transform, "Single Player Training", StartTraining);
        AddButton(_mainPanel.transform, "Host FFA", StartHostFreeForAll);
        AddButton(_mainPanel.transform, "Host Team Deathmatch", StartHostTeamDeathmatch);
        AddButton(_mainPanel.transform, "Join Match", JoinGame);
        _botCountLabel = AddButton(_mainPanel.transform, "", CycleBotCount);
        _botDifficultyLabel = AddButton(_mainPanel.transform, "", CycleBotDifficulty);
        AddButton(_mainPanel.transform, "Quit Game", QuitGame);
        RefreshOptionLabels();

        _pausePanel = CreatePanel("PauseMenu", new Vector2(360f, 240f));
        AddTitle(_pausePanel.transform, "PAUSED");
        AddButton(_pausePanel.transform, "Continue", ResumeGame);
        AddButton(_pausePanel.transform, "Exit To Menu", ExitToMenu);
        AddButton(_pausePanel.transform, "Quit Game", QuitGame);
        _pausePanel.SetActive(false);
    }

    private CustomNetManager GetNetManager()
    {
        return NetworkManager.singleton as CustomNetManager;
    }

    private GameObject CreatePanel(string name, Vector2 size)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(_canvas.transform, false);

        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;

        Image image = panel.AddComponent<Image>();
        image.color = panelColor;

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 28, 28);
        layout.spacing = 14f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        return panel;
    }

    private void AddTitle(Transform parent, string title)
    {
        Text text = CreateText("Title", parent, title, 30, FontStyle.Bold);
        LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 56f;
    }

    private Text AddButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(label);
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = buttonColor;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 44f;

        Text text = CreateText("Text", buttonObject.transform, label, 16, FontStyle.Bold);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        return text;
    }

    private void CycleBotCount()
    {
        _selectedBotCount++;
        if (_selectedBotCount > 8)
            _selectedBotCount = 0;

        RefreshOptionLabels();
    }

    private void CycleBotDifficulty()
    {
        _selectedBotDifficulty = (BotDifficulty)(((int)_selectedBotDifficulty + 1) % 3);
        RefreshOptionLabels();
    }

    private void RefreshOptionLabels()
    {
        if (_botCountLabel != null)
            _botCountLabel.text = $"AI Players: {_selectedBotCount}";
        if (_botDifficultyLabel != null)
            _botDifficultyLabel.text = $"AI Difficulty: {_selectedBotDifficulty}";
    }

    private Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle fontStyle)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = textColor;
        text.alignment = TextAnchor.MiddleCenter;

        return text;
    }

    private void DisableLegacyMenus()
    {
        foreach (UINetBtn menu in FindObjectsOfType<UINetBtn>(true))
            menu.gameObject.SetActive(false);

        foreach (ServerModeUI menu in FindObjectsOfType<ServerModeUI>(true))
            menu.gameObject.SetActive(false);
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
