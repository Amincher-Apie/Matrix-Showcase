// 文件位置: LogicLayer/Module/SkillModule/PlayerSkillModule.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家技能模块（客户端逻辑层）
/// - 负责：输入 → 本地前摇/预测 → 发 Rpc 给服务器
/// - 与 AttributeSystem 做弱耦合：每次施放前从属性系统做一次「五维快照」
/// - 为 UI 提供本地状态：技能槽运行时 + 最近一次属性快照
/// </summary>
public class PlayerSkillModule : IModule
{
    private readonly PlayerActor _owner;

    /// <summary>
    /// 技能槽运行时列表（每个槽一个 PlayerSkillRuntime）
    /// </summary>
    private readonly List<PlayerSkillRuntime> _skills = new();

    /// <summary>
    /// 最近一次从 AttributeSystem 拿到的技能五维快照（倍率）
    /// </summary>
    private SkillStatSnapshot _stats;

    #region 对外只读接口（给 UI 用）

    /// <summary>
    /// 逻辑对象 ID（转发自 PlayerActor）
    /// </summary>
    public ulong ObjectId => _owner?.ObjectId ?? 0;

    /// <summary>
    /// UI 可直接读取的技能槽运行时状态（冷却、充能、phase 等）
    /// </summary>
    public IReadOnlyList<PlayerSkillRuntime> SkillRuntimes => _skills;

    /// <summary>
    /// UI 可直接读取的最近一次技能属性快照
    /// （注意：只有在 LocalInit / RefreshStatsFromAttributes() / 释放技能前更新）
    /// </summary>
    public SkillStatSnapshot LastStatsSnapshot => _stats;

    #endregion

    public PlayerSkillModule(PlayerActor owner)
    {
        _owner = owner;

        // 默认倍率：全 1，冷却缩减 0
        _stats = new SkillStatSnapshot
        {
            strength = 1f,
            duration = 1f,
            range = 1f,
            efficiency = 1f,
            cooldownReduction = 0f
        };
    }

    #region 生命周期

    public void LocalInit()
    {
        SkillExecuteRegistry.EnsureInitialized();

        // 初次从属性系统拉一份快照，避免 UI 看到全 1
        RefreshStatsFromAttributes();
    }

    public void OnActivate()
    {
        // 目前不用做特殊激活逻辑
    }

    public void LocalDestroy()
    {
        _skills.Clear();
    }

    #endregion

    #region 核心：释放技能（每次都会做一次属性快照）

    /// <summary>
    /// 由外部（如 UI / 输入系统）调用：尝试释放指定技能槽
    /// </summary>
    public bool TryCastSkill(int slotIndex, SkillCastContext castContext)
    {
        if (slotIndex < 0 || slotIndex >= _skills.Count) return false;
        var runtime = _skills[slotIndex];
        if (runtime.definition == null) return false;
        var skillDef = runtime.definition;

        castContext.slotIndex = slotIndex;
        castContext.skillId = skillDef.id;
        castContext.targetType = skillDef.targetType;

        if (!runtime.IsReady())
        {
            Debug.Log($"[PlayerSkillModule] Skill {runtime.definition.displayName} 冷却中或充能不足");
            return false;
        }

        // 关键点 1：在真正检查能量 / 构造上下文之前，先从 AttributeSystem 做一次「五维快照」
        CaptureSnapshotFromAttributes();

        // 关键点 2：用最新快照做软校验（真正的扣蓝/冷却仍由服务器裁定）
        if (!ClientCheckEnergyCost(skillDef))
        {
            Debug.Log("[PlayerSkillModule] 能量不足，无法释放技能");
            return false;
        }

        // 关键点 3：构造 runtimeContext（里面带本次释放用的最终数值）
        var ctx = BuildRuntimeContext(skillDef, castContext);

        // 客户端预测执行（仅表现，不做真实结算）
        var handler = SkillExecuteRegistry.Get(skillDef.ExecuteHandlerId);
        handler?.ClientPredictExecute(ctx);

        // 本地冷却 / 充能预测
        PredictConsumeCost(runtime, ctx);

        // 动画接口预留：你可以在这里调用 Animator.SetTrigger(.)
        TriggerAnimation(skillDef, SkillPhaseState.Precast);

        // 通知服务器进行真实结算
        SendCastRequestToServer(ctx.castContext);

        return true;
    }

    /// <summary>
    /// 客户端的能量消耗软检查（真正扣蓝仍由服务器做）
    /// </summary>
    private bool ClientCheckEnergyCost(SkillDefinitionSO def)
    {
        if (_owner == null || _owner.networkProxy == null) return true;

        // 这里用的是服务端的属性模块镜像（只读）
        var serverAttr = _owner.networkProxy.GetServerAttributeModule<ServerAttributeModule>();
        if (serverAttr == null) return true; // 客户端不知道就先放

        if (def.costType == SkillCostType.EnergyOnly || def.costType == SkillCostType.CooldownAndEnergy)
        {
            // 你的公式：100 * (2 - 效率)
            float cost = ComputeEnergyCost(def);
            float currentEnergy = serverAttr.GetAttribute(AttributeType.Energy);
            return currentEnergy >= cost;
        }

        return true;
    }

