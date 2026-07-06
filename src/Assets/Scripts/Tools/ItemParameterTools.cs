using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemParameterTools 
{
    /// <summary>
    /// 根据物品等级（EnumItemLevel）获取名称显示颜色（十六进制色值）
    /// </summary>
    /// <param name="level">物品等级枚举</param>
    /// <returns>十六进制颜色字符串（带#），用于UI富文本或颜色设置</returns>
    public static string GetItemLevelColor(EnumQualityLevel level)
    {
        switch (level)
        {
            case EnumQualityLevel.Common:
                // 白色品质
                return "#FFFFFF";
            case EnumQualityLevel.Uncommon:
                // 绿色品质
                return "#33FF33";
            case EnumQualityLevel.Rare:
                // 蓝色品质
                return "#3399FF";
            case EnumQualityLevel.Epic:
                // 紫色品质
                return "#9933FF";
            case EnumQualityLevel.Legendary:
                // 红色品质
                return "#CC0000";
            default:
                // 默认返回白色
                return "#FFFFFF";
        }
    }

    /// <summary>
    /// 重载：直接返回Unity Color类型（用于材质、图片等非文本颜色设置）
    /// </summary>
    /// <param name="level">物品等级枚举</param>
    /// <returns>Unity Color类型</returns>
    public static Color GetItemLevelUnityColor(EnumQualityLevel level)
    {
        // 先获取十六进制色值，再转换为Unity Color
        string hexColor = GetItemLevelColor(level);
        ColorUtility.TryParseHtmlString(hexColor, out Color color);
        return color;
    }
}
