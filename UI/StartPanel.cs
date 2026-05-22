using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StartPanel : UIBase
{
    public Button btnStartAi;
    public Button btnStart;
    public Button btnJoinRoom;   // 新增：加入房间(Client)
    public InputField inputIp;   // 新增：输入房主的IP地址 (如果是本机双开测试，默认写 127.0.0.1)
    protected override void OnShow()
    {
        base.OnShow();
        
        btnStartAi.onClick.RemoveAllListeners();
        btnStartAi.onClick.AddListener(StartBattle);
        
        btnStart.onClick.RemoveAllListeners();
        btnStart.onClick.AddListener(() =>
        {
            NetworkMgr.Instance.StartHost();
            Debug.Log("房间已创建，等待客机加入...");
            // 注意：这里不要直接进 BattlePanel！要等客机连进来！
        });
        // 客机：加入房间
        btnJoinRoom.onClick.RemoveAllListeners();
        btnJoinRoom.onClick.AddListener(() =>
        {
            string ip = string.IsNullOrEmpty(inputIp.text) ? "127.0.0.1" : inputIp.text;
            NetworkMgr.Instance.JoinRoom(ip);
        });
    }

    private async void StartBattle()
    {
        // 💡 核心修复：必须先告诉战局，我们要打人机！
        BattleMgr.Instance.isAI = true; 

        await UIMgr.Instance.ShowPanel("BattlePanel");
        BattleMgr.Instance.InitBattle(); // 此时 isAI 为 true，完美运行！
    }
}