    /// <summary>
    /// 构造「本次释放」的运行时上下文（基于当前快照计算最终数值）
    /// </summary>
    private SkillRuntimeContext BuildRuntimeContext(SkillDefinitionSO def, SkillCastContext castContext)
    {
        var ctx = new SkillRuntimeContext
        {
            caster = _owner,
            definition = def,
            castContext = castContext,
            stats = _stats       // 本次释放使用的快照（已经从 AttributeSystem 更新过）
        };

        // 计算最终数值（强度/持续/范围/效率）
        float strength = Mathf.Max(_stats.strength, 0f);
        float duration = Mathf.Max(_stats.duration, 0f);
        float range = Mathf.Max(_stats.range, 0f);
        float eff = Mathf.Clamp(_stats.efficiency, 0f, 1.75f);

        ctx.finalDamageStrength = def.useStrength ? def.baseStrength * strength : def.baseStrength;
        ctx.finalDuration       = def.useDuration ? def.baseDuration * duration : def.baseDuration;
        ctx.finalRange          = def.useRange    ? def.baseRange * range       : def.baseRange;
        ctx.baseDamageProfile   = BuildSkillDamageProfile(def, ctx.finalDamageStrength);
        ctx.bulletType          = def.bulletType;

        if (def.baseEnergyCost > 0f &&
            (def.costType == SkillCostType.EnergyOnly || def.costType == SkillCostType.CooldownAndEnergy))
        {
            ctx.finalEnergyCost = def.useEfficiency
                ? def.baseEnergyCost * (2f - eff)
                : def.baseEnergyCost;
            ctx.finalEnergyCost = Mathf.Max(0f, ctx.finalEnergyCost);
        }
        else
        {
            ctx.finalEnergyCost = 0f;
        }

        // 冷却：客户端这里先用原始 CD，真正的冷却缩减由服务器通过 AttributeModule 计算
        ctx.finalCooldown = def.baseCooldown;

        return ctx;
    }

    private static DamageProfile BuildSkillDamageProfile(SkillDefinitionSO def, float damageStrength)
    {
        float strength = Mathf.Max(0f, damageStrength);
        return new DamageProfile
        {
            solid = Mathf.RoundToInt(def.baseSolidDamage * strength),
            liquid = Mathf.RoundToInt(def.baseLiquidDamage * strength),
            gas = Mathf.RoundToInt(def.baseGasDamage * strength),
            ice = Mathf.RoundToInt(def.baseIceDamage * strength),
            fire = Mathf.RoundToInt(def.baseFireDamage * strength),
            toxic = Mathf.RoundToInt(def.baseToxicDamage * strength),
            electric = Mathf.RoundToInt(def.baseElectricDamage * strength),
        };
    }

    /// <summary>
    /// 本地预测地消耗一次技能（充能–1，启动冷却）
    /// </summary>
    private void PredictConsumeCost(PlayerSkillRuntime runtime, in SkillRuntimeContext ctx)
    {
        var def = runtime.definition;
        if (def == null) return;

        // 冷却类技能：启动本地冷却预测
        if (def.costType == SkillCostType.CooldownOnly || def.costType == SkillCostType.CooldownAndEnergy)
        {
            runtime.StartCooldown(ctx.finalCooldown, _stats.cooldownReduction);
        }

        // EnergyOnly 技能不消耗充能、不进入冷却；仅消耗 1 次充能占位即可反复释放
        // （能量本身就是限制，肉鸽鼓励玩家滥用构筑）
        if (def.costType != SkillCostType.EnergyOnly)
        {
            runtime.ConsumeOneCharge();
        }
    }

    /// <summary>
    /// 向服务器发送技能释放请求（真正结算）
    /// </summary>
    private void SendCastRequestToServer(SkillCastContext ctx)
    {
        if (_owner == null || _owner.networkProxy == null) return;

        _owner.networkProxy.CastSkillServerRpc(new ClientSkillCastRequest
        {
            context = ctx
        });
    }

    /// <summary>
    /// 动画触发预留接口
    /// </summary>
    private void TriggerAnimation(SkillDefinitionSO def, SkillPhaseState phase)
    {
        // 预留动画接口：这里只写 Debug，你之后和 AnimatorManager / AnimationModule 对接
        if (def == null)
            return;

        string trigger = phase switch
        {
            SkillPhaseState.Precast => def.precastAnimTrigger,
            SkillPhaseState.Casting => def.castAnimTrigger,
            SkillPhaseState.Postcast => def.postcastAnimTrigger,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(trigger))
            return;

        Animator animator = _owner != null ? _owner.GetComponent<Animator>() : null;
        if (animator == null && _owner != null)
        {
            animator = _owner.GetComponentInChildren<Animator>(true);
        }
        if (animator == null && _owner?.networkProxy != null)
        {
            animator = _owner.networkProxy.GetComponentInChildren<Animator>(true);
        }

        if (animator == null)
        {
            Debug.LogWarning($"[PlayerSkillModule] Animator not found for skill {def.displayName}.");
            return;
        }

        animator.SetTrigger(trigger);
    }

