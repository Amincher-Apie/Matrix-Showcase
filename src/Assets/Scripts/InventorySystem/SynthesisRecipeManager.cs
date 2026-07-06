using System;
using System.Collections.Generic;
using Framework.Singleton;
using UnityEngine;

/// <summary>
/// 负责维护并查询道具合成配方的数据管理器。
/// </summary>
public class SynthesisRecipeManager : MonoSingletonBase<SynthesisRecipeManager>
{
    [SerializeField]
    private List<SynthesisRecipeDefinition> recipes = new();

    /// <summary>
    /// 根据两个材料 ID 查询匹配的合成配方。
    /// </summary>
    /// <param name="id1">材料一 ID。</param>
    /// <param name="id2">材料二 ID。</param>
    /// <returns>匹配的配方；若无返回 <c>null</c>。</returns>
    public SynthesisRecipeDefinition GetRecipe(string id1, string id2)
    {
        foreach (var recipe in recipes)
        {
            if (recipe == null)
            {
                continue;
            }

            if (recipe.Matches(id1, id2))
            {
                return recipe;
            }
        }

        return null;
    }

#if UNITY_EDITOR
    [ContextMenu("自动排序配方")]
    private void SortRecipes()
    {
        recipes.Sort((a, b) => string.Compare(a?.GetKey(), b?.GetKey(), StringComparison.Ordinal));
    }
#endif
}

[Serializable]
public class SynthesisRecipeDefinition
{
    [Tooltip("材料A的物品ID")]
    public string ingredientA;

    [Tooltip("材料B的物品ID")]
    public string ingredientB;

    [Tooltip("产出物品ID")]
    public string productId;

    [Tooltip("合成后道具的品质，用于规则校验")]
    public EnumQualityLevel productQuality = EnumQualityLevel.Common;

    [Tooltip("是否为同类合成（可用于编辑器校验）")]
    public bool isSameTypeSynthesis;

    /// <summary>
    /// 产出物品 ID。
    /// </summary>
    public string ProductId => productId;

    /// <summary>
    /// 产出物品的品质。
    /// </summary>
    public EnumQualityLevel ProductQuality => productQuality;

    /// <summary>
    /// 判断两个材料 ID 是否与当前配方匹配（顺序无关）。
    /// </summary>
    public bool Matches(string id1, string id2)
    {
        return (ingredientA == id1 && ingredientB == id2) || (ingredientA == id2 && ingredientB == id1);
    }

    /// <summary>
    /// 获取用于比较排序的唯一键。
    /// </summary>
    public string GetKey()
    {
        return string.CompareOrdinal(ingredientA, ingredientB) <= 0
            ? $"{ingredientA}_{ingredientB}"
            : $"{ingredientB}_{ingredientA}";
    }
}

