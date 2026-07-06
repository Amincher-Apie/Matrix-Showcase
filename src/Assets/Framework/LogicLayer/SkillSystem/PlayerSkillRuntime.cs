using UnityEngine;

/// <summary>
/// 单个技能槽运行时状态（客户端逻辑）
/// </summary>
[System.Serializable]
public class PlayerSkillRuntime
{
    public SkillDefinitionSO definition;

    /// <summary>下次可用时间（本地预测冷却）</summary>
    public float nextAvailableTime;

    /// <summary>当前可用充能数（默认为 1；冷却技能冷却结束后自动恢复）</summary>
    public int currentCharges;

    /// <summary>最大充能数（Phase 1 固定为 1，后续可扩展多段充能）</summary>
    public int maxCharges;

    /// <summary>UI 查询用缓存：最终能量消耗</summary>
    public float cachedEnergyCost;

    public SkillPhaseState phase;

    public PlayerSkillRuntime(SkillDefinitionSO def)
    {
        definition = def;
        nextAvailableTime = 0f;
        currentCharges = 1;
        maxCharges = 1;
        cachedEnergyCost = 0f;
        phase = SkillPhaseState.Idle;
    }

    /// <summary>
    /// 懒检测：冷却技能在冷却结束后自动恢复充能。
    /// EnergyOnly 技能不消耗充能（能量本身就是限制），只要技能已就绪即返回 true。
    /// </summary>
    public bool IsReady()
    {
        if (definition == null) return false;

        // 冷却到期 → 自动恢复充能
        if (Time.time >= nextAvailableTime && currentCharges <= 0)
        {
            RestoreCharge();
        }

        if (Time.time < nextAvailableTime) return false;
        if (currentCharges <= 0) return false;
        return true;
    }

    /// <summary>消耗一次充能（最多减到 0）</summary>
    public void ConsumeOneCharge()
    {
        if (currentCharges > 0) currentCharges--;
    }

    /// <summary>恢复充能到最大值</summary>
    private void RestoreCharge()
    {
        currentCharges = maxCharges;
    }

    /// <summary>
    /// 启动本地预测冷却。
    /// </summary>
    public void StartCooldown(float cooldown, float cooldownReduction)
    {
        cooldownReduction = Mathf.Clamp01(cooldownReduction);
        float finalCd = cooldown * (1f - cooldownReduction);
        nextAvailableTime = Time.time + finalCd;
    }
}
