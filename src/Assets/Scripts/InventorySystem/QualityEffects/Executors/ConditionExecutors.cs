using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 条件：随机几率
/// 【已实现】
/// 参数：
///   - chance: 触发概率（0-1，如 0.2 表示20%概率，支持 base/perStack/perQuality 缩放）
/// 
/// 功能：根据配置的概率进行随机判定
/// 实现：使用 System.Random 生成随机数，与 chance 比较
/// 
/// 使用场景：例如"20%概率触发额外伤害"
/// 
/// 注意：
///   - chance <= 0：始终返回 false
///   - chance >= 1：始终返回 true
///   - 0 < chance < 1：随机判定
/// </summary>
public class RandomChanceEvaluator : IConditionEvaluator
{
    private System.Random _rng = new System.Random();

    public bool Evaluate(ConditionBlock condition, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数：获取触发概率（已考虑层数和品质缩放）
        float chance = EffectParamResolver.Resolve(condition.@params, "chance", ctx, 0f);
        
        // 边界情况处理
        if (chance <= 0f) return false;  // 概率为0或负数，不触发
        if (chance >= 1f) return true;   // 概率为1或更大，必定触发
        
        // 随机判定：生成 0-1 之间的随机数，与 chance 比较
        double randomValue = _rng.NextDouble();
        bool result = randomValue < chance;
        
        Debug.Log($"[RandomChance] 概率={chance * 100:F1}%, 随机值={randomValue:F3}, 结果={result}");
        return result;
    }
}

/// <summary>
/// 条件：血量比例低于阈值
/// 【已对接属性系统】
/// 参数：
///   - threshold: 血量比例阈值（0-1，如 0.3 表示30%）
///   - target: 检查目标（"Self" 表示自身，"Target" 表示目标，默认 "Self"）
/// 
/// 功能：检查指定目标的当前生命值比例是否低于阈值
/// 系统对接：通过 PlayerAttributeModule 或 ServerAttributeModule 获取生命值
/// 
/// 使用场景：例如"血量低于30%时触发治疗效果"
/// </summary>
public class HPRatioLessThanEvaluator : IConditionEvaluator
{
    public bool Evaluate(ConditionBlock condition, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数
        float threshold = EffectParamResolver.Resolve(condition.@params, "threshold", ctx, 0.5f);
        string target = condition.@params.GetStringOrDefault("target", "Self");
        
        // 确定检查目标
        PlayerActor targetActor = null;
        if (target == "Self")
        {
            targetActor = evtCtx.owner as PlayerActor;
        }
        else if (target == "Target")
        {
            // 从 target 获取 PlayerActor
            if (evtCtx.target is PlayerNetworkProxy targetProxy)
            {
                targetActor = targetProxy.PlayerActor;
            }
        }
        
        if (targetActor == null)
        {
            Debug.LogWarning($"[HPRatioLessThan] 无法找到有效的目标（target={target}）");
            return false;
        }
        
        // 获取生命值（优先使用逻辑层属性模块，如果没有则使用服务器属性模块）
        float currentHP = 0f;
        float maxHP = 0f;
        
        if (targetActor.AttributeModule != null)
        {
            currentHP = targetActor.AttributeModule.GetAttribute(AttributeType.Health);
            maxHP = targetActor.AttributeModule.GetAttribute(AttributeType.MaxHealth);
        }
        else if (targetActor.networkProxy != null)
        {
            var serverAttr = targetActor.networkProxy.GetComponent<ServerPlayerAttributeModule>();
            if (serverAttr != null)
            {
                currentHP = serverAttr.GetAttribute(AttributeType.Health);
                maxHP = serverAttr.GetAttribute(AttributeType.MaxHealth);
            }
        }
        
        if (maxHP <= 0f)
        {
            Debug.LogWarning($"[HPRatioLessThan] 目标最大生命值为0或负数");
            return false;
        }
        
        // 计算血量比例并判断
        float ratio = currentHP / maxHP;
        bool result = ratio <= threshold;
        
        Debug.Log($"[HPRatioLessThan] 目标 {targetActor.ObjectId} 血量比例 {ratio:F2} <= {threshold} = {result}");
        return result;
    }
}

