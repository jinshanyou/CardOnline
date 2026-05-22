using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class GameOverPanel : UIBase
{
    public Button btnBack;

    protected override void OnShow()
    {
        base.OnShow();
        btnBack.onClick.RemoveAllListeners();   
        btnBack.onClick.AddListener(() =>
        {
            EventCenter.Instance.EventTrigger(new MsgComeBackLobby());
        });
    }
}
