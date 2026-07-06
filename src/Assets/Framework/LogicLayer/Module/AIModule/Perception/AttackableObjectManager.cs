using System.Collections.Generic;
using Framework.Singleton;
using UnityEngine;

/// <summary>
/// 可攻击对象管理器。
/// 当前阶段负责统一注册、注销与粗粒度候选查询，后续可继续扩展为更高效的空间索引或兴趣区查询。
/// </summary>
public class AttackableObjectManager : SingletonBase<AttackableObjectManager>
{
    /// <summary>
    /// 已注册的可攻击对象集合，用于快速去重。
    /// </summary>
    private readonly HashSet<IAttackableObject> _registeredSet = new HashSet<IAttackableObject>();

    /// <summary>
    /// 已注册的可攻击对象列表，用于顺序遍历查询。
    /// </summary>
    private readonly List<IAttackableObject> _registeredList = new List<IAttackableObject>();

    /// <summary>
    /// 注册一个可攻击对象。
    /// </summary>
    /// <param name="attackableObject">要注册的可攻击对象。</param>
    public void Register(IAttackableObject attackableObject)
    {
        if (attackableObject == null)
            return;

        if (_registeredSet.Add(attackableObject))
        {
            _registeredList.Add(attackableObject);
            AIDebug.LogChannel("AI.Perception", $"[AttackableObjectManager] Registered: {attackableObject.TargetType} ({attackableObject.ObjectId}), Total={_registeredList.Count}");
        }
    }

    /// <summary>
    /// 注销一个可攻击对象。
    /// </summary>
    /// <param name="attackableObject">要注销的可攻击对象。</param>
    public void Unregister(IAttackableObject attackableObject)
    {
        if (attackableObject == null)
            return;

        if (_registeredSet.Remove(attackableObject))
        {
            _registeredList.Remove(attackableObject);
            AIDebug.LogChannel("AI.Perception", $"[AttackableObjectManager] Unregistered: {attackableObject.TargetType} ({attackableObject.ObjectId}), Total={_registeredList.Count}");
        }
    }

    /// <summary>
    /// 按半径查询候选目标。
    /// 当前阶段仍是线性扫描，但已经把候选来源从场景全量枚举收口到了统一管理器。
    /// </summary>
    /// <param name="center">查询中心点。</param>
    /// <param name="radius">查询半径。</param>
    /// <param name="resultsBuffer">用于写入结果的外部缓冲区。</param>
    public void GetCandidates(Vector3 center, float radius, List<IAttackableObject> resultsBuffer)
    {
        if (resultsBuffer == null)
            return;

        resultsBuffer.Clear();

        if (_registeredList.Count == 0)
        {
            AIDebug.LogChannel("AI.Perception", $"[AttackableObjectManager.GetCandidates] center={center}, radius={radius}, registered=0 -> empty");
            return;
        }

        var sqrRadius = radius * radius;
        AIDebug.LogChannel("AI.Perception", $"[AttackableObjectManager.GetCandidates] center={center}, radius={radius}, registered={_registeredList.Count}");
        for (var i = _registeredList.Count - 1; i >= 0; i--)
        {
            var attackableObject = _registeredList[i];
            if (!IsCandidateValid(attackableObject))
            {
                AIDebug.LogWarning($"[AttackableObjectManager.GetCandidates] Skipped invalid: {attackableObject?.TargetType}({attackableObject?.ObjectId}), Active={attackableObject?.IsActiveForAI}, Transform={attackableObject?.TargetTransform}");
                RemoveAt(i);
                continue;
            }

            var delta = attackableObject.GetTargetPoint() - center;
            if (delta.sqrMagnitude <= sqrRadius)
            {
                resultsBuffer.Add(attackableObject);
            }
        }
    }

    /// <summary>
    /// 获取离指定点最近的可攻击对象距离。
    /// 当前阶段该方法主要供服务端 AIScheduler 做粗粒度仿真级别判断。
    /// </summary>
    /// <param name="center">查询中心点。</param>
    /// <returns>返回最近目标距离；若没有有效目标则返回正无穷。</returns>
    public float GetNearestTargetDistance(Vector3 center)
    {
        AIDebug.LogChannel("AI.Perception", $"[AttackableObjectManager.GetNearestTargetDistance] center={center}, registered={_registeredList.Count}");
        var bestSqrDistance = float.PositiveInfinity;

        for (var i = _registeredList.Count - 1; i >= 0; i--)
        {
            var attackableObject = _registeredList[i];
            // 只做非空和 Transform 检查，不检查活跃状态。
            // 活跃性判断由感知候选列表负责（GetCandidates），距离计算应覆盖所有已注册对象。
            if (attackableObject == null || attackableObject.TargetTransform == null)
            {
                AIDebug.LogWarning($"[AttackableObjectManager.GetNearestTargetDistance] Skipped null/invalid: {attackableObject?.ObjectId}");
                RemoveAt(i);
                continue;
            }

            var sqrDistance = (attackableObject.GetTargetPoint() - center).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
            }
        }

        return float.IsPositiveInfinity(bestSqrDistance) ? float.PositiveInfinity : Mathf.Sqrt(bestSqrDistance);
    }

        /// <summary>
        /// 获取当前已注册目标数量。
        /// 该接口主要用于调试与后续监控。
        /// </summary>
        /// <returns>返回当前注册表中的有效目标数。</returns>
        public int GetRegisteredCount()
        {
            return _registeredList.Count;
        }

        /// <summary>
        /// 获取当前已注册的所有 IAttackableObject 的只读快照。
        /// </summary>
        public IReadOnlyList<IAttackableObject> GetAllRegistered()
        {
            return _registeredList;
        }

    /// <summary>
    /// 判断候选目标当前是否仍然有效。
    /// 仅检查对象本身是否仍存在于场景中。
    /// 活跃性（IsActiveForAI）由感知层在选择最终目标时单独判断，不在此处过滤。
    /// </summary>
    /// <param name="attackableObject">待校验的可攻击对象。</param>
    /// <returns>返回 true 表示对象仍可用于查询。</returns>
    private static bool IsCandidateValid(IAttackableObject attackableObject)
    {
        if (attackableObject == null)
            return false;

        if (attackableObject.TargetTransform == null)
            return false;

        return true;
    }

    /// <summary>
    /// 移除指定索引上的无效候选对象。
    /// </summary>
    /// <param name="index">要移除的索引位置。</param>
    private void RemoveAt(int index)
    {
        var attackableObject = _registeredList[index];
        _registeredList.RemoveAt(index);

        if (attackableObject != null)
        {
            _registeredSet.Remove(attackableObject);
        }
    }
}
