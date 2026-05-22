using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MyObjectPool<T>
{
    private Stack<T> pool = new Stack<T>();

    private int maxSize;
    /// <summary>池子最大容量</summary>
    public int MaxSize => maxSize;
    /// <summary>池中当前可用对象数</summary>
    public int Count => pool.Count;

    //池子没有对象时创建
    private Func<T> Pool_ActionCreate;
    //从池子里拿到对象时的回调函数
    private Action<T> Pool_ActionOnGet;
    //把对象放回池子里的回调函数
    private Action<T> Pool_ActionOnRelease;
    //池子对象满了销毁的回调函数
    private Action<T> Pool_ActionOnDestroy;

    //构造函数将回调函数全都初始化
    public MyObjectPool(Func<T> ActionCreate,Action<T> ActionOnGet = null, Action<T> ActionOnRelease = null, Action<T> ActionOnDestroy = null,int maxSize = 50)
    {
        Pool_ActionCreate = ActionCreate ?? throw new ArgumentNullException(nameof(ActionCreate), "创建函数不能为空");
        Pool_ActionOnGet = ActionOnGet;
        Pool_ActionOnRelease = ActionOnRelease;
        Pool_ActionOnDestroy = ActionOnDestroy;
        this.maxSize = maxSize > 0 ? maxSize : 1;

        pool = new Stack<T>(this.maxSize);
    }


    ///<summary>从池子里拿东西</summary>
    ///<returns>OBJ(T)</returns>
    public T Get()
    {
        T obj;
        //池子没有对象时创建
        if(pool.Count == 0)
        {
           obj = Pool_ActionCreate.Invoke();
        }
        else
        {
            obj = pool.Pop();
        }
        Pool_ActionOnGet?.Invoke(obj);
        return obj;
    }
    ///<summary>将对象放回池子</summary>
    public void Release(T obj)
    {
        if (obj == null)
        {
            return;
        }
        if (pool.Count < maxSize)
        {
            Pool_ActionOnRelease?.Invoke(obj);
            pool.Push(obj);
        }
        else
        {
            Pool_ActionOnDestroy?.Invoke(obj);
        }
    }
    //清空池子
    public void Clear()
    {
        foreach (var obj in pool)
        {
            if (Pool_ActionOnDestroy != null)
            {
                Pool_ActionOnDestroy(obj);
            }
        }
        pool.Clear(); 
    }
}
