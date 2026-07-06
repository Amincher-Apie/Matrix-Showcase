// LogicLayer/Module/AttributeModule/AttributeModule.cs
using UnityEngine;

/// <summary>
/// 逻辑层属性模块基类 - 实现IAttribute接口，直接读取服务器属性模块的值
/// </summary>
public abstract class AttributeModule : IAttribute
{
    protected LogicActor _owner;
    protected ServerAttributeModule _serverAttributeModule;
    
    public abstract ulong ObjectId { get; }
    
    protected AttributeModule(LogicActor owner)
    {
        _owner = owner;
    }
    
    #region IModule实现
    public virtual void LocalInit()
    {

    }
    
    public virtual void OnActivate()
    {
        // 逻辑层模块激活时不需要特殊处理
    }
    
    public virtual void LocalDestroy()
    {
        _serverAttributeModule = null;
        _owner = null;
    }
    #endregion
    
    #region IAttribute实现
    public virtual float GetAttribute(AttributeType type)
    {
        // AttributeModule的GetAttribute方法，直接从服务器模块的NetworkVariable获取值,
        // 但是由于ServerAttribute为泛型类，所以要转换模式。
        // if (_serverAttributeModule == null)
        // {
        //     Debug.LogWarning($"[AttributeModule] ServerAttributeModule为空，无法获取属性: {type}");
        //     return 0f;
        // }
        //
        // // 直接从服务器模块的NetworkVariable获取值
        // return type switch
        // {
        //     // 基础属性
        //     AttributeType.Health => _serverAttributeModule.GetAttribute(AttributeType.Health),
        //     AttributeType.MaxHealth => _serverAttributeModule.GetAttribute(AttributeType.MaxHealth),
        //     AttributeType.Shield => _serverAttributeModule.GetAttribute(AttributeType.Shield),
        //     AttributeType.MaxShield => _serverAttributeModule.GetAttribute(AttributeType.MaxShield),
        //     AttributeType.Armor => _serverAttributeModule.GetAttribute(AttributeType.Armor),
        //     AttributeType.MoveSpeed => _serverAttributeModule.GetAttribute(AttributeType.MoveSpeed),
        //     AttributeType.Level => _serverAttributeModule.GetAttribute(AttributeType.Level),
        //     
        //     // 抗性属性
        //     AttributeType.Resistance_Solid => _serverAttributeModule.GetAttribute(AttributeType.Resistance_Solid),
        //     AttributeType.Resistance_Liquid => _serverAttributeModule.GetAttribute(AttributeType.Resistance_Liquid),
        //     AttributeType.Resistance_Gas => _serverAttributeModule.GetAttribute(AttributeType.Resistance_Gas),
        //     AttributeType.Resistance_Toxic => _serverAttributeModule.GetAttribute(AttributeType.Resistance_Toxic),
        //     AttributeType.Resistance_Fire => _serverAttributeModule.GetAttribute(AttributeType.Resistance_Fire),
        //     AttributeType.Resistance_Ice => _serverAttributeModule.GetAttribute(AttributeType.Resistance_Ice),
        //     AttributeType.Resistance_Electric => _serverAttributeModule.GetAttribute(AttributeType.Resistance_Electric),
        //     
        //     // 伤害加成
        //     AttributeType.DamageOutPutRate => _serverAttributeModule.GetAttribute(AttributeType.DamageOutPutRate),
        //     
        //     // 派系
        //     AttributeType.Faction => _serverAttributeModule.GetAttribute(AttributeType.Faction),
        //     
        //     _ => GetSpecificAttribute(type)
        // };
        Debug.LogError("无法从基类[LogicLayer]Attribute中返回值。");
        return 0;
    }
    
    public virtual int GetLevel()
    {
        return (int)(_serverAttributeModule?.GetAttribute(AttributeType.Level) ?? 1);
    }

    #endregion
    
}