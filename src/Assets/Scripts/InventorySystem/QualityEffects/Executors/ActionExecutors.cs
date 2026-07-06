using System;
using Framework.LogicLayer.DamageCenter;
using UnityEngine;

/// <summary>
/// 动作：增加属性
/// 【已对接属性系统】
/// 参数：
///   - stat: 属性名称（如 "Attack", "Defense", "MoveSpeed" 等，对应 AttributeType 枚举）
///   - amount: 增加的数值（支持 base/perStack/perQuality 缩放）
/// 
/// 功能：给拥有者增加指定属性的数值
/// 系统对接：通过 ServerPlayerAttributeModule.ModifyAttributeServerRpc 修改属性
/// </summary>
public class AddStatExecutor : IActionExecutor
{
    public void Execute(ActionBlock action, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        Debug.Log($"[AddStat] ★ 开始执行 AddStat 动作");
        
        // 解析参数：获取要增加的属性数值（已考虑层数和品质缩放）
        float amount = EffectParamResolver.Resolve(action.@params, "amount", ctx, 0f);
        string statName = action.@params.GetStringOrDefault("stat", "");
        
        Debug.Log($"[AddStat] 解析参数: stat={statName}, amount={amount}, stacks={ctx.stacks}, quality={ctx.quality}, effectId={ctx.effectId}");
        
        // 获取拥有者（必须是 PlayerActor）
        var owner = evtCtx.owner as PlayerActor;
        if (owner == null || owner.networkProxy == null)
        {
            Debug.LogWarning($"[AddStat] owner 不是 PlayerActor 或 networkProxy 为空. owner类型: {evtCtx.owner?.GetType().Name ?? "null"}");
            return;
        }
        
        Debug.Log($"[AddStat] 找到玩家: {owner.ObjectId}, networkProxy: {owner.networkProxy?.name ?? "null"}");
        
        // 将字符串属性名转换为 AttributeType 枚举
        if (System.Enum.TryParse<AttributeType>(statName, out var attributeType))
        {
            Debug.Log($"[AddStat] 属性名 '{statName}' 成功解析为 AttributeType.{attributeType}");
            
            // 获取服务器权威属性模块
            var serverAttr = owner.networkProxy.GetComponent<ServerPlayerAttributeModule>();
            if (serverAttr != null)
            {
                // 检查是否在服务器端
                bool isServer = serverAttr.IsServer;
                Debug.Log($"[AddStat] 找到 ServerPlayerAttributeModule, IsServer={isServer}");
                
                // 获取当前属性值（用于日志显示）
                float currentValue = serverAttr.GetAttribute(attributeType);
                float baseValue = serverAttr.GetBaseAttribute(attributeType);
                
                Debug.Log($"[AddStat] 属性当前状态: 基础值={baseValue}, 当前值={currentValue}");
                
                // ★ 性能优化：使用效果ID + 属性类型 + 玩家ID 作为唯一的 sourceId
                // 这样每个效果对每个属性的修改都有唯一的来源ID，可以正确更新而不是创建新的修改器
                // 使用字符串哈希生成唯一的 ulong ID
                string effectId = !string.IsNullOrEmpty(ctx.effectId) ? ctx.effectId : "unknown";
                string uniqueSourceId = $"{effectId}_{attributeType}_{owner.ObjectId}";
                ulong sourceId = (ulong)uniqueSourceId.GetHashCode();
                
                // 检查网络对象状态
                if (serverAttr.NetworkObject == null)
                {
                    Debug.LogError($"[AddStat] NetworkObject 为空，无法调用 ServerRpc");
                    return;
                }
                
                Debug.Log($"[AddStat] 准备调用 ModifyAttributeServerRpc: type={attributeType}, value={amount}, modifyType=Add, sourceId={sourceId} (from '{uniqueSourceId}')");
                serverAttr.ModifyAttributeServerRpc(attributeType, amount, 
                    AttributeModifyType.Add, sourceId);
                
                // 等待一帧后再次检查属性值（因为 ServerRpc 是异步的）
                Debug.Log($"[AddStat] ★ ServerRpc 调用完成，属性 {statName} 应该增加 {amount}");
                Debug.Log($"[AddStat] 预期最终值: {baseValue + amount} (基础值 {baseValue} + 增量 {amount})");
            }
            else
            {
                Debug.LogWarning($"[AddStat] 找不到 ServerPlayerAttributeModule，尝试查找其他属性模块...");
                var attrModule = owner.networkProxy.GetComponent<ServerAttributeModule>();
                if (attrModule != null)
                {
                    Debug.LogWarning($"[AddStat] 找到 ServerAttributeModule (基类)，但需要 ServerPlayerAttributeModule");
                }
                else
                {
                    Debug.LogError($"[AddStat] 完全找不到任何属性模块！");
                }
            }
        }
        else
        {
            Debug.LogError($"[AddStat] 未知的属性类型: '{statName}'，请使用 AttributeType 枚举中的值");
            Debug.LogError($"[AddStat] 可用的属性类型包括: DamageOutPutRate, Health, MaxHealth, MoveSpeed 等");
        }
    }
}

