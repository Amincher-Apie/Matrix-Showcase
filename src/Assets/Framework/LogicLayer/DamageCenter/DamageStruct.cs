// LogicLayer/DamageCenter/DamageStruct.cs

using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 物理子弹类型
/// </summary>
public enum PhysicalBulletType
{
    Solid,      // 固体
    Liquid,     // 液体  
    Gas         // 气体
}

/// <summary>
/// 伤害信息（扩展版）
/// </summary>
[Serializable]
public struct DamageInfo
{
    public float amount;                 // 总伤害量
    public PhysicalBulletType physicalBulletType; // 物理伤害类型
    public ulong instigator;
    public ulong sourceActorId;          // 造成伤害的对象ID
    public ulong targetActorId;          // 受击对象ID
    public Vector3 hitWorldPos;          // 命中的世界坐标
    public bool hasHitWorldPos;          // 是否有命中的世界坐标
    
    public bool isCritical;              // 是否暴击
    public bool isSkill;
    
    public float iceDamage;              // 冰元素的伤害
    public float fireDamage;             // 火元素的伤害
    public float poisonDamage;           // 毒元素的伤害
    public float electricDamage;         // 电元素的伤害
    
    public int iceTriggerLayer;          // 冰元素的触发层数
    public int fireTriggerLayer;         // 火元素的触发层数
    public int poisonTriggerLayer;       // 毒元素的触发层数
    public int electricTriggerLayer;     // 电元素的触发层数
    
    public DamageInfo(PhysicalBulletType bulletType, ulong sourceId, ulong targetId)
    {
        physicalBulletType = bulletType;
        sourceActorId = sourceId;
        targetActorId = targetId;
        hasHitWorldPos = false;
        hitWorldPos = Vector3.zero;
        amount = 0f;
        isCritical = false;
        isSkill = false;
        iceDamage = 0f;
        fireDamage = 0f;
        poisonDamage = 0f;
        electricDamage = 0f;
        iceTriggerLayer = 0;
        fireTriggerLayer = 0;
        poisonTriggerLayer = 0;
        electricTriggerLayer = 0;
        instigator = 0;
    }

    // public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    // {
    //     serializer.SerializeValue(ref amount);
    //     serializer.SerializeValue(ref physicalBulletType);
    //     serializer.SerializeValue(ref sourceActorId);
    //     serializer.SerializeValue(ref targetActorId);
    //     serializer.SerializeValue(ref isCritical);
    //     serializer.SerializeValue(ref isSkill);
    //     serializer.SerializeValue(ref iceDamage);
    //     serializer.SerializeValue(ref fireDamage);
    //     serializer.SerializeValue(ref poisonDamage);
    //     serializer.SerializeValue(ref electricDamage);
    //     serializer.SerializeValue(ref iceTriggerLayer);
    //     serializer.SerializeValue(ref fireTriggerLayer);
    //     serializer.SerializeValue(ref poisonTriggerLayer);
    //     serializer.SerializeValue(ref electricTriggerLayer);
    // }
}

/// <summary>
/// 伤害结果（扩展版）
/// </summary>
[Serializable]
public struct DamageResult : INetworkSerializable
{
    public float totalDamage;            // 总伤害（= shieldDamage + healthDamage）
    public float shieldDamage;           // 护盾伤害（实际扣除的护盾值）
    public float healthDamage;           // 生命伤害（实际扣除的生命值）
    public bool isCritical;              // 是否暴击
    public bool isSkill;

    public float iceDamage;              // 冰元素的伤害
    public float fireDamage;             // 火元素的伤害
    public float poisonDamage;           // 毒元素的伤害
    public float electricDamage;         // 电元素的伤害

    public int iceTriggerLayer;          // 冰元素的触发层数
    public int fireTriggerLayer;         // 火元素的触发层数
    public int poisonTriggerLayer;       // 毒元素的触发层数
    public int electricTriggerLayer;     // 电元素的触发层数

    public bool targetDied;              // 目标是否死亡
    
    public DamageResult(DamageInfo damageInfo)
    {
        totalDamage = damageInfo.amount;
        shieldDamage = 0f;
        healthDamage = 0f;
        isCritical = damageInfo.isCritical;
        isSkill = damageInfo.isSkill;
        
        iceDamage = damageInfo.iceDamage;
        fireDamage = damageInfo.fireDamage;
        poisonDamage = damageInfo.poisonDamage;
        electricDamage = damageInfo.electricDamage;
        
        iceTriggerLayer = damageInfo.iceTriggerLayer;
        fireTriggerLayer = damageInfo.fireTriggerLayer;
        poisonTriggerLayer = damageInfo.poisonTriggerLayer;
        electricTriggerLayer = damageInfo.electricTriggerLayer;
        
        targetDied = false;
    }
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref totalDamage);
        serializer.SerializeValue(ref shieldDamage);
        serializer.SerializeValue(ref healthDamage);
        serializer.SerializeValue(ref isCritical);
        serializer.SerializeValue(ref isSkill);
        
        serializer.SerializeValue(ref iceDamage);
        serializer.SerializeValue(ref fireDamage);
        serializer.SerializeValue(ref poisonDamage);
        serializer.SerializeValue(ref electricDamage);
        
        serializer.SerializeValue(ref iceTriggerLayer);
        serializer.SerializeValue(ref fireTriggerLayer);
        serializer.SerializeValue(ref poisonTriggerLayer);
        serializer.SerializeValue(ref electricTriggerLayer);
        
        serializer.SerializeValue(ref targetDied);
    }
}