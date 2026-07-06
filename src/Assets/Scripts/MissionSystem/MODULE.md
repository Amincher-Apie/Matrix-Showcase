# MissionSystem 任务系统

## 1. 模块职责

为 Roguelike 对局提供「主线 + 支线」双层任务系统。服务器权威驱动，通过 `NetworkList<MissionNetState>` 将任务状态同步到所有客户端。负责：

- 从 MissionLibrary 中随机构建「1 主 + N 次」任务组
- 将任务组的语义需求转为 PCG 可消费的 `MapTaskInput`
- 在地图生成完成后，将任务绑定到正确的 PCG 房间
- 管理 5 种任务类型（Boss / Eliminate / Defense / Capture / Destroy）的完整生命周期
- 为客户端提供屏幕边缘任务指引器（MissionPointer）
- 通过 EventCenter 与 RunSystem / ArchiveSystem 联动

## 2. 模块边界

| 边界 | 说明 |
|------|------|
| **上游** | `MissionLibrary` (ScriptableObject) → 任务配置数据；`IMissionGroupProvider` / `IMissionLobbyForwarder` 接口 → 外部任务源 |
| **下游** | `MissionBase` 子类实例 → 具体任务逻辑；`MissionPointerManager` → UI 指引 |
| **输入** | `MissionLibrary` SO 中的 `MissionConfig` 列表 + 外部 `IMissionGroupProvider` 接口 |
| **输出** | `MapTaskInput` (给 PCG) + `MissionNetState` 同步列表 + `MissionPointer` UI |
| **不负责** | 怪物属性/行为（由 AI/Buff 模块负责）；奖励发放（由 EnemyDropTableSO + ServerEnemyDrop 在怪物死亡时触发） |

## 3. 主线任务与次要任务关系

```
MissionCategory.Primary (1个)
    └─ MissionType.Boss ──→ RoomRole.Boss ──→ BossMission
                                  │
MissionCategory.Secondary (≥2个)  │
    ├─ MissionType.Eliminate → RoomRole.SideElimination → EliminateMission
    ├─ MissionType.Defense   → RoomRole.SideDefense     → DefenseMission
    ├─ MissionType.Capture   → RoomRole.SideCapture     → CaptureMission
    └─ MissionType.Destroy   → RoomRole.SideDestroy     → DestroyMission
```

**约定**：
- 一组有效任务组必须满足 `1 主 + ≥2 次`（`IsValidGroup()` 校验）
- Boss 是唯一的主线任务类型，所有 Side 任务为次线
- 任务类型通过 `MissionGroupRuntimeData.MapToSideTaskType()` / `MissionConfig.ResolveRoomRole()` 映射到 PCG 的 `RoomRole`

## 4. 任务生成时机

```
RunManager.RunInit()
    │
    ├─ missionManager.TryBuildCurrentPcgTaskInput(out taskInput)
    │     └─ MissionManager.EnsureMissionGroupPrepared()
    │           ├─ 1. MissionLobbyForwarder.TryGetForwardedMissionGroup()   # 大厅已转发
    │           ├─ 2. MissionGroupProvider.TryPullMissionGroup()            # 服务器拉取
    │           └─ 3. missionLibrary.TryBuildRandomGroup(seed)              # 本地随机
    │                 └─ 选 1 个 Primary + 不重复类型的 2 个 Secondary
    │
    └─ PcgGeneratePackage.TaskInput = taskInput
```

**时机**：RunManager 进入 `RunState.RunInit` 时，在调用 `PcgMapGenerator.Generate()` **之前**先通过 `TryBuildCurrentPcgTaskInput()` 从 MissionManager 拉取任务组。

**特殊情况**：
- 如果 `SelectedMissionGroup` 已存在且 `_replicatedMissions` 非空 → 跳过重建
- 如果 MissionLibrary 中缺少 Primary 或 Secondary 候选 → 返回失败，打印 Error

## 5. 任务绑定房间的方式

```
PCG 管线:
   RoomRoleAllocator.AssignRoles()
     → graph 节点被赋予 AssignedRole (Boss / SideDefense / SideCapture / ...)
     → 同时输出 TaskTriggerConnection 列表

MissionSystem 回填:
   MissionManager.ResolveMissionRoomsAgainstMap()
     → 遍历 _replicatedMissions 中 RoomNodeId == -1 的条目
     → 优先读取 PcgMapGenerationResult.TaskTriggerConnections
         ├─ TaskRole 匹配 MissionType 对应的 RoomRole
         ├─ 优先使用 IsPrimaryTrigger
         └─ 跳过已被其他任务占用的 TaskNodeId
     → Boss 或 TaskTriggerConnection 缺失时回退 FindMissionRoomNode()
     → 验证 Graph AssignedRole / AssignedSideTask / PcgPlacedRoom.Role 匹配任务类型
     → 将找到的 node.Id 写入 MissionNetState.RoomNodeId
     → 同步到所有客户端
     → 若个别任务仍未绑定房间，不阻塞其他已绑定任务注册；未绑定任务保持在 NetworkList 中并打印 warning
```

**流程**：PCG 先完成图构建+角色分配 → MissionManager 在 `TryBootstrapRuntimeMissions()` 中回填房间绑定。已绑定房间的任务会先创建 `RuntimeMissions`、触发区和指引器；未绑定房间或 PCG 图语义不匹配的任务不会创建运行时实例，并会输出 warning 供排查 PCG TaskInput / RoomRole 分配。

**2026-06-27 更新**：`ResolveMissionRoomsAgainstMap()` 已优先使用 `TaskTriggerConnections` 绑定支线任务房间，并用 `HashSet<int>` 防止多个同类型任务绑定到同一节点。Boss 任务保留 Graph 角色查找兜底。

