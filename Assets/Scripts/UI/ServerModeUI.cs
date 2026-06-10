using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class ServerModeUI : MonoBehaviour
{
    public Button btnStartServer;
    public Button btnConnectClient;
    public Button btnStopNet;

    void Start()
    {
        btnStartServer.onClick.AddListener(OpenDedicatedServer);
        btnConnectClient.onClick.AddListener(ConnectToServer);
        btnStopNet.onClick.AddListener(StopAllNet);
    }

    // 开启独立专用服务器
    void OpenDedicatedServer()
    {
        // 先关闭原有网络
        if (NetworkManager.singleton.isNetworkActive)
            NetworkManager.singleton.StopHost();
        
        // 启动纯服务端
        NetworkManager.singleton.StartServer();
        Debug.Log("✅ 独立专用服务器启动成功，等待客户端连接...");
    }

    // 客户端连接专用服务器
    void ConnectToServer()
    {
        if (NetworkManager.singleton.isNetworkActive) return;
        NetworkManager.singleton.StartClient();
        Debug.Log("🔗 客户端正在连接服务器...");
    }

    // 停止所有网络
    void StopAllNet()
    {
        if (NetworkManager.singleton.isNetworkActive)
        {
            NetworkManager.singleton.StopHost();
            Debug.Log("❌ 网络全部关闭");
        }
    }
}