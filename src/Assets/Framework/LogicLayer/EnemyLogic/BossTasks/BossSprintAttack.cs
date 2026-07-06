using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;
using UnityEngine.AI;

[TaskDescription("冲刺靠近目标后触发攻击（scytheAttack1/2/3）。space 控制触发攻击的距离，sprintSpeed 控制冲刺速度。")]
public class BossSprintAttack : Action
{
    public float space = 3f;
    public float sprintSpeed = 25f;
    public float maxChaseTime = 2.5f;

    private BossBTBridge _bridge;
    private NavMeshAgent _agent;
    private GameObject _attackTarget;

    private float _originSpeed;
    private bool _attack;
    private float _chaseClock;
    private bool _startFailed;

    public override void OnStart()
    {
        _bridge = GetComponent<BossBTBridge>();
        _agent = _bridge?.GetNavAgent();
        _attackTarget = _bridge?.GetAttackTarget();

        _attack = false;
        _chaseClock = 0f;
        _startFailed = false;

        if (_agent == null || _attackTarget == null || !_bridge.CanUseNavAgent())
        {
            _startFailed = true;
            return;
        }

        _originSpeed = _agent.speed;
        _agent.speed = sprintSpeed;

        int attackModel = Random.Range(1, 4);
        _bridge.SetAnimInt("attackModel", attackModel);
        _bridge.SetAnimTrigger("forward");

        _bridge.TrySetNavDestination(_attackTarget.transform.position);
    }

    public override TaskStatus OnUpdate()
    {
        if (_startFailed)
            return TaskStatus.Failure;

        if (_attackTarget == null || _agent == null)
            return TaskStatus.Failure;

        if (!_attack)
        {
            _chaseClock += Time.deltaTime;
            _bridge.TrySetNavDestination(_attackTarget.transform.position);

            float dist = Vector3.Distance(transform.position, _attackTarget.transform.position);
            if (dist <= space)
            {
                _bridge.TryStopNav();
                _agent.speed = _originSpeed;
                _bridge.SetAnimTrigger("attack");
                _attack = true;
            }
            else if (_chaseClock >= maxChaseTime)
            {
                _agent.speed = _originSpeed;
                return TaskStatus.Failure;
            }
        }

        if (_attack)
        {
            var state = _bridge.GetCurrentAnimState();
            if ((state.IsName("scytheAttack1") || state.IsName("scytheAttack2") || state.IsName("scytheAttack3"))
                && state.normalizedTime >= 0.86f)
            {
                _bridge.TrySetNavDestination(transform.position);
                _bridge.TryResumeNav();
                return TaskStatus.Success;
            }
        }

        return TaskStatus.Running;
    }
}
