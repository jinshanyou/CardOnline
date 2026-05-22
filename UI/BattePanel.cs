using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;


public class BattePanel : UIBase
{
    [Header("玩家 UI")]
    public Button btnPlayCard; // 模拟出牌测试按钮
    public Button btnEndTurn;  // 结束回合按钮
    public Text txtHp;
    public Slider sliderHp;
    public Text txtCost;
    
    [Header("敌人 UI")]
    public Text txtEnemyHp;    // 敌人血量
    public Slider sliderEnemyHp; // 💡 新增：敌人血量滑条
    public Text txtEnemyHandCount; // 💡 新增：敌人手牌数量文本 (例如显示 "手牌 5")
    
    [Header("全局 UI")]
    public Text txtRound;      // 回合提示文字
    
    [Header("牌库与墓地数量 UI")] // 💡 新增：用于显示你切好的那一列卡牌/墓地图标底下的数量
    public Text txtMyDrawCount;       // 我方牌库剩余数量
    public Text txtMyDiscardCount;    // 我方墓地数量
    public Text txtEnemyDrawCount;    // 敌方牌库剩余数量
    public Text txtEnemyDiscardCount; // 敌方墓地数量
    
    public Transform handCardArea; // 拖入挂了 HorizontalLayoutGroup 的节点
    public List<CardItem> myCardItems;  // 拖入单张卡牌预制体 (后期换成 Addressables 异步加载)
    
    [Header("头像组件")]
    public Image imgPlayerHead; // 💡 拖入我方头像的 Image 组件
    public Image imgEnemyHead;  // 💡 拖入敌方头像的 Image 组件

    [Header("头像美术资源 (拖入你切好的小图)")]
    public Sprite knightSprite; // 圣骑士
    public Sprite demonSprite;  // 恶魔
    
    [Header("特效预制体")]
    public GameObject damageTextPrefab; // 💡 在 Inspector 里拖入一个红色的 Text 预制体

    protected override void OnShow()
    {
        base.OnShow();
        // 💡 每次绑定前先清空旧的，杜绝重复触发！
        btnPlayCard.onClick.RemoveAllListeners();
        btnPlayCard.onClick.AddListener(() =>
        {
            if (string.IsNullOrEmpty(BattleMgr.Instance.SelectedCardInstanceId))
            {
                return;
            }
            //BattleMgr.Instance.PlayCard();
            ReqPlayCard req = new ReqPlayCard();
            req.InstanceId =  BattleMgr.Instance.SelectedCardInstanceId;
            req.PlayerUid = BattleMgr.Instance.myPlayer.EntityUid;
            TcpAsyncMgr.Instance.SendMsg(req);
        });
        
        btnEndTurn.onClick.RemoveAllListeners();
        btnEndTurn.onClick.AddListener(() =>
        {
            if (BattleMgr.Instance.CurrentState != EBattleState.LocalPlayerTurn) return;

            // 💡 核心修复：结束回合单机与联机分流！
            if (BattleMgr.Instance.isAI)
            {
                // 1. 如果是单机：直接本地切换回合，触发敌方人机思考
                BattleMgr.Instance.EndTurn();
            }
            else
            {
                // 2. 如果是联机：发包给主机
                ReqEndTurn req = new ReqEndTurn();
                req.PlayerUid = BattleMgr.Instance.myPlayer.EntityUid;
                TcpAsyncMgr.Instance.SendMsg(req);
            }
            // 丢弃本地所有卡牌视觉
            ThrowAllCard();
        });
        AddUIEvent<MsgInitBattleUi>(FirstInit);
        AddUIEvent<MsgEntityHpChange>(OnEntityHpChange);
        AddUIEvent<MsgCostChange>(OnCostChange);
        AddUIEvent<MsgOnPlayerTurnStart>(OnLocalTurnStart);
        AddUIEvent<MsgOnEnemyTurnStart>(OnRemoteTurnStart);
        AddUIEvent<MsgUseCard>(OnUsedCard);
        //结束锁ui
        AddUIEvent<MsgOnGameOver>(OnGameEndLockUI);
        AddUIEvent<MsgOnWin>(OnGameEndLockUI);
        AddUIEvent<MsgDrawCard>(OnDrawCard);
        
        // 初始化回合提示
        txtRound.text = "【战斗开始】";
    }

