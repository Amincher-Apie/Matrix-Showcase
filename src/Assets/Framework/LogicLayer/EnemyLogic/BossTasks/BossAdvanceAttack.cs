using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;
using UnityEngine.AI;

[TaskDescription("前进靠近目标后触发普通攻击（scytheAttack1/2/3）。攻击动画播放至 86% 时返回 Success。")]
public class BossAdvanceAttack : Action
{
    private BossBTBridge _bridge;
    private NavMeshAgent _agent;
    private GameObject _attackTarget;

    private bool _moving;
    private bool _attack;
    private readonly float _rotaSpeed = 9f;

    public override void OnStart()
    {
        _bridge = GetComponent<BossBTBridge>();
        _agent = _bridge?.GetNavAgent();
        _attackTarget = _bridge?.GetAttackTarget();

        _moving = false;
        _attack = false;

        int attackModel = Random.Range(1, 4);
        _bridge.SetAnimInt("attackModel", attackModel);
    }

    public override TaskStatus OnUpdate()
    {
        if (_attackTarget == null) return TaskStatus.Failure;

        float dist = Vector3.Distance(_attackTarget.transform.position, transform.position);

        if (!_moving && !_attack && dist > 3f)
        {
            _bridge.SetAnimTrigger("forward");
            _moving = true;
        }

        if (dist <= 3f && !_attack)
        {
            _bridge.SetAnimTrigger("attack");
            _attack = true;
        }

        if (_attack)
        {
            var state = _bridge.GetCurrentAnimState();
            if ((state.IsName("scytheAttack1") || state.IsName("scytheAttack2") || state.IsName("scytheAttack3"))
                && state.normalizedTime >= 0.86f)
                return TaskStatus.Success;
        }

        return TaskStatus.Running;
    }

    public override void OnFixedUpdate()
    {
        if (_attackTarget == null) return;
        if (_bridge.IsTurnEnabled())
            _bridge.RotateTowards(_attackTarget.transform.position, _rotaSpeed);
        _bridge.TrySetNavDestination(_attackTarget.transform.position);
    }
}
