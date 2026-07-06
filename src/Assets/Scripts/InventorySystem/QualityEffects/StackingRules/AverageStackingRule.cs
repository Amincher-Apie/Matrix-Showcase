using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 平均值规则：返回所有值的平均值
/// 适用场景：需要取平均值的效果
/// </summary>
public class AverageStackingRule : IStackingRule
{
    public float MergeValues(List<(float value, int quality, int stacks)> values, string paramName)
    {
        if (values == null || values.Count == 0)
            return 0f;
            
        return values.Average(v => v.value);
    }
}

