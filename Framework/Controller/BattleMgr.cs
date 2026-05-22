using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public enum EBattleState
{
    Init,           // 初始化准备
    LocalPlayerTurn,// 轮到本地玩家出牌
    EnemyTurn,      // 轮到敌人出牌
    Win,            // 胜利
    GameOver        // 游戏结束
}

public struct MsgInitBattleUi
{
   public BaseEntity player1;
   public BaseEntity player2;
}

public struct MsgOnPlayerTurnStart { }
public struct MsgOnEnemyTurnStart { }
public struct MsgOnGameOver { }
public struct MsgOnWin { }

public class BattleMgr : Singleton<BattleMgr>
{
    // ... 保持原有属性不变 ...
    public int BattleSeed; // 💡 新增：保存当前战局的随机种子，用于后续洗牌
    
    public PlayerEntity myPlayer;
    public PlayerEntity enemy;
    public SkillSystem skillSystem;
    public BattleContext  currentContext;

    public string SelectedCardInstanceId;
    public int SelectedCardId;

    public bool isAI;
    
    // 💡 核心：当前战局处于什么状态？
    public EBattleState CurrentState { get; private set; }
    
    public void InitBattle(int seed = -1) 
    {
        // 💡 根据是否是房主，分配不同的 UID！
        //string myUid = NetworkMgr.Instance.server != null ? "Host" : "Client";
        //string enemyUid = NetworkMgr.Instance.server != null ? "Client" : "Host";
        
        // 💡 核心修复：如果是单机AI模式，我方直接当 Host，对方当 Client；如果是联机，才按网络角色分配！
        string myUid = isAI ? "Host" : (NetworkMgr.Instance.server != null ? "Host" : "Client");
        string enemyUid = isAI ? "Client" : (NetworkMgr.Instance.server != null ? "Client" : "Host");
        
        myPlayer = new PlayerEntity();
        myPlayer.InitPlayer(myUid, "我", 100, 5); 

        enemy = new PlayerEntity();
        enemy.InitPlayer(enemyUid, "对手", 100, 5); // 暂且都当 PlayerEntity 玩
        
        SelectedCardId = -1;
        SelectedCardInstanceId = null;
        
        currentContext = new BattleContext();
        currentContext.battleMgr = this;
        currentContext.player1 = myPlayer;
        
        skillSystem = new SkillSystem();
        skillSystem.Init(currentContext);
        
        // ✅ 换成这行：全自动向数据库索要初始卡组！
        List<int> baseCards = CardMgr.Instance.GenerateDefaultDeck();
        // 💡 重点：两边在本地同时生成“我的牌”和“对手的牌”！
        myPlayer.InitDeck(baseCards, myPlayer.EntityUid);
        enemy.InitDeck(baseCards, enemy.EntityUid);
        
        // 💡 神奇的伪随机：只要种子一样，两边洗出来的牌序绝对一模一样！
        if (seed == -1) seed = 12345;
        // 💡 重点：将种子保存起来
        this.BattleSeed = seed;
        
        // ================== 💡 核心修复：绝对身份洗牌法 ==================
        int hostSeed = seed;
        int clientSeed = seed + 999;
        // 无论在主机还是客机，只要 UID 是 "Host" 的实体，就用 hostSeed 洗牌；
        // 只要 UID 是 "Client" 的实体，就用 clientSeed 洗牌！
        if (myPlayer.EntityUid == "Host")
        {
            myPlayer.ShuffleDeck(myPlayer.drawPile, hostSeed);
            enemy.ShuffleDeck(enemy.drawPile, clientSeed);
        }
        else // 即 myPlayer.EntityUid == "Client"
        {
            myPlayer.ShuffleDeck(myPlayer.drawPile, clientSeed);
            enemy.ShuffleDeck(enemy.drawPile, hostSeed);
        }
        // 💡 重点：洗牌种子，我的牌用 Seed，对手的牌用 Seed + 999 保持两端对称！
        //myPlayer.ShuffleDeck(myPlayer.drawPile, seed);
        //enemy.ShuffleDeck(enemy.drawPile, seed + 999); 
        
        // 💡 重点：控制先后手！主机先手，客机挂起等通知
        if (myUid == "Host")
        {
            ChangeState(EBattleState.LocalPlayerTurn);
        }
        else
        {
            ChangeState(EBattleState.EnemyTurn);
        }
        //初始化battlePanel
        EventCenter.Instance.EventTrigger(new MsgInitBattleUi() { player1 = myPlayer, player2 = enemy});
        // 💡 裁判注册监听：只要场上有实体死亡，裁判就会收到通知！
        EventCenter.Instance.AddEventListener<MsgEntityDead>(OnEntityDead);
        Debug.Log("开始你的回合");
    }

    private void OnEntityDead(MsgEntityDead deathMsg)
    {
        if (deathMsg.uid == myPlayer.EntityUid)
        {
            ChangeState(EBattleState.GameOver);
        }
        else if (deathMsg.uid == enemy.EntityUid)
        {
            ChangeState(EBattleState.Win);
        }
    }

    public void ChangeState(EBattleState newState)
    {
        CurrentState = newState;
        Debug.Log($"==== 战局切换至状态: {newState} ====");
        
        switch (CurrentState)
        {
            case EBattleState.LocalPlayerTurn:
                OnLocalPlayerTurnStart();
                break;
            case EBattleState.EnemyTurn:
                OnEnemyTurnStart();
                break;
            case EBattleState.GameOver:
                OnGameOver();
                break;
            case  EBattleState.Win:
                OnWin();
                break;
        }
    }

