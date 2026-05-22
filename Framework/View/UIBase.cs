using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
public class UIBase : MonoBehaviour
{
    // 弃用 UnityAction，使用纯 C# Action
    private List<Action> unregisterActions = new List<Action>();

    /// <summary>打开面板</summary>
    public virtual void Show()
    {
        gameObject.SetActive(true);
        OnShow(); // 暴露给子类，处理显示逻辑和事件注册
    }

    /// <summary>关闭面板</summary>
    public virtual void Hide()
    {
        UnregisterAllEvents(); // 核心：面板一旦隐藏，切断所有事件监听！
        OnHide(); // 暴露给子类，处理清理逻辑
        gameObject.SetActive(false);
    }
    
    /// <summary> 子类重写：面板显示时调用 </summary>
    protected virtual void OnShow() { }

    /// <summary> 子类重写：面板隐藏时调用 </summary>
    protected virtual void OnHide() { }
    
    /// <summary>子类注册事件专用接口（禁止子类直接调用 EventCenter）</summary>
    protected void AddUIEvent<T>(Action<T> action)
    {
        EventCenter.Instance.AddEventListener<T>(action);
        // 记录注销动作（闭包捕获 action 和 T）
        unregisterActions.Add(() => EventCenter.Instance.RemoveEventListener<T>(action));
    }    
    private void UnregisterAllEvents()
    {
        for (int i = 0; i < unregisterActions.Count; i++)
        {
            unregisterActions[i].Invoke();
        }
        unregisterActions.Clear();
    }
    protected virtual void OnDestroy()
    {
        // 兜底机制：万一面板不是被 Hide 而是被直接 Destroy，也要清空
        UnregisterAllEvents();
    }
}
