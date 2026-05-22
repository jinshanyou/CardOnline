#  CardOnline - 局域网 P2P 状态同步卡牌对战系统

![Unity](https://img.shields.io/badge/Unity-2022.3%20LTS-blue.svg)
![C#](https://img.shields.io/badge/C%23-9.0-green.svg)
![Protobuf](https://img.shields.io/badge/Protobuf-v3-orange.svg)
![Addressables](https://img.shields.io/badge/Addressables-v1.21-red.svg)
![License](https://img.shields.io/badge/License-MIT-brightgreen.svg)

> **项目演示 (B站实机录屏)**: [https://www.bilibili.com/video/BV1aVLe6xEps/?spm_id_from=333.1365.list.card_archive.click]
> **技术栈**: Unity 3D, C# (Task 异步), TCP Socket, Google Protobuf, Addressables, ScriptableObject, DOTween

`CardOnline` 是一款基于 Unity 引擎开发的 **Host-Client (主客机) 状态同步** 联机回合制卡牌对战系统。项目遵循 **数据与表现彻底分离 (MVC)** 的架构思想，底层网络通信、反序列化路由、对象池与事件中心均采用纯 C# 手写实现，致力于实现极致的 **0 GC 运行开销** 与 **高内聚低耦合** 的网络代码管线。

---

## 核心技术特色 & 商业化设计

### 1. 手写异步 TCP Socket 与网络封包 (0 粘包/半包)
*   **网络底座**：基于 C# 异步套接字（EAP/APM 模型）搭建底座。客户端采用 `SocketAsyncEventArgs` 极速收发数据，服务端采用线程池与 `delayDelSocket` 队列安全剔除断线玩家。
*   **流式拆包**：设计 `[4字节MessageID] + [4字节PayloadLength] + [Protobuf Body]` 的标准二进制网络封包（Envelope）协议。在接收端通过双游标移动与内存拷贝 (`Array.Copy`) 完美攻克 TCP 粘包与半包痛点。

### 2. Host-Client P2P 状态同步架构
*   **去中心化裁判**：采用“房主即服务器”的 P2P 架构。客机仅负责发送操作请求（`ReqPlayCard`），主机作为权威服务器进行费用与状态校验后，向全服广播（`SyncPlayCard`）同步结果，彻底杜绝客户端本地篡改内存作弊。
*   **绝对对称伪随机洗牌**：游戏启动时由 Host 生成唯一的随机数种子并同步。主客机采用**基于实体绝对身份（Host vs Client）**的洗牌种子（`seed` 和 `seed + 999`），解决双端视角对调问题，保证两端手牌在内存中 100% 镜像一致。

### 3. 彻底解耦的 MVC 架构与 0 GC 优化
*   **0 GC 事件中心**：利用泛型静态类和 `Dictionary<Type, Delegate>` 摒弃了 `Action<object>` 的装箱拆箱与 String 查找，实现事件分发的 0 托管堆内存分配。
*   **0 GC 对象池管理**：纯 C# 类对象池利用泛型委托（`Func/Action`）实现对任意 C# 类的极速复用；GameObject 缓存池配合 `CanvasGroup` 实现卡牌在拖拽过程中的高速回收。
*   **数据表现解耦**：战斗状态机（FSM）仅修改后台数据模型（`myPlayer` 与 `enemy` 实体），UI 表现层通过事件广播被动刷新，并使用 **DOTween Sequence** 编写卡牌悬浮放大、冲锋、受击震屏与伤害飘字，逻辑零耦合。

### 4. 现代资源管线与内存温热 (Preloading)
*   **异步解耦**：彻底废弃 Resources，所有卡牌数据（`CardData`）通过 `ScriptableObject` 配置并使用 Addressables 软引用。
*   **图片预加载**：在 Loading 界面利用 `Addressables` 异步预加载所有卡牌立绘至内存缓存（`iconCache`），战斗开始后 UI 换装 0 ms 延迟，完美消除卡牌抽到时“立绘慢半拍闪烁”的不良体验。

---

## 系统数据流向图 (Data & Message Flow)

卡牌打出时，系统遵循以下环形闭环数据流，确保数据安全与双端绝对同步：

```text
  [ 本地客户端 ]                                                      [ 远端客户端 ]
  UI 拖拽松手 (OnEndDrag)                                              接收广播，播动画
       │                                                                   ▲
       ▼                                                                   │
  本地预校验 (费用检查) ──► ReqPlayCard (TCP) ──► [主机服务端 Server]                │
                                                   │                       │
                                                   ▼                       │
                                             权威校验手牌与回合                    │
                                                   │                       │
                                                   ▼                       │
                                            BroadMsg (广播) ───────────────────────┘
                                                   │
                                                   ▼
                                         [双方本地数据层 (BattleMgr)]
                                         ExecutePlayCard ──► 扣血算伤 ──► UI 震屏飘字