/// <summary>
/// 动作：施加持续伤害（DOT - Damage Over Time）
/// 【已对接伤害系统】
/// 参数：
///   - damagePctOfBase: 基于基础伤害的百分比（0-1，如 0.2 表示20%）
///   - duration: 持续时间（秒）
///   - tickInterval: 每次伤害的间隔（秒，默认1秒）
/// 
/// 功能：对目标施加持续伤害效果，每隔 tickInterval 秒造成一次伤害
/// 系统对接：通过 Buff 系统实现，创建一个带 OnTick 回调的 BuffData
/// 
/// 注意：此功能需要配合 Buff 系统的 OnTick 回调使用，目前先记录日志
/// 完整实现需要：
///   1. 创建 BuffData SO，配置 OnTick 回调
///   2. 在 OnTick 回调中调用伤害系统造成伤害
/// </summary>
public class ApplyDotExecutor : IActionExecutor
{
    public void Execute(ActionBlock action, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数
        float pct = EffectParamResolver.Resolve(action.@params, "damagePctOfBase", ctx, 0f);
        float duration = EffectParamResolver.Resolve(action.@params, "duration", ctx, 3f);
        float tickInterval = EffectParamResolver.Resolve(action.@params, "tickInterval", ctx, 1f);
        
        // 计算单次伤害：基于事件的基础伤害或上下文的基础伤害
        float baseDmg = evtCtx.baseDamage > 0f ? evtCtx.baseDamage : ctx.baseDamage;
        float tickDamage = baseDmg * pct;
        
        // 获取目标（优先使用 target，如果没有则使用 owner）
        var targetProxy = evtCtx.target as NetworkProxyBase;
        if (targetProxy == null)
        {
            // 如果没有目标，尝试从 owner 获取
            var ownerProxy = (evtCtx.owner as PlayerActor)?.networkProxy;
            if (ownerProxy == null)
            {
                Debug.LogWarning("[ApplyDot] 无法找到有效的目标");
                return;
            }
            targetProxy = ownerProxy;
        }
        
        // TODO: 完整实现需要创建 BuffData SO 并配置 OnTick 回调
        // 当前实现：通过 Buff 系统施加一个带 OnTick 的 Buff
        // 建议方案：
        //   1. 通过 SOManager 获取预配置的 DOT BuffData（如 "BleedDot", "PoisonDot"）
        //   2. 使用 PlayerBuffModule.ApplyBuff 施加 Buff
        //   3. BuffData 的 OnTick 回调中调用伤害系统
        
        Debug.Log($"[ApplyDot] 对目标施加持续伤害: 单次伤害={tickDamage:F1}, 间隔={tickInterval}s, 持续={duration}s");
        
        // 示例：如果将来有预配置的 DOT BuffData，可以这样调用：
        // var dotBuffData = SOManager.Instance?.GetSOById<BuffData>("BleedDot");
        // if (dotBuffData != null)
        // {
        //     var targetPlayer = targetProxy.GetComponent<PlayerNetworkProxy>()?.PlayerActor;
        //     targetPlayer?.BuffModule?.ApplyBuff(dotBuffData, duration);
        // }
    }
}

/// <summary>
/// 动作：召唤单位（幽灵、宠物等）
/// 【待对接召唤系统】
/// 参数：
///   - unitId: 召唤单位的ID（字符串，对应召唤物配置的ID）
///   - damageMultiplier: 伤害倍数（支持 base/perStack/perQuality 缩放）
///   - duration: 持续时间（秒，-1 表示永久存在）
/// 
/// 功能：在拥有者位置召唤一个单位
/// 系统对接：需要实现召唤系统后对接
/// 
/// 注意：此功能需要召唤系统支持，目前仅记录日志
/// 完整实现需要：
///   1. 实现召唤系统（SummonSystem）
///   2. 通过召唤系统在指定位置生成单位
///   3. 配置召唤物的属性（伤害倍数、持续时间等）
/// </summary>
public class SummonExecutor : IActionExecutor
{
    public void Execute(ActionBlock action, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数
        string unitId = action.@params.GetStringOrDefault("unitId", "");
        float mult = EffectParamResolver.Resolve(action.@params, "damageMultiplier", ctx, 1f);
        float duration = EffectParamResolver.Resolve(action.@params, "duration", ctx, 5f);
        
        if (string.IsNullOrEmpty(unitId))
        {
            Debug.LogWarning("[Summon] unitId 参数为空，无法召唤单位");
            return;
        }
        
        // 获取拥有者位置
        var owner = evtCtx.owner as PlayerActor;
        if (owner == null || owner.networkProxy == null)
        {
            Debug.LogWarning("[Summon] owner 不是 PlayerActor 或 networkProxy 为空");
            return;
        }
        
        Vector3 summonPosition = owner.networkProxy.transform.position;
        
        // TODO: 对接召唤系统
        // 示例实现：
        // var summonSystem = SummonSystem.Instance;
        // if (summonSystem != null)
        // {
        //     summonSystem.SummonUnit(unitId, summonPosition, mult, duration, owner.ObjectId);
        // }
        
        Debug.Log($"[Summon] 在位置 {summonPosition} 召唤单位: {unitId}, 伤害倍数={mult}, 持续时间={duration}s");
    }
}

