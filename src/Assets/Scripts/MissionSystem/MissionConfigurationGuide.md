# 五类任务配置手册

本文说明 `MissionConfig`、`MissionLibrary`、任务相关 Prefab 和道具 SO 的配置方式，覆盖当前 MissionSystem 支持的五类任务：

- `Boss`
- `Eliminate`
- `Defense`
- `Capture`
- `Destroy`

## 1. 资源放置约定

### 1.1 MissionConfig / MissionLibrary

任务配置统一放在：

```text
Assets/Resources/Configs/Missions
├── Libraries
│   └── MissionLibrary_Default.asset
├── Primary
│   └── Boss
│       └── Mission_Boss_001_Guardian.asset
└── Secondary
    ├── Eliminate
    │   └── Mission_Eliminate_001_Sweep.asset
    ├── Defense
    │   └── Mission_Defense_001_Core.asset
    ├── Capture
    │   └── Mission_Capture_001_Relay.asset
    └── Destroy
        └── Mission_Destroy_001_Reactor.asset
```

命名建议：

- `MissionLibrary_<用途>.asset`
- `Mission_<Type>_<三位编号>_<语义名>.asset`
- `Type` 固定使用 `Boss / Eliminate / Defense / Capture / Destroy`

### 1.2 敌人 Prefab

敌人 Prefab 放在：

```text
Assets/Resources/Prefab/Enemy
├── Boss
│   └── Boss.prefab
├── Normal
│   ├── 001.prefab
│   └── 002.prefab
└── Elite
```

`MissionSpawnEntry.EnemyPrefabAddress` 不是完整路径，而是 `Resources/Prefab/Enemy/` 后面的相对地址。

示例：

| 实际 Prefab | 配置里填写 |
|---|---|
| `Assets/Resources/Prefab/Enemy/Boss/Boss.prefab` | `Boss/Boss` |
| `Assets/Resources/Prefab/Enemy/Normal/001.prefab` | `Normal/001` |
| `Assets/Resources/Prefab/Enemy/Normal/002.prefab` | `Normal/002` |

普通敌人 Prefab 至少需要：

- `NetworkObject`
- `EnemyNetworkProxy`
- `EnemyActor`
- `ServerEnemyAttributeModule`
- `EnemyNavAgentController`
- `ServerCombatModule`
- `ServerWeaponRuntime`
- 可被射线命中的 Collider

如果敌人要攻击 Defense 目标，还需要确认 `ServerWeaponRuntime` 已绑定可用 `WeaponSO`，AI 配置中的感知距离、攻击距离和开火参数合理。

Boss Prefab 至少需要：

- `NetworkObject`
- `BossNetworkProxy`
- `BossActor`
- `ServerBossAttributeModule`
- Behavior Designer `BehaviorTree`
- Collider / NavMeshAgent / 表现层组件

Boss 的 AI 不使用 `AiConfigPath`，由 Prefab 内行为树驱动。

### 1.3 AI Config

AI 配置放在：

```text
Assets/Resources/Configs/AI
└── EnemyAI_Default.asset
```

`MissionSpawnEntry.AiConfigPath` 也是 Resources 相对路径，示例：

```text
Configs/AI/EnemyAI_Default
```

如果某个敌人需要独立 AI 参数，可新增：

```text
Assets/Resources/Configs/AI/Enemy_Melee.asset
```

并在配置里填写：

```text
Configs/AI/Enemy_Melee
```

### 1.4 任务目标 Prefab

正式任务目标建议放在：

```text
Assets/Resources/Prefab/Missions
├── Defense
│   └── DefenseObjective_Core.prefab
├── Capture
│   ├── CaptureTarget_Relay.prefab
│   └── Pickups
│       └── CapturePickup_RelayCore.prefab
└── Destroy
    └── DestroyTarget_Reactor.prefab
```

这些目录目前可按需新建。`MissionConfig.objectivePrefab`、`capturePickupPrefab`、`destroyRounds.TargetPrefab` 是直接拖拽 GameObject Prefab 引用，不要求通过 `Resources.Load` 加载，但所有会被网络生成的 Prefab 都必须注册到 Netcode `NetworkPrefabs`。

### 1.5 Capture / Reward 道具 SO

`captureItemId` 和 `MissionRewardEntry.RewardId` 不是资源路径，而是 `BaseSO.id`。

运行时 `SOManager` 从以下目录加载：

```text
Assets/Resources/Data/SO
```

因此 Capture 掉落物和任务奖励用到的道具 SO 需要放在 `Assets/Resources/Data/SO/...` 下，并保证 `BaseSO.id` 与配置一致。

示例：

```text
captureItemId = Capture/RelayCore
```

则必须存在一个继承 `BaseInventoryItemSO` 的道具 SO，且：

```text
id = Capture/RelayCore
```

## 2. MissionConfig 通用字段

