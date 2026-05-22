using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[NetMsgID(2002)]
public class SyncPlayCardHandler : BaseHandler<SyncPlayCard>
{
    protected override void HandleMsg(SyncPlayCard msg)
    {
        Debug.Log($"<color=yellow>[表现层] 收到全服广播：玩家 {msg.PlayerUid} 出了牌 {msg.CardId}</color>");
        
        BattleMgr.Instance.ExecutePlayCard(msg.PlayerUid, msg.InstanceId);
    }
    // （进阶：调用 skillSystem.ExecuteCard 真实扣血）
}
