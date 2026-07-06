using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 累加规则：直接累加所有值
/// 适用场景：持续时间、数值累加等
/// 示例：2秒 + 2秒 + 3秒 = 7秒
/// </summary>
public class AddStackingRule : IStackingRule
{
    public float MergeValues(List<(float value, int quality, int stacks)> values, string paramName)
    {
        if (values == null || values.Count == 0)
            return 0f;
            
        return values.Sum(v => v.value);
    }
}

