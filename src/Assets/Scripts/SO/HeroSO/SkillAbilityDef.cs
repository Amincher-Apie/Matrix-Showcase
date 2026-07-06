using System;
using UnityEngine;

/// <summary>
/// 主动技能能力定义。包含技能的文字描述和对应的 SkillDefinitionSO。
/// 技能执行逻辑由 SkillDefinitionSO.executeHandler 指向 SkillExecuteRegistry 中的 ISkillExecute。
/// </summary>
[Serializable]
public class SkillAbilityDef
{
    [TextArea(3, 5)]
    [Tooltip("技能文字描述（给 UI 展示）")]
    public string skillDescription;

    [Tooltip("技能数据定义（五维数值/动画/冷却等）")]
    public SkillDefinitionSO skillDefinition;
}
