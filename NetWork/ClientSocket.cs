using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using UnityEngine;

//本质是对和客户端连接的Socket的封装
public class ClientSocket
{
    public Socket socket;
    //分机id
    public int clientId;
    //第一个分机的id注册(初始化)时使用
    private static int CLIENT_BEGIN_ID = 1;

    //缓存接收的文件
    private byte[] cacheBytes = new byte[1024 * 1024];
    //游标
    private int cacheLen = 0;

    // 标记是否已经调用过清理，防重复断开
    private bool isClosed = false;

    //接收上次心跳是什么时候
    private long lastHeartTime = -1;
    //心跳超时时间
    private int OUT_TIME = 10;

    //注册分机
    public ClientSocket(Socket socket)
    {
        this.socket = socket;
        clientId = CLIENT_BEGIN_ID;
        CLIENT_BEGIN_ID++;

        //一旦初始化马上开始接收消息
        ReceiveMsg();
        //开一条线程池来检测心跳消息
        ThreadPool.QueueUserWorkItem(CheckHeart);
    }

    //简单的发送消息
    public void SendMsg(IMessage  sendData)
    {
        if (socket!= null && socket.Connected)
        {
            if (!TcpAsyncMgr.Instance.TryGetMsgIdByType(sendData.GetType(), out int msgID))
            {
                Debug.LogError($"[服务端发错] 未注册的协议类型，无法发送: {sendData.GetType().Name}");
                return;
            }
            // 获取 Proto 序列化数据
            byte[] bodyBytes = sendData.ToByteArray();
            int bodyLength = bodyBytes.Length;
            // 封装包头 [ MsgID(4) ] + [ 长度(4) ] + [ 数据包体(...) ]
            byte[] finalBytes = new byte[8 + bodyLength];
            BitConverter.GetBytes(msgID).CopyTo(finalBytes, 0);
            BitConverter.GetBytes(bodyLength).CopyTo(finalBytes, 4);
            bodyBytes.CopyTo(finalBytes, 8);
            
            socket.BeginSend(finalBytes , 0 , finalBytes.Length , SocketFlags.None , SendCallBack, null);
        }
    }

    //接收消息但是用的是APM模型
    public void ReceiveMsg()
    {
        // 🔥 修复1：使用 Length - cacheLen 防止数组越界！
        socket.BeginReceive(cacheBytes, cacheLen, cacheBytes.Length - cacheLen, SocketFlags.None, ReceiveCallBack, socket);
        //socket.BeginReceive(cacheBytes , cacheLen , cacheBytes.Length , SocketFlags.None , ReceiveCallBack , socket);
    }

