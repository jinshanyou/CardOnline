using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using Google.Protobuf; // 引入 Protobuf 核心库

public class TcpAsyncMgr : PersistentMonoSingleton<TcpAsyncMgr>
{
    
    //本质是封装socket
    private Socket socket;

    //缓存池用来接收Receive的信息
    private byte[] cacheBytes = new byte[1024 * 1024];
    //缓存池的游标,在分包,粘包处理时有重要的作用
    private int cacheLength = 0;

    //将接收的文件存到队列里,然后在Update生命周期调用不用受到线程无法使用unity数据的影响
    //private Queue<SendData> ReceiveData = new Queue<SendData>();
    // 【修改点1】：泛型队列改为存 IMessage
    private Queue<IMessage> ReceiveData = new Queue<IMessage>();
    
    //用来lock接收文件防止多线程带来的数据意外
    private object receiveDataUsed = new object();

    //成员心跳信息为了节省一点性能,不用一直new心跳信息
    private HeartMsg heartMsg = new HeartMsg();

    //标识保证可以在主线程安全地触发连接成功逻辑
    private bool isConnectedStatus = false;
    private UnityAction onConnectEndAction;

    private Dictionary<int, IHandler> handlerDict = new Dictionary<int, IHandler>();
    
    //给你一个 ID，我返回一个能 new 出对应 SendData 实例的方法
    //private Dictionary<int, Func<SendData>> msgCreatorDict = new Dictionary<int, Func<SendData>>();
    
    // 【新增点3】：接收消息时，通过 ID 找到对应的 Protobuf 解析器 (Parser)
    private Dictionary<int, MessageParser> msgParserDict = new Dictionary<int, MessageParser>();
    // 【新增点4】：发送消息时，根据消息的 Type 反查它的 ID，为了封装表头
    private Dictionary<Type, int> msgTypeToIdDict = new Dictionary<Type, int>();
    
    private void AutoRegisterNetMsgs()
    {
        handlerDict.Clear();
        //msgCreatorDict.Clear();
        msgParserDict.Clear();
        msgTypeToIdDict.Clear();
        
        Assembly assembly = Assembly.GetExecutingAssembly();
        // 2. 拿到所有的类型 (Types)
        Type[] allTypes = assembly.GetTypes();
        
        foreach (var type in allTypes)
        {
            var attribute = type.GetCustomAttribute<NetMsgIDAttribute>();
            if (attribute == null)
            {
                continue;
            }
            int msgID = attribute.msgID;

            // 如果它继承自 SendData 
            if (typeof(IMessage).IsAssignableFrom(type) && !type.IsAbstract)
            {
                // 用反射生成一个闭包方法，存进创造器字典里
                // 这里用 Activator.CreateInstance 动态创建对象
                //msgCreatorDict.Add(msgID, () => (SendData)Activator.CreateInstance(type));
                // Protobuf 生成的类，都有一个静态属性叫 Parser，利用反射拿出来
                PropertyInfo parserProp = type.GetProperty("Parser", BindingFlags.Public | BindingFlags.Static);
                if (parserProp != null)
                {
                    MessageParser parser = (MessageParser)parserProp.GetValue(null);
                    msgParserDict.Add(msgID, parser);
                    msgTypeToIdDict.Add(type, msgID); // 记录发送用的反查表
                    Debug.Log($"[网络模块] 注册协议 Parser: {type.Name} -> ID:{msgID}");
                }
            }
            // 如果它实现了 IHandler 接口
            else if (typeof(IHandler).IsAssignableFrom(type) && !type.IsAbstract)
            {
                
                // Handler全局只需要一个实例，直接实例化存进去
                IHandler handlerInstance = (IHandler)Activator.CreateInstance(type);
                handlerDict.Add(msgID, handlerInstance);
                Debug.Log($"自动注册Handler类: {type.Name} -> ID:{msgID}");
                // 注意这里要改一点：需要获取 Handler 处理的具体泛型类型，绑给对应的 msgID
                // 工业界一般也会给 Handler 打 Attribute，这里按你原逻辑简写
                // *注：你可以通过接口泛型找到它的目标ID，这里假设你已经对应好了
            }

        }
    }
    
