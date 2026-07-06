# ArchiveSystem 存档系统

## 1. 模块职责

玩家档案数据的持久化管理系统。负责：

- 玩家档案的加载/保存/自动保存（本地 JSON 文件）
- 7 个维度的统计增量更新（对局/战斗/探索/任务/社交/养成/趣味）
- 线程安全的数据读写（`_dataLock` 锁 + 异步保存）
- 存档备份与数量控制（最多 5 个备份）
- 对局历史记录维护（最多 100 条）

## 2. 模块边界

| 边界 | 说明 |
|------|------|
| **上游** | `RunSummaryCalculator`（对局结算时写入） / 各战斗/探索系统（通过 Payload 注入增量数据） |
| **下游** | `IArchiveStorage` → 磁盘文件（JSON） |
| **存储** | `JsonArchiveStorage` — `Application.persistentDataPath/Archives/{playerId}_archive.json` |
| **不负责** | 存档 UI 展示（UI 模块）；云端同步（通过 `IArchiveStorage` 扩展）；网络传输（仅本地存储） |

**文件分布**：

```
Assets/Scripts/ArchiveSystem/
├── Core/
│   ├── ArchiveManager.cs           # 核心管理器 (SingletonBase)
│   └── ArchiveUpdatePayloads.cs    # 7 种增量更新 Payload
├── DataModel/
│   └── PlayerArchiveData.cs        # 根数据模型 (8 个子结构 + 对局历史)
└── Storage/
    ├── IArchiveStorage.cs          # 存储抽象接口
    └── JsonArchiveStorage.cs       # JSON 文件实现
```

## 3. 核心流程

### 3.1 生命周期

```
ArchiveManager.Instance.Setup(playerId, overrideStorage?)
    │
    ├─ LoadOrCreateArchive()
    │   ├─ _storage.Load(playerId) → 从磁盘加载
    │   └─ 失败/null → CreateDefaultArchive()
    │
    ├─ RegisterLifecycleHooks()
    │   ├─ SaveOnlyOnSessionEnd=true → 不挂 Tick 自动保存
    │   └─ SaveOnlyOnSessionEnd=false → MonoManager.Instance.OnUpdate += Tick
    │
    └─ Application.quitting += OnApplicationQuitting
        ├─ SaveOnlyOnSessionEnd && _sessionEndedThisRun → Save()
        └─ !SaveOnlyOnSessionEnd → Save()
```

### 3.2 数据写入流程

```
外部系统（RunSummaryCalculator / 战斗统计 / ...）
    │
    ├─ ArchiveManager.Instance.RegisterSession(payload)     ← 对局结算
    ├─ ArchiveManager.Instance.RecordCombatSnapshot(payload) ← 战斗统计
    ├─ ArchiveManager.Instance.RecordExplorationSnapshot(payload)
    ├─ ArchiveManager.Instance.RecordMissionResult(payload)
    ├─ ArchiveManager.Instance.RecordSocialSnapshot(payload)
    ├─ ArchiveManager.Instance.RecordGrowthSnapshot(payload)
    └─ ArchiveManager.Instance.RecordFunEvent(payload)
         │
         └─ UpdateData(updater)
              ├─ lock(_dataLock) → updater(_data) → _isDirty=true
              └─ RegisterSession 额外: _sessionEndedThisRun=true → Save()
```

### 3.3 保存策略

| 模式 | `SaveOnlyOnSessionEnd` | 行为 |
|------|----------------------|------|
| 开发/调试 | `false` | `Tick()` 每 120 秒自动保存 + 退出保存 |
| 正式环境 | `true`（默认） | 仅在 `RegisterSession()` 时保存 + 退出时如已结算则保存 |

**设计意图**：`SaveOnlyOnSessionEnd = true` 确保不会因「打开账户页未结算」而把默认 0 数据覆盖写回磁盘。

## 4. 数据模型

### PlayerArchiveData（根对象）

`PlayerArchiveData.cs:10` — `[Serializable]`，承载一名玩家的全部统计。

