using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
public class RefHandle
{
    public int count = 0;
    public AsyncOperationHandle handle;

    public RefHandle(AsyncOperationHandle handle)
    {
        this.handle = handle;
        count = 1; 
    }
    public AsyncOperationHandle GetAndUseHandle()
    {
        count++;
        return handle;
    }

}

public class AddressMgr : PersistentMonoSingleton<AddressMgr>
{
    private Dictionary<string , RefHandle> address = new Dictionary<string , RefHandle>();

    private Dictionary<string, RefHandle> listResDic = new Dictionary<string, RefHandle>();

    private string GetListKey<T>(params string[] keys)
    {
        var list = new List<string>(keys);
        list.Sort(); // 排序保证唯一性
        //将得到的变长字符拼接成key
        return string.Join("_", list) + "_" + typeof(T).Name;
    }

    /// <summary>
    /// 读取Addressables的资源并把操作柄通过回调函数返回给外部
    /// </summary>
    /// <param name="keys">输入名字和tag</param>
    /// <typeparam name="T"></typeparam>
    public async Task<IList<T>> LoadAssetsAsync<T>(params string[] keys)where T : UnityEngine.Object
    {
        List<string> list = new List<string>(keys);
        string dicKey = GetListKey<T>(keys);
        
        if (listResDic.ContainsKey(dicKey))
        {
            RefHandle refHandle = listResDic[dicKey];
            refHandle.count++; //增加引用次数
            
            // 由于存入字典时泛型丢失（被当成 object 或无类型 handle），
            // 提取时必须使用 Convert<IList<T>>() 转换回来，然后再 await 其 Task！
            AsyncOperationHandle<IList<T>> existHandle = refHandle.handle.Convert<IList<T>>();

            return await existHandle.Task;
        }

        AsyncOperationHandle<IList<T>> handle = 
        Addressables.LoadAssetsAsync<T>(list, null, Addressables.MergeMode.Union);
        listResDic.Add(dicKey, new RefHandle(handle));

        IList<T> result = await handle.Task;

        //错误拦截与清理
        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError($"批量加载失败，Key组合为: {dicKey}");
            listResDic.Remove(dicKey);
            return null;
        }

        return result;
    }


    /// <summary>
    /// 读取Addressables的资源并把资源通过回调函数返回给外部
    /// </summary>
    /// <param name="assetName">资源名</param>
    /// <typeparam name="T"></typeparam>
    public async Task<T> LoadAssetAsync<T>(string assetName) where T:UnityEngine.Object
    {
        string keyName = assetName + "_" + typeof(T).Name;

        if (address.ContainsKey(keyName))
        {
            RefHandle refHandle = address[keyName];
            refHandle.count++;

            return (T)await address[keyName].handle.Task;
        }
        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(assetName);
        address.Add(keyName, new RefHandle(handle));

        T result = await handle.Task;
        // 3. 错误校验
        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError($"加载失败: {assetName}");
            address.Remove(keyName);
            return null;
        }
        return result;
    }
    
    // 在 AddressMgr 中添加这个方法，完美复用你之前写的引用计数和异步逻辑！
    public async Task<T> LoadAssetAsync<T>(AssetReference assetRef) where T : UnityEngine.Object
    {
        if (assetRef == null || !assetRef.RuntimeKeyIsValid())
        {
            Debug.LogError("AssetReference 为空或无效！");
            return null;
        }
    
        // RuntimeKey 就是它底层的唯一地址（GUID 字符串）
        string keyName = assetRef.RuntimeKey.ToString();
    
        // 直接复用你之前写好的、坚如磐石的异步加载！
        return await LoadAssetAsync<T>(keyName);
    }

    public void LoadScene(string sceneName , Action callBack = null)
    {

        Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Single, false).Completed += (handle) => {

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                handle.Result.ActivateAsync().completed += (activateOp) =>
                {

                    callBack?.Invoke();

                };
            }
        };
    }

    public void ReleaseAsset<T>(string assetName)
    {
        string keyName = assetName + "_" + typeof(T).Name;
        if (address.ContainsKey(keyName))
        {
            address[keyName].count--;
            if (address[keyName].count <= 0)
            {
                Addressables.Release(address[keyName].handle);
                address.Remove(keyName);
                Debug.Log($"{assetName}");
            }
        }
    }
    public void ReleaseAssets<T>(params string[] keys)
    {
        string dicKey = GetListKey<T>(keys);

        if (listResDic.ContainsKey(dicKey))
        {
            listResDic[dicKey].count--;
            if (listResDic[dicKey].count <= 0)
            {
                Addressables.Release(listResDic[dicKey].handle);
                listResDic.Remove(dicKey);
                Debug.Log($"{dicKey}");
            }
        }

    }


    public void ClearAllAssets()
    {
        foreach (var item in address.Values)
        {
            Addressables.Release(item.handle);
        }
        address.Clear();

        foreach (var item in listResDic.Values)
        {
            Addressables.Release(item.handle);
        }
        listResDic.Clear();
    }
}

