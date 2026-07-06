using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 单个效果的条件块配置。
/// </summary>
[Serializable]
public class ConditionBlock
{
    [LabelText("条件ID")]
    [Tooltip("选择触发条件类型，所有条件都通过才能触发效果")]
    [ValueDropdown("GetConditionIds")]
    [OnValueChanged("OnConditionIdChanged")]
    public string conditionId;
    
    [LabelText("参数配置")]
    [Tooltip("配置该条件所需的参数（如概率、阈值等）")]
    [HideIf("@string.IsNullOrEmpty(conditionId)")]
    public SerializableStringFloatMap @params = new SerializableStringFloatMap();
    
    private IEnumerable<ValueDropdownItem<string>> GetConditionIds()
    {
        var items = new ValueDropdownList<string>();
        foreach (var id in QualityEffectConstants.ConditionIds)
        {
            string chineseName = QualityEffectConstants.ConditionIdNames.TryGetValue(id, out var name) ? name : id;
            items.Add($"{chineseName} ({id})", id);
        }
        return items;
    }
    
    private void OnConditionIdChanged()
    {
        // 更新参数映射的上下文ID，用于过滤可用参数
        if (@params != null)
        {
            @params.contextId = conditionId;
            // 更新所有已有参数条目的上下文ID
            foreach (var entry in @params.paramEntries)
            {
                entry.contextId = conditionId;
            }
        }
    }
}

/// <summary>
/// 单个效果的动作块配置。
/// </summary>
[Serializable]
public class ActionBlock
{
    [LabelText("动作ID")]
    [Tooltip("选择要执行的动作类型")]
    [ValueDropdown("GetActionIds")]
    [OnValueChanged("OnActionIdChanged")]
    public string actionId;
    
    [LabelText("参数配置")]
    [Tooltip("配置该动作所需的参数（如伤害值、持续时间等）")]
    [HideIf("@string.IsNullOrEmpty(actionId)")]
    public SerializableStringFloatMap @params = new SerializableStringFloatMap();
    
    private IEnumerable<ValueDropdownItem<string>> GetActionIds()
    {
        var items = new ValueDropdownList<string>();
        foreach (var id in QualityEffectConstants.ActionIds)
        {
            string chineseName = QualityEffectConstants.ActionIdNames.TryGetValue(id, out var name) ? name : id;
            items.Add($"{chineseName} ({id})", id);
        }
        return items;
    }
    
    private void OnActionIdChanged()
    {
        // 更新参数映射的上下文ID，用于过滤可用参数
        if (@params != null)
        {
            @params.contextId = actionId;
            // 更新所有已有参数条目的上下文ID
            foreach (var entry in @params.paramEntries)
            {
                entry.contextId = actionId;
            }
        }
    }
}

/// <summary>
/// 可数据驱动的品质效果定义（触发-条件-动作）。
/// </summary>
[Serializable]
public class QualityEffectDefinition
{
    [LabelText("效果ID")]
    [Tooltip("效果的唯一标识符，用于冷却和检测")]
    public string id;
    
    [LabelText("标签")]
    [Tooltip("逗号分隔的标签，控制效果合并策略（如：MergeAll、MergeByQuality、NoMerge）")]
    public string tags;
    
    [LabelText("最大层数")]
    [Tooltip("该效果最多可叠加多少层（默认99）")]
    public int maxStacks = 99;
    
    [LabelText("冷却时间(秒)")]
    [Tooltip("效果触发后的冷却时间，0表示无冷却")]
    public float cooldown = 0f;

    [LabelText("触发器列表")]
    [Tooltip("满足任意一个触发器即可进入条件检查（如：OnHitDealt、OnKill等）")]
    [ValueDropdown("GetTriggerIds")]
    public List<string> triggers = new List<string>(); // 例: OnHitDealt, OnKill, OnTick:1.0
    
    private IEnumerable<ValueDropdownItem<string>> GetTriggerIds()
    {
        var items = new ValueDropdownList<string>();
        foreach (var id in QualityEffectConstants.TriggerIds)
        {
            string chineseName = QualityEffectConstants.TriggerIdNames.TryGetValue(id, out var name) ? name : id;
            items.Add($"{chineseName} ({id})", id);
        }
        return items;
    }

    [LabelText("条件列表")]
    [Tooltip("所有条件都通过才能触发道具效果")]
    public List<ConditionBlock> conditions = new List<ConditionBlock>();

    [LabelText("动作列表")]
    [Tooltip("条件通过后要执行的动作（按顺序执行）")]
    public List<ActionBlock> actions = new List<ActionBlock>();
    