/// <summary>
/// 动作：施加持续治疗（HoT - Heal Over Time）
/// 【已对接治疗系统】
/// 参数：
///   - amount: 每次治疗量（支持 base/perStack/perQuality 缩放）
///   - duration: 持续时间（秒）
///   - tickInterval: 每次治疗的间隔（秒，默认1秒）
/// 
/// 功能：对目标施加持续治疗效果，每隔 tickInterval 秒恢复一次生命值
/// 系统对接：通过 Buff 系统实现，创建一个带 OnTick 回调的 BuffData
/// 
/// 注意：此功能需要配合 Buff 系统的 OnTick 回调使用
/// 完整实现需要：
///   1. 创建 BuffData SO，配置 OnTick 回调
///   2. 在 OnTick 回调中调用治疗系统恢复生命值
/// </summary>
public class ApplyHoTExecutor : IActionExecutor
{
    public void Execute(ActionBlock action, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数
        float amount = EffectParamResolver.Resolve(action.@params, "amount", ctx, 10f);
        float duration = EffectParamResolver.Resolve(action.@params, "duration", ctx, 5f);
        float tickInterval = EffectParamResolver.Resolve(action.@params, "tickInterval", ctx, 1f);
        
        // 确定目标：优先使用 target，如果没有则使用 owner
        PlayerActor targetActor = null;
        if (evtCtx.target is PlayerNetworkProxy targetProxy)
        {
            targetActor = targetProxy.PlayerActor;
        }
        else if (evtCtx.owner is PlayerActor owner)
        {
            targetActor = owner;
        }
        
        if (targetActor == null || targetActor.BuffModule == null)
        {
            Debug.LogWarning("[ApplyHoT] 无法找到有效的目标或目标没有 BuffModule");
            return;
        }
        
        // TODO: 完整实现需要创建 BuffData SO 并配置 OnTick 回调
        // 当前实现：通过 Buff 系统施加一个带 OnTick 的 Buff
        // 建议方案：
        //   1. 通过 SOManager 获取预配置的 HoT BuffData（如 "RegenerationHoT"）
        //   2. 使用 PlayerBuffModule.ApplyBuff 施加 Buff
        //   3. BuffData 的 OnTick 回调中调用治疗系统
        
        Debug.Log($"[ApplyHoT] 对目标施加持续治疗: 单次治疗={amount:F1}, 间隔={tickInterval}s, 持续={duration}s");
        
        // 示例：如果将来有预配置的 HoT BuffData，可以这样调用：
        // var hoTBuffData = SOManager.Instance?.GetSOById<BuffData>("RegenerationHoT");
        // if (hoTBuffData != null)
        // {
        //     targetActor.BuffModule?.ApplyBuff(hoTBuffData, duration);
        // }
    }
}

/// <summary>
/// 动作：移除属性（减少属性值）
/// 【已对接属性系统】
/// 参数：
///   - stat: 属性名称（如 "Attack", "Defense" 等，对应 AttributeType 枚举）
///   - amount: 减少的数值（支持 base/perStack/perQuality 缩放）
/// 
/// 功能：减少拥有者的指定属性数值
/// 系统对接：通过 ServerPlayerAttributeModule.ModifyAttributeServerRpc 修改属性（Subtract模式）
/// </summary>
public class RemoveStatExecutor : IActionExecutor
{
    public void Execute(ActionBlock action, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数
        float amount = EffectParamResolver.Resolve(action.@params, "amount", ctx, 0f);
        string stat = action.@params.GetStringOrDefault("stat", "");
        
        if (string.IsNullOrEmpty(stat))
        {
            Debug.LogWarning("[RemoveStat] stat 参数为空");
            return;
        }
        
        // 获取拥有者
        var owner = evtCtx.owner as PlayerActor;
        if (owner == null || owner.networkProxy == null)
        {
            Debug.LogWarning("[RemoveStat] owner 不是 PlayerActor 或 networkProxy 为空");
            return;
        }
        
        // 将字符串属性名转换为 AttributeType 枚举
        if (System.Enum.TryParse<AttributeType>(stat, out var attributeType))
        {
            // 获取服务器权威属性模块
            var serverAttr = owner.networkProxy.GetComponent<ServerPlayerAttributeModule>();
            if (serverAttr != null)
            {
                // ★ 性能优化：使用效果ID + 属性类型 + 玩家ID 作为唯一的 sourceId
                // 使用 RemoveModifiersServerRpc 来移除修改器，而不是添加负数的修改器
                string effectId = !string.IsNullOrEmpty(ctx.effectId) ? ctx.effectId : "unknown";
                string uniqueSourceId = $"{effectId}_{attributeType}_{owner.ObjectId}";
                ulong sourceId = (ulong)uniqueSourceId.GetHashCode();
                
                Debug.Log($"[RemoveStat] 准备移除修改器: type={attributeType}, sourceId={sourceId} (from '{uniqueSourceId}')");
                serverAttr.RemoveModifiersServerRpc(attributeType, sourceId, 0); // 0 表示移除所有层数
        
                float currentValue = serverAttr.GetAttribute(attributeType);
                Debug.Log($"[RemoveStat] 玩家 {owner.ObjectId} 属性 {stat} 修改器已移除 (当前值: {currentValue})");
            }
            else
            {
                Debug.LogWarning("[RemoveStat] 找不到 ServerPlayerAttributeModule");
            }
        }
        else
        {
            Debug.LogWarning($"[RemoveStat] 未知的属性类型: {stat}，请使用 AttributeType 枚举中的值");
        }
    }
}

/// <summary>
/// 动作：增加武器属性。
/// 参数：
///   - stat: WeaponAttributeType 名称
///   - amount: 修改数值
///   - modifyType: WeaponModifyType 名称（可选，缺省按 stat 推断）
///   - operator: WeaponModifyOperator 名称（Add/Percent/Multiply/Set，默认 Add）
/// </summary>
public class AddWeaponStatExecutor : IActionExecutor
{
    public void Execute(ActionBlock action, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        if (!TryResolveWeaponRuntime(evtCtx, out var owner, out var weaponRuntime))
        {
            return;
        }

        string statName = action.@params.GetStringOrDefault("stat", "");
        if (!Enum.TryParse(statName, out WeaponAttributeType attributeType))
        {
            Debug.LogWarning($"[AddWeaponStat] 未知武器属性: {statName}");
            return;
        }

        float amount = EffectParamResolver.Resolve(action.@params, "amount", ctx, 0f);
        var modifyType = ParseModifyType(action, attributeType);
        var op = ParseOperator(action.@params.GetStringOrDefault("operator", "Add"));
        var elementType = ParseElement(action.@params.GetStringOrDefault("elementType", "Fire"));
        ulong sourceId = BuildSourceId(ctx.effectId, attributeType, owner.ObjectId);

        weaponRuntime.AddModifierServerRpc(
            modifyType,
            attributeType,
            amount,
            op,
            sourceId,
            elementType);

        Debug.Log($"[AddWeaponStat] 玩家 {owner.ObjectId} 武器 {attributeType} {op} {amount} source={sourceId}");
    }