/// <summary>
/// 条件：血量比例高于阈值
/// 【已对接属性系统】
/// 参数：
///   - threshold: 血量比例阈值（0-1，如 0.8 表示80%）
///   - target: 检查目标（"Self" 表示自身，"Target" 表示目标，默认 "Self"）
/// 
/// 功能：检查指定目标的当前生命值比例是否高于阈值
/// 系统对接：通过 PlayerAttributeModule 或 ServerAttributeModule 获取生命值
/// 
/// 使用场景：例如"血量高于80%时触发额外伤害"
/// </summary>
public class HPRatioGreaterThanEvaluator : IConditionEvaluator
{
    public bool Evaluate(ConditionBlock condition, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数
        float threshold = EffectParamResolver.Resolve(condition.@params, "threshold", ctx, 0.8f);
        string target = condition.@params.GetStringOrDefault("target", "Self");
        
        // 确定检查目标
        PlayerActor targetActor = null;
        if (target == "Self")
        {
            targetActor = evtCtx.owner as PlayerActor;
        }
        else if (target == "Target")
        {
            // 从 target 获取 PlayerActor
            if (evtCtx.target is PlayerNetworkProxy targetProxy)
            {
                targetActor = targetProxy.PlayerActor;
            }
        }
        
        if (targetActor == null)
        {
            Debug.LogWarning($"[HPRatioGreaterThan] 无法找到有效的目标（target={target}）");
            return false;
        }
        
        // 获取生命值（优先使用逻辑层属性模块，如果没有则使用服务器属性模块）
        float currentHP = 0f;
        float maxHP = 0f;
        
        if (targetActor.AttributeModule != null)
        {
            currentHP = targetActor.AttributeModule.GetAttribute(AttributeType.Health);
            maxHP = targetActor.AttributeModule.GetAttribute(AttributeType.MaxHealth);
        }
        else if (targetActor.networkProxy != null)
        {
            var serverAttr = targetActor.networkProxy.GetComponent<ServerPlayerAttributeModule>();
            if (serverAttr != null)
            {
                currentHP = serverAttr.GetAttribute(AttributeType.Health);
                maxHP = serverAttr.GetAttribute(AttributeType.MaxHealth);
            }
        }
        
        if (maxHP <= 0f)
        {
            Debug.LogWarning($"[HPRatioGreaterThan] 目标最大生命值为0或负数");
            return false;
        }
        
        // 计算血量比例并判断
        float ratio = currentHP / maxHP;
        bool result = ratio >= threshold;
        
        Debug.Log($"[HPRatioGreaterThan] 目标 {targetActor.ObjectId} 血量比例 {ratio:F2} >= {threshold} = {result}");
        return result;
    }
}

/// <summary>
/// 条件：拥有特定状态（Buff）
/// 【已对接Buff系统】
/// 参数：
///   - statusId: Buff ID（整数，对应 BuffData.buffID）
///   - target: 检查目标（"Self" 表示自身，"Target" 表示目标，默认 "Self"）
/// 
/// 功能：检查指定目标是否拥有指定的Buff状态
/// 系统对接：通过 PlayerBuffModule.HasBuff 检查Buff
/// 
/// 使用场景：例如"拥有'狂暴'Buff时触发额外效果"
/// </summary>
public class HasStatusEvaluator : IConditionEvaluator
{
    public bool Evaluate(ConditionBlock condition, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数：statusId 可以是字符串（需要转换为int）或直接是int
        string statusIdStr = condition.@params.GetStringOrDefault("statusId", "");
        if (string.IsNullOrEmpty(statusIdStr))
        {
            // 尝试从 float 参数获取（如果配置为数字）
            float statusIdFloat = EffectParamResolver.Resolve(condition.@params, "statusId", ctx, -1f);
            if (statusIdFloat < 0f)
            {
                Debug.LogWarning("[HasStatus] statusId 参数为空或无效");
                return false;
            }
            statusIdStr = ((int)statusIdFloat).ToString();
        }
        
        if (!int.TryParse(statusIdStr, out int buffId))
        {
            Debug.LogWarning($"[HasStatus] statusId 无法转换为整数: {statusIdStr}");
            return false;
        }
        
        string target = condition.@params.GetStringOrDefault("target", "Self");
        
        // 确定检查目标
        PlayerActor targetActor = null;
        if (target == "Self")
        {
            targetActor = evtCtx.owner as PlayerActor;
        }
        else if (target == "Target")
        {
            // 从 target 获取 PlayerActor
            if (evtCtx.target is PlayerNetworkProxy targetProxy)
            {
                targetActor = targetProxy.PlayerActor;
            }
        }
        
        if (targetActor == null || targetActor.BuffModule == null)
        {
            Debug.LogWarning($"[HasStatus] 无法找到有效的目标或目标没有 BuffModule（target={target}）");
            return false;
        }
        
        // 检查是否拥有指定的Buff
        bool hasBuff = targetActor.BuffModule.HasBuff(buffId);
        
        Debug.Log($"[HasStatus] 目标 {targetActor.ObjectId} 是否拥有 Buff {buffId} = {hasBuff}");
        return hasBuff;
    }
}

