using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

[TaskDescription("死亡处理：播放 death 动画，并在黑板中清除 death 标志。")]
public class BossDeath : Action
{
    private BossBTBridge _bridge;

    public override void OnStart()
    {
        _bridge = GetComponent<BossBTBridge>();
        _bridge?.SetAnimTrigger("death");
        _bridge?.SetBTVector3("targetPosition", Vector3.zero);
    }

    public override TaskStatus OnUpdate()
    {
        if (_bridge == null) return TaskStatus.Failure;
        return TaskStatus.Success;
    }
}
