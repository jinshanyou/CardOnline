using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//单场景基类
public abstract class MonoSingleton<T> : MonoBehaviour where T:MonoBehaviour
{
    private static T instance;
    public static T Instance
    {
        get
        {
            if(instance!= null)
            {
                return instance;
            }

            instance = FindObjectOfType<T>();

            if (instance == null)
            {
                GameObject obj = new GameObject(typeof(T).Name);
                instance = obj.AddComponent<T>();
            }
            return instance;
        }
    }

    protected virtual void Awake()
    {
        if (instance == null)
        {
            instance = this as T;
            OnSingletonAwake();
        }
        else if (instance != this)
        {
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