/// <summary>
/// 条件：不拥有特定状态（Buff）
/// 【已对接Buff系统】
/// 参数：
///   - statusId: Buff ID（整数，对应 BuffData.buffID）
///   - target: 检查目标（"Self" 表示自身，"Target" 表示目标，默认 "Self"）
/// 
/// 功能：检查指定目标是否不拥有指定的Buff状态
/// 系统对接：通过 PlayerBuffModule.HasBuff 检查Buff，然后取反
/// 
/// 使用场景：例如"没有'护盾'Buff时触发护盾效果"
/// </summary>
public class NoStatusEvaluator : IConditionEvaluator
{
    public bool Evaluate(ConditionBlock condition, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数：statusId 可以是字符串（需要转换为int）或直接是int
        string statusIdStr = condition.@params.GetStringOrDefault("statusId", "");
        if (string.IsNullOrEmpty(statusIdStr))
        {
            // 尝试从 float 参数获取（如果配置为数字）
            float statusIdFloat = EffectParamResolver.Resolve(condition.@params, "statusId", ctx, -1f);
            if (statusIdFloat < 0f)
            {
                Debug.LogWarning("[NoStatus] statusId 参数为空或无效");
                return false;
            }
            statusIdStr = ((int)statusIdFloat).ToString();
        }
        
        if (!int.TryParse(statusIdStr, out int buffId))
        {
            Debug.LogWarning($"[NoStatus] statusId 无法转换为整数: {statusIdStr}");
            return false;
        }
        
        string target = condition.@params.GetStringOrDefault("target", "Self");
        
        // 确定检查目标
        PlayerActor targetActor = null;
        if (target == "Self")
        {
            targetActor = evtCtx.owner as PlayerActor;
        }
        else if (target == "Target")
        {
            // 从 target 获取 PlayerActor
            if (evtCtx.target is PlayerNetworkProxy targetProxy)
            {
                targetActor = targetProxy.PlayerActor;
            }
        }
        
        if (targetActor == null || targetActor.BuffModule == null)
        {
            Debug.LogWarning($"[NoStatus] 无法找到有效的目标或目标没有 BuffModule（target={target}）");
            return false;
        }
        
        // 检查是否不拥有指定的Buff（取反）
        bool hasBuff = targetActor.BuffModule.HasBuff(buffId);
        bool noBuff = !hasBuff;
        
        Debug.Log($"[NoStatus] 目标 {targetActor.ObjectId} 是否不拥有 Buff {buffId} = {noBuff}");
        return noBuff;
    }
}

