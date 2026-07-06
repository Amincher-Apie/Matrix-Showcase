using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 背包道具 ScriptableObject 基类，统一提供品质、等级与通用配置。
/// </summary>
public abstract class BaseInventoryItemSO : BaseSO
{   
    [Space(10)]
    [FoldoutGroup("渲染属性")]
    [LabelText("预制体")]
    public GameObject prefab;
    
    [FoldoutGroup("数值属性")]
    [LabelText("品质等级")]
    public EnumQualityLevel qualityLevel;

    [FoldoutGroup("数值属性")]
    [LabelText("价格")]
    public int price = 100;

    /// <summary>
    /// 道具所属的背包槽类型
    /// </summary>
    [HideInInspector] public abstract EnumItemType itemType { get; }
}
