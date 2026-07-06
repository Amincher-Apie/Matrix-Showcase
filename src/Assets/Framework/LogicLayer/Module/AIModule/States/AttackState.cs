using Framework.LogicLayer.DamageCenter;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Attack state: enemy attacks its current target.
/// </summary>
public class AttackState : AIStateBase
{
    private float _lastAttackTime;
    private bool _isAttacking;

    public AttackState(AIStateMachine stateMachine, EnemyAIConfig config)
        : base(stateMachine, config)
    {
    }

    public override void OnEnter()
    {
        base.OnEnter();
        _lastAttackTime = 0f;
        _isAttacking = false;

        _owner.GetComponent<Animator>()?.SetTrigger("Attack");

        var target = _aiModule?.GetCurrentTarget();
        AIDebug.Log(OwnerId, "enter attack state, target={target?.ObjectId}, position={_owner?.WorldPosition}");

        if (target == null || _owner == null || _config == null)
            return;

        var targetPos = target.GetTargetPoint();
        if (Vector3.Distance(_owner.WorldPosition, targetPos) <= _config.attackRange)
        {
            PerformAttack(target, targetPos);
        }
    }

    public override void OnUpdate()
    {
        if (_owner == null || _aiModule == null)
            return;

        var target = _aiModule.GetCurrentTarget();

        if (target == null)
        {
            AIDebug.Log(OwnerId, "lost target, switch to idle.");
            _stateMachine.ChangeState(new IdleState(_stateMachine, _config));
            return;
        }

        var targetPos = target.GetTargetPoint();
        var distanceToTarget = Vector3.Distance(_owner.WorldPosition, targetPos);

        if (distanceToTarget > _config.attackRange)
        {
            AIDebug.Log(OwnerId, $"target distance={distanceToTarget:F2}m out of range, switch to chase.");
            _aiModule.StopMoving();
            _stateMachine.ChangeState(new ChaseState(_stateMachine, _config));
            return;
        }

        var direction = targetPos - _owner.WorldPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.01f)
        {
            _owner.WorldRotation = Quaternion.LookRotation(direction.normalized);
        }

        var timeSinceLastAttack = Time.time - _lastAttackTime;
        if (!_isAttacking && timeSinceLastAttack >= _config.attackCooldown)
        {
            PerformAttack(target, targetPos);
        }
    }

    private void PerformAttack(IAttackableObject target, Vector3 targetPos)
    {
        _isAttacking = true;
        _lastAttackTime = Time.time;

        if (!TryApplyDirectAttackDamage(target, targetPos))
        {
            var combatModule = _aiModule.GetCombatModule();
            if (combatModule != null)
            {
                var fireContext = new FireContext
                {
                    origin = _owner.WorldPosition,
                    dir = (targetPos - _owner.WorldPosition).normalized
                };
                combatModule.TryFireAtTarget(fireContext, target);
            }
        }

        _isAttacking = false;
        AIDebug.Log(OwnerId, $"attack target {target.TargetType}({target.ObjectId}).");
    }

    private bool TryApplyDirectAttackDamage(IAttackableObject target, Vector3 targetPos)
    {
        if (target == null || target.ObjectId == 0)
            return false;

        var networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer)
            return false;

        if (NetworkObjectManager.Instance == null ||
            !NetworkObjectManager.Instance.TryGetNetworkProxy<NetworkProxyBase>(target.ObjectId, out var targetProxy) ||
            targetProxy == null)
        {
            return false;
        }

        var targetAttr = targetProxy.GetServerAttributeModule<ServerAttributeModule>();
        if (targetAttr == null)
            return false;

        var combatModule = _aiModule?.GetCombatModule();
        if (combatModule == null ||
            !combatModule.TryBuildConfiguredDamageProfile(out var profile, out var bulletType))
        {
            return false;
        }

        var damageInfo = DamageCalculator.CalculateDamageFromProfile(
            OwnerId,
            target.ObjectId,
            profile,
            bulletType);
        damageInfo.isSkill = false;
        damageInfo.instigator = NetworkManager.ServerClientId;
        damageInfo.hasHitWorldPos = true;
        damageInfo.hitWorldPos = targetPos;

        targetAttr.TakeDamage(damageInfo);
        return true;
    }

    public override void OnExit()
    {
        base.OnExit();
        _aiModule?.StopMoving();
        _isAttacking = false;
        AIDebug.Log(OwnerId, "exit attack state.");
    }
}