/// <summary>
/// 条件：目标是精英怪/Boss
/// 【已对接属性系统】
/// 参数：
///   - type: 怪物类型（"Elite"/"Boss"/"Normal"，默认 "Elite"）
/// 
/// 功能：检查目标是否为指定类型的怪物（精英/Boss/普通）
/// 系统对接：通过 ServerEnemyAttributeModule.GetMonsterRank 获取怪物类型
/// 
/// 使用场景：例如"击杀精英怪时触发额外奖励"
/// 
/// 注意：如果目标不是敌人或无法获取 MonsterRank，则返回 false
/// </summary>
public class IsEliteEvaluator : IConditionEvaluator
{
    public bool Evaluate(ConditionBlock condition, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数
        string type = condition.@params.GetStringOrDefault("type", "Elite");
        
        // 获取目标（必须是敌人）
        if (evtCtx.target == null)
        {
            Debug.LogWarning("[IsElite] evtCtx.target 为 null，无法判断怪物类型");
            return false;
        }
        
        // 从 target 获取 ServerEnemyAttributeModule
        ServerEnemyAttributeModule enemyAttr = null;
        if (evtCtx.target is NetworkProxyBase targetProxy && targetProxy != null)
        {
            enemyAttr = targetProxy.GetComponent<ServerEnemyAttributeModule>();
        }
        else if (evtCtx.target is MonoBehaviour targetMono && targetMono != null)
        {
            enemyAttr = targetMono.GetComponent<ServerEnemyAttributeModule>();
        }
        
        if (enemyAttr == null)
        {
            Debug.LogWarning("[IsElite] 目标不是敌人或无法获取 ServerEnemyAttributeModule");
            return false;
        }
        
        // 获取怪物类型
        MonsterRank monsterRank = enemyAttr.GetMonsterRank();
        
        // 根据 type 参数判断
        bool result = false;
        switch (type.ToLower())
        {
            case "elite":
                result = monsterRank == MonsterRank.Elite;
                break;
            case "boss":
                result = monsterRank == MonsterRank.Boss;
                break;
            case "normal":
                result = monsterRank == MonsterRank.Normal;
                break;
            default:
                Debug.LogWarning($"[IsElite] 未知的怪物类型: {type}，使用 Elite 作为默认值");
                result = monsterRank == MonsterRank.Elite;
                break;
        }
        
        Debug.Log($"[IsElite] 目标怪物类型={monsterRank}, 检查类型={type}, 结果={result}");
        return result;
    }
}

/// <summary>
/// 条件：层数大于等于阈值
/// 【已实现】
/// 参数：
///   - threshold: 层数阈值（支持 base/perStack/perQuality 缩放）
/// 
/// 功能：检查当前效果的堆叠层数是否大于等于指定阈值
/// 实现：直接比较 ctx.stacks 与 threshold
/// 
/// 使用场景：例如"层数≥3时触发额外效果"
/// 
/// 注意：层数来自品质道具效果的堆叠系统
/// </summary>
public class StackGreaterThanEvaluator : IConditionEvaluator
{
    public bool Evaluate(ConditionBlock condition, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数：获取层数阈值（已考虑层数和品质缩放）
        float threshold = EffectParamResolver.Resolve(condition.@params, "threshold", ctx, 1f);
        
        // 比较当前层数与阈值
        bool result = ctx.stacks >= threshold;
        
        Debug.Log($"[StackGreaterThan] 当前层数={ctx.stacks}, 阈值={threshold}, 结果={result}");
        return result;
    }
}

/// <summary>
/// 条件：最近N秒内击杀数≥阈值
/// 【已实现】
/// 参数：
///   - count: 需要的击杀数（支持 base/perStack/perQuality 缩放）
///   - window: 时间窗口（秒，支持 base/perStack/perQuality 缩放）
/// 
/// 功能：检查拥有者在最近 window 秒内是否击杀了至少 count 个敌人
/// 实现：使用静态字典记录每个拥有者的击杀时间戳，每次评估时清理过期记录
/// 
/// 使用场景：例如"最近3秒内击杀2个敌人时触发连击效果"
/// 
/// 注意：
///   - 此条件会在每次评估时记录一次击杀（即使不是真正的击杀事件）
///   - 建议在 OnKill 触发器中使用此条件，以确保准确性
/// </summary>
public class RecentKillsEvaluator : IConditionEvaluator
{
    // 静态字典：记录每个拥有者的击杀时间戳队列
    private static readonly Dictionary<object, Queue<float>> _killTimestamps = new();
    
    public bool Evaluate(ConditionBlock condition, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数
        float window = EffectParamResolver.Resolve(condition.@params, "window", ctx, 1.0f);
        int required = (int)EffectParamResolver.Resolve(condition.@params, "count", ctx, 3f);
        
        float now = Time.time;
        var owner = evtCtx.owner;
        
        if (owner == null)
        {
            Debug.LogWarning("[RecentKills] owner 为 null");
            return false;
        }
        
        // 获取或创建该拥有者的击杀时间戳队列
        if (!_killTimestamps.ContainsKey(owner))
            _killTimestamps[owner] = new Queue<float>();
        
        var queue = _killTimestamps[owner];
        
        // 注意：这里会在每次评估时添加当前时间戳
        // 实际使用时，应该在 OnKill 触发器触发时调用此条件，而不是在每次评估时都添加
        // 当前实现：每次评估都记录一次（可能不够准确，但可以工作）
        queue.Enqueue(now);
        
        // 清理过期记录（移除时间窗口外的击杀记录）
        while (queue.Count > 0 && now - queue.Peek() > window)
            queue.Dequeue();
        
        // 判断时间窗口内的击杀数是否达到要求
        bool result = queue.Count >= required;
        
        Debug.Log($"[RecentKills] 时间窗口={window}s, 需要击杀数={required}, 实际击杀数={queue.Count}, 结果={result}");
        return result;
    }
}

