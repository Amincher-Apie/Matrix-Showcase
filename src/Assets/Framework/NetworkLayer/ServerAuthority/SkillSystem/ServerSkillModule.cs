using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 服务器权威技能模块：
/// - 接收客户端请求
/// - 校验 CD / 蓝量（服务端权威）
/// - 调用 ISkillExecute.ServerExecute
/// - 真正扣蓝 & 冷却
/// - 广播 SkillCastConfirmed 事件给客户端
/// </summary>
[RequireComponent(typeof(ServerPlayerAttributeModule))]
public class ServerSkillModule : NetworkBehaviour
{
    private ServerPlayerAttributeModule _attrModule;
    private PlayerNetworkProxy _proxy;

    /// <summary>
    /// 服务端权威冷却追踪：按 skillId 记录下次可用时间。
    /// Key 为技能的 skillId（字符串，来自 SkillDefinitionSO.id）。
    /// </summary>
    private Dictionary<string, float> _nextAvailableServerTime;

    private void Awake()
    {
        _attrModule = GetComponent<ServerPlayerAttributeModule>();
        _proxy = GetComponent<PlayerNetworkProxy>();
        _nextAvailableServerTime = new Dictionary<string, float>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        SkillExecuteRegistry.EnsureInitialized();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        _nextAvailableServerTime?.Clear();
    }

    // ────────────────────────────
    //  RPC 入口（通过 PlayerNetworkProxy 转发）
    // ────────────────────────────

    [ServerRpc(RequireOwnership = true)]
    public void CastSkillServerRpc(ClientSkillCastRequest request, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        ServerTryCastSkill(request, senderClientId);
    }

    // ────────────────────────────
    //  服务端权威核心
    // ────────────────────────────

    /// <summary>
    /// 服务端校验并执行技能释放。由 PlayerNetworkProxy.CastSkillServerRpc 调用。
    /// </summary>
    /// <param name="request">客户端技能释放请求</param>
    /// <param name="senderClientId">发送者的 ClientId</param>
    public void ServerTryCastSkill(ClientSkillCastRequest request, ulong senderClientId)
    {
        if (!IsServer) return;

        var ctxNet = request.context;

        // ── 校验 1：属主身份 ──
        if (senderClientId != OwnerClientId)
        {
            Debug.LogWarning($"[ServerSkillModule] 拒绝：senderClientId({senderClientId}) != OwnerClientId({OwnerClientId})");
            return;
        }

        // ── 校验 2：slotIndex 合法性 ──
        if (ctxNet.slotIndex < 0 || ctxNet.slotIndex >= 10) // 与 PlayerSkillInputBinder.MaxSlots 保持一致
        {
            Debug.LogWarning($"[ServerSkillModule] 拒绝：非法 slotIndex = {ctxNet.slotIndex}");
            return;
        }

        // ── 查找技能定义 ──
        var skillDef = SOManager.Instance?.GetSOById<SkillDefinitionSO>(ctxNet.skillId.ToString());
        if (skillDef == null)
        {
            Debug.LogError($"[ServerSkillModule] 未找到技能定义: {ctxNet.skillId}");
            return;
        }

        // ── 校验 3：skillId 一致性 ──
        // 客户端请求的 skillId 必须与 skillDef 自身一致
        if (ctxNet.skillId.ToString() != skillDef.id)
        {
            Debug.LogWarning($"[ServerSkillModule] 拒绝：请求 skillId({ctxNet.skillId}) 与定义({skillDef.id})不匹配");
            return;
        }

        // ── 校验 4：目标类型兼容性 ──
        if (ctxNet.targetType != skillDef.targetType)
        {
            Debug.LogWarning($"[ServerSkillModule] 拒绝：目标类型不匹配 (请求:{ctxNet.targetType}, 定义:{skillDef.targetType})");
            return;
        }

        // ── 校验 5：服务端冷却 ──
        string skillId = skillDef.id;
        if (!CheckServerCooldown(skillId, skillDef))
        {
            Debug.Log($"[ServerSkillModule] 拒绝：技能 {skillId} 冷却中");
            return;
        }

        // ── 构造运行时上下文 ──
        var stats = SkillStatBuilder.FromAttributeGetter(_attrModule.GetAttribute);
        var runtimeCtx = BuildRuntimeContext(skillDef, ctxNet, stats);

        // ── 校验 6：能量校验 & 扣除 ──
        if (!CheckAndConsumeEnergy(skillDef, runtimeCtx))
        {
            Debug.Log("[ServerSkillModule] 拒绝：能量不足");
            return;
        }

        // ── 写入服务端冷却 ──
        WriteServerCooldown(skillId, runtimeCtx.finalCooldown);

        // ── 执行技能效果 ──
        string executeHandlerId = skillDef.ExecuteHandlerId;
        var handler = SkillExecuteRegistry.Get(executeHandlerId);
        if (handler == null)
        {
            Debug.LogError($"[ServerSkillModule] 未找到技能执行器: {executeHandlerId}");
            return;
        }

        handler.ServerExecute(runtimeCtx);

        // ── 触发品质效果 ──
        if (_proxy?.PlayerActor != null)
        {
            _proxy.PlayerActor.QualityEffectModule?.RaiseOnSkillCast(skillId);
        }

        // ── 广播确认事件 ──
        BroadcastSkillCastConfirmed(ctxNet.slotIndex, skillId);
    }

    // ────────────────────────────
    //  服务端冷却管理
    // ────────────────────────────