| 子结构 | 类型 | 说明 |
|--------|------|------|
| `Meta` | `ArchiveMeta` | 版本号、玩家 ID、创建/保存时间 |
| `BaseStats` | `BaseStatsData` | 游玩时长、总局数、通关率 |
| `CombatStats` | `CombatStatsData` | 击杀、伤害、Boss 记录 |
| `ExplorationStats` | `ExplorationStatsData` | 地图进度、距离、隐藏区域 |
| `MissionStats` | `MissionStatsData` | 任务触发/完成/失败原因 |
| `SocialStats` | `SocialStatsData` | 组队、救援、队友合作 |
| `GrowthStats` | `GrowthStatsData` | 解锁、资源收支、设施、挑战 |
| `FunStats` | `FunStatsData` | 趣味/黑历史（最高堆叠、空中击杀等） |
| `SessionHistory` | `List<SessionHistoryEntry>` | 对局历史（最多 100 条） |

### ArchiveUpdatePayloads（增量更新模型）

`ArchiveUpdatePayloads.cs` — 7 种 Payload：

| Payload | 对应写入方法 | 关键字段 |
|---------|-------------|---------|
| `SessionSummaryPayload` | `RegisterSession` | Result/Duration/Difficulty/Career |
| `CombatSnapshotPayload` | `RecordCombatSnapshot` | Kill/Damage/Healing 分类 + BossKillDetails |
| `ExplorationSnapshotPayload` | `RecordExplorationSnapshot` | Distance/MapProgress/HiddenAreas |
| `MissionResultPayload` | `RecordMissionResult` | MissionType/Success/FailureReason |
| `SocialSnapshotPayload` | `RecordSocialSnapshot` | Coop/TeamSize/Revive/Partner |
| `GrowthSnapshotPayload` | `RecordGrowthSnapshot` | Unlock/Resource/Career/Facility/Challenge |
| `FunEventPayload` | `RecordFunEvent` | HighestStack/ClutchKills/AirKills/Oops |

### IArchiveStorage（存储抽象）

```csharp
public interface IArchiveStorage
{
    PlayerArchiveData Load(string playerId);
    Task<PlayerArchiveData> LoadAsync(string playerId, CancellationToken token);
    void Save(PlayerArchiveData data);
    Task SaveAsync(PlayerArchiveData data, CancellationToken token);
    void Backup(PlayerArchiveData data, string reason);
}
```

### JsonArchiveStorage（JSON 实现）

`JsonArchiveStorage.cs:12` — 实现 `IArchiveStorage`，通过 `JsonManager` 序列化。

- **路径**：`Application.persistentDataPath/Archives/{playerId}_archive.json`
- **备份**：`{playerId}_archive_{时间戳}_{原因}.json`，最多保留 5 个
- **线程安全**：`lock(_fileLock)` 保护文件写入
- **文件名安全**：替换 `playerId` 中的非法字符为 `_`

## 5. 关键类与文件

### ArchiveManager (SingletonBase)

`ArchiveManager.cs:14` — 核心管理器，全局唯一。

| 方法 | 用途 |
|------|------|
| `Setup(playerId, overrideStorage)` | 初始化档案系统 |
| `GetDataSnapshot()` | 获取当前档案引用（只读） |
| `UpdateData(Action<PlayerArchiveData>)` | 核心写入口（线程安全 + 自动标记脏） |
| `RegisterSession(payload)` | 记录对局结算 + 增量更新基础统计 + 历史 |
| `RecordCombatSnapshot(payload)` | 记录战斗统计 |
| `RecordExplorationSnapshot(payload)` | 记录探索统计 |
| `RecordMissionResult(payload)` | 记录任务结果 |
| `RecordSocialSnapshot(payload)` | 记录社交统计 |
| `RecordGrowthSnapshot(payload)` | 记录养成统计 |
| `RecordFunEvent(payload)` | 记录趣味事件 |
| `Save()` / `SaveAsync()` | 同步/异步保存到磁盘 |

**线程安全**：
- `_dataLock`（`object`）保护 `_data` 读写
- `_isSaving` 布尔位防止并发异步保存
- `UpdateData()` 内部加锁

