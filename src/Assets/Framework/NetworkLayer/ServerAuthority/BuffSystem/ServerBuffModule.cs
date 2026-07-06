using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 服务器权威的 Buff 模块：
/// - 挂在具体单位的 NetworkObject 下
/// - 内部用 BuffHandler 做逻辑
/// - 用 NetworkList 把简化后的 Buff 状态同步给客户端
/// </summary>
public class ServerBuffModule : NetworkBehaviour
{
    [SerializeField] private ServerAttributeModule attrModule;
    public IBuffOwnerContext OwnerContext { get; private set; }
    public BuffHandler Handler { get; private set; }

    /// <summary>同步给客户端的“影子”列表。</summary>
    [HideInInspector]
    public NetworkList<BuffNetState> NetBuffs;
    private readonly List<BuffInfo> _runtimeBuffs = new();

    #region 生命周期函数
    private void Awake()
    {
        // ✅ 在生命周期里创建（编辑器导入/运行时都会走）
        NetBuffs ??= new NetworkList<BuffNetState>();
    }

    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        NetBuffs ??= new NetworkList<BuffNetState>();
        
        // 只在服务器初始化逻辑
        if (IsServer)
        {
            OwnerContext = BuildOwnerContext();
            Handler = new BuffHandler(OwnerContext);
            Handler.OnLayerAdd += OnBuffLayerAdd;
            Handler.OnLayerFallToZero += OnBuffLayerZero;
            NetBuffs.Clear();
        }
    }
    
    public override void OnNetworkDespawn()
    {
        if (Handler != null)
        {
            Handler.OnLayerAdd -= OnBuffLayerAdd;
            Handler.OnLayerFallToZero -= OnBuffLayerZero;
        }

        Handler = null;
        OwnerContext = null;
        _runtimeBuffs.Clear();

        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        if (Handler != null)
        {
            Handler.OnLayerAdd -= OnBuffLayerAdd;
            Handler.OnLayerFallToZero -= OnBuffLayerZero;
            Handler = null;
        }

        OwnerContext = null;
        _runtimeBuffs.Clear();
        base.OnDestroy();
    }
    #endregion

    private IBuffOwnerContext BuildOwnerContext()
    {
        if (!attrModule)
        {
            Debug.LogError("[ServerBuffModule] 缺少 ServerAttributeModule");
        }

        var category = BuffOwnerCategory.Player;
        if(attrModule is ServerEnemyAttributeModule tmp)
        {
            category = tmp.GetMonsterRank() switch
            {
                MonsterRank.Boss => BuffOwnerCategory.BossEnemy,
                MonsterRank.Elite => BuffOwnerCategory.EliteEnemy,
                MonsterRank.Normal => BuffOwnerCategory.NormalEnemy,
                _ => category
            };
        }

        return new BuffOwnerContext(
            NetworkObjectId,
            category,
            attrModule   // 直接作为 IAttributeProxy 传进去
        );
    }

    private void Update()
    {
        if (!IsServer || Handler == null) return;

        Handler.OnLogicUpdate(Time.deltaTime);
        SyncRuntimeToNet();
    }

    #region 公共接口（给技能系统 / 其它模块调用）

    public void ApplyBuff(BuffData data, int stacks = 1, float durationOverride = -1f)
    {
        ApplyBuff(data, stacks, durationOverride, 0UL, 0UL, default, ElementType.Fire, 0f);
    }

    public void ApplyBuff(
        BuffData data,
        int stacks,
        float durationOverride,
        ulong applierObjectId,
        ulong applierClientId,
        DamageInfo sourceDamageInfo,
        ElementType elementType,
        float elementDamageSnapshot)
    {
        if (!IsServer || !data || OwnerContext == null) return;

        stacks = Mathf.Max(1, stacks);
        var info = new BuffInfo(
            data,
            OwnerContext,
            durationOverride,
            applierObjectId,
            applierClientId,
            sourceDamageInfo,
            elementType,
            elementDamageSnapshot);

        for (int i = 0; i < stacks; i++)
        {
            if (i == 0)
            {
                Handler.AddBuff(info);
            }
            else
            {
                Handler.AddBuff(new BuffInfo(
                    data,
                    OwnerContext,
                    durationOverride,
                    applierObjectId,
                    applierClientId,
                    sourceDamageInfo,
                    elementType,
                    elementDamageSnapshot));
            }
        }
        if (!_runtimeBuffs.Contains(info))
        {
            _runtimeBuffs.Add(info);
        }
        SyncRuntimeToNet();
    }

    public void RemoveBuff(int buffId)
    {
        if (!IsServer || Handler == null) return;

        var info = Handler.FindBuff(buffId);
        if (info != null)
        {
            Handler.RemoveBuff(info);
        }
        SyncRuntimeToNet();
    }

    public bool HasBuff(int buffId)
    {
        return Handler?.FindBuff(buffId) != null;
    }

    public int GetBuffStacks(int buffId)
    {
        return Handler?.GetLayers(buffId) ?? 0;
    }

    #endregion

    #region 内部：同步给客户端

    private void SyncRuntimeToNet()
    {
        if (!IsServer || Handler == null || NetBuffs == null) return;

        NetBuffs.Clear();

        foreach (var buff in Handler.BuffInfoSets)
        {
            var state = new BuffNetState
            {
                buffId = buff.buffData.buffID,
                stacks = buff.currentStack,
                remainDuration = buff.buffData.isForever ? -1f : buff.durationTime
            };
            NetBuffs.Add(state);
        }
    }

    private void OnBuffLayerAdd(BuffInfo info)
    {
        // 这里只是给未来扩展用，比如打印日志 / 触发事件
    }

    private void OnBuffLayerZero(BuffInfo info)
    {
        // 同上…
    }

    #endregion
}

/// <summary>
/// 同步给客户端的简化 Buff 状态。
/// </summary>
public struct BuffNetState : INetworkSerializable, IEquatable<BuffNetState>
{
    public int buffId;
    public int stacks;
    public float remainDuration;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref buffId);
        serializer.SerializeValue(ref stacks);
        serializer.SerializeValue(ref remainDuration);
    }
    
    // 实现 IEquatable<BuffNetState> 接口
    public bool Equals(BuffNetState other)
    {
        return buffId == other.buffId &&
               stacks == other.stacks &&
               Mathf.Approximately(remainDuration, other.remainDuration);
    }

    public override bool Equals(object obj)
    {
        return obj is BuffNetState state && Equals(state);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 23 + buffId.GetHashCode();
            hash = hash * 23 + stacks.GetHashCode();
            hash = hash * 23 + remainDuration.GetHashCode();
            return hash;
        }
    }
}