**2026-06-28 更新**：房间绑定新增 `IsMissionRoomBindable()` 校验。只有 `RoomGraphNode.AssignedRole`、支线任务的 `AssignedSideTask`、`PcgPlacedRoom.Role` 与任务类型对应的 PCG 语义一致时，任务才会进入 `RuntimeMissions`。`PcgRoomRoot.DefaultRole` 仅作为 PCG prefab 配置 warning，不阻塞任务语义绑定。

## 6. 任务触发条件

| 触发方式 | 说明 | 对应字段 |
|---------|------|---------|
| 进房触发 | `MissionTriggerZone` 挂到任务房 `PcgRoomBounds.BoundsCollider` 上；玩家进入该 BoundCollider 时，`MissionTriggerZone.OnTriggerEnter` → `MissionManager.HandleMissionTriggerEntered()` → `mission.HandlePlayerEnteredTrigger()` → `Activate()` | `MissionConfig.triggerOnRoomEnter` |
| 服务端自动激活 | `TryBootstrapRuntimeMissions()` 完成后，服务端对仍为 `Inactive` 的任务调用 `mission.Prepare()`，将其切到 `Ready` 状态等待玩家进入 | — |

**注意**：`BossMission` 虽然 `triggerOnRoomEnter=true`，但实际激活是在 `OnActivated()` 中由服务端直接刷 Boss 并监听死亡，不依赖额外触发条件。

## 7. 任务完成条件

| 任务类型 | 完成条件 | 失败条件 |
|---------|---------|---------|
| `BossMission` | 追踪的 Boss `NetworkObjectId` 死亡 (`HandleUnitDied`) | 无（Boss 未死则玩家死亡 → RunDefeat） |
| `EliminateMission` | `CurrentProgress >= TargetProgress`（已击杀 ≥ 目标击杀数） | 无 |
| `DefenseMission` | `_remainingSeconds <= 0f`（成功防守到时限） | 防守目标被销毁 (`HandleTrackedTargetDestroyed`) |
| `CaptureMission` | 捕获目标死亡后生成拾取物，玩家拾取任务物品后完成 | 无明确失败条件 |
| `DestroyMission` | 所有轮次的所有目标被摧毁 | 无明确失败条件 |

**进度推进机制**：
- Boss：通过 `HandleUnitDied(unitId)` → 检查追踪的 Boss `NetworkObjectId` → `Complete()`
- Eliminate：通过 `HandleUnitDied(unitId)` → 确认为敌方网络对象 → `AddProgress(1)`
- Defense：通过 `TickServer(deltaTime)` 倒计时 → `SetProgress(remainingSeconds, totalSeconds)`
- Capture：目标死亡后生成 `PickupItem`，拾取物回报 `ReportCapturePickupCollected()` 后 `Complete()`
- Destroy：通过 `HandleTrackedTargetDestroyed(targetKey)` → `AddProgress(1)` → 轮次推进

## 8. 任务状态机

```
Inactive ──→ Ready ──→ Active ──→ Completed
                │          │
                │          └──→ Failed
                │
                └── (跳过 Ready? 目前 Prepare() 总是 Ready → 进房触发 Active)
```

| 状态 | 含义 | 触发 |
|------|------|------|
| `Inactive` | 尚未初始化或等待地图结果 | 初始状态 / `MissionNetState.CreateInitial()` |
| `Ready` | 已绑定房间、等待玩家进入 | `mission.Prepare()` (服务端 `TryBootstrapRuntimeMissions()`) |
| `Active` | 正在执行中 | `mission.Activate()` (玩家进入触发区 / `HandlePlayerEnteredTrigger()`) |
| `Completed` | 已完成 | `mission.Complete()`（`BossMission` 额外触发 `BossDefeatedEvt`） |
| `Failed` | 已失败 | `mission.Fail()` |

**状态迁移守卫**：
- `Prepare()` 仅在 `Inactive` → `Ready`
- `Activate()` 不能从 `Completed/Failed` 激活；若 `Inactive` 则先 `Prepare()`
- `Complete()` / `Fail()` 不重复执行

## 9. 与 RunSystem 的关系

```
RunManager                           MissionManager
    │                                     │
    ├─ RunInit ──→ TryBuildCurrentPcgTaskInput() ──→ 返回 MapTaskInput
    │                                     │
    ├─ pcgMapGenerator.Generate()         │
    │   (TaskInput 已包含任务语义)         │
    │                                     │
    ├─ OnMapGenerationCompleted()         │
    │   → TransitionTo(RoomEnter)         │
    │                                     ├─ TryBootstrapRuntimeMissions()
    │                                     │   → ResolveMissionRoomsAgainstMap()
    │                                     │   → 创建 MissionBase 实例
    │                                     │   → 将 MissionTriggerZone 绑定到任务房 BoundCollider
    │                                     │   → mission.Prepare()
    │                                     │
    ├─ Exploring                        ├─ 服务端监听 UnitDied (各自独立)
    │   MonsterSpawnManager 自主刷怪     │   HandleUnitDiedEvent()
    │                                     │
    ├─ RunVictory / RunDefeat            ├─ 任务状态在 NetworkList 中保持
    │                                     │
    ├─ RunSummary                        ├─ (任务完成数据供结算统计使用)
    │   RunSummaryCalculator             │
```

**关键差异**：
- `RunManager` 监听 `UnitDied` 是为了检测 "所有玩家死亡 → RunDefeat"
- `MissionManager` 监听 `UnitDied` 是为了 "任务击杀计数 → Boss/Eliminate 完成判定"
- 两者独立注册/注销同一个 `EventName.UnitDied` 事件

**MonsterSpawnManager 双重初始化问题**（已于 2026-05-14 修复）：
- 已移除 `MissionManager.TryBootstrapRuntimeMissions()` 中的 `monsterSpawnManager.InitializeWithMapResult()` 调用
- `MonsterSpawnManager` 现仅由 `RunManager.OnMapGenerationCompleted()` 初始化一次
- `MissionManager` 通过 `MonsterSpawnManager.FixedUpdate()` 中的 `PollMissionState()` 继续更新玩家人数和活跃任务数