**自动保存**：
- `SaveOnlyOnSessionEnd=false` → `Tick()` 每 120 秒检测 `_isDirty` 并调用 `SaveAsync()`
- `SaveOnlyOnSessionEnd=true`（默认）→ 仅 `RegisterSession()` 后保存

### JsonArchiveStorage — 存储实现

`JsonArchiveStorage.cs:12` — 依赖 `Framework/Json/JsonManager`（SingletonBase）。

- `BuildFileToken(playerId)` → `Archives/{sanitized_playerId}_archive`
- `TrimBackups()` → 按文件名排序，超出 `MaxBackupCount=5` 的删除最旧的

## 6. 对外接口

### 核心写入接口

| 方法 | 调用方 |
|------|--------|
| `RegisterSession(SessionSummaryPayload)` | `RunSummaryCalculator` |
| `RecordCombatSnapshot(CombatSnapshotPayload)` | 战斗统计收集系统 |
| `RecordExplorationSnapshot(ExplorationSnapshotPayload)` | 探索统计收集系统 |
| `RecordMissionResult(MissionResultPayload)` | MissionSystem（预留） |
| `RecordSocialSnapshot(SocialSnapshotPayload)` | 社交统计收集系统 |
| `RecordGrowthSnapshot(GrowthSnapshotPayload)` | 养成系统 |
| `RecordFunEvent(FunEventPayload)` | 趣味事件收集系统 |

### 读取接口

| 方法 | 调用方 |
|------|--------|
| `GetDataSnapshot()` | UI（账户页/统计面板） |

### 存储接口

| 方法 | 用途 |
|------|------|
| `IArchiveStorage.Load/LoadAsync` | 从磁盘加载 |
| `IArchiveStorage.Save/SaveAsync` | 写入磁盘 |
| `IArchiveStorage.Backup` | 备份当前档案 |

## 7. 依赖模块

| 依赖模块 | 用途 |
|----------|------|
| `Framework.Singleton.SingletonBase<T>` | 单例基类 |
| `Framework.Mono.MonoManager` | `OnUpdate` Tick 驱动自动保存 |
| `Framework.Json.JsonManager` | JSON 序列化/反序列化 |
| `System.Threading` | `CancellationToken` + `Task.Run` 异步保存 |
| `UnityEngine.Application` | `persistentDataPath` + `quitting` 事件 |

## 8. 被哪些模块依赖

| 依赖方 | 用途 |
|--------|------|
| `RunSummaryCalculator` (RunSystem) | 对局结算时调用 `RegisterSession()` |
| UI 层（账户页/统计面板） | 读取 `GetDataSnapshot()` 展示数据 |
| 战斗系统 | 记录 `CombatSnapshot` |
| 任务系统 (MissionSystem) | `RecordMissionResult()` + `RecordGrowthSnapshot()`（Phase 4 已接入） |

## 9. 事件订阅与广播

### 订阅

ArchiveManager 订阅 `MonoManager.Instance.OnUpdate`（Tick 自动保存）和 `Application.quitting`（退出保存）。

### 广播

ArchiveSystem **不向 EventCenter 广播任何事件**。

## 10. Inspector 字段

ArchiveManager 无 `[SerializeField]` 字段（纯 C# Singleton，非 MonoBehaviour）。仅有一个公开属性：

| 属性 | 类型 | 默认值 | 用途 |
|------|------|--------|------|
| `SaveOnlyOnSessionEnd` | `bool` | `true` | 是否仅在结算时保存（关闭则每 120s 自动保存） |

## 11. Prefab / Scene / ScriptableObject 依赖

无。ArchiveSystem 不依赖 Prefab/Scene/ScriptableObject。数据持久化为纯 JSON 文件。

## 12. 常见问题

**Q: 为什么默认 `SaveOnlyOnSessionEnd = true`？**
A: 防止玩家「打开账户页但没结算就退出」时把默认 0 数据写回磁盘覆盖真实存档。`OnApplicationQuitting` 中会检查 `_sessionEndedThisRun` 标志。

