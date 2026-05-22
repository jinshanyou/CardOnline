using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class CardMgr : MonoSingleton<CardMgr>
{
    private readonly Dictionary<int, CardData> cardDic = new Dictionary<int, CardData>();
    
    // 💡 新增：图片缓存池
    private readonly Dictionary<int, Sprite> iconCache = new Dictionary<int, Sprite>();
    
    // 💡 新增：给外部监听的进度更新事件 (返回 0.0 到 1.0 的 float)
    public event System.Action<float> OnPreloadProgress;
    
    public bool IsLoaded { get; private set; }
    protected override void OnSingletonAwake()
    {
        base.OnSingletonAwake();
        _ = LoadAllCardsAsync();
    }
    private async Task LoadAllCardsAsync()
    {
        IList<CardData> datas = await AddressMgr.Instance.LoadAssetsAsync<CardData>("Card");

        if (datas == null || datas.Count == 0)
        {
            Debug.LogError("CardMgr: 没有加载到任何卡牌数据");
            return;
        }
        cardDic.Clear();
        iconCache.Clear(); // 💡 清空缓存
        
        
        for (int i = 0; i < datas.Count; i++)
        {
            CardData data = datas[i];
            if (data == null)
            {
                Debug.LogWarning($"CardMgr: 第 {i} 个 CardData 为空，已跳过");
                continue;
            }
            if (cardDic.ContainsKey(data.Id))
            {
                Debug.LogError($"CardMgr: 发现重复卡牌ID -> {data.Id}，卡牌名：{data.CardName}");
                continue;
            } 

            cardDic.Add(data.Id, data);
            //在加载界面把卡牌立绘提前预加载并缓存！
            if (data.IconRef != null && data.IconRef.RuntimeKeyIsValid())
            {
                Sprite sp = await AddressMgr.Instance.LoadAssetAsync<Sprite>(data.IconRef);
                if (sp != null)
                {
                    iconCache.Add(data.Id, sp);
                }
            }
            // ================== 💡 核心新增：抛出真实的加载百分比 ==================
            float progress = (float)(i + 1) / datas.Count;
            OnPreloadProgress?.Invoke(progress); // 触发事件
            // ====================================================================
        }

        IsLoaded = true;
        Debug.Log($"CardMgr: 卡牌加载完成，共 {cardDic.Count} 张");
    }

    public CardData GetCardById(int cardId)
    {
        if (!IsLoaded)
        {
            Debug.LogWarning("CardMgr: 卡牌数据尚未加载完成");
            return null;
        }

        if (cardDic.TryGetValue(cardId, out var cardData))
        {
            return cardData;
        }

        Debug.LogError($"CardMgr: 不存在该卡牌，ID = {cardId}");
        return null;
    }
    // CardMgr.cs
    // 💡 动态生成一套测试卡组：把数据库里所有的卡，每样来 2 张！
    public List<int> GenerateDefaultDeck()
    {
        List<int> deck = new List<int>();

        if (!IsLoaded)
        {
            Debug.LogError("卡牌还没加载完，无法生成卡组！");
            return deck;
        }

        // 遍历字典里的所有卡牌配置
        foreach (var cardData in cardDic.Values)
        {
            deck.Add(cardData.Id); // 加第一张
            deck.Add(cardData.Id); // 加第二张
        }

        return deck;
    }
}
