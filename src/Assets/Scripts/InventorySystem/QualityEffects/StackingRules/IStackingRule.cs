using System.Collections.Generic;

/// <summary>
/// 堆叠规则接口：定义如何合并多个相同效果的值
/// </summary>
public interface IStackingRule
{
    /// <summary>
    /// 合并多个效果的值
    /// </summary>
    /// <param name="values">所有相同效果ID的值列表，包含：值、品质、层数</param>
    /// <param name="paramName">参数名（如 "duration", "chance" 等）</param>
    /// <returns>合并后的值</returns>
    float MergeValues(List<(float value, int quality, int stacks)> values, string paramName);
}