## 10. 与 PCG / RoomRoleAllocator 的关系

```
MissionGroupRuntimeData.CreatePcgTaskInput()
    │
    └─→ MapTaskInput.PrimaryTask  = PrimaryTaskType.BossBattle
    └─→ MapTaskInput.SideTasks[]  = SideTaskType.Defense/Capture/Destroy/Elimination

PcgMapGenerator.Generate(package)
    │
    └─→ RoomRoleAllocator.AssignRoles(graph, taskInput, ...)
          → 根据 taskInput 中的 SideTask 类型和数量分配房间角色
          → 设置 graph 节点 AssignedRole + HasAssignedSideTask + AssignedSideTask

MissionManager.ResolveMissionRoomsAgainstMap()
    → 优先用 TaskTriggerConnection.TaskNodeId 绑定支线任务房间
    → Boss 或缺失 TaskTriggerConnection 时回退 graph AssignedRole / AssignedSideTask 查找
    → 只接受实际放置房间与 graph 任务语义匹配的节点
```

**数据流**：`MissionManager → PCG → RoomRoleAllocator → (角色分配) → MissionManager 回填`

**`TaskTriggerConnection` 使用方式**：`RoomRoleAllocator` 输出的每条 `TaskTriggerConnection` 直接包含 `TaskNodeId`（任务房间节点 ID）和 `TaskRole`（房间角色）。`MissionManager` 已优先遍历此列表建立支线任务绑定，并用 `FindMissionRoomNode()` 作为兜底。

`GenerateTaskTriggerConnections()` 在 `RoomRoleAllocator` 中的生成逻辑：对每个 SideTask 房间，找出所有邻接的 `RoomRole.Connector` 非环边邻居，为每条关系生成一个 `TaskTriggerConnection`（`IsPrimaryTrigger` 在仅 1 个 Connector 邻居或 60% 概率时为 true）。

## 11. 与 EventCenter 的关系

| 事件 | 订阅方 | 用途 |
|------|-------|------|
| `EventName.UnitDied` | `MissionManager.HandleUnitDiedEvent()` | 服务端监听，分发给所有 `MissionBase.HandleUnitDied()` 做击杀判定 |

**MissionSystem 广播事件**：

| 事件 | 触发时机 | 用途 |
|------|----------|------|
| `EventName.MissionStateChanged` | 任务状态变化 | HUD / 音效 / 调试 |
| `EventName.MissionProgressChanged` | 任务进度变化 | HUD 状态文本 / 后续进度条 |
| `EventName.MissionCompleted` | 首次进入 Completed | 奖励、结算统计 |
| `EventName.MissionFailed` | 首次进入 Failed | HUD、结算统计 |

服务端在 `SyncMissionState()` 写入 `NetworkList` 后触发上述事件；客户端在 `NetworkList.OnListChanged` 后基于本地任务实例状态变化触发 UI 可消费事件，但不会发放奖励。

## 12. 与 UI 的关系

**MissionPointerManager** — 屏幕空间任务图标指引器：
- 挂载在 Canvas 下，为每个任务创建一个运行时 `MissionPointer` UI 控件；`MissionManager` 未绑定该组件时会运行时创建 `MissionPointerManager_Runtime` 兜底
- 初始化后会在 `LateUpdate()` 增量同步 `MissionManager.RuntimeMissions`，补建新绑定任务的指引器，并移除已不属于运行时任务的指引器
- `LateUpdate()` 每帧刷新：从 `_missionManager.GetLocalPlayerTransform()` 获取玩家位置 → 计算指引图标世界坐标 → 转为屏幕边缘或屏幕内的 UI 位置
- 进入任务房间后标记 `_localEnteredMissionSlots`，指引从 "下一扇门" 切到 "任务目标点"
- 已完成/失败的任务自动隐藏
- 指引器视觉复用 `BasicMissionIcon-Instance.prefab`，任务图标和主/次任务基底色来自 `MissionUIConfigSO`
- 指引器运行时挂入 HUD 下的 `MissionPointerLayer_Runtime`；图标位置可指向下一扇门/Connector，prefab 内 TMP 文本始终显示玩家到当前指引落点的米数距离
- 屏幕内 Connector 目标会直接投影到门点，不叠加 `screenOffset`；只有屏幕外/边缘指引会使用 `screenOffset` 做边缘偏移
- 距离文本显示玩家到当前指引落点的米数：未到任务房时为下一处 Connector，到达任务房未触发时为 TriggerArea，触发后为任务目标点

## 2026-06-30 任务指引器距离修正

- `MissionGuideTarget` 新增 `FinalWorldPoint` 字段，存储最终目标房间中心的世界坐标（`mission.ResolveObjectiveGuidePoint()`）。
- `MissionPointerManager.LateUpdate()` 的距离计算改为使用 `guideTarget.FinalWorldPoint`（最终目标房间），图标定位仍使用 `guideTarget.WorldPoint`（路径上的 Connector 门点）。玩家 UI 层的指引器现在显示"到最终目标房间还有多远"，而非"到下一个门还有多远"。
- 多个屏幕空间指引器投影位置过近或实际 UI 矩形重叠时，`MissionPointerManager` 会按横向并排方式展开，并按控件宽高约束屏幕边缘，避免图标和距离文本完全堆叠

**MissionPointer** — 单个任务图标指引器 UI：
- 使用 `BasicMissionIcon` 控制根 Image 基底色与子节点 `Icon` Sprite
- 不显示任务状态文本；状态文本仍由 HUD 任务列表负责。屏幕空间指引器会额外显示距离文本，例如 `42m`
- `CanvasGroup` 控制显隐

## 13. 与 ArchiveSystem 的关系

任务奖励分为货币奖励和物品奖励两层。`currencyReward` 字段记录局内货币数量（所有任务通用），`rewards` 列表记录可选物品奖励。