    private static WeaponModifyType ParseModifyType(ActionBlock action, WeaponAttributeType attributeType)
    {
        string modifyTypeStr = action.@params.GetStringOrDefault("modifyType", "");
        if (!string.IsNullOrEmpty(modifyTypeStr) && Enum.TryParse(modifyTypeStr, out WeaponModifyType parsed))
        {
            return parsed;
        }

        return attributeType switch
        {
            WeaponAttributeType.SolidDamage => WeaponModifyType.SpecificElement,
            WeaponAttributeType.LiquidDamage => WeaponModifyType.SpecificElement,
            WeaponAttributeType.GasDamage => WeaponModifyType.SpecificElement,
            WeaponAttributeType.IceDamage => WeaponModifyType.SpecificElement,
            WeaponAttributeType.FireDamage => WeaponModifyType.SpecificElement,
            WeaponAttributeType.ToxicDamage => WeaponModifyType.SpecificElement,
            WeaponAttributeType.ElectricDamage => WeaponModifyType.SpecificElement,
            WeaponAttributeType.CritChance => WeaponModifyType.CritChance,
            WeaponAttributeType.CritMultiplier => WeaponModifyType.CritMultiplier,
            WeaponAttributeType.ProcChance => WeaponModifyType.ProcChance,
            WeaponAttributeType.FireRate => WeaponModifyType.FireRate,
            WeaponAttributeType.MagazineSize => WeaponModifyType.MagazineSize,
            WeaponAttributeType.ReloadTime => WeaponModifyType.ReloadSpeed,
            WeaponAttributeType.BulletSpeed => WeaponModifyType.BulletSpeed,
            WeaponAttributeType.Spread => WeaponModifyType.Spread,
            WeaponAttributeType.RangeMin => WeaponModifyType.Range,
            WeaponAttributeType.RangeMax => WeaponModifyType.Range,
            _ => WeaponModifyType.TotalDamage
        };
    }

    private static WeaponModifyOperator ParseOperator(string op)
    {
        if (string.Equals(op, "Percentage", StringComparison.OrdinalIgnoreCase))
        {
            return WeaponModifyOperator.Percent;
        }

        return Enum.TryParse(op, out WeaponModifyOperator parsed)
            ? parsed
            : WeaponModifyOperator.Add;
    }

    private static ElementType ParseElement(string element)
    {
        return Enum.TryParse(element, out ElementType parsed)
            ? parsed
            : ElementType.Fire;
    }

    internal static bool TryResolveWeaponRuntime(
        in QualityEventContext evtCtx,
        out PlayerActor owner,
        out ServerWeaponRuntime weaponRuntime)
    {
        owner = evtCtx.owner as PlayerActor;
        weaponRuntime = null;

        if (owner == null || owner.networkProxy == null)
        {
            Debug.LogWarning("[AddWeaponStat] owner 不是 PlayerActor 或 networkProxy 为空");
            return false;
        }

        weaponRuntime = owner.networkProxy.GetServerWeaponRuntime<ServerWeaponRuntime>();
        if (weaponRuntime == null)
        {
            weaponRuntime = owner.networkProxy.GetComponentInChildren<ServerWeaponRuntime>();
        }

        if (weaponRuntime == null)
        {
            Debug.LogWarning("[AddWeaponStat] 找不到 ServerWeaponRuntime");
            return false;
        }

        if (!weaponRuntime.IsServer)
        {
            Debug.LogWarning("[AddWeaponStat] 武器属性修改只能在服务器执行");
            return false;
        }

        return true;
    }

    internal static ulong BuildSourceId(string effectId, WeaponAttributeType attributeType, ulong ownerId)
    {
        string key = $"{(string.IsNullOrEmpty(effectId) ? "unknown" : effectId)}_{attributeType}_{ownerId}";
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;

        for (int i = 0; i < key.Length; i++)
        {
            hash ^= key[i];
            hash *= prime;
        }

        return hash == 0 ? 1UL : hash;
    }
}

/// <summary>
/// 动作：移除武器属性修改器。
/// 参数：
///   - stat: WeaponAttributeType 名称
/// </summary>
public class RemoveWeaponStatExecutor : IActionExecutor
{
    public void Execute(ActionBlock action, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        if (!AddWeaponStatExecutor.TryResolveWeaponRuntime(evtCtx, out var owner, out var weaponRuntime))
        {
            return;
        }

        string statName = action.@params.GetStringOrDefault("stat", "");
        if (!Enum.TryParse(statName, out WeaponAttributeType attributeType))
        {
            Debug.LogWarning($"[RemoveWeaponStat] 未知武器属性: {statName}");
            return;
        }

        ulong sourceId = AddWeaponStatExecutor.BuildSourceId(ctx.effectId, attributeType, owner.ObjectId);
        weaponRuntime.RemoveModifiersFromSourceServerRpc(attributeType, sourceId);
        Debug.Log($"[RemoveWeaponStat] 玩家 {owner.ObjectId} 移除武器 {attributeType} source={sourceId}");
    }
}