    private void OnLocalPlayerTurnStart()
    {

        myPlayer.StartTurn();
        // 2. 抛出事件，通知 UI 弹出“你的回合”提示，并解锁手牌点击
        EventCenter.Instance.EventTrigger(new MsgOnPlayerTurnStart());
        Debug.Log("请行动");
        // 游戏引擎依然在跑，但逻辑“挂起”了。
        // 我们在默默等待玩家点击UI上的【出牌】或【结束回合】按钮触发事件。
    }

    private async void OnEnemyTurnStart()
    {
        // 通知 UI 锁死玩家的手牌操作，变成红色的【敌方回合】
        EventCenter.Instance.EventTrigger(new MsgOnEnemyTurnStart()); 
        if (isAI)
        {
            // 如果是单机，跑 AI 协程
            await SimulateEnemyAIAsync();
        }
        else
        {
            // 💡 核心修复：联机模式下，虽然是对方的回合，
            // 但我们要让本地的 enemy 镜像也在后台执行 StartTurn() 抽卡！
            // 这样，两端内存中对方的手牌数据就达到了绝对的镜像对称！
            enemy.StartTurn(); 
            Debug.Log("【联机模式】进入敌方回合，默默同步对方手牌数据...");
        }
    }

    private void OnGameOver()
    {
        //操作者被击败跳出失败ui
        EventCenter.Instance.EventTrigger(new MsgOnGameOver());
    }
    private void OnWin()
    {
        //操作者击败对方跳出成功ui
        EventCenter.Instance.EventTrigger(new MsgOnWin());
    }

// 2. 把敌人模拟方法改为返回 Task
    private async Task SimulateEnemyAIAsync()
    {
        Debug.Log("敌人正在思考...");
    
        // 💡 魔法时刻：不卡主线程的绝对延时（等待 1.5 秒）
        await Task.Delay(1500); 

        Debug.Log("敌人使用了普通攻击！");
        myPlayer.TakeDamage(10);
    
        // 💡 打完之后，再停顿 0.5 秒让玩家看清扣血，然后再切回合
        await Task.Delay(500);
        if(CurrentState == EBattleState.GameOver || CurrentState == EBattleState.Win)
        {
            return;
        }
        ChangeState(EBattleState.LocalPlayerTurn);
    }
    
    public void PlayCard() 
    {
        // 💡 防御性编程：不是我的回合，不许出牌！(防止抓包作弊/狂点UI)
        if (CurrentState != EBattleState.LocalPlayerTurn) 
        {
            Debug.LogWarning("还没到你的回合，无法出牌！");
            return; 
        }
        if (string.IsNullOrEmpty(SelectedCardInstanceId))
        {
            return;
        }
        CardInstance card = myPlayer.GetHandCardByInstanceId(SelectedCardInstanceId);
        if (card == null)
        {
            return;
        }
        // 2. 去 CardMgr 拿卡牌配置
        CardData cardData = CardMgr.Instance.GetCardById(card.CardId);
        // 3. 检查 myPlayer 费用够不够
        if (!myPlayer.TryUseCost(cardData.Cost))
        {
            return;
        }
        skillSystem.ExecuteCard(cardData, myPlayer,enemy);
        myPlayer.UseCard(SelectedCardInstanceId,out CardInstance cardInstance);
        
        EventCenter.Instance.EventTrigger(new MsgUseCard
        {
            uid = myPlayer.EntityUid,
            instanceId = cardInstance.InstanceId
        });
        
        SelectedCardInstanceId = null;
        // 4. 如果够，扣除己方费用，让 enemy 调用 TakeDamage
    }
    
    // 💡 这是表现层的最终执行点！无论是谁出牌，只要网络广播了，两端都无脑执行这个方法！
    public void ExecutePlayCard(string actorUid, string instanceId)
    {
        // 1. 判断是哪位大哥出牌？另一个就是挨揍的目标
        PlayerEntity actor = (actorUid == myPlayer.EntityUid) ? myPlayer : (PlayerEntity)enemy;
        PlayerEntity target = (actorUid == myPlayer.EntityUid) ? (PlayerEntity)enemy : myPlayer;

        // 2. 从他的手牌里找到这张卡
        CardInstance card = actor.GetHandCardByInstanceId(instanceId);
        if (card == null) return;
        CardData cardData = CardMgr.Instance.GetCardById(card.CardId);

        // 3. 扣费（如果是对面出牌，扣对面的费，你的费用不会变）
        if (!actor.TryUseCost(cardData.Cost)) return;

        // 4. 调用技能系统，造成实际伤害！
        skillSystem.ExecuteCard(cardData, actor, target);
    
        // 5. 移出那人的手牌
        actor.UseCard(instanceId, out CardInstance cardInstance);

        // 6. 告诉 UI，这张牌被打出去了（UI 监听到后就会销毁对应的预制体）
        EventCenter.Instance.EventTrigger(new MsgUseCard
        {
            uid = actor.EntityUid,
            instanceId = cardInstance.InstanceId
        });
    }
    
    public void EndTurn()
    {
        if (CurrentState != EBattleState.LocalPlayerTurn) return;

        SelectedCardInstanceId = null;
        myPlayer.DiscardAllHandCards();
    
        // 💡 本地进入等候状态
        ChangeState(EBattleState.EnemyTurn);
    }
    
    // 💡 记得在战斗结束/销毁时，注销事件防止内存泄漏！
    public void Dispose()
    {
        EventCenter.Instance.RemoveEventListener<MsgEntityDead>(OnEntityDead);
    }
}
