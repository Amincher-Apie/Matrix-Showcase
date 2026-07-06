using UnityEngine;

/// <summary>
/// 可被敌人感知与攻击的对象抽象接口。
/// 敌人 AI 不直接依赖具体玩家、建筑或任务目标类型，而是通过该接口统一获取最小公共信息。
/// </summary>
public interface IAttackableObject
{
    /// <summary>
    /// 获取目标逻辑对象 ID。
    /// </summary>
    ulong ObjectId { get; }

    /// <summary>
    /// 获取该目标的根 Transform。
    /// </summary>
    Transform TargetTransform { get; }

    /// <summary>
    /// 获取该目标的类型。
    /// </summary>
    AttackableObjectType TargetType { get; }

    /// <summary>
    /// 获取该目标当前是否允许被 AI 感知与攻击。
    /// 用于全局启用/禁用（如 Spawn/Despawn 时切换）。
    /// </summary>
    bool IsActiveForAI { get; }

    /// <summary>
    /// 获取该目标当前是否处于存活状态（Alive）。
    /// Dead/Spectating 返回 false；Downed 由配置决定。
    /// 与 IsActiveForAI 的区别：IsActiveForAI 是注册/注销开关，IsAliveForAI 是生命状态过滤。
    /// </summary>
    bool IsAliveForAI => true; // 默认实现：始终存活，子类按需覆写

    /// <summary>
    /// 获取该目标的基础威胁优先级。
    /// 数值越高，表示在同等条件下越值得优先被选中。
    /// </summary>
    int ThreatPriority { get; }

    /// <summary>
    /// 获取 AI 用于感知与视线检测的目标点。
    /// </summary>
    /// <returns>返回目标在世界空间中的参考点。</returns>
    Vector3 GetTargetPoint();
}
