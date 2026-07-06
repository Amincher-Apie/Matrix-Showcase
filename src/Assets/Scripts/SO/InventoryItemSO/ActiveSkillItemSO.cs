using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "ActiveSkillItem_Template", menuName = "游戏配置/道具系统/创建物品/主动技能道具")]
public class ActiveSkillItemSO : BaseInventoryItemSO
{
    public override EnumItemType itemType => EnumItemType.Active;
    
}
