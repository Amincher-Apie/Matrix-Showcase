// LogicLayer/Interfaces/IAttribute.cs
using System.Collections.Generic;

/// <summary>
/// 属性模块通用接口
/// 定义所有角色(玩家和敌人)属性模块必须实现的基础功能
/// </summary>
public interface IAttribute : IModule
{
    /// <summary>
    /// 获取属性值
    /// </summary>
    float GetAttribute(AttributeType type);
    
    
    /// <summary>
    /// 获取当前等级
    /// </summary>
    int GetLevel();
    
}