    private void FirstInit(MsgInitBattleUi msg)
    {
        //💡 冷启动初始化：主动拉取当前数据刷新界面！
        var player = (PlayerEntity)msg.player1;
        var enemy = msg.player2;

        //手动调用一次你写的刷新方法
        OnEntityHpChange(new MsgEntityHpChange { 
        uid = player.EntityUid, currentHp = player.CurrentHp, maxHp = player.MaxHp 
        });
        OnEntityHpChange(new MsgEntityHpChange { 
        uid = enemy.EntityUid, currentHp = enemy.CurrentHp, maxHp = enemy.MaxHp 
        });
        
        if (BattleMgr.Instance.myPlayer.EntityUid == "Host")
        {
            imgPlayerHead.sprite = knightSprite; // 我是房主，我是骑士
            imgEnemyHead.sprite = demonSprite;   // 对手是客机，对手是恶魔
        }
        else // 我是 "Client"
        {
            imgPlayerHead.sprite = demonSprite;   // 我是客机，我是恶魔
            imgEnemyHead.sprite = knightSprite;  // 对手是房主，对手是骑士
        }
        
        // 💡 首次初始化，强制刷新一次所有牌堆数量
        RefreshCardCountsUI();
    }
    /// <summary>核心解法：通过比对 UID 来分发刷新逻辑！ </summary>
    private void OnEntityHpChange(MsgEntityHpChange msg)
    {
        bool isMyHp = (msg.uid == BattleMgr.Instance.myPlayer.EntityUid);
    
        // 计算扣血差值 (由于你之前的 FirstInit 也会调用此方法，所以先确保 BattleMgr 已初始化)
        int oldHp = isMyHp ? BattleMgr.Instance.myPlayer.CurrentHp : BattleMgr.Instance.enemy.CurrentHp;
        int damage = oldHp - msg.currentHp;
        if (isMyHp)
        {
            sliderHp.value = msg.maxHp == 0 ? 0 : (float)msg.currentHp / msg.maxHp; 
            txtHp.text = $"{msg.currentHp}/{msg.maxHp}";
        }
        else
        {
            sliderEnemyHp.value = msg.maxHp == 0 ? 0 : (float)msg.currentHp / msg.maxHp;
            txtEnemyHp.text = $"{msg.currentHp}/{msg.maxHp}";
        }
        
        // 💡 重点：如果受到了真实伤害(大于0)，生成飘字！
        if (damage > 0)
        {
            SpawnDamageText(isMyHp ? txtHp.transform.position : txtEnemyHp.transform.position, damage);
        }
        // 如果受击者 UID 等于玩家的 UID
        if (msg.uid == BattleMgr.Instance.myPlayer.EntityUid)
        {
            // 注意：int 除以 int 会变成整数(0)，必须强制转 float！
            sliderHp.value = (float)msg.currentHp / msg.maxHp; 
            txtHp.text = $"{msg.currentHp}/{msg.maxHp}";
        }
        // 如果受击者 UID 等于敌人的 UID
        else if (msg.uid == BattleMgr.Instance.enemy.EntityUid)
        {
            // 💡 核心修复：更新敌人血量滑条与文本
            sliderEnemyHp.value = msg.maxHp == 0 ? 0 : (float)msg.currentHp / msg.maxHp;
            txtEnemyHp.text = $"{msg.currentHp}/{msg.maxHp}";
        }
    }
// 💡 漂浮字体的生成与 DOTween 动画
    private void SpawnDamageText(Vector3 position, int damageValue)
    {
        // 从对象池拿漂浮字
        GameObject popObj = ObjectPoolMgr.Instance.GetGameObject("DamageText", damageTextPrefab);
        popObj.transform.SetParent(this.transform); // 挂在 Canvas 下
        popObj.transform.position = position + new Vector3(0, 50f, 0); // 略微偏移在头像上方
        popObj.transform.localScale = Vector3.one;

        Text dmgText = popObj.GetComponent<Text>();
        dmgText.text = $"-{damageValue}";
        dmgText.color = Color.red;

        // 💡 DOTween 飘字动画：0.8秒内向上移动 100 像素，同时渐渐变淡
        popObj.transform.DOMoveY(popObj.transform.position.y + 100f, 0.8f);
        dmgText.DOFade(0, 0.8f).OnComplete(() =>
        {
            // 动画播完，放回对象池
            ObjectPoolMgr.Instance.ReleaseGameObject("DamageText", popObj);
        });
    }
    private void OnCostChange(MsgCostChange costMsg)
    {
        // 💡 核心修复：只有改变费用的 Uid 是【我方玩家】时，本地的费用文本才刷新！
        if (costMsg.uid == BattleMgr.Instance.myPlayer.EntityUid)
        {
            txtCost.text = $" {costMsg.cost}/{costMsg.maxCost}";
        }
    
        // （如果你未来想加个 txtEnemyCost 敌人费用文本，可以在 else if 里去刷新它）
    }
    private void OnLocalTurnStart(MsgOnPlayerTurnStart msg)
    {
        txtRound.text = "【你的回合】";
        txtRound.color = Color.green;
        // 开启按钮交互
        btnPlayCard.interactable = true;
        btnEndTurn.interactable = true;
    }

