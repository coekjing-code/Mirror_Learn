using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class UINetBtn : MonoBehaviour
{
    public Button btnHost;
    public Button btnJoin;
    public Button btnDisConnect;

    void Start()
    {
        btnHost.onClick.AddListener(()=>
        {
            NetworkManager.singleton.StartHost();
            Debug.Log("创建主机房间成功");
        });
        btnJoin.onClick.AddListener(()=>
        {
            NetworkManager.singleton.StartClient();
            Debug.Log("加入房间");
        });
        btnDisConnect.onClick.AddListener(()=>
        {
            if(NetworkServer.active)
                NetworkManager.singleton.StopHost();
            else
                NetworkManager.singleton.StopClient();
            Debug.Log("退出房间断开连接");
        });
    }
}