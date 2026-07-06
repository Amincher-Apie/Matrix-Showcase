using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 单个单位身上的 Buff 管理器：
/// - 不继承 MonoBehaviour
/// - 只在服务器逻辑层被调用
/// - 通过事件把状态变化抛给外层（用于网络同步 / UI）
/// </summary>
public class BuffHandler
{
    private readonly struct BuffRuntimeKey : IEquatable<BuffRuntimeKey>
    {
        public readonly int BuffId;
        public readonly ulong ApplierId;

        public BuffRuntimeKey(int buffId, ulong applierId)
        {
            BuffId = buffId;
            ApplierId = applierId;
        }

        public bool Equals(BuffRuntimeKey other)
        {
            return BuffId == other.BuffId && ApplierId == other.ApplierId;
        }

        public override bool Equals(object obj)
        {
            return obj is BuffRuntimeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (BuffId * 397) ^ ApplierId.GetHashCode();
            }
        }
    }

    public readonly IBuffOwnerContext OwnerContext;

    public SortedSet<BuffInfo> BuffInfoSets = new(new PriorityComparer());
    private readonly Dictionary<int, BuffInfo> _idToBuff = new();
    private readonly Dictionary<BuffRuntimeKey, BuffInfo> _keyedBuffs = new();

    /// <summary>添加一层 buff 时触发（用于 UI / 网络同步）。</summary>
    public event Action<BuffInfo> OnLayerAdd;

    /// <summary>某个 buff 层数跌为 0 时触发（用于 UI / 网络同步）。</summary>
    public event Action<BuffInfo> OnLayerFallToZero;

    public BuffHandler(IBuffOwnerContext ownerContext)
    {
        OwnerContext = ownerContext ?? throw new ArgumentNullException(nameof(ownerContext));
    }

    /// <summary>
    /// 只在服务器逻辑循环中调用。
    /// </summary>
    public void OnLogicUpdate(float deltaTime)
    {
        BuffTickAndRemove(deltaTime);
    }

    #region Tick & Duration

    private void BuffTickAndRemove(float deltaTime)
    {
        foreach (var buffInfo in BuffInfoSets.ToList())
        {
            if (buffInfo.buffData == null) continue;

            // Tick 逻辑（如中毒 DOT）
            if (buffInfo.buffData.defaultTickInterval > 0)
            {
                if (buffInfo.tickTimer <= 0f)
                {
                    buffInfo.buffData.OnTick?.Apply(buffInfo);
                    buffInfo.tickTimer = buffInfo.buffData.defaultTickInterval;
                }
                else
                {
                    buffInfo.tickTimer -= deltaTime;
                }

                buffInfo.lastTickRealTime = Time.time;
            }

            if (buffInfo.buffData.isForever) continue;

            if (buffInfo.buffData.buffUpdateTime == BuffUpdateTimeEnum.single &&
                buffInfo.durationTimeList.Count > 0)
            {
                TickSingleLayerDurations(buffInfo, deltaTime);
                continue;
            }

            buffInfo.durationTime -= deltaTime;
            if (buffInfo.durationTime <= 0f)
            {
                RemoveBuff(buffInfo);
            }
        }
    }

    private void TickSingleLayerDurations(BuffInfo buffInfo, float deltaTime)
    {
        int expiredCount = 0;
        for (int i = buffInfo.durationTimeList.Count - 1; i >= 0; i--)
        {
            float remain = buffInfo.durationTimeList[i] - deltaTime;
            if (remain <= 0f)
            {
                buffInfo.durationTimeList.RemoveAt(i);
                expiredCount++;
            }
            else
            {
                buffInfo.durationTimeList[i] = remain;
            }
        }

        buffInfo.durationTime = buffInfo.durationTimeList.Count > 0
            ? buffInfo.durationTimeList.Max()
            : 0f;

        if (expiredCount > 0)
        {
            RemoveStackCount(buffInfo, expiredCount);
        }
    }

    #endregion

    #region 增加 / 移除 Buff（含叠层规则）

    public BuffInfo FindBuff(int id)
    {
        _idToBuff.TryGetValue(id, out var buffInfo);
        return buffInfo;
    }

    public BuffInfo FindBuff(int buffId, ulong applierId)
    {
        if (_keyedBuffs.TryGetValue(new BuffRuntimeKey(buffId, applierId), out var keyedBuff))
        {
            return keyedBuff;
        }

        return FindBuff(buffId);
    }

    /// <summary>
    /// 添加（或叠加）一个 Buff。
    /// </summary>
    public void AddBuff(BuffInfo buffInfo)
    {
        if (buffInfo == null || buffInfo.buffData == null)
        {
            Debug.LogWarning("[BuffHandler] Try AddBuff with null BuffData.");
            return;
        }

        int id = buffInfo.buffData.buffID;
        var key = BuildKey(buffInfo);
        _keyedBuffs.TryGetValue(key, out var existing);

        int maxStack = buffInfo.buffData.ResolveMaxStackForOwner(OwnerContext.OwnerCategory);

        if (existing != null)
        {
            AddStackToExisting(existing, buffInfo, maxStack);
            return;
        }

        if (!CanAddNewApplier(buffInfo))
        {
            return;
        }

        buffInfo.reverse = false;
        if (buffInfo.currentStack <= 0)
        {
            buffInfo.currentStack = 1;
        }

        BuffInfoSets.Add(buffInfo);
        _keyedBuffs[key] = buffInfo;
        if (!_idToBuff.ContainsKey(id))
        {
            _idToBuff[id] = buffInfo;
        }

        buffInfo.buffData.OnCreat?.Apply(buffInfo);
        OnLayerAdd?.Invoke(buffInfo);
    }

    private void AddStackToExisting(BuffInfo existing, BuffInfo incoming, int maxStack)
    {
        existing.reverse = false;

        bool noLimit = maxStack == 0;
        if (!noLimit && existing.currentStack >= maxStack)
        {
            ApplyUpdateTime(existing, incoming);
            existing.buffData.OnUpdate?.Apply(existing);
            return;
        }

        existing.currentStack += 1;
        ApplyUpdateTime(existing, incoming);

        existing.buffData.OnCreat?.Apply(existing);
        existing.buffData.OnUpdate?.Apply(existing);
        OnLayerAdd?.Invoke(existing);
    }

    private void ApplyUpdateTime(BuffInfo existing, BuffInfo incoming)
    {
        if (existing.buffData == null || existing.buffData.isForever) return;

        float incomingDuration = incoming.durationTime > 0f
            ? incoming.durationTime
            : existing.buffData.defaultDuration;

        switch (existing.buffData.buffUpdateTime)
        {
            case BuffUpdateTimeEnum.add:
                existing.durationTime += incomingDuration;
                break;
            case BuffUpdateTimeEnum.keep:
                break;
            case BuffUpdateTimeEnum.single:
                existing.durationTimeList.Add(incomingDuration);
                existing.durationTime = existing.durationTimeList.Max();
                break;
            case BuffUpdateTimeEnum.replace:
            default:
                existing.durationTime = incomingDuration;
                existing.tickTimer = existing.buffData.defaultTickInterval;
                break;
        }
    }

    private bool CanAddNewApplier(BuffInfo buffInfo)
    {
        if (buffInfo.buffData.stackKeyMode != BuffStackKeyMode.BuffIdAndApplier)
        {
            return true;
        }

        int maxAppliers = buffInfo.buffData.maxAppliersPerTarget;
        if (maxAppliers <= 0)
        {
            return true;
        }

        int currentAppliers = _keyedBuffs.Keys.Count(k => k.BuffId == buffInfo.buffData.buffID);
        if (currentAppliers < maxAppliers)
        {
            return true;
        }

        Debug.LogWarning(
            $"[BuffHandler] Buff {buffInfo.buffData.buffID} on {OwnerContext.NetworkObjectId} reached max appliers {maxAppliers}.");
        return false;
    }

    public void RemoveBuff(BuffInfo buffInfo)
    {
        if (buffInfo == null || buffInfo.buffData == null) return;

        int removeCount = buffInfo.buffData.buffRemoveStackUpdate switch
        {
            BuffRemoveStackUpdateEnum.reduce => 1,
            BuffRemoveStackUpdateEnum.single => 1,
            BuffRemoveStackUpdateEnum.half => Mathf.CeilToInt(buffInfo.currentStack * 0.5f),
            BuffRemoveStackUpdateEnum.none => buffInfo.currentStack,
            BuffRemoveStackUpdateEnum.clear => buffInfo.currentStack,
            _ => buffInfo.currentStack
        };

        RemoveStackCount(buffInfo, removeCount);
    }

    private void ForceRemoveBuff(BuffInfo buffInfo)
    {
        if (buffInfo == null || buffInfo.buffData == null) return;
        RemoveStackCount(buffInfo, buffInfo.currentStack);
    }

    private void RemoveStackCount(BuffInfo buffInfo, int removeCount)
    {
        if (buffInfo == null || buffInfo.buffData == null) return;

        removeCount = Mathf.Clamp(removeCount, 0, buffInfo.currentStack);
        if (removeCount <= 0) return;

        buffInfo.reverse = true;

        for (int i = 0; i < removeCount; i++)
        {
            buffInfo.buffData.OnRemove?.Apply(buffInfo);
            buffInfo.currentStack--;

            if (buffInfo.durationTimeList.Count > buffInfo.currentStack)
            {
                buffInfo.durationTimeList.RemoveAt(buffInfo.durationTimeList.Count - 1);
            }
        }

        if (buffInfo.currentStack <= 0)
        {
            RemoveBuffFromCollections(buffInfo);
            buffInfo.reverse = false;
            return;
        }

        buffInfo.reverse = false;
        OnLayerAdd?.Invoke(buffInfo);
    }

    private void RemoveBuffFromCollections(BuffInfo buffInfo)
    {
        BuffInfoSets.Remove(buffInfo);
        _keyedBuffs.Remove(BuildKey(buffInfo));

        if (_idToBuff.TryGetValue(buffInfo.buffData.buffID, out var existing) &&
            ReferenceEquals(existing, buffInfo))
        {
            _idToBuff.Remove(buffInfo.buffData.buffID);
            var replacement = BuffInfoSets.FirstOrDefault(b => b.buffData != null && b.buffData.buffID == buffInfo.buffData.buffID);
            if (replacement != null)
            {
                _idToBuff[buffInfo.buffData.buffID] = replacement;
            }
        }

        OnLayerFallToZero?.Invoke(buffInfo);
    }

    public void ClearAll()
    {
        var tmp = BuffInfoSets.ToList();
        foreach (var buff in tmp)
        {
            ForceRemoveBuff(buff);
        }
    }

    public int GetLayers(int buffID)
    {
        return BuffInfoSets
            .Where(buff => buff.buffData != null && buff.buffData.buffID == buffID)
            .Sum(buff => buff.currentStack);
    }

    public int GetLayers(int buffID, ulong applierId)
    {
        var buff = FindBuff(buffID, applierId);
        return buff?.currentStack ?? 0;
    }

    private static BuffRuntimeKey BuildKey(BuffInfo buffInfo)
    {
        return new BuffRuntimeKey(buffInfo.buffData.buffID, buffInfo.StackKeyApplierId);
    }

    #endregion

    #region 伤害相关回调（保留原有入口）

    public void ApplyOnUseNormalAtk(DamageInfo damageInfo)
    {
        foreach (var buffInfo in BuffInfoSets.ToList())
        {
            buffInfo.buffData?.OnUseNormalAtk?.Apply(buffInfo, damageInfo);
        }
    }

    public void ApplyOnUseSkill()
    {
        foreach (var buffInfo in BuffInfoSets.ToList())
        {
            buffInfo.buffData?.OnUseSkill?.Apply(buffInfo);
        }
    }

    public void ApplyAfterUseSkill()
    {
        foreach (var buffInfo in BuffInfoSets.ToList())
        {
            buffInfo.buffData?.AfterUseSkill?.Apply(buffInfo);
        }
    }

    public void ApplyOnHit(DamageInfo damageInfo)
    {
        foreach (var buffInfo in BuffInfoSets.ToList())
        {
            buffInfo.buffData?.OnHit?.Apply(buffInfo, damageInfo);
        }
    }

    public void ApplyOnBeHurt(DamageInfo damageInfo)
    {
        foreach (var buffInfo in BuffInfoSets.ToList())
        {
            buffInfo.buffData?.OnBehurt?.Apply(buffInfo, damageInfo);
        }
    }

    public void ApplyUponBeHurt(DamageInfo damageInfo)
    {
        foreach (var buffInfo in BuffInfoSets.ToList())
        {
            buffInfo.buffData?.UponBeHurt?.Apply(buffInfo, damageInfo);
        }
    }

    public void ApplyOnDeath(DamageInfo damageInfo)
    {
        foreach (var buffInfo in BuffInfoSets.ToList())
        {
            buffInfo.buffData?.OnDeath?.Apply(buffInfo, damageInfo);
        }
    }

    public void ApplyOnKill(DamageInfo damageInfo)
    {
        foreach (var buffInfo in BuffInfoSets.ToList())
        {
            buffInfo.buffData?.OnKill?.Apply(buffInfo, damageInfo);
        }
    }

    public void ApplyOnCauseDamage(DamageInfo damageInfo)
    {
        foreach (var buffInfo in BuffInfoSets.ToList())
        {
            buffInfo.buffData?.OnCauseDamage?.Apply(buffInfo, damageInfo);
        }
    }

    #endregion

    #region SortedSet 比较器

    private class PriorityComparer : IComparer<BuffInfo>
    {
        public int Compare(BuffInfo x, BuffInfo y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            int priorityCompare = x.buffData.priority.CompareTo(y.buffData.priority);
            if (priorityCompare != 0) return priorityCompare;

            int idCompare = x.buffData.buffID.CompareTo(y.buffData.buffID);
            if (idCompare != 0) return idCompare;

            int applierCompare = x.StackKeyApplierId.CompareTo(y.StackKeyApplierId);
            if (applierCompare != 0) return applierCompare;

            int timeCompare = x.createdRealTime.CompareTo(y.createdRealTime);
            if (timeCompare != 0) return timeCompare;

            return x.RuntimeSourceId.CompareTo(y.RuntimeSourceId);
        }
    }

    #endregion
}
