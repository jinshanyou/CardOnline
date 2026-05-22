using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using UnityEngine;

//对服务器socket总机的封装
public class ServerSocket
{
    public Socket server;
    //将分机全都存在字典中方便对其操作
    private Dictionary<int , ClientSocket> clients = new Dictionary<int , ClientSocket>();
    private bool isServerRunning = false; // 💡 新增：标记服务器运行状态
    //待删除的分机
    public List<ClientSocket> delayDelSocket = new List<ClientSocket>();

    //注册主机
    public void Start(string ip, int port, int num)
    {
        server = new Socket(AddressFamily.InterNetwork , SocketType.Stream , ProtocolType.Tcp);
        IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(ip), port);

        try
        {
            server.Bind(endPoint);
            server.Listen(num);
            isServerRunning = true; // 💡 标记启动

            //等待客户端上线来注册与其连接的分机
            server.BeginAccept(AcceptCallBack , null);
            ThreadPool.QueueUserWorkItem(DelClient);
            Debug.Log("连接了成员");
        }
        catch (Exception)
        {

            throw;
        }
    }

    /// <summary>
    /// 注册分机的回调函数
    /// </summary>
    /// <param name="result">得到了如分机socket这样重要参数的参数</param>
    public void AcceptCallBack(IAsyncResult result)
    {
        try
        {
            Socket socket = server.EndAccept(result);
            ClientSocket client = new ClientSocket(socket);
            lock (clients)
            {
                clients.Add(client.clientId, client);
            }
        
            // 💡 加上这句高亮的打印！
            Debug.Log($"<color=green>[服务端] 收到连接！已为其分配分机号: {client.clientId}</color>");
            // 💡 满 2 人了！（房主自己占1个，外面的客机占1个）
            if (clients.Count == 2)
            {
                Debug.Log("<color=yellow>[服务端] 人数已满2人，全服广播开战！</color>");
        
                SyncStartBattle startMsg = new SyncStartBattle();
                startMsg.RandomSeed = new System.Random().Next(1000, 9999); // 房主生成唯一的洗牌种子

                BroadMsg(startMsg); // 告诉所有人切画面！
            }
            server.BeginAccept(AcceptCallBack , null);
        }
        catch (Exception e)
        {
            // 💡 绝对不要用 throw，改成 Debug.LogError 把错误暴露在 Unity 面板上！
            Debug.LogError($"[服务端连接断开] {e.Message}");
        } 
    }


    //广播测试
    public void BroadMsg(IMessage msg)
    {
        foreach (var client in clients.Values)
        {                
            client.SendMsg(msg);
        }
    }

    //将断开的客户加入待删列表
    public void AddToDelayDel(ClientSocket socket)
    {
        lock (clients)
        {
            if (!delayDelSocket.Contains(socket))
            {
                delayDelSocket.Add(socket);
                // Console.WriteLine($"客户端 {socket.clientId} 已加入待删除队列");
            }
        }
    }

    //关闭分机
    public void CloseClient(ClientSocket socket)
    {
        lock (clients)
        {
            socket.Close();
            if (clients.ContainsKey(socket.clientId))
            {
                clients.Remove(socket.clientId);
            }
        }
    }
    
    // 💡 新增：优雅关闭整个服务器，释放所有分机
    public void CloseAll()
    {
        isServerRunning = false; // 停止 DelClient 线程

        lock (clients)
        {
            foreach (var client in clients.Values)
            {
                client.Close(); // 掐断每一个连进来的客机
            }
            clients.Clear();
            delayDelSocket.Clear();
        }

        if (server != null)
        {
            try
            {
                server.Close();
            }
            catch (Exception e)
            {
                Debug.LogError($"[服务端关闭异常] {e.Message}");
            }
            server = null;
        }
        Debug.Log("<color=red>[服务端] 服务器已彻底关闭，端口已释放</color>");
    }

    //将待删列表清空使用单独线程池控制注意使用lock防止多线程数据冲突
    private void DelClient(object? obj)
    {
        while (isServerRunning)
        {
            lock (clients)
            {
                if (clients.Count > 0)
                {

                    for (int i = 0; i < delayDelSocket.Count; i++)
                    {
                        CloseClient(delayDelSocket[i]);
                    }

                    delayDelSocket.Clear();

                }
            }

            Thread.Sleep(200);
        }
    }


}

