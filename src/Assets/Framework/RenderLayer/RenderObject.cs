using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 所有渲染对象的基类
/// 管理渲染对象的基础生命周期
/// </summary>
public abstract class RenderObject : MonoBehaviour, IRenderObject
{
    /// <summary>
    /// 标记是否已激活
    /// </summary>
    protected bool _isActivated = false;

    protected virtual void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// 初始化渲染对象
    /// 逻辑对象创建后调用
    /// </summary>
    public virtual void Initialize()
    {
    }

    /// <summary>
    /// 激活渲染对象
    /// 对应逻辑对象的网络生成完成
    /// </summary>
    public virtual void OnActivate()
    {
        if (!_isActivated)
        {
            _isActivated = true;
            gameObject.SetActive(true);
        }
    }

    private void OnDestroy()
    {
        Destroy();
    }

    /// <summary>
    /// 销毁渲染对象
    /// 在逻辑对象销毁前调用
    /// </summary>
    public virtual void Destroy()
    {
        Destroy(gameObject);
    }
    
}