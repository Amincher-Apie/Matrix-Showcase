using System;
using System.Collections.Generic;

/// <summary>
/// 堆叠规则注册表：管理所有可用的堆叠规则
/// </summary>
public static class StackingRuleRegistry
{
    private static readonly Dictionary<string, IStackingRule> _rules = new Dictionary<string, IStackingRule>();
    
    /// <summary>
    /// 注册堆叠规则
    /// </summary>
    public static void Register(string name, IStackingRule rule)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("规则名称不能为空", nameof(name));
        if (rule == null)
            throw new ArgumentNullException(nameof(rule));
            
        _rules[name] = rule;
    }
    
    /// <summary>
    /// 获取堆叠规则
    /// </summary>
    public static IStackingRule Get(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
            
        _rules.TryGetValue(name, out var rule);
        return rule;
    }
    
    /// <summary>
    /// 检查规则是否存在
    /// </summary>
    public static bool HasRule(string name)
    {
        return !string.IsNullOrEmpty(name) && _rules.ContainsKey(name);
    }
    
    /// <summary>
    /// 获取所有已注册的规则名称
    /// </summary>
    public static IEnumerable<string> GetAllRuleNames()
    {
        return _rules.Keys;
    }
}