/// <summary>
/// 动作：施加Buff（增益效果）
/// 【已对接Buff系统】
/// 参数：
///   - buffId: BuffData 的 ID（字符串，对应 BuffData SO 的 id 字段）
///   - duration: 持续时间（秒，如果为 -1 则使用 BuffData 的默认持续时间）
///   - stacks: 叠加层数（默认1层）
/// 
/// 功能：对目标（或拥有者）施加一个增益Buff
/// 系统对接：通过 PlayerBuffModule.ApplyBuff 施加 Buff
/// 
/// 注意：
///   - buffId 必须是已配置的 BuffData SO 的 ID
///   - Buff 的具体效果（如移速、攻速、伤害加成等）在 BuffData SO 中配置
///   - 如果 duration 为 -1，则使用 BuffData 的 defaultDuration
/// </summary>
public class ApplyBuffExecutor : IActionExecutor
{
    public void Execute(ActionBlock action, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数
        string buffId = action.@params.GetStringOrDefault("buffId", "");
        if (string.IsNullOrEmpty(buffId))
        {
            Debug.LogWarning("[ApplyBuff] buffId 参数为空，无法施加Buff");
            return;
        }
        
        float duration = EffectParamResolver.Resolve(action.@params, "duration", ctx, -1f);
        int stacks = (int)EffectParamResolver.Resolve(action.@params, "stacks", ctx, 1f);
        
        // 确定目标：优先使用 target，如果没有则使用 owner
        PlayerActor targetActor = null;
        if (evtCtx.target is PlayerNetworkProxy targetProxy)
        {
            targetActor = targetProxy.PlayerActor;
        }
        else if (evtCtx.owner is PlayerActor owner)
        {
            targetActor = owner;
        }
        
        if (targetActor == null || targetActor.BuffModule == null)
        {
            Debug.LogWarning("[ApplyBuff] 无法找到有效的目标或目标没有 BuffModule");
            return;
        }
        
        // 从 Resources 加载 BuffData SO（BuffData 不是 BaseSO，需要通过 Resources 加载）
        // 注意：buffId 应该是 BuffData 的资源路径（如 "BuffData/TestBuff"）或文件名
        BuffData buffData = null;
        if (int.TryParse(buffId, out int buffIdInt))
        {
            // 如果 buffId 是数字，尝试通过 Resources 加载所有 BuffData 并查找匹配的
            var allBuffs = Resources.LoadAll<BuffData>("");
            buffData = System.Array.Find(allBuffs, b => b.buffID == buffIdInt);
        }
        else
        {
            // 如果 buffId 是字符串路径，直接加载
            buffData = Resources.Load<BuffData>(buffId);
        }
        
        if (buffData == null)
        {
            Debug.LogWarning($"[ApplyBuff] 未找到 BuffData: {buffId}，请检查资源路径或 buffID");
            return;
        }
        
        // 检查是否在服务器端（Buff 系统要求服务器权威）
        if (!targetActor.IsServer)
        {
            Debug.LogWarning("[ApplyBuff] 只能在服务器端调用，当前在客户端调用无效");
            return;
        }
        
        // 施加 Buff（如果 duration 为 -1，使用 BuffData 的默认持续时间）
        float finalDuration = duration >= 0f ? duration : -1f;
        for (int i = 0; i < stacks; i++)
        {
            targetActor.BuffModule.ApplyBuff(buffData, finalDuration);
        }
        
        Debug.Log($"[ApplyBuff] 对玩家 {targetActor.ObjectId} 施加 Buff: {buffData.buffName}(ID:{buffId}), 持续时间={finalDuration}s, 层数={stacks}");
    }
}

/// <summary>
/// 动作：施加Debuff（减益效果）
/// 【已对接Buff系统】
/// 参数：
///   - debuffId: BuffData 的 ID（字符串，对应 BuffData SO 的 id 字段）
///   - duration: 持续时间（秒，如果为 -1 则使用 BuffData 的默认持续时间）
///   - stacks: 叠加层数（默认1层）
/// 
/// 功能：对目标施加一个减益Debuff
/// 系统对接：通过 PlayerBuffModule.ApplyBuff 施加 Buff（Buff 和 Debuff 在系统中统一处理）
/// 
/// 注意：
///   - debuffId 必须是已配置的 BuffData SO 的 ID
///   - Debuff 的具体效果（如减速、护甲削减等）在 BuffData SO 中配置
///   - 如果目标为 null，则无法施加
/// </summary>
public class ApplyDebuffExecutor : IActionExecutor
{
    public void Execute(ActionBlock action, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数
        string debuffId = action.@params.GetStringOrDefault("debuffId", "");
        if (string.IsNullOrEmpty(debuffId))
        {
            Debug.LogWarning("[ApplyDebuff] debuffId 参数为空，无法施加Debuff");
            return;
        }
        
        float duration = EffectParamResolver.Resolve(action.@params, "duration", ctx, -1f);
        int stacks = (int)EffectParamResolver.Resolve(action.@params, "stacks", ctx, 1f);
        
        // 获取目标（Debuff 必须有明确的目标）
        PlayerActor targetActor = null;
        if (evtCtx.target is PlayerNetworkProxy targetProxy)
        {
            targetActor = targetProxy.PlayerActor;
        }
        else if (evtCtx.target is NetworkProxyBase networkProxy)
        {
            // 如果是敌人，尝试获取其 PlayerActor（如果有的话）
            var playerProxy = networkProxy as PlayerNetworkProxy;
            targetActor = playerProxy?.PlayerActor;
        }
        
        if (targetActor == null)
        {
            Debug.LogWarning("[ApplyDebuff] 无法找到有效的目标，Debuff 必须作用于明确的目标");
            return;
        }
        
        if (targetActor.BuffModule == null)
        {
            Debug.LogWarning("[ApplyDebuff] 目标没有 BuffModule，无法施加Debuff");
            return;
        }
        
        // 从 Resources 加载 BuffData SO（Debuff 也是 BuffData）
        // 注意：debuffId 应该是 BuffData 的资源路径（如 "BuffData/PoisonDebuff"）或文件名
        BuffData debuffData = null;
        if (int.TryParse(debuffId, out int debuffIdInt))
        {
            // 如果 debuffId 是数字，尝试通过 Resources 加载所有 BuffData 并查找匹配的
            var allBuffs = Resources.LoadAll<BuffData>("");
            debuffData = System.Array.Find(allBuffs, b => b.buffID == debuffIdInt);
        }
        else
        {
            // 如果 debuffId 是字符串路径，直接加载
            debuffData = Resources.Load<BuffData>(debuffId);
        }
        
        if (debuffData == null)
        {
            Debug.LogWarning($"[ApplyDebuff] 未找到 BuffData: {debuffId}，请检查资源路径或 buffID");
            return;
        }
        
        // 检查是否在服务器端
        if (!targetActor.IsServer)
        {
            Debug.LogWarning("[ApplyDebuff] 只能在服务器端调用，当前在客户端调用无效");
            return;
        }
        
        // 施加 Debuff（如果 duration 为 -1，使用 BuffData 的默认持续时间）
        float finalDuration = duration >= 0f ? duration : -1f;
        for (int i = 0; i < stacks; i++)
        {
            targetActor.BuffModule.ApplyBuff(debuffData, finalDuration);
        }
        
        Debug.Log($"[ApplyDebuff] 对玩家 {targetActor.ObjectId} 施加 Debuff: {debuffData.buffName}(ID:{debuffId}), 持续时间={finalDuration}s, 层数={stacks}");
    }
}

