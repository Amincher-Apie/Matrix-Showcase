# SpawnSystem 怪物生成模块

## 模块定位

`Assets/Framework/LogicLayer/Module/SpawnSystem/` 负责开放地图制下的服务端持续刷怪。模块消费 PCG 生成结果中的逻辑房间图、房间刷怪点和当前玩家所在房间，按“跨区块兴趣点”权重在逻辑通路距离上分配刷怪预算。

该模块只负责怪物生成策略与运行时统计，不负责 PCG 地图生成、任务生命周期、敌人 prefab 具体实例化或对象池回收。

## 文件结构

```text
Assets/Framework/LogicLayer/Module/SpawnSystem/
├── MonsterSpawnManager.cs   # 服务端刷怪控制器
├── MonsterRegistry.cs       # 活跃怪物/总生成/击杀统计
├── RoomGraphExtensions.cs   # RoomGraph BFS 最短路径工具
├── SpawnPointData.cs        # PCG 刷怪点运行时包装
└── MODULE.md
```

相关轻量配置位于：

```text
Assets/Scripts/SpawnSystem/
├── IMonsterSpawnSystem.cs
└── MonsterSpawnConfig.cs
```

## 核心流程

1. `RunManager.OnMapGenerationCompleted()` 调用 `MonsterSpawnManager.InitializeWithMapResult()`，注入 `PcgMapGenerationResult` 与 `PcgGenerationProfile`。
2. `MonsterSpawnManager` 从 `result.SpawnPoints` 收集 `NormalEnemy` 刷怪点，并按 `NodeId` 建立区块到刷怪点的映射。
3. `FixedUpdate()` 中轮询当前任务状态、玩家数量、难度等级和玩家所在房间。
4. 每个 `spawnTickInterval` 触发一次刷怪预算分配。
5. 对每个候选房间，通过 `RoomGraph.GetShortestDistance(playerNode, targetNode)` 计算逻辑图最短通路距离。
6. 使用 `MonsterSpawnConfig.distance1Weight / distance2Weight / distance3Weight / distance4PlusWeight` 分配跨区块兴趣点权重。
7. 调用 `EnemySpawnService.SpawnEnemy()` 生成敌人，并通过 `MonsterRegistry` 记录活跃怪物。

## 逻辑通路刷怪契约

- 刷怪候选必须来自 PCG `RoomGraph` 的可达逻辑路径，而不是世界坐标或视觉距离。
- `RoomGraphExtensions.GetShortestDistance()` 使用 BFS 层级距离，返回值单位是图边数量。
- `distance <= 0` 的房间不会刷怪，避免在玩家当前房间直接刷怪。
- `int.MaxValue` 或不可达房间不会刷怪，即使物理位置很近也会被排除。
- `distance >= 4` 是否参与刷怪由 `MonsterSpawnConfig.distance4PlusWeight` 决定，但前提仍然是该房间在逻辑图上可达。
- 玩家所在房间优先用 `PcgRoomBounds.TryGetWorldFootprintCorners()` 的旋转 footprint 做 XZ 平面内检测；只有未命中任何 footprint 时才回退到 `TryGetWorldBounds()` 最近房间。

## Inspector 需要人工确认

以下字段依赖场景或 prefab 绑定，无法仅从 `.cs` 文件完全确认：

| 位置 | 字段 | 需要人工确认 |
|------|------|--------------|
| `MonsterSpawnManager` | `missionManager` | 运行场景中已指向当前 `MissionManager` |
| `MonsterSpawnManager` | `enemySpawnService` | 运行场景中已指向服务端敌人生成服务 |
| `MonsterSpawnManager` | `config` | 已绑定 `MonsterSpawnConfig`，并按设计配置距离权重 |
| `MonsterSpawnManager` | `monsterEnemyId` | Profile 没有可用敌人池时的回退敌人 ID 有效 |
| PCG 房间 prefab | `PcgRoomBounds` | 房间 footprint 覆盖真实可行走区域，避免玩家所在房间误判 |
| PCG 房间 prefab | `PcgSpawnPointMarker` | 普通刷怪点位于对应房间内且可被 NavMesh 采样 |

## 依赖关系

| 依赖 | 用途 |
|------|------|
| `Matrix.PCG.PcgMapGenerationResult` | 读取逻辑图、房间实例与刷怪点 |
| `Matrix.PCG.RoomGraph` | 计算玩家房间到候选房间的逻辑通路距离 |
| `Matrix.Missions.MissionManager` | 获取当前地图结果与任务活跃状态 |
| `EnemySpawnService` | 实例化敌人网络对象 |
| `MonsterSpawnConfig` | 控制刷怪间隔、上限、任务倍率和跨区块距离权重 |
| `EventCenter.UnitDied` | 监听怪物死亡并更新统计 |

## 设计边界

- 本模块是服务端运行逻辑，客户端不参与刷怪决策。
- `NetworkVariable` / `NetworkList` 不在本模块直接写入；敌人网络对象由 `EnemySpawnService` 和对象池系统负责。
- 本模块不修改 `.unity`、`.prefab`、`.asset` 资源；房间 footprint、刷怪点和配置权重需要在 Unity Editor 中人工检查。
