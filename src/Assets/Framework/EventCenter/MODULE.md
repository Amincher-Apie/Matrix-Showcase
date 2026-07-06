# EventCenter 事件中心

## 模块定位

全局事件系统，是项目中所有模块之间的解耦通信骨架。逻辑层、网络层、渲染层、UI 层均通过 EventCenter 进行松耦合通信，不直接持有对方引用。

## 文件结构

```
Assets/Framework/EventCenter/
├── EventCenter.cs       # 事件中心核心实现（Singleton）
├── EventName.cs         # 事件枚举 + 事件参数结构体定义
└── MODULE.md
```

## 核心类与接口

### EventCenter (Singleton)

`EventCenter.cs:7` — 继承 `SingletonBase<EventCenter>`，全局唯一实例。提供以下能力：

| 功能 | 方法 |
|------|------|
| 注册监听（0参数） | `AddListener(EventName, UnityAction, priority)` |
| 注册监听（1参数） | `AddListener<T>(EventName, UnityAction<T>, priority)` |
| 注册监听（2参数） | `AddListener<T1,T2>(EventName, UnityAction<T1,T2>, priority)` |
| 注册监听（3参数） | `AddListener<T1,T2,T3>(EventName, UnityAction<T1,T2,T3>, priority)` |
| 注册监听（4参数） | `AddListener<T1,T2,T3,T4>(EventName, UnityAction<T1,T2,T3,T4>, priority)` |
| 移除监听 | 对应的 `RemoveListener` / `RemoveListener<T>` ... |
| 触发事件 | 对应的 `Trigger` / `Trigger<T>` ... |
| 清空所有事件 | `ClearAllEvents()` |

**关键设计**：
- 按 `priority` 降序排序分发（高优先级先执行）
- 触发时拷贝监听列表，避免执行中修改列表导致异常
- `lock (_eventLock)` 保证线程安全
- 防止重复注册同一委托

### EventName 枚举

`EventName.cs:8` — 定义事件标识，分四类：

**战斗相关：**
- `WeaponEquipped` / `LocalWeaponFired` / `RemoteWeaponFired` — 武器装备与开火
- `ProjectileSpawned` — 弹体生成
- `HitResolved` — 命中结算
- `ReloadStarted` / `ReloadFinished` — 换弹
- `UnitDamaged` / `UnitDied` — 受伤与死亡
- `AttributeChanged` — 属性变化
- `SkillCastConfirmed` — 服务端确认技能释放成功（Phase 1 最小实现）

**道具相关：**
- `ItemPickedUp` / `ItemUsed` / `ItemRemoved` — 物品拾取/使用/移除
- `WeaponAttributeModified` — 武器属性修改
- `PlayerEnergyExhaust` — 玩家能量耗尽

**对局流程（Run）相关：**
- `RunStateChanged` — 对局状态变更
- `RunSeedFinalized` — Seed 确定
- `RoomEntered` — 房间生命周期（`RoomCombatStarted`/`RoomCleared` 已废弃，开放地图不再触发）
- ~~`PathChoiceOffered` / `PathChosen`~~ — 已废弃（开放地图无需路线选择）
- `BossFightStarted` / `BossDefeated` — Boss 战（`BossDefeated` 由 `BossMission.Complete()` 触发）
- `RunVictory` / `RunDefeat` / `RunSummaryReady` — 对局结果
- `AllPlayersDead` — 全灭

**任务流程（Mission）相关：**
- `MissionStateChanged` — 任务状态变化
- `MissionProgressChanged` — 任务进度变化
- `MissionCompleted` — 任务完成
- `MissionFailed` — 任务失败

### 事件参数结构体

`EventName.cs:42-188` — 每个事件有对应的参数结构体（只列关键字段）：