    protected override void OnSingletonAwake()
    {
        base.OnSingletonAwake();
        AutoRegisterNetMsgs();
    }

    /// <summary>
    /// 发送心跳信息
    /// </summary>
    [ContextMenu("心跳测试")]
    public void SendHeartBeatMsg()
    {
        SendMsg(heartMsg);
    }
    
    // Update is called once per frame
    void Update()
    {
        //当标识被激活时开始调用发送心跳
        if (isConnectedStatus)
        {
            //因为只要触发一次,所以记得取反
            isConnectedStatus = false;
            // 因为现在是在 Update 里，调用 InvokeRepeating 是绝对安全且符合规范的
            InvokeRepeating("SendHeartBeatMsg", 0, 5f);
            onConnectEndAction?.Invoke();
        }
        // 优化多线程取数据的安全性：
        // 一次性把队列里积压的包全取出来，防止一直占用锁
        int currentCount = 0;
        lock (receiveDataUsed)
        {
            currentCount = ReceiveData.Count;
        }
        // [优化] 改为 while 循环，一帧内处理完所有积压的网络消息，防止延迟堆积
        while (currentCount > 0)
        {
            IMessage data = null;
            //使用锁,并把要处理数据全都取出来
            lock (receiveDataUsed)
            {
                if (ReceiveData.Count > 0)
                {
                    data = ReceiveData.Dequeue();
                }
            }
            currentCount--;
            if (data != null)
            {
                Type dataType = data.GetType();
                if (!msgTypeToIdDict.TryGetValue(dataType, out int msgID))
                {
                    Debug.LogError($"未注册的协议类型，无法发送: {dataType.Name}");
                    return;
                }
                // [优化点]：安全获取 Handler 并判断防错
                if (handlerDict.TryGetValue(msgID, out IHandler handler))
                {
                    handler.Execute(data);
                }
                else
                {
                    //Debug.LogError($"收到了消息 ID:{data.GetId()}，但没有找到对应的 Handler！请检查 InitHandlers 是否注册。");
                }
            }
        }
    }

