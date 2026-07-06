// LogicActor.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 逻辑角色基类，专注于模块管理和业务逻辑
/// </summary>
public abstract class LogicActor : LogicObject
{
    #region 模块管理
    /// <summary>
    /// 模块列表，存储所有注册的模块
    /// </summary>
    protected List<IModule> Modules = new List<IModule>();

    /// <summary>
    /// 注册模块
    /// </summary>
    protected void AddModule(IModule module)
    {
        if (module == null)
        {
            Debug.LogError($"{name}：尝试注册空模块！");
            return;
        }

        if (!Modules.Contains(module))
        {
            Modules.Add(module);
            Debug.Log($"{name}：注册模块 {module.GetType().Name}");
        }
    }

    /// <summary>
    /// 获取指定类型的模块
    /// </summary>
    public T GetModule<T>() where T : class, IModule
    {
        foreach (var module in Modules)
        {
            if (module is T targetModule)
            {
                return targetModule;
            }
        }
        return null;
    }

    /// <summary>
    /// 移除指定模块
    /// </summary>
    protected void RemoveModule(IModule module)
    {
        if (module != null && Modules.Contains(module))
        {
            Modules.Remove(module);
            module.LocalDestroy();
        }
    }
    #endregion

    #region 生命周期管理（自动触发模块生命周期）
    public override void LocalInit()
    {
        base.LocalInit();
        RegisterModules();
        
        // 触发所有模块的LocalInit
        foreach (var module in Modules)
        {
            module.LocalInit();
        }
    }

    public override void OnActivate()
    {
        base.OnActivate();
        
        // 触发所有模块的OnActivate
        foreach (var module in Modules)
        {
            module.OnActivate();
        }
    }

    public override void LocalDestroy()
    {
        base.LocalDestroy();
        
        // 触发所有模块的LocalDestroy
        foreach (var module in Modules)
        {
            module.LocalDestroy();
        }
        Modules.Clear();
    }
    #endregion

    /// <summary>
    /// 注册模块（子类实现，注册所有需要的模块）
    /// </summary>
    protected abstract void RegisterModules();
}
