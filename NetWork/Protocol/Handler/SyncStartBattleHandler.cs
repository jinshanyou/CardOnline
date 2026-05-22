using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[NetMsgID(2003)]
public class SyncStartBattleHandler : BaseHandler<SyncStartBattle>
{
    protected async override void HandleMsg(SyncStartBattle msg)
    {
        Debug.Log($"<color=cyan>[表现层] 收到开战指令！本局随机种子为: {msg.RandomSeed}</color>");

        // 1. 设置网络模式
        BattleMgr.Instance.isAI = false; 

        // 2. 隐藏大厅面板
        UIMgr.Instance.HidePanel("StartPanel");

        // 3. 呼出战斗UI
        await UIMgr.Instance.ShowPanel("BattlePanel");

        // 4. 💡 重点：把房主发来的种子塞给 BattleMgr，让它去初始化！
        BattleMgr.Instance.InitBattle(msg.RandomSeed); 
    }
}
