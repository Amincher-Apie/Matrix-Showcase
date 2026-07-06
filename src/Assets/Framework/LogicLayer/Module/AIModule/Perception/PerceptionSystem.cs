using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 感知系统，负责基于统一注册表筛选当前最合适的目标。
/// </summary>
public class PerceptionSystem
{
    /// <summary>
    /// 感知系统所属的敌人逻辑对象。
    /// </summary>
    private readonly EnemyActor _owner;

    /// <summary>
    /// 感知系统使用的 AI 配置。
    /// </summary>
    private readonly EnemyAIConfig _config;

    /// <summary>
    /// 查询候选时复用的缓冲区，避免每次感知产生新分配。
    /// </summary>
    private readonly List<IAttackableObject> _candidateBuffer = new List<IAttackableObject>(8);
    
    /// <summary>
    /// 构造感知系统。
    /// </summary>
    /// <param name="owner">拥有该感知系统的敌人对象。</param>
    /// <param name="config">感知系统使用的 AI 配置。</param>
    public PerceptionSystem(EnemyActor owner, EnemyAIConfig config)
    {
        _owner = owner;
        _config = config;
    }
    
    /// <summary>
    /// 检测当前最合适的可攻击目标。
    /// 当前阶段从 AttackableObjectManager 拉取候选，再做距离、视野和遮挡筛选。
    /// </summary>
    /// <returns>返回当前最合适的目标；若没有可用目标则返回 null。</returns>
    public IAttackableObject DetectTarget()
    {
        if (_owner == null)
            return null;

        // 使用物理载体的实际位置进行感知
        var enemyPosition = _owner.WorldPosition;
        AttackableObjectManager.Instance.GetCandidates(
            enemyPosition,
            _config.detectionRange,
            _candidateBuffer
        );

        if (_candidateBuffer.Count == 0)
            return null;

        IAttackableObject bestTarget = null;
        var bestScore = float.MinValue;

        for (var i = 0; i < _candidateBuffer.Count; i++)
        {
            var candidate = _candidateBuffer[i];
            if (candidate == null || !candidate.IsActiveForAI || !candidate.IsAliveForAI)
                continue;

            var targetPosition = candidate.GetTargetPoint();
            var distance = Vector3.Distance(enemyPosition, targetPosition);

            if (distance > _config.detectionRange)
                continue;

            if (_config.useFieldOfView && !IsInFieldOfView(targetPosition))
                continue;

            if (_config.checkLineOfSight && !HasLineOfSight(candidate))
                continue;

            var score = CalculateTargetScore(candidate, distance);
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }
    
    /// <summary>
    /// 计算目标得分。
    /// 当前阶段采用“威胁优先级权重 - 距离权重”的最小可用公式，避免魔法数散落在代码中。
    /// </summary>
    /// <param name="candidate">待评估的目标候选。</param>
    /// <param name="distance">敌人与候选目标的距离。</param>
    /// <returns>返回当前候选的综合得分。</returns>
    private float CalculateTargetScore(IAttackableObject candidate, float distance)
    {
        return candidate.ThreatPriority * _config.perceptionThreatScoreWeight
               - distance * _config.perceptionDistanceScoreWeight;
    }
    
    /// <summary>
    /// 检查目标是否位于视野范围内。
    /// </summary>
    /// <param name="targetPosition">目标位置。</param>
    /// <returns>返回 true 表示目标位于视野锥内。</returns>
    private bool IsInFieldOfView(Vector3 targetPosition)
    {
        var enemyPosition = _owner.WorldPosition;
        var enemyForward = _owner.WorldRotation * Vector3.forward;

        var directionToTarget = (targetPosition - enemyPosition).normalized;
        var angle = Vector3.Angle(enemyForward, directionToTarget);
        return angle <= _config.fieldOfViewAngle / 2f;
    }

    /// <summary>
    /// 检查与目标之间是否存在有效视线。
    /// 只有当射线被场景障碍物阻挡（非目标自身）时才判定为视线被遮挡。
    /// </summary>
    /// <param name="target">要检测视线的目标。</param>
    /// <returns>返回 true 表示敌人与目标之间视线通畅或被玩家自身遮挡。</returns>
    private bool HasLineOfSight(IAttackableObject target)
    {
        if (target == null || target.TargetTransform == null)
            return false;

        var origin = _owner.WorldPosition + Vector3.up * 1f;
        var targetPoint = target.GetTargetPoint();
        var direction = (targetPoint - origin).normalized;
        var distance = Vector3.Distance(origin, targetPoint);

        if (Physics.Raycast(origin, direction, out var hit, distance, _config.obstacleLayerMask))
        {
            return hit.collider.transform == target.TargetTransform ||
                   hit.collider.transform.IsChildOf(target.TargetTransform);
        }

        return true;
    }
}
