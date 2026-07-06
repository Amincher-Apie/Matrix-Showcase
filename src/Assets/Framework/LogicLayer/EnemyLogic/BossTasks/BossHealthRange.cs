using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

[TaskDescription("血量百分比 ≤ healthRange 时返回 Success，否则 Failure。healthRange 取值为百分比（如 50 = 50%）。")]
public class BossHealthRange : Conditional
{
    public float healthRange = 50f;

    private BossBTBridge _bridge;

    public override void OnStart()
    {
        _bridge = GetComponent<BossBTBridge>();
        Debug.Log($"[BossBT][BossHealthRange] OnStart threshold={healthRange}");
    }

    public override TaskStatus OnUpdate()
    {
        if (_bridge == null)
        {
            Debug.LogWarning("[BossBT][BossHealthRange] bridge is null -> Failure");
            return TaskStatus.Failure;
        }

        float hpPct = _bridge.GetHealthPercent() * 100f;
        var status = hpPct <= healthRange ? TaskStatus.Success : TaskStatus.Failure;
        Debug.Log($"[BossBT][BossHealthRange] hpPct={hpPct:F2} threshold={healthRange:F2} result={status}");
        return status;
    }
}
