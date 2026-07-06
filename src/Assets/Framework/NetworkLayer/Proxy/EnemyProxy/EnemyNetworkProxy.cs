using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 敌人网络代理，负责在服务端网络生命周期内驱动权威 AI Tick。
/// 当前阶段继续保留单个敌人的自治结构，但将 Tick 频率委托给服务端 AIScheduler 统一决策。
/// </summary>
public class EnemyNetworkProxy : NetworkProxyBase
{
    /// <summary>
    /// 当前网络代理关联的敌人逻辑对象。
    /// </summary>
    public EnemyActor EnemyActor { get; private set; }

    /// <summary>
    /// 当前敌人的服务端属性模块引用。
    /// </summary>
    [field: SerializeField]
    public ServerEnemyAttributeModule ServerEnemyAttributeModule { get; private set; }
    
    /// <summary>
    /// 当前敌人的服务端战斗模块引用。
    /// </summary>
    [field: SerializeField]
    public ServerCombatModule ServerCombatModule { get; private set; }

    /// <summary>
    /// 当前敌人的服务端武器运行时引用。
    /// </summary>
    [field: SerializeField]
    public ServerWeaponRuntime ServerWeaponRuntime { get; private set; }
    
    /// <summary>
    /// 当前敌人的服务端技能模块引用。
    /// </summary>
    [field: SerializeField]
    public ServerSkillModule ServerSkillModule { get; private set; }
    
    /// <summary>
    /// 当前敌人的服务端 Buff 模块引用。
    /// </summary>
    [field: SerializeField]
    public ServerBuffModule ServerBuffModule { get; private set; }
    
    /// <summary>
    /// 当前缓存的敌人 AI 模块引用。
    /// </summary>
    private EnemyAIModule _ai;

    /// <summary>
    /// 当前服务端 AI Tick 协程句柄。
    /// </summary>
    private Coroutine _aiTickRoutine;

    /// <summary>
    /// 当调度器不可用时使用的回退 Tick 间隔。
    /// 该字段保留用于兼容与调试，不再作为唯一调度来源。
    /// </summary>
    [Header("UI")]
    [SerializeField] private Transform _uiAnchor;

    [Header("AI Tick")]
    [SerializeField] private float aiTickInterval = 0.1f;
    
    /// <summary>
    /// 设置与该网络代理关联的敌人逻辑对象。
    /// </summary>
    /// <param name="enemyActor">要绑定的敌人逻辑对象。</param>
    public void SetEnemyActor(EnemyActor enemyActor)
    {
        EnemyActor = enemyActor;
        base.SetLogicObject(enemyActor);
        if (enemyActor != null)
        {
            enemyActor.TryResolveNetworkObject(GetComponent<NetworkObject>());
        }
    }
    
    /// <summary>
    /// 获取敌人对应的服务端属性模块。
    /// </summary>
    /// <typeparam name="T">服务端属性模块类型。</typeparam>
    /// <returns>返回匹配类型的服务端属性模块。</returns>
    public override T GetServerAttributeModule<T>()
    {
        return ServerEnemyAttributeModule as T;
    }

    /// <summary>
    /// 获取敌人对应的服务端武器运行时。
    /// </summary>
    /// <typeparam name="T">服务端武器运行时类型。</typeparam>
    /// <returns>返回匹配类型的服务端武器运行时。</returns>
    public override T GetServerWeaponRuntime<T>()
    {
        return ServerWeaponRuntime as T;
    }

    /// <summary>
    /// 获取敌人对应的服务端战斗模块。
    /// </summary>
    /// <typeparam name="T">服务端战斗模块类型。</typeparam>
    /// <returns>返回匹配类型的服务端战斗模块。</returns>
    public override T GetServerCombatModule<T>()
    {
        return ServerCombatModule as T;
    }
    
