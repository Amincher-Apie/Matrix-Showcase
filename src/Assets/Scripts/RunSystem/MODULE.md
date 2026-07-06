# RunSystem 对局系统

## 模块定位

Roguelike 对局的生命周期管理器。服务器权威驱动，通过 NetworkVariable/NetworkList 将状态同步到客户端。控制从大厅→选英雄→地图生成→自由探索→Boss→结算的完整流程。

> **2026-05-16 更新**：删除了 RoomCombatController / PathChoiceManager，状态机从「房间制」（进门→锁门→波次战斗→清空→选路）改为「开放地图制」（自由探索，MonsterSpawnManager 按玩家位置刷怪）。

## 文件结构

```
Assets/Scripts/RunSystem/
├── RunManager.cs              # 对局状态机核心（NetworkBehaviour）
├── RunConfig.cs               # Run 系统全局配置 ScriptableObject
├── RunStateModels.cs          # 状态枚举 + RoomNetState
├── RunContext.cs              # 对局运行时依赖上下文
├── RunSessionData.cs          # 单次对局可变数据
├── RunRoomTrigger.cs          # 房间进入触发器（BoxCollider）
├── LobbyManager.cs            # 大厅管理（P0 占位）
├── HeroSelector.cs            # 英雄选择（P0 占位）
├── RunSummaryCalculator.cs    # 结算统计 + 写入 ArchiveManager
├── RunResultWindow.cs          # 对局结算 UI（WindowBase）
├── RunResultUIBridge.cs        # EventCenter → UI 桥接（MonoBehaviour）
└── MODULE.md
```

## 核心类

### RunManager (NetworkBehaviour)

`RunManager.cs:14` — **对局主状态机**。服务器权威（所有 `TransitionTo` 仅在 `IsServer` 时生效）。

**网络同步状态：**

| NetworkVariable | 类型 | 用途 |
|----------------|------|------|
| `_currentState` | `NetworkVariable<int>` | 当前 RunState |
| `_currentRoomNodeId` | `NetworkVariable<int>` | 当前房间节点ID |
| `_runSeed` | `NetworkVariable<int>` | 对局随机种子 |
| `_difficultyLevel` | `NetworkVariable<int>` | 难度 |
| `_roomStates` | `NetworkList<RoomNetState>` | 所有房间状态 |

**公开 API：**

| 方法 | 用途 |
|------|------|
| `TransitionTo(RunState)` | 服务端触发状态切换 |
| `SetSelectedHero(heroId, loadoutId)` | 设置英雄选择 |
| `ReportRoomEnteredServerRpc(roomNodeId)` | 客户端上报进入房间 |
| `TryGetRoomState(nodeId, out state)` | 查询房间状态 |
| `GetDifficultyTier(intervalMinutes)` | 获取当前难度等级 |
| `CurrentState` / `CurrentRoomNodeId` / `RunSeed` / `Difficulty` | 公开属性 |
| `Context` / `Session` | 上下文和数据访问 |

### 状态机转换表

```
MainMenu → Lobby → HeroSelect → RunInit → RoomEnter
                                              ├─ BossFight → RunVictory
                                              └─ Exploring (自由探索)
                                                    ↑
RoomEnter ← ReportRoomEnteredServerRpc ←────────┘

RunDefeat (从任何状态，全员死亡时触发)
    ↓
RunSummary → MainMenu/Lobby
```

`CanTransition()` 方法对每个转换做了显式验证。`RunDefeat` 允许从**任意状态**直接切入（全灭是突发条件）。

**关键流程节点：**

1. **RunInit** — 确定 Seed → 创建 `PcgGeneratePackage` → 调用 `PcgMapGenerator.Generate()` → 订阅 `OnGenerationCompleted` → 构建房间状态列表 → 初始化 MonsterSpawnManager → 解析出生房间 → 触发 `RunSeedFinalized` 事件
2. **RoomEnter** — 获取当前房间 `RoomGraphNode` → 发 `RoomEnteredEvt` → 按 `AssignedRole` 路由：
   - `Boss` → `BossFight`
   - 其余全部 → `Exploring`