/// <summary>
/// 动作：造成瞬时伤害
/// 【已对接伤害系统】
/// 参数：
///   - damage: 基础伤害值（支持 base/perStack/perQuality 缩放）
///   - bulletType: 物理子弹类型（"Solid"/"Liquid"/"Gas"，默认 "Solid"）
///   - enableCrit: 是否允许暴击（默认 false）
/// 
/// 功能：对目标造成一次瞬时伤害
/// 系统对接：通过 DamageCalculator.CalculateDamageFromProfile 计算伤害，然后调用 ServerAttributeModule.TakeDamage
/// 
/// 注意：
///   - 伤害会经过目标的护甲、抗性等计算
///   - 如果目标为 null，则无法造成伤害
/// </summary>
public class AddDamageExecutor : IActionExecutor
{
    public void Execute(ActionBlock action, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数
        float damage = EffectParamResolver.Resolve(action.@params, "damage", ctx, 100f);
        string bulletTypeStr = action.@params.GetStringOrDefault("bulletType", "Solid");
        bool enableCrit = action.@params.GetOrDefault("enableCrit", 0f) > 0f;
        
        // 解析物理子弹类型
        PhysicalBulletType bulletType = PhysicalBulletType.Solid;
        if (System.Enum.TryParse<PhysicalBulletType>(bulletTypeStr, out var parsedType))
        {
            bulletType = parsedType;
        }
        
        // 获取目标
        var targetProxy = evtCtx.target as NetworkProxyBase;
        if (targetProxy == null)
        {
            Debug.LogWarning("[AddDamage] 无法找到有效的目标，无法造成伤害");
            return;
        }
        
        // 获取拥有者（作为伤害来源）
        var owner = evtCtx.owner as PlayerActor;
        if (owner == null || owner.networkProxy == null)
        {
            Debug.LogWarning("[AddDamage] owner 不是 PlayerActor 或 networkProxy 为空");
            return;
        }
        
        ulong sourceId = owner.networkProxy.NetworkObjectId;
        ulong targetId = targetProxy.NetworkObjectId;
        
        // 构造伤害面板（DamageProfile）- 注意：DamageProfile 的字段是 int 类型
        var damageProfile = new DamageProfile
        {
            solid = bulletType == PhysicalBulletType.Solid ? (int)damage : 0,
            liquid = bulletType == PhysicalBulletType.Liquid ? (int)damage : 0,
            gas = bulletType == PhysicalBulletType.Gas ? (int)damage : 0,
            ice = 0,
            fire = 0,
            toxic = 0,
            electric = 0
        };
        
        // 通过伤害计算器计算最终伤害（会考虑护甲、抗性等）
        var damageInfo = DamageCalculator.CalculateDamageFromProfile(
            sourceId,
            targetId,
            damageProfile,
            bulletType,
            enableCrit: enableCrit
        );
        
        // 获取目标的属性模块并应用伤害
        var targetAttr = targetProxy.GetServerAttributeModule<ServerAttributeModule>();
        if (targetAttr != null)
        {
            targetAttr.TakeDamage(damageInfo);
            Debug.Log($"[AddDamage] 对目标 {targetId} 造成 {damageInfo.amount:F1} 点伤害 (类型: {bulletType}, 暴击: {damageInfo.isCritical})");
        }
        else
        {
            Debug.LogWarning($"[AddDamage] 目标 {targetId} 没有 ServerAttributeModule，无法造成伤害");
        }
    }
}

