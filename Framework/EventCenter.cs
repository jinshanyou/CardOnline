using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class EventCenter : Singleton<EventCenter>
{
    //用Type做key,不要传入不普通变量类型而是自定义的结构体消息
    private Dictionary<Type , Delegate> eventDic = new Dictionary<Type, Delegate>();

    /// <summary>添加监听者,把自己要做的事情传进这个监听事件</summary>
    /// <param name="acion">传入监听函数的方法</param>
    public void AddEventListener<T>(Action<T> acion)
    {
        Type key = typeof(T);
        if (eventDic.TryGetValue(key , out var callbackAction))
        {
            eventDic[key] = Delegate.Combine(callbackAction , acion);
            return;
        }
        eventDic[key] = acion;
    }
    /// <summary>被监听者调用事件中心的监听事件,完成所有监听者的要求</summary>
    public void EventTrigger<T>(T data)
    {
        Type key = typeof(T);
        if(eventDic.TryGetValue(key,out var callbackAction) && callbackAction is Action<T> action)
        {
            action?.Invoke(data);
        }
    }
    /// <summary>移除监听者的事件,防止内存泄漏</summary>
    /// <param name="callbackActionName">监听函数的索引</param>
    /// <param name="action">传入监听函数的方法</param>
    public void RemoveEventListener<T>(Action<T> acion)
    {
        Type key = typeof(T);
        if (eventDic.TryGetValue(key, out var callbackAction))
        {
            var result = Delegate.Remove(callbackAction, acion);
            if (result == null)
                eventDic.Remove(key);   // 没人监听了，清理 key
            else
                eventDic[key] = result; // 写回
        }
    }

    public void Clear()
    {
        eventDic.Clear();
    }
}


