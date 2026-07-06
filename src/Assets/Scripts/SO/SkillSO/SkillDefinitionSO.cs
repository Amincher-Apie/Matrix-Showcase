using UnityEngine;

/// <summary>
/// 技能配置：描述一个“技能原型”
/// - 这里只放「基础数值」和「动画/表现」相关描述
/// - 具体逻辑（范围内伤害、锁定等）由 ISkillExecute 来做
/// </summary>
[CreateAssetMenu(menuName = "Skill/SkillDefinition", fileName = "Skill_Definition")]
public class SkillDefinitionSO : BaseSO
{
    [Header("基础信息")]
    public string displayName;

    [Header("技能类型")]
    public SkillCostType costType = SkillCostType.CooldownOnly;
    public SkillTargetType targetType = SkillTargetType.Point;

    [Header("五维原始数值")]
    [Tooltip("强度基准（例如 1 = 100% 武器伤害，5 = 500% 武器伤害）")]
    public float baseStrength = 1f;

    [Tooltip("持续时间基准，单位秒（持续类技能用）")]
    public float baseDuration = 0f;

    [Tooltip("技能作用距离 / 指定范围，单位米")]
    public float baseRange = 10f;

    [Header("Direction 技能扩散参数")]
    [Tooltip("扩散角（度），0 = 精确射击/单射线。仅 Direction 类型技能使用")]
    [Range(0f, 90f)]
    public float spreadAngle = 0f;

    [Tooltip("终点球体半径（米），0 = 单点命中。仅 Direction 类型技能使用")]
    [Range(0f, 20f)]
    public float spreadRadius = 0f;

    [Tooltip("能量消耗基准（在效率公式里会用到）")]
    public float baseEnergyCost = 0f;

    [Tooltip("冷却时间基准，单位秒")]
    public float baseCooldown = 0f;

    [Header("各维度是否生效")]
    public bool useStrength = true;
    public bool useDuration = true;
    public bool useRange = true;
    public bool useEfficiency = true;
    public bool useCooldown = true;

    [Header("伤害面板")]
    public PhysicalBulletType bulletType = PhysicalBulletType.Solid;
    public int baseSolidDamage = 0;
    public int baseLiquidDamage = 0;
    public int baseGasDamage = 0;
    public int baseIceDamage = 0;
    public int baseFireDamage = 0;
    public int baseToxicDamage = 0;
    public int baseElectricDamage = 0;

    [Header("暴击与元素触发")]
    public bool enableCrit = false;
    [Range(0f, 1f)]
    public float extraCritChance = 0f;
    public float extraCritMulti = 0f;
    [Range(0f, 5f)]
    public float skillProcChance = 0f;

    [Header("目标过滤")]
    public FactionFilterType factionFilter = FactionFilterType.EnemyOnly;
    [Tooltip("范围/溅射半径。0 表示使用 finalRange。")]
    public float splashRadius = 0f;
    [Tooltip("最大命中数，0 表示不限制。")]
    public int maxTargets = 0;

    [Header("Buff 引用")]
    public BuffData[] buffRefs;

    [Header("动画与阶段配置")]
    [Tooltip("前摇时长（秒），用于和动画系统对接")]
    public float precastDuration = 0.2f;
    [Tooltip("释放动画阶段时长（秒）")]
    public float castDuration = 0.3f;
    [Tooltip("后摇时长（秒）")]
    public float postcastDuration = 0.2f;

    [Tooltip("Animator 前摇触发参数名（预留接口）")]
    public string precastAnimTrigger;
    [Tooltip("Animator 施放触发参数名（预留接口）")]
    public string castAnimTrigger;
    [Tooltip("Animator 后摇触发参数名（预留接口）")]
    public string postcastAnimTrigger;

    [Header("执行器")]
    [Tooltip("用于在运行时找到对应的 ISkillExecute 实现。")]
    public SkillExecuteHandlerId executeHandler = SkillExecuteHandlerId.None;

    [HideInInspector]
    public string executeHandlerId;

    [SerializeField, HideInInspector]
    private bool executeHandlerIdMigrated;

    public string ExecuteHandlerId
    {
        get
        {
            string selectedId = executeHandler.ToHandlerIdString();
            return string.IsNullOrEmpty(selectedId) ? executeHandlerId : selectedId;
        }
    }

    private void OnEnable()
    {
        SyncExecuteHandlerId();
    }

    private void OnValidate()
    {
        SyncExecuteHandlerId();
    }

    private void SyncExecuteHandlerId()
    {
        if (!executeHandlerIdMigrated &&
            executeHandler == SkillExecuteHandlerId.None &&
            !string.IsNullOrEmpty(executeHandlerId))
        {
            var migratedHandler = SkillExecuteHandlerIdExtensions.FromHandlerIdString(executeHandlerId);
            if (migratedHandler != SkillExecuteHandlerId.None)
            {
                executeHandler = migratedHandler;
                executeHandlerIdMigrated = true;
            }
            else
            {
                return;
            }
        }

        if (executeHandler == SkillExecuteHandlerId.None)
        {
            if (executeHandlerIdMigrated)
            {
                executeHandlerId = string.Empty;
            }

            return;
        }

        executeHandlerId = executeHandler.ToHandlerIdString();
        executeHandlerIdMigrated = true;
    }
}
