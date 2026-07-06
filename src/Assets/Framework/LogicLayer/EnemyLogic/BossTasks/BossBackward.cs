using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;
using UnityEngine.AI;

[TaskDescription("Boss 向后冲刺闪避。distance 控制后退距离，sprintSpeed 控制冲刺速度。")]
public class BossBackward : Action
{
    public float distance = 18f;
    public float sprintSpeed = 18f;

    private BossBTBridge _bridge;
    private NavMeshAgent _agent;
    private float _originSpeed;
    private Vector3 _targetPosition;
    private float _sprintTime = 0.8f;
    private float _clock;

    public override void OnStart()
    {
        _bridge = GetComponent<BossBTBridge>();
        _agent = _bridge?.GetNavAgent();

        if (_agent == null)
        {
            Debug.LogWarning("[BossBT][BossBackward] agent is null -> start failed");
            return;
        }

        _originSpeed = _agent.speed;
        _agent.speed = sprintSpeed;
        _clock = 0f;

        if (_bridge.CanUseNavAgent())
            _agent.updateRotation = false;

        _bridge.SetAnimTrigger("backward");
        _targetPosition = transform.position + (-transform.forward * distance);
        Debug.Log($"[BossBT][BossBackward] OnStart distance={distance} sprintSpeed={sprintSpeed}");
    }

    public override TaskStatus OnUpdate()
    {
        if (_agent == null)
            return TaskStatus.Failure;

        if (_clock >= _sprintTime)
        {
            _bridge.TrySetNavDestination(transform.position);
            if (_bridge.CanUseNavAgent())
                _agent.updateRotation = true;
            _agent.speed = _originSpeed;
            Debug.Log("[BossBT][BossBackward] Success");
            return TaskStatus.Success;
        }
        return TaskStatus.Running;
    }

    public override void OnFixedUpdate()
    {
        if (_agent == null)
            return;

        if (_clock <= _sprintTime)
        {
            _clock += Time.fixedDeltaTime;
            _bridge.TrySetNavDestination(_targetPosition);
        }
    }
}
