using System;
using Unity.Netcode;

/// <summary>
/// 单个背包槽位的网络同步结构，记录物品、数量与空槽标记。
/// </summary>
[Serializable]
public struct InventorySlot : INetworkSerializable, IEquatable<InventorySlot>
{
    /// <summary>
    /// 预置的空槽实例，供初始化与占位使用。
    /// </summary>
    public static readonly InventorySlot Empty = new InventorySlot
    {
        isNull = true,
        item = default,
        amount = 0
    };

    /// <summary>
    /// 当前槽位持有的物品数据。
    /// </summary>
    public InventoryItem item;

    /// <summary>
    /// 物品数量（对可堆叠物品有效）。
    /// </summary>
    public long amount;

    /// <summary>
    /// 是否为空槽（无有效物品或数量为零）。
    /// </summary>
    public bool isNull;

    /// <summary>
    /// 以物品与数量初始化槽位结构。
    /// </summary>
    /// <param name="itemData">要放入的物品实例。</param>
    /// <param name="amountValue">初始数量。</param>
    public InventorySlot(InventoryItem itemData, long amountValue)
    {
        item = itemData;
        amount = amountValue;
        isNull = !itemData.IsValid;
    }

    /// <summary>
    /// 判断是否可以与另一个物品进行堆叠。
    /// </summary>
    public bool IsStackableWith(InventoryItem other)
    {
        return !isNull && item.isStackable && item.itemId.Equals(other.itemId);
    }

    /// <summary>
    /// 增加堆叠数量。
    /// </summary>
    /// <param name="value">增量。</param>
    public void AddAmount(long value)
    {
        amount += value;
    }

    /// <summary>
    /// 槽位是否为空或数量无效。
    /// </summary>
    public bool IsEmpty()
    {
        return isNull || amount <= 0;
    }

    /// <summary>
    /// 实现 NGO 序列化以同步槽位数据。
    /// </summary>
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref item);
        serializer.SerializeValue(ref amount);
        serializer.SerializeValue(ref isNull);
    }

    /// <summary>
    /// 比较两个槽位的物品与数量是否完全一致。
    /// </summary>
    public bool Equals(InventorySlot other)
    {
        return item.Equals(other.item) && amount == other.amount && isNull == other.isNull;
    }

    /// <inheritdoc />
    public override bool Equals(object obj)
    {
        return obj is InventorySlot other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(item, amount, isNull);
    }
}
