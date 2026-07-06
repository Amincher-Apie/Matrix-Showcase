using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 按品质分组叠加规则：相同品质的值累加，不同品质分别处理
/// 注意：这个规则通常配合 MergeByQuality 合并策略使用
/// 在合并策略中，相同品质的效果会被分组，然后在这个组内应用此规则
/// 示例：2个绿色(20%) → 40%，1个蓝色(30%) → 30%（分别判断）
/// </summary>
public class StackByQualityRule : IStackingRule
{
    public float MergeValues(List<(float value, int quality, int stacks)> values, string paramName)
    {
        if (values == null || values.Count == 0)
            return 0f;
            
        // 在按品质分组的情况下，values 应该都是相同品质的
        // 所以直接累加即可
        return values.Sum(v => v.value);
    }
}

