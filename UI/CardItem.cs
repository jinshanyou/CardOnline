using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening; // 💡 引入 DOTween
using UnityEngine.EventSystems; // 💡 必须引入 Unity 事件系统接口
public class CardItem : MonoBehaviour ,IBeginDragHandler, IDragHandler, IEndDragHandler
{
    //public Toggle cardToggle;
    public Text txtName;
    public Text txtDescription;
    public Text txtCost;
    public Image imgCardBK;
    public Text txtValue;
    
    public string InstanceId { get; private set; }
    public int ConfigId { get; private set; } // 存一下自己的配置 ID
    // 💡 记录拖拽前的临时变量
    private Transform originalParent;
    private int originalIndex;
    private Vector3 originalLocalScale;
    private CanvasGroup canvasGroup;
    
    
    // CardItem.cs
    public void PlayUseAnimation(Vector3 targetPos, System.Action onComplete)
    {
        // 1. 💡 核心技巧：脱离 LayoutGroup 的约束，放到 Canvas 根节点，否则卡牌无法自由飞行！
        transform.SetParent(transform.root); 

        // 2. 创建一个 DOTween 动画序列
        Sequence seq = DOTween.Sequence();

        // 步骤一：0.3秒内飞到屏幕中央，并放大到 1.2 倍
        Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
        seq.Append(transform.DOMove(screenCenter, 0.3f).SetEase(Ease.OutBack));
        seq.Join(transform.DOScale(new Vector3(1.2f, 1.2f, 1.2f), 0.3f));

        // 步骤二：在中央悬停 0.4 秒，让玩家看清卡牌原画
        seq.AppendInterval(0.4f);

        // 步骤三：用 0.2 秒的极快速度，缩成一个点并猛冲向目标头像
        seq.Append(transform.DOMove(targetPos, 0.2f).SetEase(Ease.InQuad));
        seq.Join(transform.DOScale(Vector3.zero, 0.2f));

        // 步骤四：动画播完后，执行回调函数（回收卡牌）
        seq.OnComplete(() =>
        {
            onComplete?.Invoke();
        });
    }
    
    
    public async void InitCard(string instanceId,int cardId)
    {
        InstanceId = instanceId;
        ConfigId = cardId;
        // ================== 💡 核心修复：重置卡牌尺寸 ==================
        // 每次从对象池拿出来初始化时，强行把它的缩放值恢复为 1！
        transform.localScale = Vector3.one; 
        // =============================================================
        
        //cardToggle.onValueChanged.RemoveAllListeners();
        //cardToggle.SetIsOnWithoutNotify(false);
        //cardToggle.onValueChanged.AddListener(OnToggleChanged);
        
        CardData cardData = CardMgr.Instance.GetCardById(cardId);
        
        txtName.text = cardData.CardName;
        txtCost.text = cardData.Cost.ToString();
        txtDescription.text = cardData.Description;
        txtValue.text = cardData.BaseValue.ToString(); 
        //imgCardBK.sprite = await AddressMgr.Instance.LoadAssetAsync<Sprite>(cardData.IconRef);
        // 💡 一行代码，极速且安全地异步加载图片！
        if (cardData.IconRef != null && cardData.IconRef.RuntimeKeyIsValid())
        {
            Sprite sp = await AddressMgr.Instance.LoadAssetAsync<Sprite>(cardData.IconRef);
            if (sp != null)
            {
                imgCardBK.sprite = sp;
            }
        }
    }
    
    private void OnToggleChanged(bool isOn)
    {
        if (isOn)
        {
            // 通知 BattleMgr：我被选中了！
            // 初版你先传 ConfigId，未来进阶了改成传 InstanceUID！
            print(ConfigId);
            BattleMgr.Instance.SelectedCardInstanceId = InstanceId;
            print("当前卡牌是id"+BattleMgr.Instance.SelectedCardId);
            Debug.Log($"选中了卡牌: {txtName.text}");
        }
        else
        {
            BattleMgr.Instance.SelectedCardInstanceId = null;
        }
    }
    // ================== 💡 核心新增：拖拽事件流 ==================