/// <summary>
/// 动作：瞬时治疗
/// 【已对接属性系统】
/// 参数：
///   - amount: 治疗量（支持 base/perStack/perQuality 缩放）
/// 
/// 功能：立即恢复拥有者的生命值
/// 系统对接：通过 ServerPlayerAttributeModule.ModifyAttributeServerRpc 修改 Health 属性
/// 
/// 注意：
///   - 治疗量不会超过最大生命值
///   - 实际治疗量 = min(amount, maxHealth - currentHealth)
/// </summary>
public class HealExecutor : IActionExecutor
{
    public void Execute(ActionBlock action, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数：获取治疗量（已考虑层数和品质缩放）
        float amount = EffectParamResolver.Resolve(action.@params, "amount", ctx, 50f);
        
        // 获取拥有者
        var owner = evtCtx.owner as PlayerActor;
        if (owner == null || owner.networkProxy == null)
        {
            Debug.LogWarning($"[Heal] owner 不是 PlayerActor 或 networkProxy 为空");
            return;
        }
        
        // 获取服务器权威属性模块
        var serverAttr = owner.networkProxy.GetComponent<ServerPlayerAttributeModule>();
        if (serverAttr != null)
        {
            // 获取当前生命值和最大生命值
            float currentHealth = serverAttr.GetAttribute(AttributeType.Health);
            float maxHealth = serverAttr.GetAttribute(AttributeType.MaxHealth);
            
            // 计算新生命值（不超过最大生命值）
            float newHealth = Mathf.Min(currentHealth + amount, maxHealth);
            float actualHeal = newHealth - currentHealth; // 实际治疗量
            
            // 通过服务器RPC修改生命值（Set模式：直接设置为新值）
            serverAttr.ModifyAttributeServerRpc(AttributeType.Health, newHealth, 
                AttributeModifyType.Set, owner.ObjectId);
            
            Debug.Log($"[Heal] 玩家 {owner.ObjectId} 恢复 {actualHeal:F1} 生命值 (当前: {currentHealth:F1} -> {newHealth:F1}/{maxHealth:F1})");
        }
        else
        {
            Debug.LogWarning($"[Heal] 找不到 ServerPlayerAttributeModule");
        }
    }
}

/// <summary>
/// 动作：瞬移
/// 【已对接位置系统】
/// 参数：
///   - distance: 瞬移距离（米，支持 base/perStack/perQuality 缩放）
///   - direction: 瞬移方向（"Forward"/"Backward"/"Random"/"BehindTarget"，默认 "Forward"）
/// 
/// 功能：将拥有者瞬移到指定方向的距离
/// 系统对接：直接修改 Transform 位置
/// 
/// 方向说明：
///   - "Forward": 向前瞬移（沿拥有者的 forward 方向）
///   - "Backward": 向后瞬移（沿拥有者的 -forward 方向）
///   - "Random": 随机方向瞬移
///   - "BehindTarget": 瞬移到目标身后（需要 evtCtx.target 不为 null）
/// </summary>
public class TeleportExecutor : IActionExecutor
{
    public void Execute(ActionBlock action, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数
        float distance = EffectParamResolver.Resolve(action.@params, "distance", ctx, 5f);
        string direction = action.@params.GetStringOrDefault("direction", "Forward");
        
        // 获取拥有者的 Transform
        var owner = evtCtx.owner as PlayerActor;
        if (owner == null || owner.networkProxy == null)
        {
            Debug.LogWarning("[Teleport] owner 不是 PlayerActor 或 networkProxy 为空");
            return;
        }
        
        Transform ownerTransform = owner.networkProxy.transform;
        if (ownerTransform == null)
        {
            Debug.LogWarning("[Teleport] 无法获取拥有者的 Transform");
            return;
        }
        
        // 计算瞬移方向
        Vector3 moveDirection = Vector3.zero;
        switch (direction.ToLower())
        {
            case "forward":
                // 向前瞬移（沿拥有者的 forward 方向）
                moveDirection = ownerTransform.forward;
                break;
                
            case "backward":
                // 向后瞬移（沿拥有者的 -forward 方向）
                moveDirection = -ownerTransform.forward;
                break;
                
            case "random":
                // 随机方向瞬移（水平面随机）
                float randomAngle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                moveDirection = new Vector3(Mathf.Cos(randomAngle), 0f, Mathf.Sin(randomAngle));
                break;
                
            case "behindtarget":
                // 瞬移到目标身后
                if (evtCtx.target is NetworkProxyBase targetProxy && targetProxy != null)
                {
                    Vector3 targetPos = targetProxy.transform.position;
                    Vector3 ownerPos = ownerTransform.position;
                    Vector3 toTarget = (targetPos - ownerPos).normalized;
                    // 目标身后 = 目标位置 - 目标朝向 * 距离
                    moveDirection = -toTarget;
                }
                else
                {
                    Debug.LogWarning("[Teleport] BehindTarget 模式需要有效的目标，回退到 Forward 模式");
                    moveDirection = ownerTransform.forward;
                }
                break;
                
            default:
                Debug.LogWarning($"[Teleport] 未知的方向类型: {direction}，使用 Forward 模式");
                moveDirection = ownerTransform.forward;
                break;
        }
        
        // 计算新位置
        Vector3 newPosition = ownerTransform.position + moveDirection.normalized * distance;
        
        // 执行瞬移（通过 NetworkObject 的 Transform，服务器权威）
        if (owner.networkProxy.IsServer)
        {
            ownerTransform.position = newPosition;
            Debug.Log($"[Teleport] 玩家 {owner.ObjectId} 瞬移到位置: {newPosition}, 方向: {direction}, 距离: {distance}m");
        }
        else
        {
            Debug.LogWarning("[Teleport] 只能在服务器端执行瞬移");
        }
    }
}