| 结构体 | 关键字段 | 用途 |
|--------|---------|------|
| `WeaponEquippedEvt` | actorId, weaponId | 武器装备通知 |
| `WeaponFiredEvt` | actorId, weaponId, shotId, origin, dir, isLocalPlayer | 开火（Local=客户端预测，Remote=服务器下发） |
| `ProjectileSpawnedEvt` | actorId, weaponId, origin, dir, speed, range, pelletCount | 弹体生成 |
| `HitResolvedEvt` | actorId, targetId | 命中结算 |
| `ReloadEvt` | actorId, weaponId, duration | 换弹 |
| `UnitDamagedEvt` | targetId, instigatorId, damageResult | 受击（含 DamageResult） |
| `UnitDiedEvt` | unitId | 死亡 |
| `AttributeChangedEvt` | unitId, attributeType, oldValue, newValue | 属性变化 |
| `ItemPickedUpEvt` | itemId, ownerId, itemType | 物品拾取 |
| `ItemUsedEvt` | itemId, ownerId, itemType | 物品使用 |
| `ItemRemovedEvt` | itemId, ownerId, itemType | 物品移除 |
| `WeaponAttributeModifiedEvt` | ownerId, value, source | 武器属性修改 |
| `PlayerEnergyExhaustEvt` | unitId | 能量耗尽 |
| `SkillCastConfirmedEvt` | unitId, slotIndex, skillId | 技能释放服务端确认 |
| `RunStateChangedEvt` | OldState, NewState, RoomNodeId | 对局状态变更 |
| `RoomEnteredEvt` | RoomNodeId, RoomRole, EnteredByClientId | 进入房间 |
| `RoomCombatStartedEvt` | RoomNodeId, RoomRole | ~~开始战斗~~（已废弃） |
| `RoomClearedEvt` | RoomNodeId, RoomRole, CombatDurationSeconds | ~~房间清空~~（已废弃） |
| `PathChoiceOfferedEvt` | FromRoomNodeId, AvailableRoomNodeIds[] | ~~路线选择~~（已废弃） |
| `PathChosenEvt` | FromRoomNodeId, ToRoomNodeId, ToRoomRole | ~~路线确定~~（已废弃） |
| `BossFightStartedEvt` | RoomNodeId, BossId | Boss 开始 |
| `BossDefeatedEvt` | RoomNodeId, BossId, FightDurationSeconds | Boss 击破（由 `BossMission.Complete()` 触发） |
| `RunSeedFinalized` | (裸 int) | Seed 确定（裸类型触发，非结构体） |
| `RunResultEvt` | IsVictory, Seed, Difficulty, TotalDurationSeconds, TotalKills, RoomsCleared | 对局结果 |
| `AllPlayersDeadEvt` | (空结构体) | 全灭信号 |
| `MissionStateChangedEvt` | SlotIndex, MissionId, MissionType, OldState, NewState, RoomNodeId | 任务状态变化 |
| `MissionProgressChangedEvt` | SlotIndex, MissionId, MissionType, State, CurrentProgress, TargetProgress, RoomNodeId | 任务进度变化 |
| `MissionCompletedEvt` | SlotIndex, MissionId, MissionType, RoomNodeId | 任务完成 |
| `MissionFailedEvt` | SlotIndex, MissionId, MissionType, RoomNodeId | 任务失败 |

> **注意**：`AttributeChanged` 事件当前统一使用裸参数形式：
> `Trigger<AttributeType, float, float>(EventName.AttributeChanged, type, old, new)`
>
> 未使用 `AttributeChangedEvt` 结构体形式。

## 依赖关系

- **依赖**：`Framework.Singleton.SingletonBase<T>` — 单例基类
- **被依赖**：几乎所有模块 — RunManager, ServerAttributeModule, RoomCombatController, ServerBuffModule, 渲染层等

## 使用模式

```csharp
// 监听
EventCenter.Instance.AddListener<UnitDiedEvt>(EventName.UnitDied, OnUnitDied);

// 触发（服务端 → 所有客户端）
EventCenter.Instance.Trigger<RoomClearedEvt>(EventName.RoomCleared, evt);

// 移除
EventCenter.Instance.RemoveListener<UnitDiedEvt>(EventName.UnitDied, OnUnitDied);
```

## 关键约定

1. **LocalWeaponFired vs RemoteWeaponFired**：本地预测用 Local（isLocalPlayer=true），服务器同步用 Remote
2. **RunStateChangedEvt**：OldState/NewState 直接传 RunState 枚举的 int 值
3. **场景切换时**：调用 `ClearAllEvents()` 清理所有监听，避免跨场景内存泄漏
4. **UnitDied → AllPlayersDead**：RunManager 监听 UnitDied，统计存活玩家数为 0 时触发 AllPlayersDead
5. **Mission 状态事件**：MissionSystem 仍以 `NetworkList<MissionNetState>` 为权威同步通道，EventCenter 事件只用于 HUD、音效、结算和调试响应
6. **SkillCastConfirmed**：由 `ServerSkillModule` 在服务端技能释放成功后触发。Phase 1 仅用于确认与调试，Phase 3 再扩展远端表现同步。

## 注意事项

- EventName 枚举当前只向上增长，避免重排导致序列化问题
- 增加新事件结构体时，确保引用类型字段可空检查
- 大量触发的事件（如 AttributeChanged）需注意 GC 分配
