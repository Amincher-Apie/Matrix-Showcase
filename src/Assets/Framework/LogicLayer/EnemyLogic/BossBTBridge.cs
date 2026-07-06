using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// BT 任务节点与 BossActor/BossModule 之间的唯一通道。
/// Task 只调 Bridge，不直接持有 Actor 或网络组件引用。
/// </summary>
public class BossBTBridge : MonoBehaviour
{
    private BossActor _actor;
    private BossModule _module;

    private void Awake()
    {
        _actor = GetComponent<BossActor>();
    }

    private void Start()
    {
        _module = _actor?.BossModule;
    }

    public GameObject GetAttackTarget()
    {
        var bt = _actor?.BehaviorTree;
        if (bt == null)
            return null;

        var target = bt.GetVariable("attackTarget")?.GetValue() as GameObject;
        if (target != null)
        {
            bt.SetVariableValue("targetPosition", target.transform.position);
            return target;
        }

        var fallback = GameObject.FindWithTag("Player");
        if (fallback != null)
        {
            bt.SetVariableValue("attackTarget", fallback);
            bt.SetVariableValue("targetPosition", fallback.transform.position);
            Debug.Log($"[BossBT][BossBTBridge] auto bind attackTarget -> {fallback.name}");
            return fallback;
        }

        return null;
    }

    public bool HasAttackTarget()
    {
        return GetAttackTarget() != null;
    }

    public float GetDistanceToTarget()
    {
        var target = GetAttackTarget();
        if (target == null) return -1f;
        return Vector3.Distance(transform.position, target.transform.position);
    }

    public float GetHealthPercent()
    {
        if (_module == null) return 1f;
        return _module.MaxHealth > 0f ? _module.Health / _module.MaxHealth : 0f;
    }

    public bool IsShootCompleted() => _module?.ShootCompleted ?? false;

    public bool IsTurnEnabled() => _module?.IsTurnEnabled ?? false;

    public bool IsNear(float threshold = 15f)
    {
        float dist = GetDistanceToTarget();
        if (dist < 0f) return false;
        return dist <= threshold;
    }

    public bool IsRemote(float threshold = 15f)
    {
        float dist = GetDistanceToTarget();
        if (dist < 0f) return false;
        return dist > threshold;
    }

    public void SetAnimTrigger(string triggerName)
    {
        _actor?.Animator?.SetTrigger(triggerName);
    }

    public void SetAnimInt(string paramName, int value)
    {
        _actor?.Animator?.SetInteger(paramName, value);
    }

    public AnimatorStateInfo GetCurrentAnimState()
    {
        return _actor.Animator.GetCurrentAnimatorStateInfo(0);
    }

    public AnimatorClipInfo[] GetCurrentClipInfo()
    {
        return _actor.Animator.GetCurrentAnimatorClipInfo(0);
    }

    public NavMeshAgent GetNavAgent() => _actor?.NavMeshAgent;

    public bool CanUseNavAgent()
    {
        var agent = _actor?.NavMeshAgent;
        return agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
    }

    public bool TrySetNavDestination(Vector3 pos)
    {
        var agent = _actor?.NavMeshAgent;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return false;

        return agent.SetDestination(pos);
    }

    public void TryStopNav()
    {
        var agent = _actor?.NavMeshAgent;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return;
        agent.isStopped = true;
    }

    public void TryResumeNav()
    {
        var agent = _actor?.NavMeshAgent;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return;
        agent.isStopped = false;
    }

    public void RotateTowards(Vector3 targetPos, float speed)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        Quaternion target = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * speed);
    }

    public float AngleTo(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        Quaternion targetRot = Quaternion.LookRotation(dir);
        return Quaternion.Angle(transform.rotation, targetRot);
    }

    public void SetBTVector3(string key, Vector3 value)
    {
        _actor?.BehaviorTree?.SetVariableValue(key, value);
    }
}
