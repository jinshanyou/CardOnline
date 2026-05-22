using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

public class UIMgr : PersistentMonoSingleton<UIMgr>
{
    Dictionary<string, UIBase> panelDic = new Dictionary<string, UIBase>();

    // 💡 高级并发控制：缓存正在加载的【任务(Task)】，而不是简简单单的 bool 或 string
    private Dictionary<string, Task<UIBase>> loadingTasks = new Dictionary<string, Task<UIBase>>();

    private Transform canvasRoot;

    protected override void OnSingletonAwake()
    {
        GameObject canvasObj = GameObject.Find("Canvas");
        if (canvasObj == null) Debug.LogError("没有找到 Canvas！");
        else
        {
            canvasRoot = canvasObj.transform;
            DontDestroyOnLoad(canvasObj);
        }
        EventCenter.Instance.AddEventListener<MsgOnGameOver>(OnGameOver);
        EventCenter.Instance.AddEventListener<MsgOnWin>(OnWin);
        EventCenter.Instance.AddEventListener<MsgComeBackLobby>(ComeBackLobby);
    }

    public async Task<UIBase> ShowPanel(string panelName)
    {
        if (panelDic.TryGetValue(panelName,out UIBase panel))
        {
            panel.Show();
            return panel;
        }
        // 2. 已有正在进行的加载任务，等它
        if (loadingTasks.TryGetValue(panelName, out Task<UIBase> existingTask))
        {
            UIBase ui = await existingTask;
            if (ui != null) ui.Show();  // ← 加 null 检查
            return ui;
        } 
        Task<UIBase> loadTask = InternalLoadAndInstantiateAsync(panelName);

        loadingTasks.Add(panelName,loadTask);
        UIBase resultUI = await loadTask;
        loadingTasks.Remove(panelName);  // ← 无论成功失败，清理掉
        return resultUI;
    }

    // 内部核心加载实例化逻辑
    private async Task<UIBase> InternalLoadAndInstantiateAsync(string panelName)
    {
        //等 Addressables 加载
        GameObject prefab = await AddressMgr.Instance.LoadAssetAsync<GameObject>(panelName);
        if (prefab == null)
        {
            Debug.LogError($"加载面板失败: {panelName}");
            return null;
        }
        // 实例化
        GameObject panelObj = Instantiate(prefab, canvasRoot);
        panelObj.name = panelName;

        UIBase uiBase = panelObj.GetComponent<UIBase>();
        if (uiBase != null)
        {
            panelDic.Add(panelName, uiBase);
            uiBase.Show();
        }
        return uiBase;
    }
    
    // HidePanel 和 DestroyPanel 保持不变
        /// <summary>
    /// 隐藏面板 (不销毁，缓存在内存中)
    /// </summary>
    public void HidePanel(string panelName)
    {
        if (panelDic.ContainsKey(panelName))
        {
            panelDic[panelName].Hide(); // 内部会自动注销事件
        }
    }
        
    private async void OnGameOver(MsgOnGameOver msg)
    {
        //等待 2 秒钟的死亡动画演出时间
        await Task.Delay(2000); 
        //呼出失败结算面板 (假设你将来会做一个 LosePanel 预制体)
        await ShowPanel("GameOverPanel");
        Debug.Log("弹出失败面板！");
    }

    private async void OnWin(MsgOnWin msg)
    {
        await Task.Delay(2000); 
        await ShowPanel("WinPanel");
        Debug.Log("弹出胜利面板！");
    }

    private async void ComeBackLobby(MsgComeBackLobby msg)
    {
        //重置战斗状态(是不是让Battlemgr自己做比较好???)
        BattleMgr.Instance.Dispose();
        
        // 2. 💡 重点：彻底断开并清理所有网络连接！释放端口！
        NetworkMgr.Instance.Shutdown();
        
        //关闭所有面板
        DestroyPanel("BattlePanel");
        DestroyPanel("WinPanel");
        DestroyPanel("GameOverPanel");
        //打开大厅面板
        await Task.Delay(2000);
        await ShowPanel("StartPanel");
    }

    /// <summary>
    /// 彻底销毁面板，并释放 Addressables 内存
    /// </summary>
    public void DestroyPanel(string panelName)
    {
        if (panelDic.TryGetValue(panelName, out UIBase uiBase))
        {
            Destroy(uiBase.gameObject); // 销毁实体
            panelDic.Remove(panelName); // 移出字典
            
            // 💡 重点：调用你的 AddressMgr 释放引用计数，如果归零，底层会自动卸载 AssetBundle！
            AddressMgr.Instance.ReleaseAsset<GameObject>(panelName); 
        }
    }
    
}