以下字段五类任务都会用到：

| 字段 | 写法 |
|---|---|
| `missionId` | 全局唯一，建议 `mission_<type>_<编号>_<语义>` |
| `displayName` | HUD/选择界面显示名 |
| `description` | 策划说明文本 |
| `missionType` | `Boss / Eliminate / Defense / Capture / Destroy` |
| `missionCategory` | Boss 必须 `Primary`，其余通常 `Secondary` |
| `externalTaskId` | 外部任务源 ID；没有外部源也建议填稳定值 |
| `triggerOnRoomEnter` | 通常为 `true` |
| `triggerHeight` | 历史字段；当前默认进房范围以任务房 `PcgRoomBounds.boundsCollider` 为准 |
| `pointerLabel` | 屏幕边缘任务指引文本 |
| `currencyReward` | 任务完成后发给所有已连接玩家的局内货币 |
| `rewards` | 可选物品奖励列表，`RewardId` 填 `BaseSO.id` |

需要人工确认：所有可能承载任务的 PCG 房间 Prefab 都必须配置 `PcgRoomBounds.boundsCollider`，并让该 collider 覆盖玩家进入任务房的区域。运行时 `MissionTriggerZone` 会挂到该 collider 上并设置 `isTrigger=true`。

三种常见引用写法必须区分：

| 字段类型 | 示例 | 含义 |
|---|---|---|
| Prefab 引用 | 拖 `DefenseObjective_Core.prefab` | Unity 对象引用 |
| Resources 地址 | `Normal/001` | 敌人地址，会拼成 `Prefab/Enemy/Normal/001` |
| 业务 ID | `Capture/RelayCore` | `SOManager.GetSOById<BaseInventoryItemSO>()` 查找 |

## 3. Boss 配置

Boss 是唯一主线任务。

推荐位置：

```text
Assets/Resources/Configs/Missions/Primary/Boss/Mission_Boss_001_Guardian.asset
```

字段示例：

```text
missionId: mission_boss_001_guardian
displayName: 击败守门者
missionType: Boss
missionCategory: Primary
externalTaskId: main_boss_guardian
triggerOnRoomEnter: true
triggerHeight: 8  # 历史字段，当前触发范围以房间 BoundCollider 为准
pointerLabel: 前往 Boss 房

spawnEntries:
  - EnemyPrefabAddress: Boss/Boss
    Count: 1
    AiConfigPath:

killTargetCount: 1
currencyReward: 1000
```

不需要填写：

- `objectivePrefab`
- `capturePickupPrefab`
- `captureItemId`
- `destroyRounds`

需要人工确认：

- Boss Prefab 已注册到 NetworkPrefabs。
- Boss 房间 Prefab 有 `BossSpawnPoints`，否则会在房间根节点生成。
- Boss 的 BehaviorTree 黑板变量和 `BossNetworkProxy` 运行时绑定正常。

## 4. Eliminate 配置

Eliminate 是歼灭支线，进入任务房后不再负责刷怪，只追踪全图敌人死亡数量。敌人生成由房间规则、`MonsterSpawnManager` 或其他对局刷怪系统提供。

推荐位置：

```text
Assets/Resources/Configs/Missions/Secondary/Eliminate/Mission_Eliminate_001_Sweep.asset
```

字段示例：

```text
missionId: mission_eliminate_001_sweep
displayName: 清理敌群
missionType: Eliminate
missionCategory: Secondary
externalTaskId: side_eliminate_sweep
triggerOnRoomEnter: true
triggerHeight: 6  # 历史字段，当前触发范围以房间 BoundCollider 为准
pointerLabel: 前往歼灭房

killTargetCount: 10
currencyReward: 1000
```

`killTargetCount` 是本任务唯一需要的击杀目标数；`spawnEntries` 对 Eliminate 不再生效，建议保持为空，避免策划误以为该任务会主动刷怪。

需要人工确认：

- 敌人 Prefab 死亡时能触发 `UnitDied`。
- 死亡对象可被识别为 `EnemyNetworkProxy`、`BossNetworkProxy` 或带有 `ServerEnemyAttributeModule` 的敌方网络对象。
- 房间或全局刷怪系统能在任务激活后持续提供敌人，否则击杀计数不会推进。

## 5. Defense 配置

Defense 是防守支线，进入房间后生成一个可被 AI 攻击的目标，目标存活到倒计时结束则完成，目标死亡则失败。

推荐位置：

```text
Assets/Resources/Configs/Missions/Secondary/Defense/Mission_Defense_001_Core.asset
```

Defense 目标 Prefab 推荐位置：

```text
Assets/Resources/Prefab/Missions/Defense/DefenseObjective_Core.prefab
```

Defense 目标 Prefab 至少需要：

- `NetworkObject`
- `DefenseObjective`
- 可被射线命中的 Collider
- 可选表现层 Mesh / VFX / Audio