当前连接点：
- `MissionBase.Complete()` → `MissionManager.SyncMissionState()` 检测首次 Completed → `GrantMissionRewards()`
- `GrantMissionRewards()` → `MissionConfig.CurrencyReward` → 每个客户端 `NetworkInventory.InGameCurrency.Value += amount`
- `GrantMissionRewards()` → `MissionConfig.Rewards` → `SOManager.GetSOById<BaseInventoryItemSO>()` → `NetworkInventory.TryAddItemServer()`
- `RunManager.EnterRunSummary()` 读取 `MissionManager.RuntimeMissions` → 统计支线完成/失败数 + 填充 `RunSessionData.MissionResults`
- `RunSummaryCalculator` 从 `RunSessionData` 读取 → `ArchiveManager.RecordMissionResult()` + `RecordGrowthSnapshot()`

## 14. 与 AI 模块的潜在关系

**当前无直接集成。** 需要注意项目中存在**两套独立的 AI 系统**：

| AI 系统 | 适用对象 | 配置方式 | 驱动方式 |
|---------|---------|---------|---------|
| `EnemyActor` + `EnemyAIConfig` 状态机 | 普通敌人（歼灭/防御等任务小怪） | `MissionSpawnEntry.AiConfigPath` 指向 `Resources/Configs/AI/` 下的 `EnemyAIConfig` SO | `EnemyAIModule` 状态机（Idle/Patrol/Chase/Attack） |
| **Behavior Designer** 行为树 | Boss 敌人 | `AiConfigPath` **无效**，AI 由 prefab 根节点的 `BehaviorTree` 组件内嵌定义 | `BossNetworkProxy` 初始化黑板变量并周期性绑定 `attackTarget` / `targetPosition` |

潜在关系链（仅普通敌人）：

```
MissionManager
    → MissionBase.Activate() → OnActivated()
        → EnemySpawnService.SpawnEnemy(enemyId, pos, rot, aiConfigPath)
            → AI 模块从 aiConfigPath 加载 EnemyAIConfig(SO)
            → 为该敌人绑定 AI 行为
```

`MissionSpawnEntry.AiConfigPath` 字段传递 AI 配置路径，格式如 `"Configs/AI/Enemy/Enemy_Melee"`，对应 `Resources/Configs/AI/` 目录下的 `EnemyAIConfig` 资源。

**Boss 任务的 `AiConfigPath` 无需填写**。Boss 的 AI 行为完全由 prefab 上的 `BehaviorTree` 组件（Behavior Designer）定义，包括 `BossShoot`、`BossSprintAttack`、`BossAdvanceAttack`、`BossAdvanceCombo`、`BossSkillA` 等自定义任务节点。`BossNetworkProxy` 会在服务端生成时把默认 `isIdle` 切到 `false`，并从已注册玩家目标中写入 `attackTarget`。`EnemySpawnService.SpawnEnemy()` 检测到 Boss prefab 没有 `EnemyActor` 组件时会跳过 AI 配置，仅打印 Warning。

## 15. 关键类与文件

| 文件 | 类 | 类型 | 职责 |
|------|----|------|------|
| `MissionManager.cs` | `MissionManager` | `NetworkBehaviour` | 任务系统中枢：任务组选取、房间绑定、触发区管理、事件监听、网络同步 |
| `MissionBase.cs` | `MissionBase` | 抽象类 | 任务基类：状态机、进度、Unit追踪、网络快照应用 |
| `MissionImplementations.cs` | `BossMission` / `EliminateMission` / `DefenseMission` / `CaptureMission` / `DestroyMission` | 密封类 | 5 种任务的具体实现 |
| `MissionRuntimeModels.cs` | `MissionType/Category/State` | 枚举 | 任务类型/分类/状态 |
| `MissionRuntimeModels.cs` | `MissionNetState` | `struct: INetworkSerializable` | 网络同步快照 |
| `MissionRuntimeModels.cs` | `MissionHudEntry` | `struct` | HUD 任务列表快照，包含 Slot / MissionId / 类型 / 分类 / 状态 / 进度 / 显示名 / 状态文本，允许 UI 在 RuntimeMissions 注册前显示已同步任务 |
| `MissionRuntimeModels.cs` | `MissionGroupRuntimeData` | 类 | 本局任务组（包含 CreatePcgTaskInput） |
| `MissionRuntimeModels.cs` | `MissionPullContext` | 类 | 任务拉取上下文 |
| `MissionRuntimeModels.cs` | `IMissionGroupProvider` | 接口 | 外部任务源提供者 |
| `MissionRuntimeModels.cs` | `IMissionLobbyForwarder` | 接口 | 大厅转发层 |
| `MissionRuntimeModels.cs` | `MissionSpawnEntry` / `MissionRewardEntry` / `MissionDestroyRoundConfig` / `MissionSelectionData` | 数据类 | 配置数据结构 |
| `MissionConfig.cs` | `MissionConfig` | `ScriptableObject` | 单任务配置：类型/触发/刷怪/奖励 |
| `MissionLibrary.cs` | `MissionLibrary` | `ScriptableObject` | 任务库：存储所有 MissionConfig，提供随机组队 |
| `MissionUIConfigSO.cs` | `MissionUIConfigSO` / `MissionTypeIconEntry` | `ScriptableObject` / 数据类 | HUD 任务列表 Icon、主/次任务基底色、状态文字色配置 |
| `MissionContext.cs` | `MissionContext` | 类 | 运行时依赖注入容器 |
| `MissionPointer.cs` | `MissionPointer` | `MonoBehaviour` | 单个屏幕空间任务图标指引器 UI，复用 `BasicMissionIcon` |
| `MissionPointerManager.cs` | `MissionPointerManager` | `MonoBehaviour` | 指引器管理器，负责将任务目标世界坐标投影到 Canvas |
| `MissionTriggerZone.cs` | `MissionTriggerZone` | `MonoBehaviour` | 房间任务触发区，复用任务房 `PcgRoomBounds.BoundsCollider` |
| `MissionTrackedTarget.cs` | `MissionTrackedTarget` | `MonoBehaviour` | 被追踪的任务目标（销毁时回调） |
| `Editor/MissionSampleAssetGenerator.cs` | `MissionSampleAssetGenerator` | 静态类 | 编辑器工具：生成 5 个示例 SO + 1 个 Library |