    /// <summary>
    /// 客户端连接服务端
    /// </summary>
    /// <param name="ip">ip地址</param>
    /// <param name="port">端口号</param>
    /// <param name="onConnetEnd">连接完成端口后的回调函数</param>
    public void Connect(string ip , int port , UnityAction onConnetEnd)
    {
        //判断是否已经连接
        if (socket != null && socket.Connected)
        {
            return;
        }
        //将回调函数存入委托中在unity生命周期中安全调用
        onConnectEndAction = onConnetEnd;
        //连接ip和端口
        IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(ip) , port);
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        
        //Socket异步连接EAP基于事件的异步模式的重要参数
        SocketAsyncEventArgs e = new SocketAsyncEventArgs();
        //往事件参数里填入服务端的ipendpoint为了连接服务端
        e.RemoteEndPoint = endPoint;
        //连接服务器时触发的回调函数,socket是调用了方法的回传的socket,args是异步方法结束后回传的事件参数,里面存了各种有用的参数给我们使用
        e.Completed += (socket , args) =>
        {
            //检测是否成功连接
            if (args.SocketError == SocketError.Success)
            {
                print("连接了服务器");
                //服务器连接成功时,就开启接受消息,不然错过时机,就不知道什么时候去接收消息了
                StartReceive();
                //前面要用的标识记得启动
                isConnectedStatus = true;
            }
        };
        //使用异步方法连接服务端
        socket.ConnectAsync(e);
    }


    /// <summary>
    /// 使用异步方法开启线程来不停接收服务端发送的消息
    /// </summary>
    private void StartReceive()
    {
        //异步方法要用的事件参数
        SocketAsyncEventArgs e2 = new SocketAsyncEventArgs();
        //把收取容器设置成缓存池,这样异步方法执行完后接收的数据就会从缓存池的游标位置开始存入接收的数据
        //,后面的参数是设置最大可接收数据,防止溢出
        e2.SetBuffer(cacheBytes, cacheLength, cacheBytes.Length - cacheLength);
        //异步接收完数据后的回调函数
        e2.Completed += ReceiveCallBack;

        //异步接收消息方法
        socket.ReceiveAsync(e2);
    }

    /// <summary>
    /// 异步接收完数据后的回调函数
    /// </summary>
    /// <param name="socket">调用异步方法回传的socket</param>
    /// <param name="args">接收完消息回传的事件参数</param>
    private void ReceiveCallBack(object socket , SocketAsyncEventArgs args)
    {
        //判断是否成功连接,并且BytesTransferred这个属性提供异步套接字作业中传送的字节数目，可接收或传送数据。
        //如果读取作业传回零，则远程端已关闭连接。所以记得要判断否则线程断开后线程无限接收消息
        if (args.SocketError == SocketError.Success && args.BytesTransferred > 0)
        {
            //成功读到消息后记得把缓存池的游标也改变,不仅是用来接收文件的长度
            //更是为了后面处理数据剪切时来获得残余文件的头部游标位置
            cacheLength += args.BytesTransferred;

            //消息分类的id用来处理不同的方法
            int msgID = 0;
            //分步处理读取文件的游标
            int nowIndex = 0;
            //文件体的长度和整个文件长度分开因为还有头文件
            int msgBoydLength = 0;
            //循环是为了处理粘包在剪切了一部分文件后可以继续处理后面的包体,缓存池的游标也上升了
            while (true)
            {
                //缓存池游标减去读文件游标是为了再次循环时检测的长度是残余文件的长度,8是头文件长度
                if (cacheLength - nowIndex >= 8)
                {
                    //获得头文件的信息id
                    msgID = BitConverter.ToInt32(cacheBytes, nowIndex);
                    //游标浮动
                    nowIndex += 4;
                    //获得文件体长度
                    msgBoydLength = BitConverter.ToInt32(cacheBytes, nowIndex);
                    nowIndex += 4;
                }
                else
                {
                    //将文件体长度置为负数用来后续判断是否获得文件体长度
                    msgBoydLength = -1;
                }
                //确保没有进入头文件不会去解读文件体,
                if (msgBoydLength != -1 && cacheLength - nowIndex >= msgBoydLength)
                {
                    //父类容器用来装载获得的数据
                    IMessage receiveMsg = null;
                    if (msgParserDict.TryGetValue(msgID, out MessageParser parser))
                    {
                        try
                        {
                            // 极速反序列化：只取包体长度的字节去转译
                            byte[] bodyData = new byte[msgBoydLength];
                            Array.Copy(cacheBytes, nowIndex, bodyData, 0, msgBoydLength);
                            //使用parser反序列化
                            receiveMsg = parser.ParseFrom(bodyData);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Protobuf 解析失败 ID:{msgID}, 错误:{ex.Message}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("未知的消息ID，无法反序列化: " + msgID);
                    }
                    // 没有类型也要游标浮动，不然会卡死
                    nowIndex += msgBoydLength;
                    if (receiveMsg != null)
                    {
                        //处理信息的方法
                        HandleMsg(receiveMsg);
                    }
                    //如果经历这轮解包后缓存池的浮标和读文件浮标相同说明读取的文件长度刚好等与包体的长度
                    //完美的解读没有后续的粘包或分包了,当然没有完全解读会后续循环继续解包
                    if (cacheLength == nowIndex)
                    {
                        //解包完成将浮标归为0,下次会从零存入数据
                        cacheLength = 0;
                        //为了再次调用接收记得设置数据参数中的容器
                        args.SetBuffer(cacheLength, cacheBytes.Length - cacheLength);
                        //args.SetBuffer(cacheLength, args.Buffer.Length);
                        //循环调用方法的延续确保可以继续进行文件的接收
                        this.socket.ReceiveAsync(args);
                        break;
                    }

                }
                //没有读到文件体也有可能没读到头文件
                else
                {
                    //如果读到了头文件
                    if (msgBoydLength != -1)
                    {
                        //记得把读文件游标放回,因为我们并没有真正的用到它,还得存会包里,不移动的话可能把头文件略过了
                        nowIndex -= 8;
                    }
                    //将残存的数据从nowIndex的后面的残留的缓存数据移动到缓存数据开头
                    Array.Copy(cacheBytes , nowIndex , cacheBytes, 0 ,cacheLength - nowIndex);
                    //水位(游标)下降
                    cacheLength = cacheLength - nowIndex;
                    //同上方法延续
                    args.SetBuffer(cacheLength, cacheBytes.Length - cacheLength);
                    //args.SetBuffer(cacheLength,args.Buffer.Length);
                    this.socket.ReceiveAsync(args);
                    break;
                }
            }
        }
        else
        {
            //失败了说明socket出问题了直接关闭
            // args.BytesTransferred == 0 走到这里
            Debug.LogWarning("服务器断开连接");
            if (socket != null)
            {
                CloseSocket();
            }
        }
    }

    //发送信息比较简单
    public void SendMsg(IMessage sendData)
    {
        //确保还在连接
        if (socket != null && socket.Connected)
        {
            Type msgType = sendData.GetType();
            if (!msgTypeToIdDict.TryGetValue(msgType, out int msgID))
            {
                Debug.LogError($"未注册的协议类型，无法发送: {msgType.Name}");
                return;
            }
            // 2. Protobuf 将类序列化为 Byte[]
            byte[] bodyBytes = sendData.ToByteArray();
            int bodyLength = bodyBytes.Length;
            // 3. 构建 8字节包头 + 包体 的最终发送数据
            // [ ID (4位) ] + [ 长度 (4位) ] + [ Body (...) ]
            byte[] finalBytes = new byte[8 + bodyLength];
            BitConverter.GetBytes(msgID).CopyTo(finalBytes, 0);
            BitConverter.GetBytes(bodyLength).CopyTo(finalBytes, 4);
            bodyBytes.CopyTo(finalBytes, 8); // 包体紧随其后
            
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            
            args.SetBuffer(finalBytes , 0 , finalBytes.Length);
            args.Completed += SendCallBack;
            socket.SendAsync(args);
        }
    }

    private void SendCallBack(object socket, SocketAsyncEventArgs args)
    {
        if (args.SocketError == SocketError.Success)
        {
            Debug.Log("成功发送消息给服务器");
        }
        else
        {
            Console.WriteLine("发送错误" + args.SocketError.ToString());
        }
        //因为只使用一次参数记得销毁
        args.Completed -= SendCallBack;
        args.Dispose();
    }

    //根据文件的不同处理文件
    private void HandleMsg(IMessage receiveMsg)
    {
        lock (receiveDataUsed)
        {
            ReceiveData.Enqueue(receiveMsg);
        }
    }
    
    //关闭socket
    public void CloseSocket()
    {
        if (socket == null)
        { return; }
        try
        {
            if (socket.Connected)
            {
                print("主动断开连接");
                //正常退出时发送关闭信息通知服务端
                //心跳信息是为了处理异常关闭
                //QuitMsg msg = new QuitMsg();
                //socket.Send(msg.Writing());
                socket.Shutdown(SocketShutdown.Both);
                socket.Disconnect(false);
                socket.Close();
                // 💡 核心清理：把没处理完的网络包彻底清空，防止带入下一局游戏！
                lock (receiveDataUsed)
                {
                    ReceiveData.Clear();
                }
                //记得关闭心跳
                CancelInvoke("SendHeartBeatMsg");
                isConnectedStatus = false;
                Debug.Log("<color=red>[客户端] 已断开与主机的连接，数据队列已清空</color>");
            }
        }
        catch
        {

        }
    }
    
    // ---- 新增提供给外面（比如服务端模块）调用的辅助方法 ----
    // 给服务端解包用的：通过 ID 获取对应 Protobuf 的 Parser
    public bool TryGetMsgParser(int msgID, out Google.Protobuf.MessageParser parser)
    {
        return msgParserDict.TryGetValue(msgID, out parser);
    }
    // 给服务端发包用的：通过具体类型反查对应的 ID
    public bool TryGetMsgIdByType(Type type, out int msgID)
    {
        return msgTypeToIdDict.TryGetValue(type, out msgID);
    }

    public bool TryGetMsgHanler(int msgID, out IHandler handler)
    {
        return handlerDict.TryGetValue(msgID, out handler);
    }
}