    private void OnRemoteTurnStart(MsgOnEnemyTurnStart msg)
    {
        txtRound.text = "【敌方回合】";
        txtRound.color = Color.red;
        // 锁死按钮交互，防止敌方回合玩家乱点
        btnPlayCard.interactable = false;
        btnEndTurn.interactable = false;
    }
    private void OnGameEndLockUI<T>(T msg) // 用泛型适配两个事件
    {
        btnPlayCard.interactable = false;
        btnEndTurn.interactable = false;
        txtRound.text = "【战斗结束】";
    }
    // 当玩家逻辑层抽到了一张牌
    private async void OnDrawCard(MsgDrawCard msg)
    {
        // 💡 任何人抽卡，后台数据都会变，我们同步刷新一次卡牌数量 UI
        RefreshCardCountsUI();
        
        if (msg.uid != BattleMgr.Instance.myPlayer.EntityUid) return;

        // 1. 直接用资源底座加载预制体
        GameObject prefab = await AddressMgr.Instance.LoadAssetAsync<GameObject>("CardItem");
    
        // 2. 实例化为全新的克隆体，并塞进 LayoutGroup！
        //GameObject newCardObj = Instantiate(prefab, handCardArea);
        
        GameObject myCardItem = ObjectPoolMgr.Instance.GetGameObject("CardItem",prefab);
        myCardItem.transform.SetParent(handCardArea);
        
        CardItem cardItem = myCardItem.GetComponent<CardItem>();
        
        // 💡 ToggleGroup 单选绑定核心操作：
        // 让这张卡的 Toggle 归属到 LayoutGroup 身上挂载的 ToggleGroup 组件里
        //cardItem.cardToggle.group = handCardArea.GetComponent<ToggleGroup>(); 
    
        cardItem.InitCard( msg.instanceId,msg.cardId);
        
        myCardItems.Add(cardItem);
        // 💡 魔法发生：一旦预制体进到 handCardArea 下，
        // HorizontalLayoutGroup 会自动把它排列得整整齐齐，无需你写代码算坐标！
    }
    
    //因为触发用牌方法就是自己好像没必要发信息啊,但是现在我是可以根据id来判断删那张牌,但是有同id怎么办??
    private void OnUsedCard(MsgUseCard msg)
    {
        // 💡 丢弃全部手牌后刷新数量 UI
        RefreshCardCountsUI();
        // 1. 💡 确定卡牌要砸向谁？
        // 如果是我出牌，卡牌冲向敌人头像；如果是敌人出牌，卡牌冲向我的头像
        bool isMyCard = (msg.uid == BattleMgr.Instance.myPlayer.EntityUid);
        Vector3 targetPos = isMyCard ? txtEnemyHp.transform.position : txtHp.transform.position;

        if (isMyCard)
        {
            // 2. 如果是我出的牌（手牌里有实体）
            for (int i = 0; i < myCardItems.Count; i++)
            {
                CardItem item = myCardItems[i];
                if (item.InstanceId == msg.instanceId)
                {
                    myCardItems.RemoveAt(i); // 先从手牌列表移出

                    // 3. 💡 播放华丽的飞行动画！
                    item.PlayUseAnimation(targetPos, () =>
                    {
                        // 飞到头像并缩为 0 后，再放进对象池回收
                        ObjectPoolMgr.Instance.ReleaseGameObject("CardItem", item.gameObject);
                    
                        // 卡牌砸碎的瞬间，触发敌方头像受击震动！
                        TriggerPortraitShake(false); 
                    });
                    break;
                }
            }
        }
        else
        {
            // 4. 如果是对方出牌（我们在本地没有他的 CardItem 实体）
            // 我们不需要看他飞，直接让我们的头像震动，模拟被砸中的感觉！
            TriggerPortraitShake(true); 
        }
        if (msg.uid != BattleMgr.Instance.myPlayer.EntityUid)
        {
            return;
        }
        for (int i = 0; i < myCardItems.Count; i++)
        {
            if (myCardItems[i].InstanceId == msg.instanceId)
            {
                ObjectPoolMgr.Instance.ReleaseGameObject("CardItem", myCardItems[i].gameObject);
                myCardItems.RemoveAt(i);
                break;
            }
        }
    }

    private void ThrowAllCard()
    {
        for (int i = 0; i < myCardItems.Count ;i++)
        {
            ObjectPoolMgr.Instance.ReleaseGameObject( "CardItem" , myCardItems[i].gameObject);
        }
        myCardItems.Clear();
        // 💡 丢弃全部手牌后刷新数量 UI
        RefreshCardCountsUI();
    }
    // ================== 💡 核心新增：一键拉取底层数据刷新所有文本 ==================
    private void RefreshCardCountsUI()
    {
        var player = BattleMgr.Instance.myPlayer;
        var enemy = BattleMgr.Instance.enemy;

        if (player == null || enemy == null) return;

        // 1. 我方牌库剩余数 & 墓地数
        txtMyDrawCount.text = player.drawPile.Count.ToString();
        txtMyDiscardCount.text = player.discardPile.Count.ToString();

        // 2. 敌方牌库剩余数 & 敌方墓地数
        txtEnemyDrawCount.text = enemy.drawPile.Count.ToString();
        txtEnemyDiscardCount.text = enemy.discardPile.Count.ToString();

        // 3. 敌方手牌数量
        txtEnemyHandCount.text = enemy.handCards.Count.ToString();
    }
    
    // 💡 新增：头像受击震动方法
    private void TriggerPortraitShake(bool shakeMyPlayer)
    {
        // 如果是我方受击，震动我方血条；否则震动敌方血条
        Transform targetHead = shakeMyPlayer ? sliderHp.transform : sliderEnemyHp.transform;
    
        // DOTween 震动：持续 0.4秒，强度 15，震动 10 次
        targetHead.DOShakePosition(0.4f, 15f, 10);
    }
}