    #endregion

    #region 与 AttributeSystem 的快照对接

    /// <summary>
    /// 公开接口：手动从 AttributeSystem 刷新技能五维快照
    /// - 可供 UI 在打开技能面板 / 属性面板时主动调用
    /// </summary>
    public void RefreshStatsFromAttributes()
    {
        _stats = BuildStatsSnapshotFromAttributes();
        RefreshAllCachedEnergyCosts();
    }

    /// <summary>
    /// TryCastSkill 内部使用：每次释放前做一次快照并缓存
    /// </summary>
    private void CaptureSnapshotFromAttributes()
    {
        _stats = BuildStatsSnapshotFromAttributes();
    }

    /// <summary>
    /// 核心快照构造逻辑：
    /// - 尝试从 ServerPlayerAttributeModule 读取 SkillStrength 等属性
    /// - 失败时回退使用上一帧的 _stats
    /// </summary>
    private SkillStatSnapshot BuildStatsSnapshotFromAttributes()
    {
        var snapshot = _stats; // 默认沿用上一次的结果，避免 null / 0 抖动

        if (_owner == null || _owner.networkProxy == null)
            return snapshot;

        // 注意：这里用的是 ServerPlayerAttributeModule（包含玩家技能相关属性）
        var serverAttr = _owner.networkProxy.GetServerAttributeModule<ServerPlayerAttributeModule>();
        if (serverAttr == null)
            return snapshot;

        // ——— 从属性系统读取技能相关属性 ———
        // 根据你的 AttributeType 和 PlayerAttributeConfig 约定：
        // SkillStrength / SkillDuration / SkillRange / SkillEfficiency / CooldownReduction
        float skillStrength   = serverAttr.GetAttribute(AttributeType.SkillStrength);
        float skillDuration   = serverAttr.GetAttribute(AttributeType.SkillDuration);
        float skillRange      = serverAttr.GetAttribute(AttributeType.SkillRange);
        float skillEfficiency = serverAttr.GetAttribute(AttributeType.SkillEfficiency);
        float cdr             = serverAttr.GetAttribute(AttributeType.CooldownReduction);

        // 做一点安全保护：防止出现负数或完全为 0 的异常值
        snapshot.strength          = Mathf.Max(skillStrength,   0f);
        snapshot.duration          = Mathf.Max(skillDuration,   0f);
        snapshot.range             = Mathf.Max(skillRange,      0f);
        snapshot.efficiency        = Mathf.Max(skillEfficiency, 0f);
        snapshot.cooldownReduction = Mathf.Clamp01(cdr);

        return snapshot;
    }

    #endregion

    #region 对外接口：管理技能槽（UI / 配置系统 会用到）

    /// <summary>
    /// 设置某个技能槽里的技能定义
    /// </summary>
    public void SetSkillInSlot(int index, SkillDefinitionSO def)
    {
        while (_skills.Count <= index)
        {
            _skills.Add(new PlayerSkillRuntime(null));
        }

        _skills[index] = new PlayerSkillRuntime(def);
        RefreshCachedEnergyCost(_skills[index]);
    }

    /// <summary>
    /// 只拿定义（老接口，保留兼容）
    /// </summary>
    public SkillDefinitionSO GetSkillInSlot(int index)
    {
        if (index < 0 || index >= _skills.Count) return null;
        return _skills[index].definition;
    }

    /// <summary>
    /// UI 如果想拿到冷却、充能、phase 等完整信息，用这个。
    /// </summary>
    public PlayerSkillRuntime GetRuntimeInSlot(int index)
    {
        if (index < 0 || index >= _skills.Count) return null;
        return _skills[index];
    }

    /// <summary>
    /// 计算技能的最终能量消耗，存入 runtime.cachedEnergyCost
    /// </summary>
    private void RefreshCachedEnergyCost(PlayerSkillRuntime runtime)
    {
        if (runtime?.definition == null)
        {
            if (runtime != null) runtime.cachedEnergyCost = 0f;
            return;
        }
        runtime.cachedEnergyCost = ComputeEnergyCost(runtime.definition);
    }

    /// <summary>
    /// 刷新所有技能槽的 cachedEnergyCost
    /// </summary>
    private void RefreshAllCachedEnergyCosts()
    {
        foreach (var rt in _skills)
            RefreshCachedEnergyCost(rt);
    }

    /// <summary>
    /// 根据当前五维快照计算技能的最终能量消耗
    /// </summary>
    private float ComputeEnergyCost(SkillDefinitionSO def)
    {
        if (def.baseEnergyCost <= 0f) return 0f;
        if (def.costType != SkillCostType.EnergyOnly && def.costType != SkillCostType.CooldownAndEnergy) return 0f;
        if (!def.useEfficiency) return Mathf.Max(0f, def.baseEnergyCost);
        float eff = Mathf.Clamp(_stats.efficiency, 0f, 1.75f);
        return Mathf.Max(0f, def.baseEnergyCost * (2f - eff));
    }

    #endregion
}
