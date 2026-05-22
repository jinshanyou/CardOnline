using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public struct MsgComeBackLobby
{
    
}

public class WinPanel : UIBase
{
    public Button btnBack;

    protected override void OnShow()
    {
        base.OnShow();
        btnBack.onClick.RemoveAllListeners();   
        btnBack.onClick.AddListener(() =>
        {
            print("返回大厅");
            EventCenter.Instance.EventTrigger(new MsgComeBackLobby());
        });
    }
}
