using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


/// <summary>
/// 命中结果
/// </summary>
public struct HitResult : INetworkSerializable
{
    public ulong targetId;
    public Vector3 point;
    public Vector3 normal;
    public float distance;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref targetId);
        serializer.SerializeValue(ref point);
        serializer.SerializeValue(ref normal);
        serializer.SerializeValue(ref distance);
    }
}

/// <summary>
/// 投射物信息
/// </summary>
public struct ProjectileInfo : INetworkSerializable
{
    public Vector3 origin;
    public Vector3 direction;
    public float speed;
    public float range;
    public string weaponId;
    public ulong instigatorId;
    public ulong shotId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref origin);
        serializer.SerializeValue(ref direction);
        serializer.SerializeValue(ref speed);
        serializer.SerializeValue(ref range);
        serializer.SerializeValue(ref weaponId);
        serializer.SerializeValue(ref instigatorId);
        serializer.SerializeValue(ref shotId);
    }
}

// 文件位置: LogicLayer/CombatModule/FireDataStruct.cs (新增结构)
public struct FireValidationResult
{
    public bool isValid;
    public List<ValidatedHit> validatedHits;
    public ProjectileInfo? projectileInfo;
}

public struct ValidatedHit
{
    public ulong targetId;
    public Vector3 point;
    public Vector3 normal;
    public float distance;
}

// 修改现有的 FireContext，增加开火方法类型
public struct FireContext : INetworkSerializable
{
    public ulong shotId;
    public Vector3 origin;
    public Vector3 dir;
    public ulong instigator;        // 发起者客户端ID
    public ulong shooterObjectId;   // 新增：开火者网络对象ID
    public BulletKind bulletKind; // 新增：开火方式类型
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref shotId);
        serializer.SerializeValue(ref origin);
        serializer.SerializeValue(ref dir);
        serializer.SerializeValue(ref instigator);
        serializer.SerializeValue(ref shooterObjectId);
        serializer.SerializeValue(ref bulletKind);
    }
}

public struct ClientFireRequest : INetworkSerializable
{
    public FireContext context;
    public List<HitResult> predictedHits;
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        // 序列化 FireContext
        context.NetworkSerialize(serializer);
        
        // 手动序列化 List<HitResult>
        int hitCount = predictedHits?.Count ?? 0;
        serializer.SerializeValue(ref hitCount);
        
        if (serializer.IsReader)
        {
            predictedHits = new List<HitResult>();
            for (int i = 0; i < hitCount; i++)
            {
                HitResult hit = default;
                hit.NetworkSerialize(serializer);
                predictedHits.Add(hit);
            }
        }
        else
        {
            foreach (var hit in predictedHits)
            {
                var tempHit = hit;
                tempHit.NetworkSerialize(serializer);
            }
        }
    }
}
