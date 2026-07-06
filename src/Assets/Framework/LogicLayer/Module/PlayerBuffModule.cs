using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家 Buff 业务模块：
/// - 实现 IModule（也就是 ILogicObject）
/// - 通过 PlayerNetworkProxy 访问 ServerBuffModule
/// - 对上层逻辑/品质系统/技能系统提供统一接口
/// </summary>
public class PlayerBuffModule : IModule
{
    private readonly PlayerActor _owner;
    private ServerBuffModule _serverBuffModule;    // 网络端权威 Buff 模块（挂在同一个 NetworkObject 上）

    public ulong ObjectId => _owner.ObjectId;

    public PlayerBuffModule(PlayerActor owner)
    {
        _owner = owner;
    }

    /// <summary>
    /// 本地初始化：绑定 ServerBuffModule
    /// </summary>
    public void LocalInit()
    {
        if (_owner?.networkProxy == null)
        {
            Debug.LogError("PlayerBuffModule 初始化失败：PlayerActor 缺少 networkProxy");
            return;
        }

        _serverBuffModule = _owner.networkProxy.GetComponent<ServerBuffModule>();
        if (_serverBuffModule == null)
        {
            Debug.LogError("PlayerBuffModule 初始化失败：NetworkObject 上缺少 ServerBuffModule 组件");
        }
    }

    /// <summary>
    /// 激活回调（目前可以空着，将来需要时可用于刷新 UI）
    /// </summary>
    public void OnActivate()
    {
        // 比如：通知 UI 系统“可以开始监听 Buff 更新”
    }

    /// <summary>
    /// 本地销毁：解除引用
    /// </summary>
    public void LocalDestroy()
    {
        _serverBuffModule = null;
    }

    // ========= 对上层暴露的查询接口 =========

    /// <summary>逻辑层判断是否有某个 Buff（给品质系统/技能系统用）。</summary>
    public bool HasBuff(int buffId)
    {
        return _serverBuffModule != null && _serverBuffModule.HasBuff(buffId);
    }

    public int GetBuffStacks(int buffId)
    {
        return _serverBuffModule != null ? _serverBuffModule.GetBuffStacks(buffId) : 0;
    }

    /// <summary>
    /// 给 UI 使用的“影子快照”：
    /// - 只包含 buffId / 层数 / 剩余时间
    /// - 数据来自服务器权威模块的 NetworkList
    /// </summary>
    public IReadOnlyList<BuffNetState> GetBuffSnapshot()
    {
        if (_serverBuffModule?.NetBuffs == null)
            return new List<BuffNetState>().AsReadOnly();

        // 使用 ToList() 创建一个快照副本并转为只读列表
        var snapshot = new List<BuffNetState>();
        foreach (var buff in _serverBuffModule.NetBuffs)
        {
            snapshot.Add(buff);
        }
        return snapshot.AsReadOnly();
    }

    // ========= 给技能 / 品质系统调用的施加接口（只在服务器调用） =========

    /// <summary>
    /// 应用 Buff（服务器权威）。
    /// 建议只在服务器侧调用（比如品质触发在服务器算完后调用）。
    /// </summary>
    public void ApplyBuff(BuffData buffData, float durationOverride = -1f)
    {
        if (_serverBuffModule == null)
            return;

        if (!_serverBuffModule.IsServer)
        {
            Debug.LogWarning("在客户端调用 PlayerBuffModule.ApplyBuff 无效，请在服务器调用。");
            return;
        }

        _serverBuffModule.ApplyBuff(buffData,1, durationOverride);
    }

    public void RemoveBuff(int buffId)
    {
        if (_serverBuffModule == null)
            return;

        if (!_serverBuffModule.IsServer)
        {
            Debug.LogWarning("在客户端调用 PlayerBuffModule.RemoveBuff 无效，请在服务器调用。");
            return;
        }

        _serverBuffModule.RemoveBuff(buffId);
    }
}
