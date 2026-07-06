using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "ConsumableItem_Template", menuName = "游戏配置/道具系统/创建物品/消耗性道具")]
public class ConsumableItemSO : BaseInventoryItemSO
{
    public override EnumItemType itemType => EnumItemType.Consumable;
}