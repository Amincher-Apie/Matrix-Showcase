using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

[TaskDescription("等待一段时间后开始旋转朝向目标，旋转完成后将 targetPosition 写入黑板并返回 Success。")]
public class BossFaceToPlayer : Action
{
    public float rotaSpeed = 8f;
    public float waitTime = 0f;

    private BossBTBridge _bridge;
    private Vector3 _targetPosition;
    private float _waitClock;

    public override void OnStart()
    {
        _bridge = GetComponent<BossBTBridge>();
        var target = _bridge?.GetAttackTarget();
        _targetPosition = target != null ? target.transform.position : transform.position;
        _waitClock = 0f;
        Debug.Log($"[BossBT][BossFaceToPlayer] OnStart waitTime={waitTime} rotaSpeed={rotaSpeed} target={target}");
    }

    public override TaskStatus OnUpdate()
    {
        if (_bridge == null)
        {
            Debug.LogWarning("[BossBT][BossFaceToPlayer] bridge is null -> Failure");
            return TaskStatus.Failure;
        }

        _waitClock += Time.deltaTime;
        if (_waitClock < waitTime)
            return TaskStatus.Running;

        float angle = _bridge.AngleTo(_targetPosition);
        if (angle <= 0.2f)
        {
            _bridge.SetBTVector3("targetPosition", _targetPosition);
            Debug.Log($"[BossBT][BossFaceToPlayer] Success angle={angle:F3}");
            return TaskStatus.Success;
        }

        _bridge.RotateTowards(_targetPosition, rotaSpeed);
        return TaskStatus.Running;
    }
}
