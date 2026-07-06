using Sirenix.OdinInspector;
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "QualityItem_Template", menuName = "游戏配置/道具系统/创建物品/品质被动道具")]
/// <summary>
/// 描述品质道具（被动效果带副作用）的配置对象。
/// </summary>
public class QualityItemSO : BaseInventoryItemSO
{
    [Space(10)]
    [FoldoutGroup("数值属性")]
    [LabelText("道具效果列表")]
    public List<QualityEffectDefinition> effects = new List<QualityEffectDefinition>();

    /// <inheritdoc />
    public override EnumItemType itemType => EnumItemType.QualityPassive;
}

