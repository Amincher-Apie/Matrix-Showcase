using System;
using Unity.Collections;
using Unity.Netcode;

/// <summary>
/// 描述同步到网络层的道具实例数据，包含品质、类型与副作用信息。
/// </summary>
[Serializable]
public struct InventoryItem : INetworkSerializable, IEquatable<InventoryItem>
{
    /// <summary>
    /// 物品唯一标识（与配置 ID 对应）。
    /// </summary>
    public FixedString128Bytes itemId;

    /// <summary>
    /// 物品所在的槽位类型。
    /// </summary>
    public EnumItemType itemType;

    /// <summary>
    /// 物品品质层级。
    /// </summary>
    public EnumQualityLevel qualityLevel;

    /// <summary>
    /// 是否允许在同一槽位堆叠多个副本。
    /// </summary>
    public bool isStackable;

    /// <summary>
    /// 是否为主动道具（主动槽专用）。
    /// </summary>
    public bool isActive;

    /// <summary>
    /// 是否为有效的物品实例（具有合法 ID）。
    /// </summary>
    public bool IsValid => itemId.Length > 0;

    /// <summary>
    /// 从配置 ScriptableObject 生成网络物品实例。
    /// </summary>
    /// <param name="itemSO">基础道具配置。</param>
    public InventoryItem(BaseInventoryItemSO itemSO)
    {
        if (itemSO == null)
        {
            itemId = default;
            itemType = EnumItemType.Consumable;
            qualityLevel = EnumQualityLevel.Common;
            isStackable = false;
            isActive = false;
            return;
        }

        itemId = itemSO.id;
        itemType = itemSO.itemType;
        qualityLevel = itemSO.qualityLevel;
        isStackable = itemType == EnumItemType.Consumable;
        isActive = itemType == EnumItemType.Active;
    }

    /// <summary>
    /// 实现 NGO 的序列化接口以同步字段。
    /// </summary>
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref itemId);
        serializer.SerializeValue(ref itemType);
        serializer.SerializeValue(ref qualityLevel);
        serializer.SerializeValue(ref isStackable);
        serializer.SerializeValue(ref isActive);
        
    }

    /// <summary>
    /// 判断两个物品实例的所有字段是否一致。
    /// </summary>
    public bool Equals(InventoryItem other)
    {
        return itemId.Equals(other.itemId) && itemType == other.itemType &&
               qualityLevel == other.qualityLevel && isStackable == other.isStackable &&
               isActive == other.isActive ;
    }

    /// <inheritdoc />
    public override bool Equals(object obj)
    {
        return obj is InventoryItem other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(itemId.GetHashCode());
        hashCode.Add((int)itemType);
        hashCode.Add((int)qualityLevel);
        hashCode.Add(isStackable);
        hashCode.Add(isActive);
        return hashCode.ToHashCode();
    }
}
