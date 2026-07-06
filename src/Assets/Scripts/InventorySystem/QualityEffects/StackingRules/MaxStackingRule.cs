using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 取最大值规则：返回所有值中的最大值
/// 适用场景：取最高值的效果
/// </summary>
public class MaxStackingRule : IStackingRule
{
    public float MergeValues(List<(float value, int quality, int stacks)> values, string paramName)
    {
        if (values == null || values.Count == 0)
            return 0f;
            
        return values.Max(v => v.value);
    }
}

