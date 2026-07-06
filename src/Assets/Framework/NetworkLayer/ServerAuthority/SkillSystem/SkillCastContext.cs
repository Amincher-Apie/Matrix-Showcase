using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 客户端发起一次施法时携带的上下文：位置/朝向/锁定目标/索引等
/// </summary>
public struct SkillCastContext : INetworkSerializable
{
    public int slotIndex;                     // 技能槽 index（0/1/2/...）
    public FixedString64Bytes skillId;        // 对应 SkillDefinitionSO.id
    public SkillTargetType targetType;

    public Vector3 origin;                    // 释放起点（比如枪口、手心等）
    public Vector3 direction;                 // 面朝方向
    public Vector3 point;                     // 地面点目标
    public ulong targetActorId;               // 锁定单位

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref slotIndex);
        serializer.SerializeValue(ref skillId);
        serializer.SerializeValue(ref targetType);
        serializer.SerializeValue(ref origin);
        serializer.SerializeValue(ref direction);
        serializer.SerializeValue(ref point);
        serializer.SerializeValue(ref targetActorId);
    }
}

/// <summary>
/// 和武器那边的 ClientFireRequest 类似，用来打包发送
/// </summary>
public struct ClientSkillCastRequest : INetworkSerializable
{
    public SkillCastContext context;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        context.NetworkSerialize(serializer);
    }
}