// 文件：NetworkLayer/Drop/NetworkDropItem.cs
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Framework.NetworkLayer.NetworkObjectPool;

[RequireComponent(typeof(NetworkObject))]
public class NetworkDropItem : NetworkBehaviour
{
    // --- 运行时由服务器注入 ---
    private string _prefabPath;                 // 回收到池里需要
    private string _itemSoId;
    private int _count;
    private bool _isShared;

    // 归属制：默认只有 ownerClientId 能捡（-1 表示未指定，谁先捡谁得）
    private ulong? _ownerClientId = null;

    // shared：每个玩家只能捡一次
    private readonly HashSet<ulong> _claimedClients = new();

    private bool _picked = false;

    [Header("掉落物存在时长（超时回收）")]
    [SerializeField] private float lifeTime = 30f;

    private float _spawnTime;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            _spawnTime = Time.time;
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        if (!_picked && lifeTime > 0f && Time.time - _spawnTime >= lifeTime)
        {
            RecycleToPool();
        }
    }

    /// <summary>
    /// 服务器初始化（生成后立刻调用）
    /// </summary>
    public void ServerInit(string prefabPath, string itemSoId, int count, bool isShared, ulong? ownerClientId)
    {
        if (!IsServer) return;

        _prefabPath = prefabPath;
        _itemSoId = itemSoId;
        _count = Mathf.Max(1, count);
        _isShared = isShared;
        _ownerClientId = ownerClientId;
        _picked = false;
        _claimedClients.Clear();
        _spawnTime = Time.time;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (_picked && !_isShared) return;

        // 你项目里玩家一般有 PlayerNetworkProxy，这里用它来拿 clientId 最靠谱
        var proxy = other.GetComponentInParent<PlayerNetworkProxy>();
        if (proxy == null) return;

        ulong pickerClientId = proxy.OwnerClientId;

        // 归属制
        if (_ownerClientId.HasValue && pickerClientId != _ownerClientId.Value)
            return;

        if (_isShared)
        {
            if (_claimedClients.Contains(pickerClientId))
                return;

            _claimedClients.Add(pickerClientId);
            GrantItemToPlayer(proxy, _itemSoId, _count);

            // shared：如果你希望“所有玩家都捡完才消失”，需要知道房间人数
            // 这里先做一个常见策略：被任何人捡到也不立刻消失，直到超时回收。
            return;
        }

        // 非 shared：谁先捡谁得
        GrantItemToPlayer(proxy, _itemSoId, _count);
        _picked = true;
        RecycleToPool();
    }

    private void GrantItemToPlayer(PlayerNetworkProxy proxy, string itemSoId, int count)
    {
        // ✅ 服务器侧发放：推荐你走 Inventory 的“服务器直接加物品”入口
        var inv = proxy.NetworkInventory;
        if (inv != null)
        {
            var item = SOManager.Instance.GetSOById<BaseInventoryItemSO>(itemSoId);
            inv.AddItemServerRpc(new InventoryItem(item), count);
        }
    }

    private void RecycleToPool()
    {
        var no = GetComponent<NetworkObject>();
        if (no == null) return;

        if (!string.IsNullOrEmpty(_prefabPath))
            NetworkObjectPoolManager.Instance.DespawnAndRecycle(no, _prefabPath);
        else
            no.Despawn();
    }
}
