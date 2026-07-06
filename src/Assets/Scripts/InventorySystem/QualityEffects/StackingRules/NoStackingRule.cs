using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 不叠加规则：使用最高品质的值（或第一个值）
/// 适用场景：某些效果不叠加，只取最高品质的值
/// </summary>
public class NoStackingRule : IStackingRule
{
    public float MergeValues(List<(float value, int quality, int stacks)> values, string paramName)
    {
        if (values == null || values.Count == 0)
            return 0f;
            
        // 返回最高品质的值
        return values.OrderByDescending(v => v.quality).First().value;
    }
}

