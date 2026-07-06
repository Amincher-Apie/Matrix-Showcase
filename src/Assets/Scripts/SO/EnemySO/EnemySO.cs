using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 敌人配置 ScriptableObject
/// 根据Excel表格配置敌人的所有属性
/// </summary>
[CreateAssetMenu(fileName = "Enemy_Template", menuName = "游戏配置/怪物系统/创建配置/敌人配置")]
public class EnemySO : BaseSO
{
    [Space(10)]
    [FoldoutGroup("渲染属性")]
    [LabelText("预制体")]
    [Tooltip("敌人的预制体GameObject，位于 Prefab/Enemy/{Rank}/{id}.prefab")]
    public GameObject prefab;

    [FoldoutGroup("渲染属性")]
    [LabelText("怪物等级")]
    [Tooltip("决定预制体所在子目录名称（Normal / Elite / Boss）")]
    public MonsterRank rank;
    
    [Space(10)]
    [FoldoutGroup("攻击属性")]
    [LabelText("攻击类型")]
    [Tooltip("敌人的攻击方式：近战、远程、混合或无")]
    public EnemyAttackType attackType = EnemyAttackType.Ranged;
    
    [FoldoutGroup("攻击属性")]
    [LabelText("攻击距离(米)")]
    [Tooltip("远程攻击距离，混合类型时表示远程距离")]
    public float attackRange = 15f;
    
    [FoldoutGroup("攻击属性")]
    [LabelText("近战攻击距离(米)")]
    [Tooltip("仅混合类型使用，近战攻击距离")]
    [ShowIf("attackType", EnemyAttackType.Mixed)]
    public float meleeAttackRange = 5f;
    
    [FoldoutGroup("攻击属性")]
    [LabelText("攻击速度(次/秒)")]
    [MinValue(0.1f)]
    public float attackSpeed = 1f;
    
    [Space(10)]
    [FoldoutGroup("伤害属性")]
    [LabelText("物理伤害(固)")]
    [Tooltip("物理/固体伤害值")]
    public float physicalDamage = 0f;
    
    [FoldoutGroup("伤害属性")]
    [LabelText("液体伤害")]
    public float liquidDamage = 0f;
    
    [FoldoutGroup("伤害属性")]
    [LabelText("气体伤害")]
    public float gasDamage = 0f;
    
    [FoldoutGroup("伤害属性")]
    [LabelText("火焰伤害")]
    public float fireDamage = 0f;
    
    [FoldoutGroup("伤害属性")]
    [LabelText("冰冻伤害")]
    public float iceDamage = 0f;
    
    [FoldoutGroup("伤害属性")]
    [LabelText("电击伤害")]
    public float electricDamage = 0f;
    
    [FoldoutGroup("伤害属性")]
    [LabelText("毒素伤害")]
    public float poisonDamage = 0f;
    
    [FoldoutGroup("伤害属性")]
    [LabelText("远程伤害类型")]
    [Tooltip("混合类型时，远程攻击的伤害类型")]
    [ShowIf("attackType", EnemyAttackType.Mixed)]
    public DamageType rangedDamageType = DamageType.Physical;
    
    [FoldoutGroup("伤害属性")]
    [LabelText("远程伤害值")]
    [ShowIf("attackType", EnemyAttackType.Mixed)]
    public float rangedDamageValue = 0f;
    
    [FoldoutGroup("伤害属性")]
    [LabelText("近战伤害类型")]
    [Tooltip("混合类型时，近战攻击的伤害类型")]
    [ShowIf("attackType", EnemyAttackType.Mixed)]
    public DamageType meleeDamageType = DamageType.Physical;
    
    [FoldoutGroup("伤害属性")]
    [LabelText("近战伤害值")]
    [ShowIf("attackType", EnemyAttackType.Mixed)]
    public float meleeDamageValue = 0f;
    
    [Space(10)]
    [FoldoutGroup("防御属性")]
    [LabelText("生命值")]
    [MinValue(0f)]
    public float health = 100f;
    
    [FoldoutGroup("防御属性")]
    [LabelText("护盾值")]
    [MinValue(0f)]
    public float shield = 0f;
    
    [FoldoutGroup("防御属性")]
    [LabelText("护甲值")]
    [MinValue(0f)]
    public float armor = 0f;
    
    [Space(10)]
    [FoldoutGroup("抗性属性")]
    [LabelText("火焰抗性")]
    [Tooltip("范围：-1.0 到 1.0，负数表示易伤，正数表示抗性")]
    [Range(-1f, 1f)]
    public float fireResistance = 0f;
    
    [FoldoutGroup("抗性属性")]
    [LabelText("冰冻抗性")]
    [Range(-1f, 1f)]
    public float iceResistance = 0f;
    
    [FoldoutGroup("抗性属性")]
    [LabelText("电击抗性")]
    [Range(-1f, 1f)]
    public float electricResistance = 0f;
    
    [FoldoutGroup("抗性属性")]
    [LabelText("毒素抗性")]
    [Range(-1f, 1f)]
    public float poisonResistance = 0f;
    
    [Space(10)]
    [FoldoutGroup("其他属性")]
    [LabelText("韧性")]
    [Tooltip("控制抗性，影响受控效果")]
    [MinValue(0f)]
    public float tenacity = 0f;
    
    [FoldoutGroup("其他属性")]
    [LabelText("移动速度(米/秒)")]
    [MinValue(0f)]
    public float moveSpeed = 4f;
    
    #region 辅助方法
    
    /// <summary>
    /// 获取指定伤害类型的伤害值
    /// </summary>
    /// <param name="damageType">伤害类型</param>
    /// <returns>伤害值</returns>
    public float GetDamageValue(DamageType damageType)
    {
        return damageType switch
        {
            DamageType.Physical => physicalDamage,
            DamageType.Fire => fireDamage,
            DamageType.Ice => iceDamage,
            DamageType.Electric => electricDamage,
            DamageType.Poison => poisonDamage,
            _ => 0f
        };
    }
    
    /// <summary>
    /// 获取指定抗性类型的抗性值
    /// </summary>
    /// <param name="damageType">伤害类型</param>
    /// <returns>抗性值（-1.0 到 1.0）</returns>
    public float GetResistanceValue(DamageType damageType)
    {
        return damageType switch
        {
            DamageType.Fire => fireResistance,
            DamageType.Ice => iceResistance,
            DamageType.Electric => electricResistance,
            DamageType.Poison => poisonResistance,
            _ => 0f
        };
    }
    
    /// <summary>
    /// 获取当前攻击类型的攻击距离
    /// </summary>
    /// <param name="isMelee">是否为近战攻击（仅混合类型使用）</param>
    /// <returns>攻击距离（米）</returns>
    public float GetAttackRange(bool isMelee = false)
    {
        if (attackType == EnemyAttackType.Mixed)
        {
            return isMelee ? meleeAttackRange : attackRange;
        }
        return attackRange;
    }
    
    #endregion
}

/// <summary>
/// 伤害类型枚举（用于混合攻击类型）
/// </summary>
public enum DamageType
{
    Physical,   // 物理
    Fire,       // 火焰
    Ice,        // 冰冻
    Electric,   // 电击
    Poison      // 毒素
}