## 16. 对外接口

### MissionManager 公开 API

| 方法 / 属性 | 用途 | 调用方 |
|------------|------|--------|
| `RuntimeMissions` (IReadOnlyList\<MissionBase\>) | 获取所有运行时任务实例 | UI / RunSummaryCalculator |
| `CurrentMapResult` | 获取当前地图结果 | MissionPointerManager |
| `TryCollectMissionHudEntries(List<MissionHudEntry>)` | 收集 HUD 可显示的任务快照；以 NetworkList 同步状态为底，再用已注册的 RuntimeMissions 覆盖同 Slot 状态和进度文本，避免 runtime 部分注册时 HUD 丢任务 | GameHUDWindow |
| `TryBuildCurrentPcgTaskInput(out MapTaskInput)` | 构建 PCG 任务输入；服务端优先使用 `_selectedMissionGroup`，客户端可从已同步的 `MissionNetState` 快照还原任务语义用于本地 PCG 复刻 | RunManager (RunInit) / FullFlow Client |
| `RequestMissionGroupServerRpc()` | 客户端请求任务组 | 客户端 / Lobby |
| `HandleMissionTriggerEntered(triggerZone, playerActor, isLocalPlayer)` | 处理玩家进入触发区 | MissionTriggerZone |
| `ReportTrackedTargetDestroyed(slotIndex, targetKey, networkObjectId)` | 上报追踪目标被摧毁 | MissionTrackedTarget |
| `ReportCaptureProgress(slotIndex, deltaProgress)` | 上报捕获进度 | 占点系统 |
| `TryResolveNextDoorGuidePoint(playerPos, targetRoomNodeId, out worldPoint)` | 解析导航指引点 | MissionPointerManager |
| `GetLocalPlayerTransform()` | 获取本地玩家 Transform | MissionPointerManager |

### IMissionGroupProvider (接口)

```csharp
public interface IMissionGroupProvider
{
    bool TryPullMissionGroup(MissionPullContext context, MissionLibrary missionLibrary,
        out MissionGroupRuntimeData missionGroup);
}
```

### IMissionLobbyForwarder (接口)

```csharp
public interface IMissionLobbyForwarder
{
    bool TryGetForwardedMissionGroup(MissionPullContext context, MissionLibrary missionLibrary,
        out MissionGroupRuntimeData missionGroup);
    void ForwardHostMissionGroup(MissionGroupRuntimeData missionGroup);
}
```

## 17. 事件订阅与广播

### 订阅

| 事件 | 订阅方法 | 注册时机 | 注销时机 |
|------|---------|---------|---------|
| `EventName.UnitDied` (struct `UnitDiedEvt`) | `HandleUnitDiedEvent()` | `OnNetworkSpawn()` | `OnNetworkDespawn()` |

### 广播

| 事件 | 参数 | 说明 |
|------|------|------|
| `MissionStateChanged` | `MissionStateChangedEvt` | 任务状态变化 |
| `MissionProgressChanged` | `MissionProgressChangedEvt` | 任务进度变化 |
| `MissionCompleted` | `MissionCompletedEvt` | 任务完成 |
| `MissionFailed` | `MissionFailedEvt` | 任务失败 |

状态仍以 `NetworkList<MissionNetState>` 为权威同步通道；EventCenter 事件用于 UI、音效、结算等本地响应。

## 18. Inspector 字段说明

### MissionManager

| 字段 | 类型 | 用途 |
|------|------|------|
| `missionLibrary` | `MissionLibrary` | 任务库 SO 引用 |
| `pcgMapGenerator` | `PcgMapGenerator` | PCG 生成器引用（获取地图结果） |
| `enemySpawnService` | `EnemySpawnService` | 敌人生成服务 |
| `missionGroupProviderBehaviour` | `MonoBehaviour` (as `IMissionGroupProvider`) | 外部任务源组件（主机从服务器接收房主 Mission 时使用） |
| `missionLobbyForwarderBehaviour` | `MonoBehaviour` (as `IMissionLobbyForwarder`) | 大厅转发组件（主机向其他客户端转发已确定的 MissionGroup） |
| `missionPointerManager` | `MissionPointerManager` | 指引器管理器引用 |
| `monsterSpawnManager` | `MonsterSpawnManager` | 怪物管理器引用（**注意**：此处调用 `InitializeWithMapResult()` 与 `RunManager` 重复，建议移除此调用，见第 9 节说明） |
| `autoRequestMissionGroupOnSpawn` | `bool` | 客户端生成后自动请求任务组 |

### MissionPointerManager

| 字段 | 类型 | 用途 |
|------|------|------|
| `pointerCanvas` | `Canvas` | 指引器 UI Canvas；未显式绑定时优先使用 `GameHUDWindowDataComponent` 所在 Canvas，其次查找场景 Canvas，最后自动创建 ScreenSpaceOverlay Canvas |
| `missionIconPrefab` | `GameObject` | 指引器图标 Prefab；未绑定时兜底加载 `Resources/Prefab/UI/UIItem/MissionBox/BasicMissionIcon-Instance.prefab` |
| `missionUIConfig` | `MissionUIConfigSO` | 任务图标与主/次任务基底色配置；未绑定时优先读取当前 `GameHUDWindowDataComponent.MissionUIConfig`，再尝试 Resources 兜底 |
| `screenOffset` | `Vector2` | 指引器屏幕偏移 |
| `screenEdgePadding` | `float` | 屏幕边缘内边距 |
| `pointerOverlapThreshold` | `float` | 指引器投影位置小于该距离时视为重叠组；实际 UI 矩形发生重叠时也会并入同组 |
| `pointerOverlapSpacing` | `float` | 重叠组横向展开时每个指引器中心之间的最小间距；实际控件宽度不足时会自动放大间距 |