    /// <summary>检查技能是否通过服务端冷却校验</summary>
    private bool CheckServerCooldown(string skillId, SkillDefinitionSO def)
    {
        // EnergyOnly 技能不检查冷却
        if (def.costType == SkillCostType.EnergyOnly)
            return true;

        if (_nextAvailableServerTime.TryGetValue(skillId, out float nextTime))
        {
            return Time.time >= nextTime;
        }

        return true; // 没有冷却记录 = 可用
    }

    /// <summary>服务端释放成功后写入冷却记录</summary>
    private void WriteServerCooldown(string skillId, float cooldown)
    {
        if (cooldown <= 0f) return;

        _nextAvailableServerTime[skillId] = Time.time + cooldown;
    }

    // ────────────────────────────
    //  能量校验（使用服务器内部方法）
    // ────────────────────────────

    /// <summary>校验能量并扣除。返回 false = 能量不足。</summary>
    private bool CheckAndConsumeEnergy(SkillDefinitionSO def, SkillRuntimeContext ctx)
    {
        if (def.costType != SkillCostType.EnergyOnly && def.costType != SkillCostType.CooldownAndEnergy)
            return true; // 无能量消耗的技能，直接通过

        if (ctx.finalEnergyCost <= 0f) return true;

        float currentEnergy = _attrModule.GetAttribute(AttributeType.Energy);
        if (currentEnergy < ctx.finalEnergyCost)
        {
            Debug.Log($"[ServerSkillModule] 能量不足: 当前={currentEnergy}, 需要={ctx.finalEnergyCost}");
            return false;
        }

        // 走服务器内部方法，不走 RPC
        return _attrModule.TryConsumeEnergyServerInternal(ctx.finalEnergyCost);
    }

    // ────────────────────────────
    //  运行时上下文构建
    // ────────────────────────────

    private SkillRuntimeContext BuildRuntimeContext(SkillDefinitionSO skillDef, SkillCastContext ctxNet,
        SkillStatSnapshot stats)
    {
        var runtimeCtx = new SkillRuntimeContext
        {
            caster = _proxy.PlayerActor,
            definition = skillDef,
            castContext = ctxNet,
            stats = stats
        };

        float strength = Mathf.Max(stats.strength, 0f);
        float duration = Mathf.Max(stats.duration, 0f);
        float range = Mathf.Max(stats.range, 0f);
        float eff = Mathf.Clamp(stats.efficiency, 0f, 1.75f);

        runtimeCtx.finalDamageStrength = skillDef.useStrength ? skillDef.baseStrength * strength : skillDef.baseStrength;
        runtimeCtx.finalDuration = skillDef.useDuration ? skillDef.baseDuration * duration : skillDef.baseDuration;
        runtimeCtx.finalRange = skillDef.useRange ? skillDef.baseRange * range : skillDef.baseRange;
        runtimeCtx.baseDamageProfile = BuildSkillDamageProfile(skillDef, runtimeCtx.finalDamageStrength);
        runtimeCtx.bulletType = skillDef.bulletType;

        if (skillDef.baseEnergyCost > 0f &&
            (skillDef.costType == SkillCostType.EnergyOnly || skillDef.costType == SkillCostType.CooldownAndEnergy))
        {
            runtimeCtx.finalEnergyCost = skillDef.useEfficiency
                ? skillDef.baseEnergyCost * (2f - eff)
                : skillDef.baseEnergyCost;
            runtimeCtx.finalEnergyCost = Mathf.Max(0f, runtimeCtx.finalEnergyCost);
        }
        else
        {
            runtimeCtx.finalEnergyCost = 0f;
        }

        if (skillDef.costType == SkillCostType.CooldownOnly || skillDef.costType == SkillCostType.CooldownAndEnergy)
        {
            runtimeCtx.finalCooldown = _attrModule.GetReducedCooldown(skillDef.baseCooldown);
        }
        else
        {
            runtimeCtx.finalCooldown = 0f;
        }

        return runtimeCtx;
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

    // ────────────────────────────
    //  确认广播（Phase 1 最小实现）
    // ────────────────────────────

    /// <summary>
    /// 广播技能释放确认到所有客户端。
    /// Phase 1：EventCenter + 定向 ClientRpc 给 Owner（用于对齐预测）。
    /// Phase 3：扩展为完整远端表现广播。
    /// </summary>
    private void BroadcastSkillCastConfirmed(int slotIndex, string skillId)
    {
        // 1. EventCenter 事件（服务端和客户端各自监听）
        EventCenter.Instance.Trigger(EventName.SkillCastConfirmed, new SkillCastConfirmedEvt
        {
            unitId = NetworkObjectId,
            slotIndex = slotIndex,
            skillId = skillId,
        });

        // 2. 定向 ClientRpc 给 Owner（Phase 1 仅日志 + 占位，Phase 3 对齐冷却/能量预测）
        var ownerParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };
        OnSkillCastConfirmedClientRpc(slotIndex, skillId, ownerParams);

        Debug.Log($"[ServerSkillModule] 技能 {skillId} 释放确认已广播 — unitId={NetworkObjectId}, slot={slotIndex}");
    }

    [ClientRpc]
    private void OnSkillCastConfirmedClientRpc(int slotIndex, string skillId, ClientRpcParams rpcParams = default)
    {
        Debug.Log($"[ServerSkillModule] ClientRpc: 技能 {skillId} slot={slotIndex} 服务端已确认 — " +
                  $"unitId={NetworkObjectId}, IsOwner={IsOwner}");

        // Phase 3 TODO:
        // - Owner 客户端对齐冷却/能量预测
        // - 非 Owner 客户端播放远端技能表现
    }
}
