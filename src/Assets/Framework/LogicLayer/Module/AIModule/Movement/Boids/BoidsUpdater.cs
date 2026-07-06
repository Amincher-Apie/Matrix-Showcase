using Framework.LogicLayer.Module.AIModule.Movement.Boids;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Boids 系统服务端每帧更新驱动器。
/// 该组件挂载在持久化的服务端对象上，负责在每帧调用 BoidsCentralController 的批量计算。
/// 
/// 职责：
/// 1. 确保 BoidsCentralController 已初始化。
/// 2. 在每帧服务端 Update 时，根据当前最高仿真级别（Full）调用 BatchComputeBoidsForAllRegistered。
/// 3. 仅在 IsServer 时激活，非服务端实例不执行任何逻辑。
/// 
/// 使用方式：
/// 在服务端的持久化 GameObject（如 NetworkManager 所在的 GameObject）上添加此组件即可。
/// </summary>
[DisallowMultipleComponent]
public class BoidsUpdater : NetworkBehaviour
{
    [Header("Boids 配置")]
    [Tooltip("每多少帧调用一次 Boids 批量计算。设为 1 表示每帧都计算。较大的值可以进一步降低服务端开销。")]
    [Range(1, 10)]
    public int boidsUpdateInterval = 1;

    [Tooltip("若启用，当所有敌人都处于 Dormant 级别时跳过本帧 Boids 计算。")]
    public bool skipWhenAllDormant = true;

    [Header("调试")]
    [Tooltip("是否在控制台输出每帧 Boids 计算统计。")]
    public bool logStatistics = false;

    private int _frameCounter = 0;
    private bool _hasTriggeredThisFrame = false;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        _frameCounter = 0;
        _hasTriggeredThisFrame = false;
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned)
            return;

        _frameCounter++;

        if (_frameCounter < boidsUpdateInterval)
            return;

        _frameCounter = 0;
        _hasTriggeredThisFrame = true;
        ExecuteBoidsBatchUpdate();
    }

    private void ExecuteBoidsBatchUpdate()
    {
        if (BoidsCentralController.Instance == null)
            return;

        if (skipWhenAllDormant && !HasAnyActiveEnemy())
            return;

        var currentLevel = GetCurrentSimulationLevel();
        BoidsCentralController.Instance.BatchComputeBoidsForAllRegistered(currentLevel);

        if (logStatistics)
        {
            var debugInfo = BoidsCentralController.Instance.GetDebugInfo();
            if (debugInfo.processedAgentCount > 0)
            {
                AIDebug.LogChannel("AI.Steering", $"[BoidsUpdater] {debugInfo.ToSummaryString()}");
            }
        }
    }

    private bool HasAnyActiveEnemy()
    {
        return BoidsCentralController.Instance != null 
               && BoidsCentralController.Instance.GetActiveCount() > 0;
    }

    private AISimulationLevel GetCurrentSimulationLevel()
    {
        if (AIScheduler.Instance == null)
            return AISimulationLevel.Full;

        return AIScheduler.Instance.GetCurrentSimulationLevel(null);
    }
}