    /// <summary>
    /// 接收回调函数
    /// </summary>
    /// <param name="result">取得的参数存储在这个参数里</param>
    public void ReceiveCallBack(IAsyncResult result)
    {
        try
        {
            //检测socket是否还存在
            if (socket == null || !socket.Connected)
            {
                return;
            }

            // 🔥 修复2：一定要拿出一个临时变量接总长度
            int receiveLen = socket.EndReceive(result);
            // 🔥 修复3：如果是0，代表客户端已经物理拔线了
            if (receiveLen == 0)
            {
                Debug.Log($"客户端 {clientId} 物理连接断开");
                //ServerSocket.Instance.AddToDelayDel(this);
                return; // 坚决不能往下执行了
            }
            //成功读到消息后记得把缓存池的游标也改变,不仅是用来接收文件的长度
            //更是为了后面处理数据剪切时来获得残余文件的头部游标位置
            cacheLen += receiveLen;
            
            int msgID = 0;
            int nowIndex = 0;
            int msgBoydLength = 0;
            //保证缓存文件长度大于8获取头文件
            while (true)
            {
                if (cacheLen - nowIndex >= 8)
                {
                    msgID = BitConverter.ToInt32(cacheBytes, nowIndex);
                    nowIndex += 4;
                    msgBoydLength = BitConverter.ToInt32(cacheBytes, nowIndex);
                    nowIndex += 4;
                }
                else
                {
                    msgBoydLength = -1;
                }
                if (msgBoydLength != -1 && cacheLen - nowIndex >= msgBoydLength)
                {
                    IMessage receiveMsg = null;
                    // 从字典里拿出解析器（Parser）进行解包
                    if (TcpAsyncMgr.Instance.TryGetMsgParser(msgID, out MessageParser parser))
                    {
                        try
                        {
                            byte[] bodyData = new byte[msgBoydLength];
                            Array.Copy(cacheBytes, nowIndex, bodyData, 0, msgBoydLength);
                            
                            receiveMsg = parser.ParseFrom(bodyData);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[服务端解析错误] MsgID:{msgID}, {ex.Message}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[服务端] 未知的消息ID，无法反序列化: {msgID}");
                    }
                    nowIndex += msgBoydLength; // 跳过未知包体，防止死循环
                    if (receiveMsg != null)
                    {
                        HandleMsg(receiveMsg);
                    }
                    if (cacheLen == nowIndex)
                    {
                        cacheLen = 0;
                        //这里与客户端不同是因为不用setbuffer,因为在方法里实现了APM中少了这一步而是直接完成
                        if (socket.Connected)
                        {
                            ReceiveMsg();
                        }
                        break;
                    }

                }
                //没有读到文件体也有可能没读到头文件
                else
                {
                    //读到了头文件
                    if (msgBoydLength != -1)
                    {
                        nowIndex -= 8;
                    }
                    //将残存的数据从nowIndex的缓存数据移动到缓存数据开头
                    Array.Copy(cacheBytes, nowIndex, cacheBytes, 0, cacheLen - nowIndex);
                    //水位(游标)下降
                    cacheLen = cacheLen - nowIndex;
                    if (socket.Connected)
                    {
                        ReceiveMsg();
                    }
                    break;

                }
            }
        }
        catch (SocketException e)
        {
            Debug.Log(e.Message);
        }
    }

    //处理文件
    private void HandleMsg(IMessage data)
    {
        if (data is HeartMsg)
        {
            lastHeartTime = DateTime.Now.Ticks / TimeSpan.TicksPerSecond;
            // 💡 打印出是谁发来的心跳！
            Debug.Log($"<color=orange>[服务端-分机{clientId}] 收到心跳包！</color>");
        }
        else if (data is QuitMsg)
        {
            Debug.Log($"客户端 {clientId} 主动发送了推出版消息。");
        }
        else if (data is ReqPlayCard)
        {
            ReqPlayCardHandler handler = new ReqPlayCardHandler();
            handler.Execute(data);
        }
        else if (data is ReqEndTurn)
        {
            ReqEndTurnHandler handler = new ReqEndTurnHandler();
            handler.Execute(data);
        }
        // {
        //     ReqPlayCard msg = (ReqPlayCard)data;
        //     // 💡 只有主机（Server端）才能处理 Req 请求！
        //     // 如果我们是普通客机，直接无视（或者底层根本就不分发给客机）
        //     if (NetworkMgr.Instance.server == null) return;
        //
        //     Debug.Log($"[主机裁判] 收到玩家请求出牌：{msg.InstanceId}，开始校验...");
        //
        //     // 校验逻辑 (这里简化，假装他有费用且是他的回合)
        //     // 去 BattleMgr 查出是哪张卡
        //     var cardInst = BattleMgr.Instance.myPlayer.GetHandCardByInstanceId(msg.InstanceId);
        //     if (cardInst != null)
        //     {
        //         // 校验通过！主机生成 Sync 广播消息
        //         SyncPlayCard syncMsg = new SyncPlayCard();
        //         syncMsg.PlayerUid = BattleMgr.Instance.myPlayer.EntityUid; // 目前先默认是房主
        //         syncMsg.InstanceId = msg.InstanceId;
        //         syncMsg.CardId = cardInst.CardId;
        //
        //         // 用大喇叭告诉全服（包括房主自己的客机端）！
        //         NetworkMgr.Instance.server.BroadMsg(syncMsg);
        //     }     
        // }
        
    }

    public void SendCallBack(IAsyncResult result)
    {
        try
        {
            //Debug.Log("成功发送消息");
            socket.EndSend(result);
        }
        catch (Exception ex)
        {
            Debug.Log($"发送数据发生异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 检测心跳
    /// </summary>
    /// <param name="obj">线程池要的参数</param>
    private void CheckHeart(object obj)
    {
        while (!isClosed)
        {
            //检测现在到上次心跳时间是否超时了
            if (lastHeartTime != -1 && (DateTime.Now.Ticks / TimeSpan.TicksPerSecond - lastHeartTime) >= OUT_TIME)
            {
                Debug.Log($"客户端 {clientId} 心跳超时！");
                //ServerSocket.Instance.AddToDelayDel(this);
                break;
            }
            Thread.Sleep(5000);
        }
    }

    //关闭自己
    public void Close()
    {
        if (isClosed) return;
        isClosed = true;
        if (socket != null)
        {
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
            socket = null;
        }
    }


}

