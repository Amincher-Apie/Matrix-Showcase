/// <summary>
/// 道具在背包中的槽位分类
/// </summary>
public enum EnumItemType
{
    Weapon,         // 武器槽（不可堆叠）
    Consumable,     // 消耗品槽（可堆叠）
    QualityPassive, // 品质道具槽（不可堆叠，被动效果）
    Active          // 主动技能槽（唯一）
}