    [Space(10)]
    [LabelText("参数堆叠规则")]
    [Tooltip("为每个参数指定堆叠规则（如：duration=Add, chance=StackByQuality）。留空使用默认规则（Add）")]
    [ListDrawerSettings(ShowIndexLabels = true, CustomAddFunction = "CreateDefaultStackingRuleEntry")]
    [InfoBox("预设规则：Add(累加), Max(最大值), Min(最小值), NoStack(不叠加), Average(平均值), StackByQuality(按品质叠加)")]
    public List<ParamStackingRuleEntry> paramStackingRules = new List<ParamStackingRuleEntry>();
    
    private ParamStackingRuleEntry CreateDefaultStackingRuleEntry()
    {
        return new ParamStackingRuleEntry();
    }
    
    /// <summary>
    /// 获取参数堆叠规则映射表
    /// </summary>
    public Dictionary<string, string> BuildParamStackingRuleMap()
    {
        var map = new Dictionary<string, string>();
        if (paramStackingRules == null) return map;
        
        foreach (var entry in paramStackingRules)
        {
            if (entry == null || string.IsNullOrEmpty(entry.paramKey)) continue;
            string rule = string.IsNullOrEmpty(entry.ruleName) ? "Add" : entry.ruleName;
            map[entry.paramKey] = rule;
        }
        return map;
    }
}

/// <summary>
/// 参数值类型
/// </summary>
public enum EffectParamType
{
    [LabelText("浮点数")]
    Float,
    [LabelText("字符串")]
    String
}

/// <summary>
/// 单个参数配置项
/// </summary>
[Serializable]
public class EffectParamEntry
{
    [HideInInspector]
    [Tooltip("上下文ID（条件ID或动作ID），用于过滤可用参数")]
    public string contextId = "";
    
    [LabelText("参数名")]
    [Tooltip("选择或输入参数名称（如：chance.base、duration、buffId等）")]
    [ValueDropdown("GetParamKeys")]
    public string key;
    
    [LabelText("参数类型")]
    [Tooltip("选择参数类型：浮点数或字符串")]
    [OnValueChanged("OnTypeChanged")]
    public EffectParamType type = EffectParamType.Float;
    
    [LabelText("数值")]
    [Tooltip("浮点数值（如：0.2、5.0等）")]
    [ShowIf("type", EffectParamType.Float)]
    public float floatValue;
    
    [LabelText("文本值")]
    [Tooltip("字符串值（如：\"Slow\"、\"Rage\"等）")]
    [ShowIf("type", EffectParamType.String)]
    public string stringValue;
    
    private IEnumerable<ValueDropdownItem<string>> GetParamKeys()
    {
        var items = new ValueDropdownList<string>();
        
        // 获取上下文ID（条件ID或动作ID），用于过滤参数
        string contextId = GetContextId();
        
        // 根据上下文ID获取允许的参数列表
        List<string> allowedParams = null;
        if (!string.IsNullOrEmpty(contextId))
        {
            // 先尝试从条件映射中查找
            if (QualityEffectConstants.ConditionParamMap.TryGetValue(contextId, out var conditionParams))
            {
                allowedParams = conditionParams;
            }
            // 如果没找到，尝试从动作映射中查找
            else if (QualityEffectConstants.ActionParamMap.TryGetValue(contextId, out var actionParams))
            {
                allowedParams = actionParams;
            }
        }
        
        // 如果没有找到映射或上下文ID为空，显示所有参数（向后兼容）
        if (allowedParams == null || allowedParams.Count == 0)
        {
            // 显示所有参数
            allowedParams = GetAllParamKeys();
        }
        
        // 添加允许的参数
        foreach (var key in allowedParams)
        {
            string chineseName = QualityEffectConstants.ParamKeyNames.TryGetValue(key, out var name) ? name : key;
            items.Add($"{chineseName} ({key})", key);
        }
        
        return items;
    }
    
    /// <summary>
    /// 获取上下文ID（从当前条目的contextId字段获取）
    /// </summary>
    private string GetContextId()
    {
        return contextId;
    }
    
    /// <summary>
    /// 获取所有参数键（用于向后兼容）
    /// </summary>
    private List<string> GetAllParamKeys()
    {
        var allParams = new List<string>();
        
        // 浮点数参数
        allParams.AddRange(new[]
        {
            "chance", "chance.base", "chance.perStack", "chance.perQuality", "chance.mult",
            "duration", "duration.base", "duration.perStack", "duration.perQuality", "duration.mult",
            "amount", "amount.base", "amount.perStack", "amount.perQuality", "amount.mult",
            "damage", "damage.base", "damage.perStack",
            "damagePctOfBase", "damagePct",
            "range", "range.base", "range.perStack",
            "threshold", "count", "window", "angle", "distance",
            "slowPct", "moveSpeedPct", "attackSpeedPct", "armorReduction", "reduceAmount", "dropChance"
        });
        
        // 字符串参数
        allParams.AddRange(new[]
        {
            "stat", "buffId", "debuffId", "statusId", "target", "type",
            "unitId", "skillId", "itemId", "areaId", "damageType", "direction",
            "ownerAttr", "targetAttr", "center", "modifyType", "operator", "elementType"
        });
        
        return allParams;
    }
    