## 19. ScriptableObject 配置说明

### MissionConfig

| 分组 | 字段 | 类型 | 说明 |
|------|------|------|------|
| Identity | `missionId` | `string` | 唯一标识（如 `mission_boss_001_guardian`） |
| Identity | `displayName` | `string` | 显示名称 |
| Identity | `description` | `string` | 描述文本（策划用） |
| Identity | `missionType` | `MissionType` | Boss/Eliminate/Defense/Capture/Destroy |
| Identity | `missionCategory` | `MissionCategory` | Primary/Secondary |
| Identity | `externalTaskId` | `string` | 外部任务 ID（对接外部任务源） |
| Trigger | `triggerOnRoomEnter` | `bool` | 是否进房时触发 |
| Trigger | `triggerHeight` | `float` | 历史字段；当前默认触发范围由任务房 `PcgRoomBounds.BoundsCollider` 的尺寸决定 |
| Trigger | `pointerLabel` | `string` | 指引器标签 |
| Combat | `spawnEntries` | `List<MissionSpawnEntry>` | 刷怪配置列表（EnemyPrefabAddress/Count/AiConfigPath） |
| Combat | `objectivePrefab` | `GameObject` | 目标预制体（防御/捕获用） |
| Combat | `killTargetCount` | `int` | 歼灭击杀目标数 |
| Combat | `defenseDurationSeconds` | `float` | 防御持续时间 |
| Combat | `captureRequiredProgress` | `float` | 捕获所需进度 |
| Combat | `destroyRounds` | `List<MissionDestroyRoundConfig>` | 破坏轮次配置（TargetPrefab/TargetCount） |
| Rewards | `currencyReward` | `int` | 完成任务后发放的局内货币数量 |
| Rewards | `rewards` | `List<MissionRewardEntry>` | 额外物品奖励列表（RewardId/Amount，可选） |

### MissionLibrary

| 字段 | 类型 | 说明 |
|------|------|------|
| `missions` | `List<MissionConfig>` | 所有可用任务配置 |

### MissionUIConfigSO

| 字段 | 类型 | 说明 |
|------|------|------|
| `iconEntries` | `List<MissionTypeIconEntry>` | `MissionType → Sprite Icon` |
| `mainMissionColor` | `Color` | 主任务 MissionBox 的 Icon 基底色 |
| `secondaryMissionColor` | `Color` | 次要任务 MissionBox 的 Icon 基底色 |
| `inactiveStateColor` | `Color` | 未激活状态任务名颜色，默认白色 |
| `activeStateColor` | `Color` | 进行中状态任务名颜色，默认金色 |
| `completedStateColor` | `Color` | 成功状态任务名颜色，默认白色，`CheckLine` 为额外表现 |
| `failedStateColor` | `Color` | 失败状态任务名颜色，默认 `RGB(64,64,64)` |

### 现有 SO 资源

| 路径 | 类型 | 说明 |
|------|------|------|
| `Resources/Configs/Missions/Libraries/MissionLibrary_Default.asset` | MissionLibrary | 默认任务库（含 5 个示例任务） |
| `Resources/Configs/Missions/Primary/Boss/Mission_Boss_001_Guardian.asset` | MissionConfig | Boss 主任务 |
| `Resources/Configs/Missions/Secondary/Eliminate/Mission_Eliminate_001_Sweep.asset` | MissionConfig | 歼灭次任务 |
| `Resources/Configs/Missions/Secondary/Defense/Mission_Defense_001_Core.asset` | MissionConfig | 防御次任务 |
| `Resources/Configs/Missions/Secondary/Capture/Mission_Capture_001_Relay.asset` | MissionConfig | 捕获次任务 |
| `Resources/Configs/Missions/Secondary/Destroy/Mission_Destroy_001_Reactor.asset` | MissionConfig | 破坏次任务 |

## 20. Prefab / Scene 依赖

| 资源类型 | 名称 / 路径 | 用途 |
|---------|------------|------|
| Scene | `SampleScene.unity` | MissionManager 所在主场景 |
| Scene | `CombatTestScene` | 战斗测试场景（预计包含 MissionManager） |
| Prefab | `Resources/Prefab/Rooms/*` | PCG 生成的房间；任务房需要配置 `PcgRoomBounds.boundsCollider` 作为进房触发区 |
| Prefab | `Resources/Prefab/Enemy/*` | 敌人预制体（EnemySpawnService.SpawnEnemy 使用） |
| Canvas | 自动创建或引用 `pointerCanvas` | MissionPointerManager 的 UI Canvas |

## 21. 常见问题

**Q: 为什么 MissionManager 和 RunManager 都监听 UnitDied？**
A: 两者目的不同。RunManager 监听是为了检测"所有玩家死亡 → RunDefeat"，MissionManager 监听是为了追踪"任务怪物是否被击杀 → 任务进度+1"。两者独立注册/注销。

**Q: 任务状态变更为什么仍需要 NetworkList？**
A: `NetworkList<MissionNetState>` 是任务状态的网络权威同步通道，EventCenter 事件只负责状态变化后的本地响应。客户端不能只依赖 EventCenter 推断任务状态。

**Q: SideCapture 和 SideDestroy 房间进入后是否会自动触发战斗？**
A: 这是 RunSystem 的问题。当前 `RunManager.IsCombatRoom()` 不包含 `SideCapture`/`SideDestroy`，见 RunSystem MODULE.md 中的 TODO 标注。

**Q: 任务的奖励何时发放？**
A: 任务首次进入 `Completed` 时由服务端 `MissionManager.GrantMissionRewards()` 发放到所有已连接玩家的 `NetworkInventory`。奖励 ID 必须能通过 `SOManager.GetSOById<BaseInventoryItemSO>()` 找到。

