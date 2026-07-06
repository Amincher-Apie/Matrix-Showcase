using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Mono;
using Framework.Singleton;
using UnityEngine;

public class EffectRenderManager : SingletonBase<EffectRenderManager>
{
    /// <summary>跨场景不移除 长期存在的特效的父节点</summary>
    private Transform _effectRoot; //暂时想不到有啥特效放这下面 可能是满世界飘动的尘埃？？
    
    /// <summary>临时特效的父节点</summary>
    private Transform _tempEffectRoot; //类似爆炸特效 不知道设置成什么为父节点 可以设置成这个

    protected override void Initialize()
    {
        base.Initialize();
        CreateEffectRoots();
    }

    #region 公开方法

    /// <summary>
    /// 同步渲染特效的方法
    /// </summary>
    /// <param name="path">特效预制体的路径</param>
    /// <param name="parent">特效的父节点</param>
    /// <param name="position">特效的位置</param>
    /// <param name="rotation">特效的旋转</param>
    /// <param name="autoRecycle">是否自动回收特效(待所有粒子播放完毕以后)</param>
    /// <param name="recycleDelay">特效回收延迟时间(默认值就是按粒子播完的最大时间 自己填时间就按这个时间回收)</param>
    /// <returns></returns>
    public GameObject PlayEffect(string path, Transform parent = null, Vector3 position = default,
        Quaternion rotation = default, bool autoRecycle = true, float recycleDelay = -1)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("特效预制体路径为空");
            return null;
        }
        
        var effectObj = PoolManager.Instance.Get(path);
        if (effectObj == null)
        {
            Debug.LogError("获得特效GameObject实例失败");
            return null;
        }
        
        // 设置特效的变换属性
        SetupEffectTransform(effectObj, parent, position, rotation);
        
        effectObj.SetActive(true);

        if (autoRecycle)
            StartRecycleTimer(effectObj, path, recycleDelay);
        
        return effectObj;
    }

    /// <summary>
    /// 异步渲染特效的方法
    /// </summary>
    /// <param name="path">特效预制体的路径</param>
    /// <param name="onComplete">渲染完成后的回调函数</param>
    /// <param name="parent">特效的父节点</param>
    /// <param name="position">特效的位置</param>
    /// <param name="rotation">特效的旋转</param>
    /// <param name="autoRecycle">是否自动回收特效(待所有粒子播放完毕以后)</param>
    /// <param name="recycleDelay">特效回收延迟时间(默认值就是按粒子播完的最大时间 自己填时间就按这个时间回收)</param>
    public void PlayEffectAsync(string path, Transform parent = null, Action<GameObject> onComplete = null, 
        Vector3 position = default, Quaternion rotation = default, bool autoRecycle = true, float recycleDelay = -1)
    {
        PoolManager.Instance.GetAsync(path, (effectObj) =>
        {
            if (effectObj == null)
            {
                Debug.LogError("获得特效GameObject实例失败");
                onComplete?.Invoke(null);
                return;
            }
            
            // 设置特效的变换属性
            SetupEffectTransform(effectObj, parent, position, rotation);
            
            effectObj.SetActive(true);

            if (autoRecycle)
                StartRecycleTimer(effectObj, path, recycleDelay);
            
            onComplete?.Invoke(effectObj);
        });
    }

    /// <summary>
    /// 回收特效GameObject
    /// </summary>
    /// <param name="path">特效预制体的路径</param>
    /// <param name="effectObj">特效实例GameObject</param>
    public void RecycleEffect(string path, GameObject effectObj)
    {
        if (effectObj == null) return;
        
        var particleSystems = effectObj.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particleSystems)
        {
            var main = ps.main;
            main.loop = false; // 停止循环
            ps.Stop(); // 立即停止播放
        }
        
        PoolManager.Instance.Recycle(path, effectObj);
    }
    
    /// <summary>预加载特效</summary>
    public void PreloadEffect(string path, int count = 1)
    {
        if (count <= 0) return;
        PoolManager.Instance.Preload(path, count);
    }

    /// <summary>异步预加载特效</summary>
    public void PreloadEffectAsync(string path, int count = 1, Action<bool> onComplete = null)
    {
        if (count <= 0)
        {
            onComplete?.Invoke(false);
            return;
        }
        PoolManager.Instance.PreloadAsync(path, count, onComplete);
    }

    /// <summary>清理指定特效池</summary>
    public void ClearEffectPool(string path, bool destroyActive = false)
    {
        PoolManager.Instance.ClearPool(path, destroyActive);
    }

    /// <summary>清理所有特效池</summary>
    public void ClearAllEffectPools(bool destroyActive = false)
    {
        PoolManager.Instance.ClearAllPools(destroyActive);
    }

    public override void Release()
    {
        ClearAllEffectPools(true);
        if (_tempEffectRoot != null)
        {
            UnityEngine.Object.Destroy(_tempEffectRoot.gameObject);
        }
        base.Release();
    }

    #endregion

    #region 私有工具方法

    private void CreateEffectRoots()
    {
        // 持久化根节点（不随场景销毁）
        var persistentRoot = new GameObject("PersistentEffectRoot");
        UnityEngine.Object.DontDestroyOnLoad(persistentRoot);
        _effectRoot = persistentRoot.transform;

        // 临时根节点（随场景销毁）
        var tempRoot = new GameObject("TempEffectRoot");
        _tempEffectRoot = tempRoot.transform;
    }
    
    /// <summary>
    /// 设置特效的变换属性（位置、旋转和父节点）
    /// </summary>
    /// <param name="effectObj">特效GameObject</param>
    /// <param name="parent">父节点（为空时使用临时特效根节点）</param>
    /// <param name="position">位置（默认为原点）</param>
    /// <param name="rotation">旋转（默认为不旋转）</param>
    private void SetupEffectTransform(GameObject effectObj, Transform parent = null, 
        Vector3 position = default, Quaternion rotation = default)
    {
        // 设置父节点（如果未指定，使用临时特效根节点）
        Transform targetParent = parent != null ? parent : _tempEffectRoot;
        effectObj.transform.SetParent(targetParent, false);
        
        // 设置位置（如果未指定，使用原点）
        effectObj.transform.localPosition = position == default ? Vector3.zero : position;
        
        // 设置旋转（如果未指定，使用不旋转）
        effectObj.transform.localRotation = rotation == default ? Quaternion.identity : rotation;
    }
    
    private void StartRecycleTimer(GameObject effectObj, string path, float delay)
    {
        if (delay < 0)
        {
            // 获取所有粒子系统（包括子对象）
            var particleSystems = effectObj.GetComponentsInChildren<ParticleSystem>(true);
            if (particleSystems.Length > 0)
            {
                float maxDuration = 0;
                foreach (var ps in particleSystems)
                {
                    var main = ps.main;
                    // 粒子总时长 = 持续时间 + 最大生命周期
                    float total = main.duration + main.startLifetime.constantMax;
                    if (total > maxDuration)
                    {
                        maxDuration = total;
                    }
                }
                delay = maxDuration;
            }
            else
            {
                Debug.LogWarning($"特效 {path} 未找到任何ParticleSystem组件，使用默认回收延迟1秒");
                delay = 1f;
            }
        }

        MonoManager.Instance.StartCoroutine(RecycleAfterDelay(effectObj, path, delay));
    }
    
    private IEnumerator RecycleAfterDelay(GameObject effectObj, string path, float delay)
    {
        yield return new WaitForSeconds(delay);
        RecycleEffect(path, effectObj);
    }

    #endregion
}