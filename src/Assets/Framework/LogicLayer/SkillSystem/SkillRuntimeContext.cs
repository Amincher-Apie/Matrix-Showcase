using UnityEngine;

/// <summary>
/// 在某次技能释放期间的运行时上下文：
/// - 谁放的，放的什么技能
/// - 目标信息
/// - 五维属性在本次施放里的最终数值
/// </summary>
public struct SkillRuntimeContext
{
    public PlayerActor caster;
    public SkillDefinitionSO definition;
    public SkillCastContext castContext;
    public SkillStatSnapshot stats;

    // 下面是已经算好的「本次释放用的最终数值」
    public float finalDamageStrength;   // 用在伤害倍率
    public float finalDuration;         // 秒
    public float finalRange;            // 米
    public float finalEnergyCost;       // 点
    public float finalCooldown;         // 秒
    
    /// <summary>
    /// 技能快照：该次施放的基础伤害面板（未过抗性/护甲，仅包含技能本身的强度、武器面板等）
    /// </summary>
    public DamageProfile baseDamageProfile;

    /// <summary>
    /// 该次施放采用的物理子弹类型，用于护甲/护盾逻辑（固/液/气）
    /// </summary>
    public PhysicalBulletType bulletType;
}

public interface ISkillExecute
{
    /// <summary>
    /// 注册表用的唯一 ID，对应 SkillDefinitionSO.ExecuteHandlerId
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 客户端本地预测执行（播放特效/生成本地范围提示等）
    /// 不做伤害结算
    /// </summary>
    void ClientPredictExecute(in SkillRuntimeContext ctx);

    /// <summary>
    /// 服务器权威执行，真正结算伤害/BUFF/召唤物等
    /// </summary>
    void ServerExecute(in SkillRuntimeContext ctx);
}
