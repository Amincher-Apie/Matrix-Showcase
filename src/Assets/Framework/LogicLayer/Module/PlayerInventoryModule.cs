using UnityEngine;

/// <summary>
/// 玩家背包业务模块，封装网络背包的高层逻辑与权限校验。
/// </summary>
public class PlayerInventoryModule : IModule
{
    private readonly PlayerActor _owner;
    private NetworkInventory _networkInventory;
    private InGameInventoryManager _inventoryManager;

    /// <summary>
    /// 关联逻辑对象的网络 ID。
    /// </summary>
    public ulong ObjectId => _owner?.ObjectId ?? 0;

    /// <summary>
    /// 玩家背包的本地缓存数据。
    /// </summary>
    public InGameInventoryData InventoryData => _inventoryManager?.InventoryData;

    /// <summary>
    /// 以玩家逻辑体构造模块。
    /// </summary>
    public PlayerInventoryModule(PlayerActor owner)
    {
        _owner = owner;
    }

    /// <summary>
    /// 本地初始化，绑定网络背包并创建缓存管理器。
    /// </summary>
    public void LocalInit()
    {
        _networkInventory = _owner.networkProxy.GetComponent<NetworkInventory>();
        if (_networkInventory == null)
        {
            Debug.LogError("PlayerInventoryModule 初始化失败：缺少 NetworkInventory 组件");
            return;
        }

        _networkInventory.EnsureContainersInitialized();
        _inventoryManager = new InGameInventoryManager(_networkInventory);
    }

    /// <summary>
    /// 激活回调（当前未使用，可在此处理 UI 初始化）。
    /// </summary>
    public void OnActivate()
    {
    }

    /// <summary>
    /// 释放模块，注销缓存与网络引用。
    /// </summary>
    public void LocalDestroy()
    {
        _inventoryManager?.Dispose();
        _inventoryManager = null;
        _networkInventory = null;
    }

    public void PickupExpandChip(EnumItemType targetType, int addCount = 1)
    {
        //判断是否是自己控制的角色
        if (!HasControlAuthority())
        {
            return;
        }
        //调用网络背包的对应方法 向服务器发送RPC申请
        _networkInventory?.ExpandSlotCapacityServerRpc(targetType, addCount);
    }

    /// <summary>
    /// 请求服务器执行合成操作。
    /// </summary>
    public void SynthesizeItems(string itemId1, string itemId2)
    {
        if (!HasControlAuthority())
        {
            return;
        }

        var item1 = SOManager.Instance?.GetSOById<BaseInventoryItemSO>(itemId1);
        var item2 = SOManager.Instance?.GetSOById<BaseInventoryItemSO>(itemId2);

        if (item1 == null || item2 == null)
        {
            ShowTips("合成道具无效");
            return;
        }

        if (item1.qualityLevel == EnumQualityLevel.Legendary || item2.qualityLevel == EnumQualityLevel.Legendary)
        {
            ShowTips("传奇道具不可合成");
            return;
        }

        _networkInventory?.SynthesizeItemServerRpc(itemId1, itemId2);
    }

    /// <summary>
    /// 切换主动道具槽为指定道具。
    /// </summary>
    public void SwitchActiveItem(string itemId)
    {
        if (!HasControlAuthority())
        {
            return;
        }

        var itemSO = SOManager.Instance?.GetSOById<BaseInventoryItemSO>(itemId);
        if (itemSO == null || itemSO.itemType != EnumItemType.Active)
        {
            ShowTips("无效的主动道具");
            return;
        }

        _networkInventory?.RemoveItemServerRpc(itemId, 1);
        _networkInventory?.AddItemServerRpc(new InventoryItem(itemSO), 1);
    }

    /// <summary>
    /// 添加道具到背包，可用于拾取或奖励。
    /// </summary>
    public void AddInventoryItem(string itemId, long amount = 1)
    {
        if (!HasControlAuthority())
        {
            return;
        }

        var itemSO = SOManager.Instance?.GetSOById<BaseInventoryItemSO>(itemId);
        if (itemSO == null)
        {
            ShowTips("道具不存在");
            return;
        }

        _networkInventory?.AddItemServerRpc(new InventoryItem(itemSO), amount);
    }

    /// <summary>
    /// 从背包中移除指定数量的道具。
    /// </summary>
    public void RemoveItem(string itemId, long amount = 1)
    {
        if (!HasControlAuthority())
        {
            return;
        }

        _networkInventory?.RemoveItemServerRpc(itemId, amount);
    }

    /// <summary>
    /// 判断当前客户端是否拥有背包操控权限。
    /// </summary>
    private bool HasControlAuthority()
    {
        if (_owner == null)
        {
            return false;
        }

        return _owner.IsOwner || _owner.IsServer;
    }

    /// <summary>
    /// 简易提示接口，后续可替换成 HUD 提示。
    /// </summary>
    private void ShowTips(string message)
    {
        Debug.Log(message);
    }
}

