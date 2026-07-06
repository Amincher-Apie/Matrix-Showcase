using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 局内背包数据类
/// </summary>
[Serializable]
public class InGameInventoryData
{
    /// <summary>
    /// 武器槽（默认2个：主武器 + 副武器）
    /// </summary>
    public List<InventorySlot> WeaponSlots = new();

    /// <summary>
    /// 消耗道具槽（可堆叠，容量动态扩展）
    /// </summary>
    public List<InventorySlot> ConsumableSlots = new();

    /// <summary>
    /// 品质道具槽（不可堆叠，容量动态扩展）
    /// </summary>
    public List<InventorySlot> QualityItemSlots = new();

    /// <summary>
    /// 主动道具槽（唯一）
    /// </summary>
    public InventorySlot ActiveItemSlot = InventorySlot.Empty;

    /// <summary>
    /// 当前消耗品槽位容量上限。
    /// </summary>
    public int ConsumableSlotCapacity = 3;

    /// <summary>
    /// 当前品质道具槽位容量上限。
    /// </summary>
    public int QualityItemSlotCapacity = 6;
}