    private void OnTypeChanged()
    {
        // 类型改变时清空值
        if (type == EffectParamType.Float)
            stringValue = "";
        else
            floatValue = 0f;
    }
}

/// <summary>
/// 参数堆叠规则配置项
/// </summary>
[Serializable]
public class ParamStackingRuleEntry
{
    [LabelText("参数名")]
    [Tooltip("选择需要自定义堆叠规则的参数名称")]
    [ValueDropdown("GetParamKeys")]
    public string paramKey;
    
    [LabelText("堆叠规则")]
    [Tooltip("选择该参数的堆叠规则")]
    [ValueDropdown("GetRuleNames")]
    public string ruleName = "Add";
    
    private static readonly string[] DefaultRuleNames = 
    {
        "Add",
        "Max",
        "Min",
        "NoStack",
        "Average",
        "StackByQuality"
    };
    
    public ParamStackingRuleEntry Clone()
    {
        return new ParamStackingRuleEntry
        {
            paramKey = this.paramKey,
            ruleName = this.ruleName
        };
    }
    
    private IEnumerable<ValueDropdownItem<string>> GetParamKeys()
    {
        var items = new ValueDropdownList<string>();
        foreach (var kvp in QualityEffectConstants.ParamKeyNames)
        {
            items.Add($"{kvp.Value} ({kvp.Key})", kvp.Key);
        }
        return items;
    }
    
    private IEnumerable<ValueDropdownItem<string>> GetRuleNames()
    {
        var items = new ValueDropdownList<string>();
        foreach (var rule in DefaultRuleNames)
        {
            items.Add(rule, rule);
        }
        return items;
    }
}

/// <summary>
/// 可序列化的键-浮点值映射，支持通过 Inspector 配置参数。
/// 改进版：使用结构化配置避免key-value错位问题
/// </summary>
[Serializable]
public class SerializableStringFloatMap
{
    [HideInInspector]
    [Tooltip("上下文ID（条件ID或动作ID），用于过滤可用参数")]
    public string contextId = "";
    
    [LabelText("参数列表")]
    [Tooltip("配置该条件/动作所需的参数，点击+添加新参数")]
    [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "key", OnBeginListElementGUI = "OnBeginListElementGUI")]
    public List<EffectParamEntry> paramEntries = new List<EffectParamEntry>();

    /// <summary>
    /// 当开始绘制列表元素时，更新该元素的上下文ID
    /// </summary>
    private void OnBeginListElementGUI(int index)
    {
        if (index >= 0 && index < paramEntries.Count)
        {
            paramEntries[index].contextId = contextId;
        }
    }

    public float GetOrDefault(string key, float defaultValue = 0f)
    {
        foreach (var entry in paramEntries)
        {
            if (entry.key == key && entry.type == EffectParamType.Float)
                return entry.floatValue;
        }
        return defaultValue;
    }

    public string GetStringOrDefault(string key, string defaultValue = "")
    {
        foreach (var entry in paramEntries)
        {
            if (entry.key == key && entry.type == EffectParamType.String)
                return entry.stringValue;
        }
        return defaultValue;
    }
}

/// <summary>
/// 运行时上下文：用于执行条件与动作参数缩放。
/// </summary>
public struct QualityEffectRuntimeContext
{
    public int stacks;
    public int quality;
    public float baseDamage;
    public float targetMaxHp;
    public float ownerLevel;
    public string effectId; // 效果ID，用于生成唯一的 sourceId
}

/// <summary>
/// 参数缩放解析工具：支持 base/perStack/perQuality/mult 组合。
/// </summary>
public static class EffectParamResolver
{
    public static float Resolve(SerializableStringFloatMap map, string name, in QualityEffectRuntimeContext ctx, float defaultBase = 0f)
    {
        float b = map.GetOrDefault(name + ".base", map.GetOrDefault(name, defaultBase));
        float ps = map.GetOrDefault(name + ".perStack", 0f) * ctx.stacks;
        float pq = map.GetOrDefault(name + ".perQuality", 0f) * ctx.quality;
        float mult = map.GetOrDefault(name + ".mult", 1f);
        return (b + ps + pq) * mult;
    }
}