字段示例：

```text
missionId: mission_defense_001_core
displayName: 守住能源核心
missionType: Defense
missionCategory: Secondary
externalTaskId: side_defense_core
triggerOnRoomEnter: true
triggerHeight: 6  # 历史字段，当前触发范围以房间 BoundCollider 为准
pointerLabel: 前往防守房

objectivePrefab: DefenseObjective_Core.prefab
defenseDurationSeconds: 75
defenseObjectiveMaxHealth: 500
defenseObjectiveShield: 100
defenseObjectiveThreatPriority: 100

currencyReward: 1000
```

当前 `DefenseMission` 不读取 `spawnEntries`，攻击压力来自房间/对局中已有的敌人生成逻辑。若希望防守房主动刷敌，需要额外接入刷怪逻辑或由 `MonsterSpawnManager` / 房间规则提供敌人。

HUD 任务框中的进度文本会直接读取剩余守护秒数，显示为 `还需守护 1分15秒` / `还需守护 45秒`。因此 `defenseDurationSeconds` 既是玩法倒计时，也是 HUD 状态提示的初始目标进度。

需要人工确认：

- Defense 房间 Prefab 上有 `PcgDefenseObjectivePointMarker`；没有时会在房间根节点生成目标。
- `objectivePrefab` 已注册到 NetworkPrefabs。
- 敌人的 AI 可感知 `MissionTarget`，且武器配置允许打到该目标。
- `defenseObjectiveThreatPriority` 越高，敌人越倾向于锁定防守目标。

## 6. Capture 配置

Capture 当前语义是“击杀目标后掉落任务拾取物，玩家按 F 拾取后完成”。

推荐位置：

```text
Assets/Resources/Configs/Missions/Secondary/Capture/Mission_Capture_001_Relay.asset
```

Capture 拾取物 Prefab 推荐位置：

```text
Assets/Resources/Prefab/Missions/Capture/Pickups/CapturePickup_RelayCore.prefab
```

Capture 拾取物 Prefab 至少需要：

- `NetworkObject`
- `PickupItem`
- 触发用 Collider，通常 `SphereCollider.isTrigger = true`
- 可选表现层 Mesh / VFX / Audio

字段示例，使用敌人作为 Capture 目标：

```text
missionId: mission_capture_001_relay
displayName: 回收中继核心
missionType: Capture
missionCategory: Secondary
externalTaskId: side_capture_relay
triggerOnRoomEnter: true
triggerHeight: 6  # 历史字段，当前触发范围以房间 BoundCollider 为准
pointerLabel: 前往捕获房

spawnEntries:
  - EnemyPrefabAddress: Normal/001
    Count: 1
    AiConfigPath: Configs/AI/EnemyAI_Default

captureRequiredProgress: 1
capturePickupPrefab: CapturePickup_RelayCore.prefab
captureItemId: Capture/RelayCore
captureItemAmount: 1
capturePickupPrompt: 按 F 拾取

currencyReward: 1000
```

也可以不用敌人，而是用 `objectivePrefab` 作为 Capture 目标。此时目标 Prefab 至少需要：

- `NetworkObject`
- 可被射线命中的 Collider
- `MissionDamageableTarget`，或完整的 `NetworkProxyBase + ServerAttributeModule` 死亡链路

需要人工确认：

- `captureItemId` 对应的道具 SO 存在于 `Assets/Resources/Data/SO/...`，并继承 `BaseInventoryItemSO`。
- `capturePickupPrefab` 已注册到 NetworkPrefabs。
- 玩家 Prefab 不必手动挂 `InteractionDetector`，当前由 `PlayerNetworkProxy` 在本地 Owner 运行时补齐。
- Capture 目标死亡后必须触发 `UnitDied`，否则不会生成拾取物。

## 7. Destroy 配置

Destroy 是多轮破坏支线，每轮生成若干破坏目标，全部摧毁后进入下一轮，所有轮次完成后任务完成。

推荐位置：

```text
Assets/Resources/Configs/Missions/Secondary/Destroy/Mission_Destroy_001_Reactor.asset
```

Destroy 目标 Prefab 推荐位置：

```text
Assets/Resources/Prefab/Missions/Destroy/DestroyTarget_Reactor.prefab
```

轻量 Destroy 目标 Prefab 至少需要：

- `NetworkObject`
- `MissionDamageableTarget`
- 可被射线命中的 Collider
- 可选表现层 Mesh / VFX / Audio

也可以使用完整可死亡对象：

- `NetworkObject`
- `NetworkProxyBase`
- `ServerAttributeModule`
- Collider

字段示例：

