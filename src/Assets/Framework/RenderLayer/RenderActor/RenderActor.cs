using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 渲染角色基类
/// 对应逻辑层的LogicActor
/// 提供模块管理功能
/// </summary>
public abstract class RenderActor : RenderObject
{
    /// <summary>
    /// 模块列表
    /// </summary>
    protected List<IRenderModule> Modules = new List<IRenderModule>();

    /// <summary>
    /// 初始化渲染对象
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();
        RegisterModules();
        
        foreach (var module in Modules)
        {
            module.Initialize();
        }
    }

    /// <summary>
    /// 激活渲染对象
    /// </summary>
    public override void OnActivate()
    {
        base.OnActivate();
        
        foreach (var module in Modules)
        {
            module.OnActivate();
        }
    }

    /// <summary>
    /// 销毁渲染对象
    /// </summary>
    public override void Destroy()
    {
        foreach (var module in Modules)
        {
            module.Destroy();
        }
        Modules.Clear();
        
        base.Destroy();
    }

    /// <summary>
    /// 添加渲染模块
    /// </summary>
    protected void AddModule(IRenderModule module)
    {
        if (module == null)
        {
            Debug.LogError("尝试添加空渲染模块");
            return;
        }
        Modules.Add(module);
    }

    /// <summary>
    /// 获取渲染模块
    /// </summary>
    public T GetModule<T>() where T : class, IRenderModule
    {
        foreach (var module in Modules)
        {
            if (module is T target)
                return target;
        }
        return null;
    }

    /// <summary>
    /// 注册模块（子类实现）
    /// </summary>
    protected abstract void RegisterModules();
}