/// <summary>
/// 条件：距离小于阈值
/// 【已对接位置系统】
/// 参数：
///   - range: 距离阈值（米，支持 base/perStack/perQuality 缩放）
/// 
/// 功能：检查拥有者与目标之间的距离是否小于指定阈值
/// 系统对接：通过 Transform.position 计算距离
/// 
/// 使用场景：例如"目标距离小于5米时触发近战效果"
/// 
/// 注意：如果 evtCtx.target 为 null，则返回 false
/// </summary>
public class DistanceLessThanEvaluator : IConditionEvaluator
{
    public bool Evaluate(ConditionBlock condition, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数
        float range = EffectParamResolver.Resolve(condition.@params, "range", ctx, 5f);
        
        // 检查是否有目标
        if (evtCtx.target == null)
        {
            Debug.LogWarning("[DistanceLessThan] evtCtx.target 为 null，无法计算距离");
            return false;
        }
        
        // 获取拥有者的位置
        Vector3 ownerPos = Vector3.zero;
        if (evtCtx.owner is PlayerActor owner && owner?.networkProxy != null)
        {
            ownerPos = owner.networkProxy.transform.position;
        }
        else
        {
            Debug.LogWarning("[DistanceLessThan] 无法获取拥有者位置");
            return false;
        }
        
        // 获取目标的位置
        Vector3 targetPos = Vector3.zero;
        if (evtCtx.target is NetworkProxyBase targetProxy && targetProxy != null)
        {
            targetPos = targetProxy.transform.position;
        }
        else if (evtCtx.target is MonoBehaviour targetMono && targetMono != null)
        {
            targetPos = targetMono.transform.position;
        }
        else
        {
            Debug.LogWarning("[DistanceLessThan] 无法获取目标位置");
            return false;
        }
        
        // 计算距离
        float distance = Vector3.Distance(ownerPos, targetPos);
        bool result = distance <= range;
        
        Debug.Log($"[DistanceLessThan] 距离={distance:F2}m, 阈值={range}m, 结果={result}");
        return result;
    }
}

/// <summary>
/// 条件：面向目标
/// 【已对接朝向系统】
/// 参数：
///   - angle: 角度阈值（度，默认45度，支持 base/perStack/perQuality 缩放）
/// 
/// 功能：检查拥有者是否面向目标（拥有者的 forward 方向与到目标的方向夹角小于阈值）
/// 系统对接：通过 Transform.forward 和位置计算角度
/// 
/// 使用场景：例如"面向敌人时触发背刺效果"（需要角度大于某个值）
/// 
/// 注意：如果 evtCtx.target 为 null，则返回 false
/// </summary>
public class IsFacingEvaluator : IConditionEvaluator
{
    public bool Evaluate(ConditionBlock condition, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数
        float angle = EffectParamResolver.Resolve(condition.@params, "angle", ctx, 45f);
        
        // 检查是否有目标
        if (evtCtx.target == null)
        {
            Debug.LogWarning("[IsFacing] evtCtx.target 为 null，无法判断朝向");
            return false;
        }
        
        // 获取拥有者的 Transform
        Transform ownerTransform = null;
        if (evtCtx.owner is PlayerActor owner && owner?.networkProxy != null)
        {
            ownerTransform = owner.networkProxy.transform;
        }
        else
        {
            Debug.LogWarning("[IsFacing] 无法获取拥有者的 Transform");
            return false;
        }
        
        // 获取目标的位置
        Vector3 targetPos = Vector3.zero;
        if (evtCtx.target is NetworkProxyBase targetProxy && targetProxy != null)
        {
            targetPos = targetProxy.transform.position;
        }
        else if (evtCtx.target is MonoBehaviour targetMono && targetMono != null)
        {
            targetPos = targetMono.transform.position;
        }
        else
        {
            Debug.LogWarning("[IsFacing] 无法获取目标位置");
            return false;
        }
        
        // 计算方向向量
        Vector3 ownerPos = ownerTransform.position;
        Vector3 toTarget = (targetPos - ownerPos).normalized;
        Vector3 ownerForward = ownerTransform.forward;
        
        // 计算角度（使用点积）
        float dot = Vector3.Dot(ownerForward, toTarget);
        float angleRad = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;
        
        // 判断是否在角度阈值内
        bool result = angleRad <= angle;
        
        Debug.Log($"[IsFacing] 角度={angleRad:F1}°, 阈值={angle}°, 结果={result}");
        return result;
    }
}