```text
missionId: mission_destroy_001_reactor
displayName: 摧毁反应堆
missionType: Destroy
missionCategory: Secondary
externalTaskId: side_destroy_reactor
triggerOnRoomEnter: true
triggerHeight: 6  # 历史字段，当前触发范围以房间 BoundCollider 为准
pointerLabel: 前往破坏房

destroyRounds:
  - TargetPrefab: DestroyTarget_Reactor.prefab
    TargetCount: 2
    GoldReward: 60
  - TargetPrefab: DestroyTarget_Reactor.prefab
    TargetCount: 3
    GoldReward: 80

currencyReward: 1000
```

`GoldReward` 是每个目标被摧毁时的即时贡献奖励，按玩家造成伤害比例分配；`currencyReward` 是任务最终完成时发给所有已连接玩家的奖励。

需要人工确认：

- `TargetPrefab` 已注册到 NetworkPrefabs。
- 如果 `TargetPrefab` 使用 `MissionDamageableTarget`，生命值来自 Prefab 上的 `initialMaxHealth` / `initialShield` / `threatPriority`。
- 如果 `TargetPrefab` 留空，系统会生成运行时 Cube 兜底，但这只适合开发验证，不建议作为正式配置。
- Destroy 房间需要有 `NormalEnemy` 刷怪点；当前目标生成会复用该类点位，没有点位时退回房间根节点。

## 8. MissionLibrary 写法

`MissionLibrary` 放在：

```text
Assets/Resources/Configs/Missions/Libraries/MissionLibrary_Default.asset
```

`missions` 列表需要包含可抽取的全部 `MissionConfig`。

最小可用组合：

```text
missions:
  - Mission_Boss_001_Guardian
  - Mission_Eliminate_001_Sweep
  - Mission_Defense_001_Core
  - Mission_Capture_001_Relay
  - Mission_Destroy_001_Reactor
```

随机组队规则：

- 必须至少有 1 个 `Primary`。
- 必须至少有 2 个 `Secondary`。
- 默认每局抽 `1 主 + 2 支`。
- 支线优先避免重复 `MissionType`。

最后在场景中的 `MissionManager.missionLibrary` 绑定该 `MissionLibrary`。

## 9. 房间 Prefab 配置要求

任务房间由 PCG 根据任务类型绑定：

| MissionType | RoomRole |
|---|---|
| `Boss` | `Boss` |
| `Eliminate` | `SideElimination` |
| `Defense` | `SideDefense` |
| `Capture` | `SideCapture` |
| `Destroy` | `SideDestroy` |

房间 Prefab 需要按类型提供对应 Role，并包含必要点位：

| 任务 | 推荐点位 |
|---|---|
| Boss | `BossSpawnPoints` |
| Eliminate | 无硬性点位要求；击杀目标来自全图敌人死亡事件 |
| Defense | `PcgDefenseObjectivePointMarker` |
| Capture | `NormalEnemy` Spawn Points |
| Destroy | `NormalEnemy` Spawn Points |

如果 PCG 没有成功给任务绑定房间，对应任务不会创建运行时实例。

## 10. NetworkPrefabs 检查清单

以下 Prefab 如果会被运行时 `Spawn()`，必须注册到 Netcode NetworkPrefabs：

- 玩家 Prefab
- 普通敌人 Prefab
- Boss Prefab
- Defense 目标 Prefab
- Capture 目标 Prefab，如果使用 `objectivePrefab`
- Capture 拾取物 Prefab
- Destroy 目标 Prefab
- Projectile / Drop 等由其他系统网络生成的 Prefab

运行时自动补 `NetworkObject` 只是开发兜底，不能替代正式 NetworkPrefabs 注册。

## 11. 快速检查清单

配置一套任务时按以下顺序检查：

1. 创建或复制 5 个 `MissionConfig`，分别放入对应 `Primary/Boss` 和 `Secondary/*` 目录。
2. 确认 Boss 是 `Primary`，其余是 `Secondary`。
3. 确认 Boss / Capture 中使用的 `EnemyPrefabAddress` 能拼出真实 `Assets/Resources/Prefab/Enemy/<地址>.prefab`；Eliminate 不需要配置 `spawnEntries`。
4. 确认普通敌人使用的 `AiConfigPath` 能拼出真实 `Assets/Resources/<路径>.asset`。
5. 确认所有 `captureItemId` / `RewardId` 都能通过 `SOManager.GetSOById<BaseInventoryItemSO>()` 查到。
6. 确认任务目标和拾取物 Prefab 都已注册到 NetworkPrefabs。
7. 确认 PCG 房间池中有对应 `RoomRole` 的房间 Prefab 和必要点位。
8. 把所有任务配置加入 `MissionLibrary_Default.missions`。
9. 把 `MissionManager.missionLibrary` 指向该 Library。
10. 在 Unity Editor 中进入 Play Mode，依次验证 Boss、Eliminate、Defense、Capture、Destroy 的触发、进度、完成/失败和奖励。
