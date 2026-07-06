using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 英雄配置 ScriptableObject。
/// 每个英雄是一个完整的 Prefab（含 CharacterController/Animator/骨骼/挂点等），
/// HeroSO 持有 Prefab 引用 + 属性/技能/被动数据。
/// 运行时：PlayerSpawnManager 直接 Instantiate heroPrefab，不再做模型替换。
/// </summary>
[CreateAssetMenu(fileName = "Hero_Template", menuName = "游戏配置/角色系统/创建英雄")]
public class HeroSO : BaseSO
{
    [FoldoutGroup("角色表现")]
    [LabelText("英雄 Prefab")]
    [Tooltip("该英雄的完整 Prefab。需挂载 NetworkObject + PlayerActor + PlayerNetworkProxy + PlayerInitializer + ThirdPersonPlayerController + CharacterController + Animator 等通用组件。")]
    public GameObject heroPrefab;

    [FoldoutGroup("角色属性")]
    [LabelText("属性配置")]
    [Tooltip("该英雄的初始属性与成长配置（生命/护盾/能量/五维等）。")]
    public PlayerAttributeConfig attributeConfig;

    [FoldoutGroup("角色能力")]
    [LabelText("主动技能列表")]
    [Tooltip("该英雄拥有的主动技能。每个技能包含描述 + SkillDefinitionSO；执行逻辑由 SkillDefinitionSO.executeHandler 指向注册表。")]
    public List<SkillAbilityDef> skills = new List<SkillAbilityDef>();

    [FoldoutGroup("角色能力")]
    [LabelText("被动能力列表")]
    [Tooltip("该英雄拥有的被动能力。每个被动包含描述 + PassiveExecutorSO 资源。")]
    public List<PassiveAbilityDef> passives = new List<PassiveAbilityDef>();
}