**Q: 存档文件放在哪里？**
A: `Application.persistentDataPath/Archives/{playerId}_archive.json`。不同平台路径不同（Windows: `%userprofile%/AppData/LocalLow/{company}/{product}`）。

**Q: 如何扩展到云端存储？**
A: 实现 `IArchiveStorage` 接口即可（如 `CloudArchiveStorage`），通过 `ArchiveManager.Setup(playerId, new CloudArchiveStorage())` 注入。

**Q: 存档数据多久保存一次？**
A: `SaveOnlyOnSessionEnd=false` 时每 120 秒（`AutoSaveInterval`）自动保存脏数据。`SaveOnlyOnSessionEnd=true` 时仅在对局结算时同步保存。

**Q: 对局结算时传入了哪些真实数据？哪些还是占位？**
A: 当前 `RunSummaryCalculator.CalculateAndRecord()` 的实现：
- `RegisterSession` → Result/Difficulty 为真实数据，`TotalDuration` 来自 `RunSessionData`，`CombatDuration=total×0.7`（硬编码估算），`LoadingDuration=5s`（硬编码）
- `RecordCombatSnapshot` → `NormalKills` 来自 `MonsterRegistry`（真实），`DamageDealt=0` / `DamageTaken=0`（占位，未接入战斗系统）
- `RecordSocialSnapshot` → `IsCoop=false` / `TeamSize=1`（硬编码单人）
- `RecordMissionResult` / `RecordGrowthSnapshot` → **Phase 4 已接入**（对局结算时写入）
- `RecordExplorationSnapshot` / `RecordFunEvent` → **未调用**（待后续接入）

**Q: `_dataLock` 的作用范围是什么？**
A: `UpdateData()` 和 `Save()`/`SaveAsync()` 中的文件写入受 `_dataLock` 保护。`GetDataSnapshot()` 读操作也加锁，但返回引用后调用方应只读使用。

## 13. 当前完成度

| 功能 | 状态 |
|------|------|
| ArchiveManager 核心管理器 | 完成 |
| 7 种 Payload 增量更新 | 完成 |
| JSON 文件保存/加载/备份 | 完成 |
| 线程安全（锁 + 异步保存） | 完成 |
| 自动保存（Tick + quitting） | 完成 |
| `SaveOnlyOnSessionEnd` 结算保护 | 完成 |
| 对局历史记录（最多 100 条） | 完成 |
| Boss 击杀记录（首次/最快） | 完成 |
| 趣味榜单（AirKills/Clutch 等） | 完成 |
| `IArchiveStorage` 扩展接口 | 完成 |
| 云端存储实现 | **暂不实现** — 服务器端暂时不做云端同步，`IArchiveStorage` 接口保留供未来扩展 |
| MissionSystem 任务结果写入 | **已完成** — Phase 4 通过 `RunSummaryCalculator` 调用 `RecordMissionResult` + `RecordGrowthSnapshot` |
| UI 账户页/统计面板读取 | **数据层已完整** — UI 面板可通过 `ArchiveManager.Instance.GetDataSnapshot()` 获取全部 7 维度数据，需 UI 层实现展示面板 |

## 14. 修改本模块时必须同步更新的内容

- **PlayerArchiveData 新增子结构** → 在构造函数中初始化 + JSON 序列化兼容（`[Serializable]`）
- **新增 Payload 类型** → 实现对应的 `RecordXxx()` 方法
- **IArchiveStorage 接口变更** → 同步更新 `JsonArchiveStorage` 实现
- **Payload 新增字段** → 注意 `IEnumerable` 类型字段的空检查（部分 Payload 的 `BossKillDetails` 等可空集合）
- **修改 `AutoSaveInterval`** → 默认 120 秒，单位秒

## 15. 文档维护信息

| 项目 | 内容 |
|------|------|
| 创建日期 | 2026-05-14 |
| 覆盖文件数 | 5 个 .cs |
| 关联模块文档 | RunSystem (RunSummaryCalculator), Json (JsonManager), Mono (MonoManager) |
