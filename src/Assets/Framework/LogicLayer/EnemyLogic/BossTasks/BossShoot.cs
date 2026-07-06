using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;
using UnityEngine.AI;

[TaskDescription("触发 shoot 动画，朝向目标旋转，等待 ShootComplete 动画事件后返回 Success。")]
public class BossShoot : Action
{
    public float rotaSpeed = 5f;

    private BossBTBridge _bridge;
    private GameObject _attackTarget;

    public override void OnStart()
    {
        _bridge = GetComponent<BossBTBridge>();
        _attackTarget = _bridge?.GetAttackTarget();
        _bridge?.SetAnimTrigger("shoot");
    }

    public override TaskStatus OnUpdate()
    {
        if (_bridge == null) return TaskStatus.Failure;
        return _bridge.IsShootCompleted() ? TaskStatus.Success : TaskStatus.Running;
    }

    public override void OnFixedUpdate()
    {
        if (_bridge == null || _attackTarget == null) return;
        if (!_bridge.IsShootCompleted())
            _bridge.RotateTowards(_attackTarget.transform.position, rotaSpeed);
    }
}