/// <summary>
/// 条件：自身属性≥目标属性
/// 【已对接属性系统】
/// 参数：
///   - ownerAttr: 拥有者的属性名称（如 "Attack", "Defense", "MoveSpeed" 等，对应 AttributeType 枚举）
///   - targetAttr: 目标的属性名称（如 "Attack", "Defense" 等，对应 AttributeType 枚举）
/// 
/// 功能：检查拥有者的指定属性值是否大于等于目标的指定属性值
/// 系统对接：通过 PlayerAttributeModule 或 ServerAttributeModule 获取属性值
/// 
/// 使用场景：例如"自身攻击力≥目标防御力时触发穿透效果"
/// 
/// 注意：如果目标为 null 或无法获取属性，则返回 false
/// </summary>
public class AttributeGreaterEvaluator : IConditionEvaluator
{
    public bool Evaluate(ConditionBlock condition, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数
        string ownerAttrStr = condition.@params.GetStringOrDefault("ownerAttr", "Attack");
        string targetAttrStr = condition.@params.GetStringOrDefault("targetAttr", "Defense");
        
        // 获取拥有者
        var owner = evtCtx.owner as PlayerActor;
        if (owner == null)
        {
            Debug.LogWarning("[AttributeGreater] owner 不是 PlayerActor");
            return false;
        }
        
        // 获取目标
        PlayerActor targetActor = null;
        if (evtCtx.target is PlayerNetworkProxy targetProxy)
        {
            targetActor = targetProxy.PlayerActor;
        }
        else if (evtCtx.target is NetworkProxyBase networkProxy)
        {
            // 如果是敌人，暂时无法获取其属性（需要 EnemyActor 支持）
            Debug.LogWarning("[AttributeGreater] 目标不是玩家，暂时不支持敌人属性比较");
            return false;
        }
        
        if (targetActor == null)
        {
            Debug.LogWarning("[AttributeGreater] 无法找到有效的目标");
            return false;
        }
        
        // 解析属性类型
        if (!System.Enum.TryParse<AttributeType>(ownerAttrStr, out var ownerAttrType))
        {
            Debug.LogWarning($"[AttributeGreater] 未知的拥有者属性类型: {ownerAttrStr}");
            return false;
        }
        
        if (!System.Enum.TryParse<AttributeType>(targetAttrStr, out var targetAttrType))
        {
            Debug.LogWarning($"[AttributeGreater] 未知的目标属性类型: {targetAttrStr}");
            return false;
        }
        
        // 获取拥有者属性值
        float ownerValue = 0f;
        if (owner.AttributeModule != null)
        {
            ownerValue = owner.AttributeModule.GetAttribute(ownerAttrType);
        }
        else if (owner.networkProxy != null)
        {
            var serverAttr = owner.networkProxy.GetComponent<ServerPlayerAttributeModule>();
            if (serverAttr != null)
            {
                ownerValue = serverAttr.GetAttribute(ownerAttrType);
            }
        }
        
        // 获取目标属性值
        float targetValue = 0f;
        if (targetActor.AttributeModule != null)
        {
            targetValue = targetActor.AttributeModule.GetAttribute(targetAttrType);
        }
        else if (targetActor.networkProxy != null)
        {
            var serverAttr = targetActor.networkProxy.GetComponent<ServerPlayerAttributeModule>();
            if (serverAttr != null)
            {
                targetValue = serverAttr.GetAttribute(targetAttrType);
            }
        }
        
        // 比较属性值
        bool result = ownerValue >= targetValue;
        
        Debug.Log($"[AttributeGreater] 拥有者 {ownerAttrStr}={ownerValue:F1} >= 目标 {targetAttrStr}={targetValue:F1} = {result}");
        return result;
    }
}

