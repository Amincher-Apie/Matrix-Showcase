using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

[TaskDescription("目标距离 ≤ 15 时返回 Success（近），否则 Failure。")]
public class BossNear : Conditional
{
    public float threshold = 15f;

    private BossBTBridge _bridge;

    public override void OnStart()
    {
        _bridge = GetComponent<BossBTBridge>();
        Debug.Log($"[BossBT][BossNear] OnStart threshold={threshold}");
    }

    public override TaskStatus OnUpdate()
    {
        if (_bridge == null)
        {
            Debug.LogWarning("[BossBT][BossNear] bridge is null -> Failure");
            return TaskStatus.Failure;
        }

        if (!_bridge.HasAttackTarget())
        {
            Debug.LogWarning("[BossBT][BossNear] attackTarget is null -> Failure");
            return TaskStatus.Failure;
        }

        float dist = _bridge.GetDistanceToTarget();
        var status = _bridge.IsNear(threshold) ? TaskStatus.Success : TaskStatus.Failure;
        Debug.Log($"[BossBT][BossNear] dist={dist:F2} threshold={threshold:F2} result={status}");
        return status;
    }
}