/// <summary>
/// 动作：修改技能冷却
/// 【已对接技能系统】
/// 参数：
///   - skillId: 技能ID（"All" 表示所有技能，否则为具体技能ID）
///   - reduceAmount: 减少的冷却时间（秒，负数表示增加冷却时间，支持 base/perStack/perQuality 缩放）
/// 
/// 功能：减少（或增加）指定技能的冷却时间
/// 系统对接：通过 PlayerSkillModule 修改技能冷却
/// 
/// 注意：
///   - reduceAmount 为正数时减少冷却（技能更快可用）
///   - reduceAmount 为负数时增加冷却（技能更慢可用）
///   - 如果技能ID为 "All"，则影响所有技能
/// </summary>
public class ModifyCooldownExecutor : IActionExecutor
{
    public void Execute(ActionBlock action, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数
        string skillId = action.@params.GetStringOrDefault("skillId", "All");
        float reduceAmount = EffectParamResolver.Resolve(action.@params, "reduceAmount", ctx, -2f);
        
        // 获取拥有者
        var owner = evtCtx.owner as PlayerActor;
        if (owner == null)
        {
            Debug.LogWarning("[ModifyCooldown] owner 不是 PlayerActor");
            return;
        }
        
        // 通过 GetModule 获取技能模块
        var skillModule = owner.GetModule<PlayerSkillModule>();
        if (skillModule == null)
        {
            Debug.LogWarning("[ModifyCooldown] owner 没有 PlayerSkillModule");
            return;
        }
        
        // 修改技能冷却
        if (skillId == "All")
        {
            // 修改所有技能的冷却
            var skillRuntimes = skillModule.SkillRuntimes;
            foreach (var skillRuntime in skillRuntimes)
            {
                if (skillRuntime != null && skillRuntime.definition != null)
                {
                    // 减少冷却时间（如果 reduceAmount 为正，则减少冷却）
                    skillRuntime.nextAvailableTime = Mathf.Max(0f, skillRuntime.nextAvailableTime - reduceAmount);
                }
            }
            Debug.Log($"[ModifyCooldown] 玩家 {owner.ObjectId} 所有技能冷却时间 {(reduceAmount > 0 ? "减少" : "增加")} {Mathf.Abs(reduceAmount)}s");
        }
        else
        {
            // 修改指定技能的冷却
            var skillRuntimes = skillModule.SkillRuntimes;
            bool found = false;
            foreach (var skillRuntime in skillRuntimes)
            {
                if (skillRuntime != null && skillRuntime.definition != null && skillRuntime.definition.id == skillId)
                {
                    skillRuntime.nextAvailableTime = Mathf.Max(0f, skillRuntime.nextAvailableTime - reduceAmount);
                    found = true;
                    Debug.Log($"[ModifyCooldown] 玩家 {owner.ObjectId} 技能 {skillId} 冷却时间 {(reduceAmount > 0 ? "减少" : "增加")} {Mathf.Abs(reduceAmount)}s");
                    break;
                }
            }
            
            if (!found)
            {
                Debug.LogWarning($"[ModifyCooldown] 未找到技能ID为 {skillId} 的技能");
            }
        }
    }
}

/// <summary>
/// 动作：掉落物品
/// 【待对接掉落系统】
/// 参数：
///   - itemId: 物品ID（字符串，对应物品配置的ID）
///   - count: 掉落数量（支持 base/perStack/perQuality 缩放，取整）
///   - dropChance: 掉落概率（0-1，如 0.5 表示50%概率）
/// 
/// 功能：在目标位置掉落指定物品
/// 系统对接：需要实现掉落系统后对接
/// 
/// 注意：此功能需要掉落系统支持，目前仅记录日志
/// 完整实现需要：
///   1. 实现掉落系统（ItemDropManager）
///   2. 在目标位置生成掉落物
///   3. 根据 dropChance 决定是否掉落
/// </summary>
public class DropItemExecutor : IActionExecutor
{
    public void Execute(ActionBlock action, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx)
    {
        // 解析参数
        string itemId = action.@params.GetStringOrDefault("itemId", "");
        int count = (int)EffectParamResolver.Resolve(action.@params, "count", ctx, 1f);
        float dropChance = EffectParamResolver.Resolve(action.@params, "dropChance", ctx, 1f);
        
        if (string.IsNullOrEmpty(itemId))
        {
            Debug.LogWarning("[DropItem] itemId 参数为空，无法掉落物品");
            return;
        }
        
        // 获取掉落位置（优先使用目标位置，否则使用拥有者位置）
        Vector3 dropPosition = Vector3.zero;
        if (evtCtx.target is NetworkProxyBase targetProxy && targetProxy != null)
        {
            dropPosition = targetProxy.transform.position;
        }
        else if (evtCtx.owner is PlayerActor owner && owner?.networkProxy != null)
        {
            dropPosition = owner.networkProxy.transform.position;
        }
        else
        {
            Debug.LogWarning("[DropItem] 无法找到有效的掉落位置");
            return;
        }
        
        // 根据掉落概率决定是否掉落
        if (UnityEngine.Random.value < dropChance)
        {
        // TODO: 对接掉落系统
            // 示例实现：
            // var dropManager = ItemDropManager.Instance;
            // if (dropManager != null)
            // {
            //     dropManager.DropItem(itemId, count, dropPosition);
            // }
        
            Debug.Log($"[DropItem] 在位置 {dropPosition} 掉落物品: {itemId} x{count} (概率: {dropChance * 100}%)");
        }
        else
        {
            Debug.Log($"[DropItem] 掉落判定失败: {itemId} (概率: {dropChance * 100}%)");
        }
    }
}
