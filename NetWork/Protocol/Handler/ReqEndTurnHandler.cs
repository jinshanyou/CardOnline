using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[NetMsgID(2004)]
public class ReqEndTurnHandler : BaseHandler<ReqEndTurn>
{
    protected override void HandleMsg(ReqEndTurn msg)
    {
        if (NetworkMgr.Instance.server == null) return;

        // 谁结束了回合，下一个就是另一个人
        string nextPlayer = (msg.PlayerUid == "Host") ? "Client" : "Host";

        SyncEndTurn sync = new SyncEndTurn();
        sync.NextPlayerUid = nextPlayer;

        // 广播切换回合
        NetworkMgr.Instance.server.BroadMsg(sync);
    }
}