**Q: `TaskTriggerConnection` 数据在哪里被使用？**
A: `ResolveMissionRoomsAgainstMap()` 已优先遍历 `PcgMapGenerationResult.TaskTriggerConnections` 绑定支线任务房间。Boss 仍保留 Graph 角色查找兜底。

**Q: Side 任务失败后会影响对局吗？**
A: 不会。除 Boss 外的所有 Side 任务（Eliminate/Defense/Capture/Destroy）失败不影响 Run 状态。Side 任务成功带来局内收益（奖励），失败无负收益。只有击杀 Boss 才能推进 RunVictory。

**Q: MonsterSpawnManager 为什么被初始化两次？**
A: `RunManager.OnMapGenerationCompleted()` 和 `MissionManager.TryBootstrapRuntimeMissions()` 各调用了一次 `InitializeWithMapResult()`。两者传入相同数据，但第二次调用会 `Clear()` MonsterRegistry。建议保留 RunManager 的调用，移除 MissionManager 中的重复调用。详见第 9 节。

## 22. 当前完成度

| 功能 | 状态 |
|------|------|
| 任务组随机选取（1主+2次） | 完成 |
| MissionLibrary SO 配置 | 完成（含示例生成器） |
| PCG 任务输入构建（MapTaskInput） | 完成 |
| 房间绑定（MissionNetState.RoomNodeId 回填） | 完成 |
| 网络同步（NetworkList） | 完成 |
| BossMission（刷 Boss + 击杀判定） | 完成 |
| EliminateMission（全图敌人死亡计数） | 完成 |
| DefenseMission（倒计时 + 目标保护 + 剩余时间进度同步） | 完成 |
| CaptureMission（目标死亡 + 拾取物回收） | 完成 |
| DestroyMission（多轮破坏） | 完成 |
| MissionTriggerZone（进房触发） | 完成 |
| MissionPointerManager（屏幕边缘指引） | 完成 |
| UnitDied 事件监听与分发 | 完成 |
| TaskTriggerConnection 房间绑定 | 完成（Boss 保留 Graph 兜底） |
| EventCenter 任务完成/失败事件 | 完成 |
| IMissionGroupProvider 接口 | 接口已定义，外部实现待接入 |
| IMissionLobbyForwarder 接口 | 接口已定义，外部实现待接入 |
| 奖励发放 | 完成（`missionConfig.CurrencyReward` 字段驱动货币奖励 + `rewards` 列表额外物品奖励） |
| 任务失败后的 Run 联动机制 | **无需实现** — Side 任务（Eliminate/Defense/Capture/Destroy）失败不影响对局结果。击杀 Boss 是唯一推进 Run 的条件。Side 任务成功给玩家局内收益，失败无负收益。 |
| 任务状态 UI（HUD 任务列表） | 完成（3 个 `BasicMissionBox` + 颜色/CheckLine 状态表现 + TMP 状态文本） |
| 任务进度 UI（HUD 面板） | 完成文本态提示（如 `还需击杀 X 个` / `还需守护 X秒`）；数值进度条未实现 |
| 任务奖励 UI 展示 | **未实现** |

## 23. 修改本模块时必须同步更新的内容

- **MissionConfig SO** 新增字段 → 同步更新 `MissionSampleAssetGenerator` 生成逻辑
- **MissionType 枚举** 新增值 → 同步更新：
  - `MissionManager.CreateMissionInstance()` switch-case
  - `MissionConfig.ResolveRoomRole()` switch-case
  - `MissionGroupRuntimeData.MapToSideTaskType()` switch-case
  - `MissionSampleAssetGenerator` 生成新类型的示例 SO
- **MissionState 枚举** 新增值 → 同步更新 HUD 状态色配置与任务状态表现逻辑
- **MissionBase 新增虚方法** → 检查所有 5 个 sealed 子类是否需要覆写
- **新增事件监听** → 确保 `OnNetworkDespawn()` 中注销
- **任务奖励变更** → 同步检查 `MissionConfig.currencyReward` / `rewards`、`SOManager` 道具 ID、`NetworkInventory.InGameCurrency` / `TryAddItemServer()`
- **任务事件新增/变更** → 同步更新 EventCenter MODULE.md 与 HUD 监听逻辑

## 24. 文档维护信息

| 项目 | 内容 |
|------|------|
| 创建日期 | 2026-05-14 |
| 基于代码版本 | 当前分支 |
| 覆盖文件数 | 12 个 .cs + 1 个 Editor 工具 + 6 个 SO .asset |
| 关联模块文档 | EventCenter, RunSystem, PCG, RoomRoleAllocator |
## 2026-06-29 三类支线任务落地更新

### CaptureMission
- 新语义为“生成捕获目标 -> 目标死亡 -> 在死亡位置生成 PickupItem -> 玩家按 F 拾取 -> Complete”，不再以占点进度作为主完成条件。
- `MissionConfig` 新增 `capturePickupPrefab`、`captureItemId`、`captureItemAmount`、`capturePickupPrompt`。`ResolveTargetProgress()` 对 Capture 返回 `1`。
- `MissionManager.ReportCapturePickupCollected(slotIndex, requesterClientId, itemId)` 是拾取物回报完成状态的服务端入口。
- 需要人工确认：`captureItemId` 必须能通过 `SOManager.GetSOById<BaseInventoryItemSO>()` 找到；`capturePickupPrefab` 若使用正式资源，需挂载 `NetworkObject` + `PickupItem` + 触发 Collider，并注册到 NetworkPrefabs。

### DefenseMission
- 激活后生成/补齐 `DefenseObjective`，目标会注册到 `AttackableObjectManager`，敌人可通过泛化后的 AI 感知锁定并攻击。
- 任务进度改为剩余守护秒数，`ResolveTargetProgress()` 对 Defense 返回 `defenseDurationSeconds` 向上取整；倒计时结束且目标存活则完成，目标生命与护盾都归零则失败。
- 需要人工确认：正式 `objectivePrefab` 需挂载 `NetworkObject`、Collider、`DefenseObjective`，并注册到 NetworkPrefabs；运行时补齐仅用于兜底验证。