    // 1. 开始拖拽
    public void OnBeginDrag(PointerEventData eventData)
    {
        // 防御性编程：不是我的回合，不准拖牌！
        if (BattleMgr.Instance.CurrentState != EBattleState.LocalPlayerTurn) return;

        // 记录拖拽前它在手牌 LayoutGroup 里的位置和父物体
        originalParent = transform.parent;
        originalIndex = transform.GetSiblingIndex();
        originalLocalScale = transform.localScale;

        // 脱离 LayoutGroup 限制，放到 Canvas 最顶层，否则无法自由拖拽
        transform.SetParent(transform.root);
        
        // 💡 核心技巧：拖拽时关闭 Raycast 检测，防止卡牌挡住鼠标，导致松手时检测不到底下的战场
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;

        // 稍微放大一点卡牌，增加“抓起”的物理反馈手感
        transform.DOScale(originalLocalScale * 1.1f, 0.1f);
    }
    // 2. 拖拽中
    public void OnDrag(PointerEventData eventData)
    {
        if (BattleMgr.Instance.CurrentState != EBattleState.LocalPlayerTurn) return;

        // 让卡牌坐标死死跟着鼠标/手指移动
        transform.position = eventData.position;
    }
    // 3. 结束拖拽（松手）
    public void OnEndDrag(PointerEventData eventData)
    {
        if (BattleMgr.Instance.CurrentState != EBattleState.LocalPlayerTurn) return;

        if (canvasGroup != null) canvasGroup.blocksRaycasts = true; // 恢复检测

        // 💡 核心判定：如果拖拽松手的高度，超过了屏幕总高度的 40% (视为扔向了战场)
        if (eventData.position.y > Screen.height * 0.4f)
        {
            // ================== 💡 核心新增：本地费用校验拦截 ==================
            CardData cardData = CardMgr.Instance.GetCardById(ConfigId);
            if (BattleMgr.Instance.myPlayer.CurrentCost < cardData.Cost)
            {
                Debug.LogWarning("<color=red>费用不足，出牌被强行弹回！</color>");
            
                // 头像震屏提示一下（或者数字飘红字提示）
                transform.SetParent(originalParent);
                transform.SetSiblingIndex(originalIndex);
                transform.localScale = Vector3.zero;
            
                // 橡皮筋回弹！
                transform.DOScale(originalLocalScale, 0.2f).SetEase(Ease.OutBack);
                return; // 👈 拦截！不许往下走，卡牌保住了！
            }
            
            // 💡 核心修复：单机与联机分流！
            if (BattleMgr.Instance.isAI)
            {
                // 1. 如果是单机：绕过网络，直接本地调用战斗管理器执行出牌！
                BattleMgr.Instance.ExecutePlayCard(
                    BattleMgr.Instance.myPlayer.EntityUid, 
                    InstanceId
                );
            }
            else
            {
                // 2. 如果是联机：像之前一样，发包给主机裁判
                ReqPlayCard req = new ReqPlayCard();
                req.InstanceId = InstanceId;
                req.PlayerUid = BattleMgr.Instance.myPlayer.EntityUid;
                TcpAsyncMgr.Instance.SendMsg(req);
            }

            transform.DOScale(Vector3.zero, 0.1f);
        }
        else
        {
            // 💡 弹回手牌区 (经典的橡皮筋回弹效果)
            // 重新塞回手牌 LayoutGroup 的原位置
            transform.SetParent(originalParent);
            transform.SetSiblingIndex(originalIndex);
            
            // 用 DOTween 缓动恢复尺寸，手感会极其丝滑、有弹性！
            transform.localScale = Vector3.zero; // 先缩到 0
            transform.DOScale(originalLocalScale, 0.2f).SetEase(Ease.OutBack); // 弹出式恢复
        }
    }
    
}
