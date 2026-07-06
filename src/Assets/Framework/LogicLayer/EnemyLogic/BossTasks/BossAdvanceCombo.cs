using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;
using UnityEngine.AI;

[TaskDescription("前进靠近目标后触发连击（scythe2HitComboA/B 或 scythe3HitCombo）。血量 ≤ 20% 时解锁三连击。")]
public class BossAdvanceCombo : Action
{
    private BossBTBridge _bridge;
    private NavMeshAgent _agent;
    private GameObject _attackTarget;

    private bool _moving;
    private bool _combo;
    private readonly float _rotaSpeed = 9f;

    public override void OnStart()
    {
        _bridge = GetComponent<BossBTBridge>();
        _agent = _bridge?.GetNavAgent();
        _attackTarget = _bridge?.GetAttackTarget();

        _moving = false;
        _combo = false;

        int maxCombo = _bridge.GetHealthPercent() * 100f <= 20f ? 3 : 2;
        int comboModel = Random.Range(1, maxCombo + 1);
        _bridge.SetAnimInt("comboModel", comboModel);
    }

    public override TaskStatus OnUpdate()
    {
        if (_attackTarget == null) return TaskStatus.Failure;

        float dist = Vector3.Distance(_attackTarget.transform.position, transform.position);

        if (!_moving && !_combo && dist > 3.3f)
        {
            _bridge.SetAnimTrigger("forward");
            _moving = true;
        }

        if (dist <= 3f && !_combo)
        {
            _bridge.SetAnimTrigger("combo");
            _combo = true;
        }

        if (_combo)
        {
            var state = _bridge.GetCurrentAnimState();
            if ((state.IsName("scythe2HitComboA") || state.IsName("scythe2HitComboB") || state.IsName("scythe3HitCombo"))
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