    /// <summary>
    /// NGO 网络生成回调。
    /// 客户端注册血条锚点；服务端启动权威 AI 的定时调度，并注册到 AIScheduler。
    /// </summary>
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsClient)
        {
            EnemyHealthBarManager.Instance.RegisterEnemy(NetworkObjectId, ResolveUIAnchor());
        }

        if (!IsServer)
        {
            return;
        }

        if (!TryResolveEnemyAI(out var aiModule))
            return;

        Debug.Log($"[EnemyNetworkProxy] Enemy[{EnemyActor.ObjectId}] OnNetworkSpawn - 注册到 AIScheduler 和 SteeringSystem");
        AIScheduler.Instance.RegisterEnemy(EnemyActor);
        SteeringSystem.Instance.RegisterEnemy(EnemyActor);

        StopAITickRoutine();
        _ai = aiModule;
        _aiTickRoutine = StartCoroutine(ServerAITick());
    }

    private void StopAITickRoutine()
    {
        if (_aiTickRoutine == null)
            return;

        Debug.Log($"[EnemyNetworkProxy] Enemy[{EnemyActor?.ObjectId}] 停止 AI Tick 协程");
        StopCoroutine(_aiTickRoutine);
        _aiTickRoutine = null;
    }

    /// <summary>
    /// 服务端 AI 调度协程。
    /// 每次执行后都会向 AIScheduler 查询下一次应等待的间隔，以实现最小可用的仿真 LOD。
    /// </summary>
    /// <returns>返回服务端 AI 协程枚举器。</returns>
    private IEnumerator ServerAITick()
    {
        var tickCount = 0;
        Debug.Log($"[EnemyNetworkProxy] Enemy[{EnemyActor.ObjectId}] ServerAITick 协程启动");

        while (IsServer && _ai != null && EnemyActor != null)
        {
            tickCount++;
            var interval = AIScheduler.Instance.GetTickInterval(EnemyActor, _ai, aiTickInterval);
            var simLevel = AIScheduler.Instance.GetCurrentSimulationLevel(EnemyActor);

            if (tickCount % 10 == 0 || simLevel != AISimulationLevel.Full)
            {
                Debug.Log($"[EnemyNetworkProxy] Enemy[{EnemyActor.ObjectId}] Tick#{tickCount} 仿真级别={simLevel} 下次间隔={interval:F3}s");
            }

            _ai.ServerTick();
            yield return new WaitForSeconds(Mathf.Max(0.02f, interval));
        }

        Debug.Log($"[EnemyNetworkProxy] Enemy[{EnemyActor?.ObjectId}] AI Tick 协程结束 (共执行 {tickCount} 次)");
    }
    
    /// <summary>
    /// NGO 网络反生成回调。
    /// 这里会停止服务端 AI 调度、从 AIScheduler 注销并释放缓存引用，避免对象池复用时残留旧状态。
    /// </summary>
    public override void OnNetworkDespawn()
    {
        if (IsClient)
        {
            EnemyHealthBarManager.Instance.UnregisterEnemy(NetworkObjectId);
        }

        if (!IsServer)
        {
            base.OnNetworkDespawn();
            return;
        }

        StopAITickRoutine();

        if (EnemyActor != null)
        {
            Debug.Log($"[EnemyNetworkProxy] Enemy[{EnemyActor.ObjectId}] OnNetworkDespawn - 从 AIScheduler 和 SteeringSystem 注销");
            AIScheduler.Instance.UnregisterEnemy(EnemyActor);
            SteeringSystem.Instance.UnregisterEnemy(EnemyActor);
        }

        _ai = null;
        base.OnNetworkDespawn();
    }

    private Transform ResolveUIAnchor()
    {
        if (_uiAnchor != null)
        {
            return _uiAnchor;
        }

        var namedAnchor = transform.Find("UIAnchor") ??
                          transform.Find("UiAnchor") ??
                          transform.Find("HealthBarAnchor") ??
                          transform.Find("HpAnchor");
        return namedAnchor != null ? namedAnchor : transform;
    }

    /// <summary>
    /// 解析并校验当前敌人的 AI 模块引用。
    /// </summary>
    /// <param name="aiModule">输出解析到的 AI 模块。</param>
    /// <returns>返回 true 表示已成功获取服务端可用的 AI 模块。</returns>
    private bool TryResolveEnemyAI(out EnemyAIModule aiModule)
    {
        aiModule = null;

        var actor = GetComponent<EnemyActor>();
        if (actor == null)
        {
            Debug.LogError("[EnemyNetworkProxy] EnemyActor not found");
            return false;
        }

        SetEnemyActor(actor);

        if (actor.AIModule == null)
        {
            actor.ActivateAfterSpawn();
        }

        aiModule = actor.AIModule;
        if (aiModule == null)
        {
            Debug.LogError("[EnemyNetworkProxy] EnemyAIModule not found");
            return false;
        }

        return true;
    }
}
