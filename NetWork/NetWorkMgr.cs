using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 继承自你写的 PersistentMonoSingleton，保证跨场景不销毁
public class NetworkMgr : PersistentMonoSingleton<NetworkMgr>
{
    // 老板手里拿着服务端的引用
    public ServerSocket server; 

    public void StartHost()
    {
        if (server != null)
        {
            Debug.LogWarning("主机已经启动过了！");
            return;
        }

        // 因为 ServerSocket 是普通 C# 类，所以我们可以放心大胆地 new！
        server = new ServerSocket();
        
        // 假设你把 ServerSocket 的 Start 方法改成了下面这样
        server.Start("127.0.0.1", 8888, 10); 
        Debug.Log("<color=green>主机启动成功！正在监听 8888 端口...</color>");

        // 【精髓】既然自己是主机，那自己也得作为客机连进去！
        JoinRoom("127.0.0.1"); 
    }

    public void JoinRoom(string ip)
    {
        Debug.Log($"<color=cyan>正在连接到房间 {ip}:8888 ...</color>");
        
        // 调用你早就写好的 TcpAsyncMgr 去连接服务端
        TcpAsyncMgr.Instance.Connect(ip, 8888, () => 
        {
            Debug.Log("<color=yellow>客机：成功连接到服务器！</color>");
        });
    }

    // 别忘了在关闭游戏时，老板要把打工仔遣散
    protected override void OnDestroy()
    {
        base.OnDestroy();
        // 如果这里有 Close 方法的话，调用一下防止端口被一直占用
        // server?.Close(); 
    }
    // 💡 新增：断开网络总入口
    public void Shutdown()
    {
        // 1. 关闭服务端
        if (server != null)
        {
            server.CloseAll();
            server = null;
        }

        // 2. 关闭客户端
        TcpAsyncMgr.Instance.CloseSocket();
    }
}
