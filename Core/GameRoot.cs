using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class GameRoot : MonoBehaviour
{
    private async void Start()
    {
        DontDestroyOnLoad(this.gameObject);
        Debug.Log("==== 游戏启动，开始初始化世界 ====");

        await InitFrameworkAsync();
        
        // ================== 💡 核心修改：立刻展示 Loading 覆盖全屏！ ==================
        LoadingPanel loadingUI = await UIMgr.Instance.ShowPanel("LoadingPanel") as LoadingPanel;
        // ===========================================================================
        
        await InitGameDataAsync();
        
        // 模拟一点点最后的停顿，让玩家看清加载到了 100%
        await Task.Delay(500); 
        
        await UIMgr.Instance.ShowPanel("StartPanel");
        Debug.Log("==== 初始化完成，进入大厅 ====");
        
        // ================== 💡 核心修改：加载完后，Loading 面板淡出并彻底销毁释放内存 ==================
        if (loadingUI != null)
        {
            await loadingUI.FadeOutAndDestroy();
        }
        // ===========================================================================

        
        //EnterLobby();
    }
    
    // 阶段一：唤醒底层技术基座 (唤醒它们的 Instance 触发单例构造)
    private async Task InitFrameworkAsync()
    {
        // 1. 唤醒对象池 (因为它要建 Root 节点)
        var pool = ObjectPoolMgr.Instance;
        
        // 2. 唤醒 UI 管理器 (它要找 Canvas)
        var uiMgr = UIMgr.Instance; 
        
        // 3. 唤醒 Addressables 管理器
        var addressMgr = AddressMgr.Instance;
        
        // 💡 核心新增：开局立刻唤醒网络管理器，让其执行反射注册，防止单机模式下报错！
        var netMgr = TcpAsyncMgr.Instance; 
        
        // 模拟一点加载延迟，或者在这里你可以控制进度条 (LoadingPanel.SetProgress(0.3f))
        await Task.Yield(); 
        Debug.Log("底座基建初始化完毕...");
    }

    // 阶段二：加载游戏核心业务数据
    private async Task InitGameDataAsync()
    {
        // 唤醒卡牌数据管理器，并等待它把所有的 ScriptableObject 加载进内存
        var cardMgr = CardMgr.Instance;
        
        // 等待直到卡牌数据彻底加载完毕！
        while (!cardMgr.IsLoaded) 
        {
            await Task.Yield();
        }

        Debug.Log("配置表与数据初始化完毕...");
    }
    
    // 阶段三：进入大厅状态
    private void EnterLobby()
    {
        // 打开大厅 UI 面板
        //UIMgr.Instance.ShowPanelAsync("LobbyPanel");
    }
}
