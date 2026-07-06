using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单个 Buff 的运行时实例，关联到一个具体 Owner 上。
/// </summary>
public class BuffInfo
{
    public BuffData buffData;
    public IBuffOwnerContext Owner;

    // 施加者快照。Owner 表示 Buff 承载者，这里表示 Buff 来源。
    public ulong applierObjectId;
    public ulong applierClientId;
    public DamageInfo sourceDamageInfo;
    public ElementType elementType;
    public float elementDamageSnapshot;

    // 层数
    public int currentStack = 1;

    // 持续时间 & 计时
    public float durationTime;           // 剩余持续时间（秒）
    public float tickTimer;             // 距离下次 Tick 的剩余时间
    public float lastTickRealTime;      // 上一次 Tick 的 Time.time
    public float createdRealTime;       // 首次添加的 Time.time

    // For single 模式
    public List<float> durationTimeList = new List<float>();

    // Buff 是否处于反向移除流程中（你原来的 reverse 字段）
    public bool reverse = false;

    public ulong StackKeyApplierId =>
        buffData != null && buffData.stackKeyMode == BuffStackKeyMode.BuffIdAndApplier
            ? applierObjectId
            : 0UL;

    public ulong RuntimeSourceId
    {
        get
        {
            ulong source = (uint)(buffData != null ? buffData.buffID : 0);
            source = (source << 32) ^ StackKeyApplierId;
            return source == 0 ? 1UL : source;
        }
    }

    public BuffInfo(
        BuffData data,
        IBuffOwnerContext owner,
        float durationOverride = -1f,
        ulong applierObjectId = 0,
        ulong applierClientId = 0,
        DamageInfo sourceDamageInfo = default,
        ElementType elementType = ElementType.Fire,
        float elementDamageSnapshot = 0f)
    {
        buffData = data;
        Owner = owner;
        this.applierObjectId = applierObjectId;
        this.applierClientId = applierClientId;
        this.sourceDamageInfo = sourceDamageInfo;
        this.elementType = elementType;
        this.elementDamageSnapshot = Mathf.Max(0f, elementDamageSnapshot);

        durationTime = durationOverride > 0 ? durationOverride : data.defaultDuration;
        tickTimer = data.defaultTickInterval;
        createdRealTime = Time.time;
        lastTickRealTime = Time.time;

        if (data != null && data.buffUpdateTime == BuffUpdateTimeEnum.single && !data.isForever)
        {
            durationTimeList.Add(durationTime);
        }
    }
}
