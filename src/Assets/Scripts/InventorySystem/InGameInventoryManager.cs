using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 客户端侧背包管理器，负责监听网络背包并生成本地缓存供 UI/逻辑使用。
/// </summary>
public class InGameInventoryManager
{
    private readonly NetworkInventory _networkInventory;
    private readonly InGameInventoryData _inventoryData = new();

    /// <summary>
    /// 当前玩家背包的本地缓存数据。
    /// </summary>
    public InGameInventoryData InventoryData => _inventoryData;

    /// <summary>
    /// 构造并绑定指定的网络背包。
    /// </summary>
    /// <param name="networkInventory">目标网络背包组件。</param>
    public InGameInventoryManager(NetworkInventory networkInventory)
    {
        _networkInventory = networkInventory;
        if (_networkInventory == null)
        {
            Debug.LogError("NetworkInventory 未初始化");
            return;
        }

        _networkInventory.EnsureContainersInitialized();
        _networkInventory.OnContainerChanged += HandleContainerChanged;
        _networkInventory.OnInventoryChanged += HandleInventoryChanged;

        SyncAll();
    }

    /// <summary>
    /// 释放事件绑定，需在模块销毁时调用。
    /// </summary>
    public void Dispose()
    {
        if (_networkInventory == null)
        {
            return;
        }

        _networkInventory.OnContainerChanged -= HandleContainerChanged;
        _networkInventory.OnInventoryChanged -= HandleInventoryChanged;
    }

    /// <summary>
    /// 请求服务器向背包添加物品。
    /// </summary>
    public void RequestAddItem(BaseInventoryItemSO itemSO, long amount = 1)
    {
        if (_networkInventory == null || itemSO == null)
        {
            return;
        }

        var item = new InventoryItem(itemSO);
        _networkInventory.AddItemServerRpc(item, amount);
    }

    /// <summary>
    /// 请求服务器移除指定物品。
    /// </summary>
    public void RequestRemoveItem(string itemId, long amount = 1)
    {
        _networkInventory?.RemoveItemServerRpc(itemId, amount);
    }

    /// <summary>
    /// 请求服务器执行道具合成。
    /// </summary>
    public void RequestSynthesis(string itemId1, string itemId2)
    {
        _networkInventory?.SynthesizeItemServerRpc(itemId1, itemId2);
    }

    /// <summary>
    /// 请求服务器扩展指定容器容量。
    /// </summary>
    public void RequestExpand(EnumItemType targetType, int addCount = 1)
    {
        _networkInventory?.ExpandSlotCapacityServerRpc(targetType, addCount);
    }

    /// <summary>
    /// 背包整体变化时刷新全部缓存。
    /// </summary>
    private void HandleInventoryChanged()
    {
        SyncAll();
    }

    /// <summary>
    /// 指定容器变化时同步匹配的缓存数据。
    /// </summary>
    private void HandleContainerChanged(EnumItemType type)
    {
        SyncContainer(type);
    }

    /// <summary>
    /// 同步所有容器以及容量信息。
    /// </summary>
    private void SyncAll()
    {
        SyncContainer(EnumItemType.Weapon);
        SyncContainer(EnumItemType.Consumable);
        SyncContainer(EnumItemType.QualityPassive);
        SyncContainer(EnumItemType.Active);
        _inventoryData.ConsumableSlotCapacity = _networkInventory.ConsumableSlotCapacity?.Value ?? 3;
        _inventoryData.QualityItemSlotCapacity = _networkInventory.QualityItemSlotCapacity?.Value ?? 6;
    }

    /// <summary>
    /// 将指定容器同步到本地缓存。
    /// </summary>
    private void SyncContainer(EnumItemType type)
    {
        if (_networkInventory == null)
        {
            return;
        }

        switch (type)
        {
            case EnumItemType.Weapon:
                _inventoryData.WeaponSlots = CopyNetworkList(_networkInventory.WeaponSlots);
                break;
            case EnumItemType.Consumable:
                _inventoryData.ConsumableSlots = CopyNetworkList(_networkInventory.ConsumableSlots);
                break;
            case EnumItemType.QualityPassive:
                _inventoryData.QualityItemSlots = CopyNetworkList(_networkInventory.QualityItemSlots);
                break;
            case EnumItemType.Active:
                _inventoryData.ActiveItemSlot = _networkInventory.ActiveItemSlot?.Value ?? InventorySlot.Empty;
                break;
        }
    }

    /// <summary>
    /// 将网络列表拷贝为普通列表，便于本地操作。
    /// </summary>
    private static List<InventorySlot> CopyNetworkList(NetworkList<InventorySlot> source)
    {
        if (source == null)
        {
            return new List<InventorySlot>();
        }

        var result = new List<InventorySlot>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            result.Add(source[i]);
        }
        return result;
    }
}
