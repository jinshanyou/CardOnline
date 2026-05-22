using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//可以跨场景的单例基类
public abstract class PersistentMonoSingleton<T> : MonoBehaviour where T : PersistentMonoSingleton<T>
{
    private static T instance;
    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                // 1. 先尝试在场景里找
                instance = FindObjectOfType<T>();
                
                // 2. 如果场景里还是没有，自动创建一个挂载该脚本的空物体兜底
                if (instance == null)
                {
                    GameObject obj = new GameObject($"[{typeof(T).Name}]");
                    instance = obj.AddComponent<T>();
                }
            }
            return instance;
        }
    }

    protected virtual void Awake()
    {
        if (instance == null)
        {
            instance = this as T;
            transform.SetParent(null); // 确保是根节点
            DontDestroyOnLoad(gameObject); // ✅ 在这里设置跨场景，只执行一次
            OnSingletonAwake();
        }
        else if (instance != this)
        {
            Debug.LogWarning($"场景中存在多个 {typeof(T).Name} 的实例，已自动销毁克隆体。");
            Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    protected virtual void OnSingletonAwake()
    {
        
    }
}
