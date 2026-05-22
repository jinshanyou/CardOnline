using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[NetMsgID(2005)]
public class SyncEndTurnHandler : BaseHandler<SyncEndTurn>
{
    protected override void HandleMsg(SyncEndTurn msg)
    {
        string myUid = BattleMgr.Instance.myPlayer.EntityUid;

        if (msg.NextPlayerUid == myUid)
        {
            // 💡 轮到我了，本地切换成我的回合（抽卡，解锁UI）
            BattleMgr.Instance.ChangeState(EBattleState.LocalPlayerTurn);
        }
        else
        {
            // 💡 轮到对手了，本地直接调用 EndTurn() 切换为等待状态
            BattleMgr.Instance.EndTurn();
        }
    }
}