3. **Exploring** — 注册死亡监听（检测全员阵亡）。MonsterSpawnManager 在 FixedUpdate 中自主按玩家位置刷怪，无需 RunManager 干预
4. **BossFight** — 发 `BossFightStartedEvt` → 注册死亡监听 → 监听 `BossDefeatedEvt`（由 `BossMission.Complete()` 触发） → `RunVictory`
5. **RunSummary** — 构建 `RunSummaryData` → `RunSummaryCalculator.CalculateAndRecord()` → 写入 `ArchiveManager`

**HUD 打开规则**：
- `RunManager` 不直接打开 `GameHUDWindow`
- 本地玩家网络对象生成并完成 `PlayerNetworkProxy.OnNetworkSpawn()` 初始化后，由 `PlayerNetworkProxy` 延迟一帧打开 `GameHUDWindow`
- HUD 生命周期跟随本地玩家可控对象，而不是 `RoomEnter` / `Exploring` / `BossFight` 等 RunState
- HUD 自身仍监听 `RunVictory` / `RunDefeat` 并销毁自身

**结果事件分发规则**：
- `RunVictory` / `RunDefeat` / `RunSummaryReady` 由服务端 `RunManager` 生成权威 `RunResultEvt`
- 服务端本地先触发 `EventCenter`，用于 Host 或服务端本地监听
- `RunManager` 再通过 `ClientRpc` 将结果事件分发给远端客户端，由客户端本地 `EventCenter` 唤醒 `RunResultUIBridge`
- `RunResultUIBridge` 收到 `RunSummaryReady` 后按 `IsVictory` 弹出 `GameVictoryWindow` 或 `GameFailedWindow`

### RunStateModels.cs

定义核心枚举：

| 枚举 | 值 | 用途 |
|------|-----|------|
| `RunState` | MainMenu(0)..RunSummary(30) | 对局顶层状态。12-14 已废弃（RoomCombat/RoomClear/PathChoice），新增 Exploring(16) |
| `RoomRunState` | Unreachable/Locked/Available/Active/Cleared | 房间状态 |
| `RunDifficulty` | Easy/Normal/Hard/Nightmare | 难度 |

### RunConfig (ScriptableObject)

可配置参数：

- `DefaultStyleKey` / `DefaultDifficulty` — 默认地图风格和难度
- `MinPlayersToStart` / `MaxPlayers` — 人数限制
- `SkipLobbyForTesting` / `SkipHeroSelectForTesting` — 测试快捷跳转

已删除的废弃字段：`RoomCombatTransitionDelay`、`RoomClearRewardDelay`、`PathChoiceTimeout`、`BirthRoomNodeId`。

### RunContext

`RunContext.cs:11` — 依赖注入容器，传递 `RunManager` / `PcgMapGenerator` / `MissionManager` / `MonsterSpawnManager` / `RunConfig`。

提供方法：
- `GetAlivePlayerCount()` — 通过 `AttackableObjectManager` 统计存活玩家
- `GetDifficultyTier(intervalMinutes=3f)` — 难度系数控制函数
- `FindPlayerRoomNode(worldPos, mapResult)` — 根据世界坐标定位房间
- `TryCollectSpawnPoints(nodeId, category, results)` — 收集刷怪点

### RunSessionData

`RunSessionData.cs:10` — 单次对局的可变数据容器。

**2026-06-28 新增字段**：
- `SideTasksCompleted` / `SideTasksFailed` — 支线任务完成/失败计数
- `TotalCurrencyEarned` — 任务奖励获得的货币总额
- `MissionResults` — `List<MissionResultRecord>` 每个任务的结果快照，供结算写入 Archive

### RunRoomTrigger