### DestroyMission
- 每轮目标生成时挂载 `MissionTrackedTarget` 与 `DamageContributionTracker`，目标销毁/反生成时推进轮次。
- `MissionDestroyRoundConfig` 新增 `GoldReward`，`DamageContributionTracker` 会按玩家造成伤害比例给 `NetworkInventory.InGameCurrency` 即时发放金币。
- 新增 `MissionDamageableTarget` 作为无 `NetworkProxyBase` 的轻量任务目标兜底，支持 HP/Shield、AI 可攻击注册、受击、死亡、分配贡献奖励并上报任务进度。
- 需要人工确认：正式 Destroy 目标 Prefab 推荐挂载 `NetworkObject` + Collider + `MissionDamageableTarget`，或使用已有 `NetworkProxyBase` + `ServerAttributeModule` 的可死亡目标；所有网络生成 Prefab 都必须注册到 NetworkPrefabs。

## 2026-06-29 五类任务配置手册

- 新增 `MissionConfigurationGuide.md`，集中说明 Boss / Eliminate / Defense / Capture / Destroy 五类任务的 `MissionConfig` 字段写法、Prefab 放置位置、任务目标组件要求、道具 SO ID 写法、`MissionLibrary` 接入方式和 NetworkPrefabs 检查清单。
- 需要人工确认：手册中的 Prefab / Inspector / NetworkPrefabs 绑定仍需在 Unity Editor 内逐项检查，`.asset` 示例资源不会由文档自动同步。

## 2026-06-29 Eliminate 与 HUD 状态文本更新

- `EliminateMission` 不再读取 `spawnEntries` 或主动刷怪；激活后只使用 `killTargetCount` 作为目标数，并通过 `UnitDied` 统计全图 `EnemyNetworkProxy` / `BossNetworkProxy` / `ServerEnemyAttributeModule` 敌方对象死亡。
- `MissionHudEntry` 新增 `CurrentProgress`、`TargetProgress`、`StatusText`，`MissionHudStatusFormatter` 统一生成 `还需击杀 X 个`、`还需守护 X秒`、`还需摧毁 X 个` 等 HUD 文案。
- `DefenseMission` 的进度同步语义改为剩余守护秒数，HUD 可直接显示倒计时；防守目标生命仍只决定任务失败条件。
- 需要人工确认：正式 `BasicMissionBox.prefab` 建议补充并绑定 TMP 文本节点（推荐命名 `MissionStatus` / `StatusText`）；当前代码可运行时创建兜底节点，但布局仍需在 Unity Editor 中检查。

## 2026-06-29 任务指引寻路更新

- `MissionManager` 新增 `MissionGuideTarget` / `MissionGuideTargetKind`，作为任务指引系统的统一寻路结果。
- 指引寻路以玩家当前所在房间为起点、任务房间为终点，优先在 `PcgMapGenerationResult.Connections` 过滤出的可用门连接图上执行 BFS；未到任务房时优先返回路径上的 `Connector` 世界坐标。有效连接要求 `PcgRoomConnection.IsResolved=true`，且至少一侧 `PcgConnectorMarker` 引用存在并处于 `activeInHierarchy`。若可用门路径无法解析出 Connector，会退回到 PCG `RoomGraph` 连通性路径继续向后查找最近的可用 Connector；若仍找不到，才使用 `MissionTriggerZone` 中心/任务目标点作为异常可见兜底，避免 UI 指引器被完全隐藏。
- 当玩家已到任务房但尚未进入任务触发区时，指引目标切换为 `MissionTriggerZone` 绑定的 BoundCollider 世界 Bounds 中心；本地玩家触发后再切换到 `MissionBase.ResolveObjectiveGuidePoint()`。
- `MissionTriggerZone` 新增 `TryGetWorldBounds()`，供屏幕指引器、小地图 TriggerArea 标记和后续调试表现读取触发区范围。
- 需要人工确认：正式任务房 Prefab 的 `PcgRoomBounds.boundsCollider` 必须覆盖玩家进入区域，且 collider 所在 GameObject 需要处于激活状态。

## 2026-06-30 任务触发区 BoundCollider 复用更新

- `MissionManager.TryBootstrapRuntimeMissions()` 不再运行时创建独立的 `MissionTrigger_*` 触发物体，而是调用 `MissionTriggerZone.TryAttachToRoomBounds()` 将触发组件挂到任务房 `PcgRoomBounds.BoundsCollider` 所在对象。
- `MissionTriggerZone` 只负责接管已有 BoundCollider：运行时会启用该 BoxCollider 并设置 `isTrigger=true`，不会重写 collider 的中心、尺寸或高度。
- `MissionManager` 退场时会清理本轮运行时添加到 BoundCollider 上的 `MissionTriggerZone` 组件，避免房间对象残留旧任务引用。
- `MissionConfig.triggerHeight` 保留为历史配置字段；默认进房触发范围以房间 BoundCollider 为准。
- 需要人工确认：所有可能承载 Boss / Side 任务的房间 Prefab 都需要配置 `PcgRoomBounds.boundsCollider`，并确认该 collider 的范围符合任务房进入判定。

## 2026-06-30 屏幕空间任务指引器可读性修正

- `MissionPointerManager` 的距离文本改为显示玩家到当前指引落点的距离，未进入任务房时与 Connector 门点一致，不再显示到最终任务目标点的距离。
- 屏幕内目标点不再叠加 `screenOffset`，因此可见 Connector 的屏幕指引器会落在门点；屏幕外或贴边目标仍走边缘夹取与偏移逻辑。
- 重叠判定新增实际 `MissionPointer` UI 矩形尺寸检测，横向展开时会按控件宽度自动增大间距，并限制整组不越出 Canvas 安全边界。
