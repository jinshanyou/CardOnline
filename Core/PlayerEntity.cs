using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardInstance
{
    public string InstanceId;
    public int CardId;

    // 💡 传入玩家 UID 和序号，拼出唯一的 ID
    public CardInstance(int cardId, string playerUid, int index)
    {
        InstanceId = $"{playerUid}_{cardId}_{index}";
        CardId = cardId;
    }
}
public struct MsgCostChange
{
    public string uid; // 💡 新增：是谁的费用变了！
    public int cost;
    public int maxCost;
}

public struct MsgDrawCard
{
    public string uid;
    public int cardId;
    public string instanceId;
}

public struct MsgUseCard
{
    public string uid;
    public int cardId;
    public string instanceId;
}

public class PlayerEntity : BaseEntity
{
    public int CurrentCost { get; private set; }
    public int MaxCost { get; private set; }
    
    // 💡 卡牌数据模型 (存的是卡牌的 ID)
    public List<CardInstance> drawPile = new List<CardInstance>();  // 抽牌堆
    public List<CardInstance> handCards = new List<CardInstance>(); // 手牌区
    public List<CardInstance> discardPile = new List<CardInstance>(); // 弃牌堆

    // 初始化时追加玩家独有的属性
    public void InitPlayer(string uid, string name, int maxHp, int maxCost)
    {
        base.Init(uid, name, maxHp); // 先调爹的初始化
        MaxCost = maxCost;
        CurrentCost = maxCost;
        //EventCenter.Instance.AddEventListener<MsgUseCard>(UseCard);
    }
    // 玩家独有的行为：消耗费用打牌
    public bool TryUseCost(int cost)
    {
        if (CurrentCost >= cost)
        {
            CurrentCost -= cost;
        
            // 💡 核心修复：带上自己的 EntityUid 抛出事件
            EventCenter.Instance.EventTrigger(new MsgCostChange() { 
                uid = this.EntityUid, // 👈 塞入身份证
                cost = CurrentCost, 
                maxCost = MaxCost 
            });
            return true;
        }
        return false;
    }
    public void StartTurn()
    {
        CurrentCost = MaxCost;
    
        // 💡 核心修复：带上自己的 EntityUid 抛出事件
        EventCenter.Instance.EventTrigger(new MsgCostChange() { 
            uid = this.EntityUid, // 👈 塞入身份证
            cost = CurrentCost, 
            maxCost = MaxCost 
        });
    
        DrawCard(5);
    }
    public void DrawCard(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (drawPile.Count == 0) 
            {
                // TODO: 牌库没牌了，把弃牌堆洗入牌库
                Shuffle();
                Debug.Log("洗牌！");
                if (drawPile.Count == 0)
                {
                    Debug.LogWarning("牌库和弃牌堆都没有牌了，停止抽牌");
                    break;
                }
            }
            // 1. 从牌库顶拿一张牌
            CardInstance drawCard = drawPile[0];
            drawPile.RemoveAt(0);
            // 2. 塞进手牌
            handCards.Add(drawCard);
            // 3. 💡 重点：抛出事件！让 UI(BattlePanel) 监听到并在屏幕上生成一张卡牌预制体！
            EventCenter.Instance.EventTrigger(new MsgDrawCard { 
                uid = this.EntityUid, 
                cardId = drawCard.CardId,
                instanceId =  drawCard.InstanceId
            });
        }
    }
    
    // 💡 核心方法：两端用完全一样的方法来初始化自己的和对方的牌库
    public void InitDeck(List<int> rawCardIds, string playerUid)
    {
        drawPile.Clear();
        handCards.Clear();
        discardPile.Clear();
        
        for (int i = 0; i < rawCardIds.Count; i++)
        {
            // 用唯一的 index 保证 InstanceId 对称
            drawPile.Add(new CardInstance(rawCardIds[i], playerUid, i));
        }
    }
    
    public bool UseCard(string instanceId, out CardInstance usedCard)
    {
        usedCard = GetHandCardByInstanceId(instanceId);

        if (usedCard == null)
        {
            Debug.LogWarning($"手牌中不存在实例卡牌: {instanceId}");
            return false;
        }

        handCards.Remove(usedCard);
        discardPile.Add(usedCard);
        return true;
    }

    public void DiscardAllHandCards()
    {
        for (int i = 0; i < handCards.Count; i++)
        {
            discardPile.Add(handCards[i]);
        }
        handCards.Clear();
    }

    public void Shuffle()
    {
        drawPile.Clear();

        for (int i = 0; i < discardPile.Count; i++)
        {
            drawPile.Add(discardPile[i]);
        }
        //这里怎么改也是host指令传种子吗?
        //ShuffleDeck(drawPile);
        // 💡 核心修复：利用战局种子 + 弃牌堆数量，拼出一个两端绝对相同、且动态变化的洗牌种子！
        int shuffleSeed = BattleMgr.Instance.BattleSeed + discardPile.Count;
        ShuffleDeck(drawPile, shuffleSeed);
        
        discardPile.Clear();
    }
    
    // 洗牌算法 (直接拷贝进你的工具类或者 Entity 里)
    public void ShuffleDeck<T>(List<T> deck, int seed)
    {
        System.Random rng = new System.Random(seed);

        int n = deck.Count;
        while (n > 1)
        {
            n--;

            int k = rng.Next(n + 1);

            T temp = deck[k];
            deck[k] = deck[n];
            deck[n] = temp;
        }
    }

    public CardInstance GetHandCardByInstanceId(string instanceId)
    {
        for (int i = 0; i < handCards.Count; i++)
        {
            if (handCards[i].InstanceId == instanceId)
                return handCards[i];
        }
        Debug.Log("不存在该实例卡牌");
        return null;
    }
    
    
}