`RunRoomTrigger.cs:11` — 挂载于房间入口的触发器。检测 `PlayerActor` → 客户端调用 `ReportRoomEnteredServerRpc`。

### LobbyManager / HeroSelector

- **LobbyManager** (P0 占位) — `OnNetworkSpawn` 后延迟自动前进到 `RunInit`
- **HeroSelector** (P0 占位) — `AutoSelectDefault()` → `SetSelectedHero("DefaultHero")` → `TransitionTo(RunState.RunInit)`

### RunSummaryCalculator

`RunSummaryCalculator.cs:10` — 结算数据写入 `ArchiveManager`。

**当前写入内容**：
- `RegisterSession()` — 对局基础统计
- `RecordCombatSnapshot()` — 击杀统计
- `RecordSocialSnapshot()` — 社交统计
- `RecordMissionResult()` — 每个任务的结果（Phase 4 接入，MissionType/IsSuccess）
- `RecordGrowthSnapshot()` — 货币收益（Phase 4 接入，ResourceEarned）

## 依赖关系

| 依赖模块 | 用途 |
|----------|------|
| `Matrix.PCG` (PcgMapGenerator, RoomGraph, RoomRole, SpawnPointCategory) | 地图生成 |
| `Matrix.Missions` (MissionManager, MapTaskInput, BossMission) | 任务输入 + `BossMission.Complete()` → `BossDefeatedEvt` |
| `Framework.LogicLayer.Module.SpawnSystem` (MonsterSpawnManager, MonsterRegistry) | 持续刷怪 + 击杀统计 |
| `EventCenter` | 所有状态变更的事件分发 |
| `ArchiveSystem` (ArchiveManager) | 结算数据持久化 |
| `Unity.Netcode` | 网络同步 |

## 关联资源

- **Scene**：`SampleScene.unity`（RunManager 所在主场景）
- **ScriptableObject**：`RunConfig.asset`
- **关联场景**：`PCGTest`（PCG 测试）、`FlowTest`（流程测试，待创建）

## 当前状态与待办

| 组件 | 状态 |
|------|------|
| RunManager 状态机 | 已改为开放地图制（Exploring + BossFight） |
| GameHUDWindow 打开链路 | 完成（本地玩家初始化后打开，不依赖 RunState） |
| MonsterSpawnManager | 完整（环境刷怪 + 难度等级接入，`_difficultyTier` → 怪物等级） |
| LobbyManager | P0 占位（自动跳过） |
| HeroSelector | P0 占位（默认英雄） |
| RunSummaryCalculator | 基本实现 |
| ~~RoomCombatController~~ | 已删除（开放地图不需要房间制战斗） |
| ~~PathChoiceManager~~ | 已删除（所有通路开放，无需选路） |


## 2026-06-30 RunResult UI 接入记录

- `RunResultUIBridge` 在收到 `RunSummaryReady` 后按 `RunResultEvt.IsVictory` 弹出 `GameVictoryWindow` 或 `GameFailedWindow`，不再弹通用 `RunResultWindow`。
- 胜利链路为 Boss 死亡触发 `UnitDied`，Boss 主线任务完成后触发 `BossDefeated`，再由 `RunManager` 进入 `RunVictory/RunSummary`。
- 需要人工确认：`WindowConfig` 在 Unity Editor 中已有 UI 窗口配置，并且非 Editor 运行时能正确加载 `GameVictoryWindow` / `GameFailedWindow` 对应资源。

## 2026-07-01 对局结果客户端分发修复

- `RunManager` 新增结果事件发布入口，统一发布 `RunVictory` / `RunDefeat` / `RunSummaryReady`。
- 服务端本地 `EventCenter` 事件保留，用于 Host；远端客户端通过 `ClientRpc` 收到相同 `RunResultEvt`，再由本地 `RunResultUIBridge` 弹出胜利或失败窗口。
- 此修复不改变 `UnitDied` / `BossDefeated` 的权威判定，仍由服务端订阅并推进 Run 状态。
