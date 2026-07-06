using System;

/// <summary>
/// 事件名称枚举类
/// 用于统一管理事件中心的所有事件标识
/// 按模块分类，便于维护
/// </summary>
public enum EventName
{
    WeaponEquipped,
    LocalWeaponFired,   // 本地玩家预测用
    RemoteWeaponFired,  // 服务器同步用
    ProjectileSpawned,
    HitResolved,
    ReloadStarted,
    ReloadFinished,
    UnitDamaged,
    UnitDied,
    AttributeChanged,
    ItemPickedUp,
    ItemUsed,
    ItemRemoved,
    WeaponAttributeModified,
    PlayerEnergyExhaust,

    // ── 技能系统事件 ──
    SkillCastConfirmed,  // 服务端确认技能释放成功（Phase 1 最小实现，Phase 3 完整表现广播）

    // ── 生成 / 销毁事件（MinimapLogic 订阅） ──
    PlayerSpawned,
    PlayerDespawned,
    EnemySpawned,
    EnemyDespawned,

    // ── 商店事件 ──
    ShopPurchaseSucceeded,  // int slotIndex, int newCurrency
    ShopPurchaseFailed,     // string reason

    // ── Run 生命周期事件 ──
    RunStateChanged,
    RunSeedFinalized,
    RoomEntered,
    RoomCombatStarted,
    RoomCleared,
    PathChoiceOffered,
    PathChosen,
    BossFightStarted,
    BossDefeated,
    RunVictory,
    RunDefeat,
    RunSummaryReady,
    AllPlayersDead,

    // ── Mission 生命周期事件 ──
    MissionStateChanged,
    MissionProgressChanged,
    MissionCompleted,
    MissionFailed,
}

public struct WeaponEquippedEvt { public ulong actorId; public string weaponId; }
public struct WeaponFiredEvt // 渲染层订阅 WeaponFired → 播枪口火光/弹壳；
{
    public ulong actorId;
    public string weaponId;
    public ulong shotId;
    public UnityEngine.Vector3 origin;
    public UnityEngine.Vector3 dir;
    public ulong instigatorClientId;  // 哪个客户端开的枪
    public bool isLocalPlayer;        // 是否“我自己”
}
// 约定：
// LocalWeaponFired：由本地输入模块触发，isLocalPlayer = true。
// RemoteWeaponFired：由服务器通过 ClientRpc 同步，isLocalPlayer 按需要设置（一般是 false，或 owner 客户端用来做回滚/对齐）。
public struct ProjectileSpawnedEvt // 订阅 ProjectileSpawned → 生成弹道Trail/实体弹体；
{
    public ulong actorId; public string weaponId; public ulong shotId;
    public UnityEngine.Vector3 origin; public UnityEngine.Vector3 dir;
    public float speed; public float range; public int pelletCount;
}
public struct HitResolvedEvt // 订阅 HitResolved → 播UI层击中特效；
{
    public ulong actorId; public string weaponId; public ulong targetId;
}
public struct ReloadEvt { public ulong actorId; public string weaponId; public float duration; }// 订阅 Reload* → 播装填动画/音效； // UI 订阅 WeaponFired/Reload* → 刷新弹药。

public struct UnitDamagedEvt // 受击事件，应该可以用来播放受击动画，以及UI层渲染跳字
{
    public ulong targetId;
    public ulong instigatorId;
    public DamageResult damageResult;
}

public struct UnitDiedEvt // 死亡事件
{
    public ulong unitId;
}

public struct AttributeChangedEvt
{
    public ulong unitId;
    public AttributeType attributeType;
    public float oldValue;
    public float newValue;

}

public struct ItemPickedUpEvt
{
    public string itemId;
    public ulong ownerId;
    public EnumItemType itemType;
}

public struct ItemUsedEvt
{
    public string itemId;
    public ulong ownerId;
    public EnumItemType itemType;
}

public struct ItemRemovedEvt
{
    public string itemId;
    public ulong ownerId;
    public EnumItemType itemType;
}

public struct WeaponAttributeModifiedEvt
{
    public ulong ownerId;
    public float value;
    public object source;
}

public struct PlayerEnergyExhaustEvt
{
    public ulong unitId;
}

/// <summary>技能释放确认事件（服务端广播，Phase 3 扩展表现层）。</summary>
public struct SkillCastConfirmedEvt
{
    public ulong unitId;
    public int slotIndex;
    public string skillId;
}

// ── Run 生命周期事件结构体 ──

/// <summary>Run 状态变更事件。OldState/NewState 对应 RunState 枚举 int 值。</summary>
public struct RunStateChangedEvt
{
    public int OldState;
    public int NewState;
    public int RoomNodeId;
}

public struct RoomEnteredEvt
{
    public int RoomNodeId;
    public int RoomRole; // RoomRole 枚举 int 值
    public ulong EnteredByClientId;
}

public struct RoomCombatStartedEvt
{
    public int RoomNodeId;
    public int RoomRole;
}

public struct RoomClearedEvt
{
    public int RoomNodeId;
    public int RoomRole;
    public float CombatDurationSeconds;
}

public struct PathChoiceOfferedEvt
{
    public int FromRoomNodeId;
    public int[] AvailableRoomNodeIds;
}

public struct PathChosenEvt
{
    public int FromRoomNodeId;
    public int ToRoomNodeId;
    public int ToRoomRole;
}

public struct BossFightStartedEvt
{
    public int RoomNodeId;
    public string BossId;
}

public struct BossDefeatedEvt
{
    public int RoomNodeId;
    public string BossId;
    public float FightDurationSeconds;
}

public struct RunResultEvt
{
    public bool IsVictory;
    public int Seed;
    public int Difficulty;
    public double TotalDurationSeconds;
    public int TotalKills;
    public int RoomsCleared;
}

public struct AllPlayersDeadEvt { }

public struct MissionStateChangedEvt
{
    public int SlotIndex;
    public string MissionId;
    public int MissionType;
    public int OldState;
    public int NewState;
    public int RoomNodeId;
}

public struct MissionProgressChangedEvt
{
    public int SlotIndex;
    public string MissionId;
    public int MissionType;
    public int State;
    public int CurrentProgress;
    public int TargetProgress;
    public int RoomNodeId;
}

public struct MissionCompletedEvt
{
    public int SlotIndex;
    public string MissionId;
    public int MissionType;
    public int RoomNodeId;
}

public struct MissionFailedEvt
{
    public int SlotIndex;
    public string MissionId;
    public int MissionType;
    public int RoomNodeId;
}

// ── 生成 / 销毁事件结构体 ──

public struct PlayerSpawnedEvt
{
    public ulong NetworkObjectId;
    public ulong ClientId;
}

public struct PlayerDespawnedEvt
{
    public ulong NetworkObjectId;
}

public struct EnemySpawnedEvt
{
    public ulong NetworkObjectId;
}

public struct EnemyDespawnedEvt
{
    public ulong NetworkObjectId;
}
// 逻辑层不感知任何具体表现对象
