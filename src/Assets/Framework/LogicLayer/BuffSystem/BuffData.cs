using UnityEngine;

[CreateAssetMenu(fileName = "BuffData_Template", menuName = "游戏配置/BuffSystem/BuffData")]
public class BuffData : ScriptableObject
{
    [Header("基础配置")]
    public int buffID;
    public string buffName;
    public Sprite buffIcon;
    public string buffDescription;
    public int priority;
    public string[] tags;
    public bool ShowBuffIcon;
    [Header("特效")]
    public GameObject EffectPrefab;

    #region 时间信息

    public bool isForever;

    [Tooltip("默认持续时间（秒），如果调用时传入 overrideDuration 则会被覆盖")]
    public float defaultDuration = 5f;

    [Tooltip("默认 Tick 周期（秒），0 表示无 Tick")]
    public float defaultTickInterval = 0f;
    
    #endregion

    #region 更新方式
    public BuffUpdateTimeEnum buffUpdateTime = BuffUpdateTimeEnum.replace;
    public BuffRemoveStackUpdateEnum buffRemoveStackUpdate = BuffRemoveStackUpdateEnum.clear;
    #endregion

    [Header("叠层归属")]
    [Tooltip("BuffIdOnly 为旧行为；BuffIdAndApplier 会让不同施加者在同一目标上独立叠层。")]
    public BuffStackKeyMode stackKeyMode = BuffStackKeyMode.BuffIdOnly;

    [Tooltip("同一目标上允许多少个不同施加者维护该 Buff。0 表示不限制。")]
    public int maxAppliersPerTarget = 4;

    [Header("按生效者类型限制最大层数（0 = 不限制，使用 defaultMaxStack）")]
    public int defaultMaxStack = 0;

    public int maxStackForPlayer = 0;
    public int maxStackForNormal = 0;
    public int maxStackForElite  = 0;
    public int maxStackForBoss   = 5;

    /// <summary>
    /// 根据 Buff 拥有者类型，解析最终的最大层数限制。
    /// </summary>
    public int ResolveMaxStackForOwner(BuffOwnerCategory ownerCategory)
    {
        int result = 0;
        switch (ownerCategory)
        {
            case BuffOwnerCategory.Player:
                result = maxStackForPlayer;
                break;
            case BuffOwnerCategory.NormalEnemy:
                result = maxStackForNormal;
                break;
            case BuffOwnerCategory.EliteEnemy:
                result = maxStackForElite;
                break;
            case BuffOwnerCategory.BossEnemy:
                result = maxStackForBoss;
                break;
        }

        if (result <= 0) result = defaultMaxStack;
        return result; // 仍然允许 0 = 不限制
    }
    
    #region 回调点设置
    //基础回调点
    public BaseBuffModule OnCreat;//创建buff
    public BaseBuffModule OnUpdate;//更新buff
    public BaseBuffModule OnTick;//触发buff/buff使用时
    public BaseBuffModule OnRemove;//移除该Buff

    //业务回调点-伤害
    /// <summary>
    /// 当击中敌人时的回调点
    /// </summary>
    public BaseBuffModule OnHit;//触发伤害
    /// <summary>
    /// 当受击伤害时,仅作伤害处理
    /// </summary>
    public BaseBuffModule OnCauseDamage;
    /// <summary>
    /// 当即将受伤时调用
    /// </summary>
    public BaseBuffModule UponBeHurt;
    /// <summary>
    /// 受伤时的回调点
    /// </summary>
    public BaseBuffModule OnBehurt;//被伤害
    /// <summary>
    /// 死亡时的回调点
    /// </summary>
    public BaseBuffModule OnDeath;//buff载体死亡时
    /// <summary>
    /// 当击杀敌人时的回调点
    /// </summary>
    public BaseBuffModule OnKill;//该载体击杀另一个GameObject时
    /// <summary>
    /// 当角色射击时的回调点
    /// </summary>
    public BaseBuffModule OnUseNormalAtk;
    /// <summary>
    /// 当角色使用技能时的回调点
    /// </summary>
    public BaseBuffModule OnUseSkill;
    /// <summary>
    /// 当角色使用技能后的回调点
    /// </summary>
    public BaseBuffModule AfterUseSkill;

    #endregion
}
