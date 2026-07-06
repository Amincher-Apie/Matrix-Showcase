using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 伤害视觉表现事件（专供 Client 使用）
/// Server -> Client，仅用于 VFX / 跳字 / 特效
/// </summary>
[Serializable]
public struct DamageVfxEvent : INetworkSerializable
{
    public ulong targetId;          // 受击对象 NetworkObjectId
    public ulong sourceId;          // 攻击者（可选）
    public Vector3 hitWorldPos;     // ★ 权威命中点（世界坐标）
    public DamageResult damageResult;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref targetId);
        serializer.SerializeValue(ref sourceId);
        serializer.SerializeValue(ref hitWorldPos);
        serializer.SerializeValue(ref damageResult);
    }
}