using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[NetMsgID(2001)]
public class ReqPlayCardHandler : BaseHandler<ReqPlayCard>
{
    protected override void HandleMsg(ReqPlayCard msg)
    {
        // 💡 只有主机（Server端）才能处理 Req 请求！
        // 如果我们是普通客机，直接无视（或者底层根本就不分发给客机）
        if (NetworkMgr.Instance.server == null) return;
        
        PlayerEntity actor = (msg.PlayerUid == "Host") 
            ? BattleMgr.Instance.myPlayer 
            : (PlayerEntity)BattleMgr.Instance.enemy;
        
        Debug.Log($"[主机裁判] 收到玩家请求出牌：{msg.InstanceId}，开始校验...");
        
        // 校验逻辑 (这里简化，假装他有费用且是他的回合)
        // 去 BattleMgr 查出是哪张卡
        var cardInst = actor.GetHandCardByInstanceId(msg.InstanceId);
        if (cardInst != null)
        {
            // 校验通过！主机生成 Sync 广播消息
            SyncPlayCard syncMsg = new SyncPlayCard();
            // 💡 核心修复：是谁请求出牌的，广播里就写谁的 Uid！
            syncMsg.PlayerUid = msg.PlayerUid; 
            syncMsg.InstanceId = msg.InstanceId;
            syncMsg.CardId = cardInst.CardId;

            // 用大喇叭告诉全服（包括房主自己的客机端）！
            NetworkMgr.Instance.server.BroadMsg(syncMsg);
        }        
    }
}
