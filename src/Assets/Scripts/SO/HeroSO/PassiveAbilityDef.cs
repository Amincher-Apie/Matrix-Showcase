using System;
using UnityEngine;

/// <summary>
/// 被动能力定义。包含文字描述和对应的被动执行器资源。
/// </summary>
[Serializable]
public class PassiveAbilityDef
{
    [TextArea(3, 5)]
    [Tooltip("被动能力文字描述（给 UI 展示）")]
    public string description;

    [Tooltip("被动能力执行器资源。通过 CreateAssetMenu 创建 PassiveExecutorSO 资产后拖入。")]
    public PassiveExecutorSO passiveExecutor;
}
