using Framework.LogicLayer.Module.AIModule.Movement;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 敌人服务端移动驱动器，负责消费 AI 输出的移动意图并执行权威位移。
///
/// 重构说明（NavMeshAgent）：
/// 当 EnemyNavAgentController 启用时，敌人移动完全由 NavMeshAgent 驱动，
/// 本驱动器退化为空操作。保留本组件仅用于兼容旧系统。
/// </summary>
[DisallowMultipleComponent]
public class ServerEnemyMovementDriver : NetworkBehaviour
{
    /// <summary>
    /// 敌人 AI 模块引用。
    /// </summary>
    private EnemyAIModule _ai;

    /// <summary>
    /// 敌人逻辑对象引用。
    /// </summary>
    private EnemyActor _actor;

    /// <summary>
    /// 物理载体 Transform。
    /// </summary>
    private Transform _physicsCarrier;

    /// <summary>
    /// NavMeshAgent 控制器引用。
    /// 若存在则表示 NavMeshAgent 正在接管移动，本驱动器退火。
    /// </summary>
    private EnemyNavAgentController _navController;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        _actor = GetComponent<EnemyActor>();
        if (_actor != null && _actor.AIModule == null)
        {
            _actor.ActivateAfterSpawn();
        }

        _ai = _actor != null ? _actor.GetModule<EnemyAIModule>() : null;
        _navController = GetComponent<EnemyNavAgentController>();

        if (_actor != null)
        {
            _physicsCarrier = _actor.physicsCarrier;
        }

        if (_navController != null)
        {
            Debug.Log($"[ServerEnemyMovementDriver] Enemy[{_actor?.ObjectId}] 检测到 EnemyNavAgentController，驱动器跳过移动逻辑");
        }
        else
        {
            Debug.Log($"[ServerEnemyMovementDriver] Enemy[{_actor?.ObjectId}] 未检测到 EnemyNavAgentController，驱动器将执行移动逻辑");
        }
    }

    public override void OnNetworkDespawn()
    {
        _ai = null;
        _actor = null;
        _physicsCarrier = null;
        _navController = null;
        base.OnNetworkDespawn();
    }

    private void FixedUpdate()
    {
        if (!IsServer || !IsSpawned || _ai == null)
            return;

        if (_navController != null)
            return;

        var intent = _ai.MoveIntent;
        if (!intent.hasIntent)
            return;

        var dir = intent.direction;
        if (dir.sqrMagnitude < 1e-4f)
            return;

        var targetTransform = _physicsCarrier != null ? _physicsCarrier : transform;
        var currentPosition = targetTransform.position;

        if (currentPosition.y < -10f)
        {
            Debug.LogWarning($"[ServerEnemyMovementDriver] 敌人 {_actor?.ObjectId} 掉出地图，强制返回出生点");
            return;
        }

        var rb = targetTransform.GetComponent<Rigidbody>();
        var newPosition = currentPosition + dir * (intent.speed * Time.fixedDeltaTime);

        if (rb != null && !rb.isKinematic)
        {
            rb.MovePosition(newPosition);
        }
        else
        {
            targetTransform.position = newPosition;
        }

        targetTransform.rotation = Quaternion.Slerp(
            targetTransform.rotation,
            Quaternion.LookRotation(dir),
            10f * Time.fixedDeltaTime
        );
    }
}
