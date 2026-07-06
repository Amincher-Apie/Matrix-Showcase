using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 取最小值规则：返回所有值中的最小值
/// 适用场景：取最低值的效果
/// </summary>
public class MinStackingRule : IStackingRule
{
    public float MergeValues(List<(float value, int quality, int stacks)> values, string paramName)
    {
        if (values == null || values.Count == 0)
            return 0f;
            
        return values.Min(v => v.value);
    }
}

