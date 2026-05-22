using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolMgr :PersistentMonoSingleton<ObjectPoolMgr>
{
    private Dictionary<string,MyObjectPool<GameObject>> poolDic = new Dictionary<string, MyObjectPool<GameObject>>();
    // 根节点，用来统一管理被隐藏的物体，保持 Hierarchy 干净
    private Transform poolRoot; 

    protected override void OnSingletonAwake()
    {
        poolRoot = new GameObject("PoolRoot").transform;
        poolRoot.SetParent(this.transform); // 将回收站挂在单例节点下
    }

    public GameObject GetGameObject(string objName,GameObject prefab)
    {
        //没有这个池子
        if(!poolDic.ContainsKey(objName))
        {
            poolDic.Add(objName , new MyObjectPool<GameObject>(
                ActionCreate:() =>
                {
                    GameObject obj = Instantiate(prefab);
                    obj.name = objName;
                    return obj;
                },
                ActionOnGet:(obj) =>
                {
                    obj.SetActive(true);
                    obj.transform.SetParent(null);
                },
                ActionOnRelease:(obj) =>
                {
                    obj.SetActive(false);
                    obj.transform.SetParent(poolRoot);
                },
                ActionOnDestroy:(obj) =>
                {
                    Destroy(obj);
                },
                maxSize: 100
            ));
        }
        return poolDic[objName].Get();
    }

    public void ReleaseGameObject(string objName, GameObject obj)
    {
        if(poolDic.TryGetValue(objName , out var pool))
        {
            pool.Release(obj);
        }
                else
        {
            Destroy(obj); // 如果没有这个池子，说明是没用对象池的对象直接销毁了
        }
    }


    /// <summary>销毁指定池子，释放 Addressables 引用</summary>
    public void DestroyPool(string key)
    {
        if (!poolDic.TryGetValue(key, out var info)) return;
        info.Clear();  // 销毁所有实例
        poolDic.Remove(key);
    }
    /// <summary>销毁所有池子</summary>
    public void DestroyAllPools()
    {
        foreach (var kv in poolDic)
        {
            kv.Value.Clear();
        }
        poolDic.Clear();
    }
}
