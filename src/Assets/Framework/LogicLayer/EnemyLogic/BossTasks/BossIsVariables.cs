using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

[TaskDescription("判断 SharedBool 变量值。isReverse=false 时变量为 true 返回 Success；isReverse=true 时效果反转。")]
public class BossIsVariables : Conditional
{
    public SharedBool variables;
    public bool isReverse = false;

    public override void OnStart()
    {
        Debug.Log($"[BossBT][BossIsVariables] OnStart var={variables?.Name} isReverse={isReverse}");
    }

    public override TaskStatus OnUpdate()
    {
        bool val = variables != null && variables.Value;
        bool result = isReverse ? !val : val;
        var status = result ? TaskStatus.Success : TaskStatus.Failure;
        Debug.Log($"[BossBT][BossIsVariables] OnUpdate val={val} result={status}");
        return status;
    }
}
