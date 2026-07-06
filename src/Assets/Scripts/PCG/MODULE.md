# PCG 程序化地图生成

## 模块定位

Roguelike 地图的程序化生成系统。使用确定性随机（DeterministicRandom）保证相同 Seed 产生相同地图。从 Profile（配置）→ Graph（拓扑）→ Role（角色分配）→ Instantiate（实例化）→ Stitch（拼接）→ SpawnPoints（收集），生成完整可玩地图。

## 文件结构

```
Assets/Scripts/PCG/
├── Config/                              # 配置层
│   ├── PcgGenerationProfile.cs          #   生成配置 ScriptableObject（房间池/规模/资源）
│   ├── PcgGenerationProfileRegistry.cs  #   StyleKey → Profile 的注册表，含可选地图预览图
│   └── PcgGeneratePackage.cs            #   外部调用封装（StyleKey + Seed + TaskInput）
├── Core/
│   └── DeterministicRandom.cs           #   确定性随机数（基于 seed 的可复现随机）
├── Data/                                # 数据模型
│   ├── RoomGraph.cs                     #   图数据结构（节点/边/PrimaryRing）
│   ├── PcgGenerationModels.cs           #   请求与结果模型
│   ├── PcgTaskModels.cs                 #   任务类型枚举（RoomRole/PrimaryTaskType/SideTaskType）
│   ├── PcgRequestCloneUtility.cs        #   请求深拷贝工具
├── Generation/                          # 生成管线
│   ├── PcgMapGenerator.cs               #   生成入口（MonoBehaviour；测试模式支持逐步生成）
│   ├── RoomGraphBuilder.cs              #   图拓扑构建（环优先策略）
│   ├── RoomRoleAllocator.cs             #   房间角色分配（1435行，核心算法）
│   ├── RoomStitcher.cs                  #   房间物理拼接（连接器对齐）
│   ├── PcgGenerationFailureReporter.cs  #   生成失败诊断报告（JSON）
│   └── PcgGenerationResult.cs           #   生成结果数据
├── Navigation/                          # NavMesh 集成
│   ├── PcgNavMeshAssembler.cs           #   运行时 NavMesh 拼合
│   ├── RoomPrebakedNavMeshAsset.cs      #   预烘焙 NavMesh 资源
│   └── Editor/RoomNavMeshBakeEditor.cs  #   NavMesh 烘焙编辑器
├── Rooms/                               # 房间预制体标记组件
│   ├── PcgRoomRoot.cs                   #   房间根节点（ConnectorMarker/功能节点引用）
│   ├── PcgRoomBounds.cs                 #   房间包围盒
│   ├── PcgConnectorMarker.cs            #   连接器标记（门）
│   ├── PcgFunctionalNodes.cs            #   功能节点集合
│   └── Instances/                       #   房间内标记点
│       ├── PcgSpawnPointMarker.cs       #     普通刷怪点
│       ├── PcgBossSpawnPointMarker.cs   #     Boss 刷怪点
│       ├── PcgDefenseObjectivePointMarker.cs  #   防御目标点
│       └── PcgResourcePointMarker.cs    #     资源生成点
└── MODULE.md
```

## 生成管线（严格顺序）

```
PcgGeneratePackage (Caller: RunManager；MissionManager 提供 MapTaskInput 数据)
    │
    ▼
PcgMapGenerator.Generate(package)
    │
    ├─ 1. Resolve Profile
    │     profileRegistry.TryGetProfile(StyleKey) → PcgGenerationProfile
    │     profile.CreateRuntimeStyleOptions() → PcgStyleOptions
    │
    ├─ 2. Build Request
    │     BuildRequest(styleOptions, seed, taskInput) → MapGenerationRequest
    │     NormalizeRequest() → 钳制/补全默认值 → ComputeTargetRoomCount() 计算 seed 随机房间数
    │     ValidateRequest() → 检查 RoomPrefabPools 非空
    │
    ├─ 3. [Outer Loop: Budget 降级] originalTargetRooms → degradeStep(4) → minTargetRooms(12)
    │     │
    │     ├─ 4. [Inner Loop: maxStitchAttempts 次 Graph Variant]
    │     │     │
    │     │     ├─ 4a. Build Graph (RoomGraphBuilder.Build)
    │     │     │     每次 variant 重新生成 graph，seed = DeriveSeed(fixedSeed, 0, budgetAttempt, graphAttempt)
    │     │     │     输入：request (含当前降级 TargetRoomCount) + deterministic random
    │     │     │     输出：RoomGraph (节点网格 + 边 + PrimaryRing)
    │     │     │
    │     │     ├─ 4b. Assign Roles (RoomRoleAllocator.AssignRoles)
    │     │     │     每次 graph 变体重新分配角色
    │     │     │     输入：graph + taskInput + deterministic random (seed = DeriveSeed(fixedSeed, 1, ...))
    │     │     │     输出：graph 每个节点的 AssignedRole + TaskTriggerConnections
    │     │     │
    │     │     ├─ 4c. Instantiate Rooms
    │     │     │     遍历 graph 每个节点 → SelectRoomPrefab(role, degree) → Instantiate
    │     │     │     若 AssignedRole 与 Prefab 的 DefaultRole 不一致，仅记录 warning；运行时任务语义以 graph role 为准
    │     │     │     → 记录 PcgPlacedRoom (stitchRng = DeriveSeed(fixedSeed, 2, ...))
    │     │     │
    │     │     ├─ 4d. Stitch (RoomStitcher.Stitch)
    │     │     │     BFS 从 Start 房间开始 → 匹配 ConnectorMarker → 对齐位置
    │     │     │     → 成功则跳到 5，失败则下一个 graph variant
    │     │     │
    │     │     └─ 4e. 若 maxStitchAttempts 个 graph variant 均失败 → 降级 TargetRoomCount → 回到 3
    │     │
    │     └─ 若降至 minTargetRooms 仍未成功 → 整体失败返回 null
    │
    ├─ 5. CollectSpawnPoints
    │     扫描所有房间的 SpawnPoints/BossSpawnPoints/DefensePoints
    │
    ├─ 6. SpawnResources (可选)
    │     按 ResourceSpawnOptions 权重随机 → 实例化资源预制体
    │
    └─ 7. Fire OnGenerationCompleted + 记录元数据 (RequestedTargetRooms / FinalTargetRooms / BudgetAttempt / GraphAttempt)
```

## 核心类

### PcgMapGenerator (MonoBehaviour)

`PcgMapGenerator.cs:12` — **PCG 系统统一入口**。在场景中挂载，由 RunManager 或测试模式驱动。

**公开属性：**

| 属性 | 类型 | 用途 |
|------|------|------|
| `DefaultProfile` | `PcgGenerationProfile` | 默认生成配置 |
| `LastResult` | `PcgMapGenerationResult` | 最近一次生成结果 |
| `CurrentSeed` | `int` | 最近一次使用的 seed |

**公开方法：**

| 方法 | 用途 |
|------|------|
| `Generate(PcgGeneratePackage)` | **主入口**：外部系统（RunManager）调用 |
| `Generate(MapGenerationRequest)` | 兼容旧接口 |
| `GenerateTest()` | 编辑器测试模式；Play Mode 下可通过 `testStepGeneration` 逐步生成 |
| `GenerateWithDefaultProfile()` | 编辑器右键快速测试 |

**事件：** `OnGenerationCompleted(PcgMapGenerationResult)` — RunManager 订阅此事件推进对局流程。

**关键设计：**
- `maxStitchAttempts`（默认 4 次）：每个 budget 级别内尝试的 graph variant 数。每次 variant 重新生成 graph + 角色分配 + 实例化 + 拼接，而非仅重试 stitch
- `DeriveSeed(baseSeed, substream, budgetAttempt, graphAttempt)`：通过 XOR 派生确定性子种子，graph/role/stitch/resource 对应 substream 0/1/2/3，budgetAttempt 和 graphAttempt 保证每次 variant 独立但可复现
- `ComputeTargetRoomCount`：根据唯一 Prefab 数和 seed 确定性计算原始目标房间数（`[2×prefabCount, 50]`）
- **Budget 降级**：若原始 TargetRoomCount 的所有 graph variant 均拼合失败，按 `degradeStep=4` 递减，下限 `minTargetRooms=12`，保证确定性和可复现
- **测试逐步生成**：`testStepGeneration` 仅影响 `GenerateTest()` 在 Play Mode 下的测试入口。开启后使用 Coroutine 在 Request、Graph、Role、隐藏准备房间、Stitch、从 StartRoom 按 BFS 顺序逐个显示房间、SpawnPoints/Resources 等阶段之间按 `testStepDelaySeconds` 暂停；正式运行时的 `Generate(PcgGeneratePackage)` / `Generate(MapGenerationRequest)` 仍保持同步返回。该模式只改变测试展示顺序，不改变 Graph/Role/Stitch 的确定性算法。
- **失败诊断报告**：若所有 budget 降级与 graph variant 均 Stitch 失败，`writeFailureDiagnostics` 会将 JSON 报告写入 `Application.persistentDataPath/PCGFailureReports`（可通过 `failureDiagnosticsDirectory` 覆盖）。报告包含 RequestSource、Seed、TargetRooms、Graph/Role/Stitch 派生 seed、逻辑图节点/边、角色分配、实例化房间、未放置节点、未解析连接以及 Stitch 失败原因，便于后续按 seed/profile 复现问题。
- `SelectRoomPrefab`：按 Role + 连接器数量筛选候选项 → 随机选一；缺少同角色候选时可回退 Connector。运行时任务语义仍以 `RoomGraphNode.AssignedRole` / `PcgPlacedRoom.Role` 为准，若 Prefab 的 `PcgRoomRoot.DefaultRole` 不一致会输出 warning，提示需要人工补齐对应角色房间 Prefab 或确认通用房间承载语义

**需要人工确认：**
- 如需观察逐步生成，需要在 `PCGTest.unity` 或测试场景中的 `PcgMapGenerator` Inspector 勾选 `useTestMode`、配置 `testStyleKey`，并勾选 `testStepGeneration` 后进入 Play Mode；`testStepDelaySeconds` 控制每个可见步骤之间的等待时间。

### RoomGraph (数据模型)

`RoomGraph.cs:43` — 地图拓扑的图数据结构。

| 成员 | 类型 | 用途 |
|------|------|------|
| `Nodes` | `IReadOnlyList<RoomGraphNode>` | 节点（Id, GridPosition, AssignedRole） |
| `Edges` | `IReadOnlyList<RoomGraphEdge>` | 边（NodeA, NodeB, IsLoopEdge） |
| `PrimaryRingNodeIds` | `IReadOnlyList<int>` | 主环路节点 ID |
| `AddNode(pos)` / `AddEdge(a,b,isLoop)` | — | 构建方法 |
| `GetNeighborsSorted(id)` | `List<int>` | 获取排序后的邻居列表 |
| `GetDegree(id)` | `int` | 获取节点度数 |
| `HasEdge(a,b)` | `bool` | 查边存在性 |

**RoomGraphNode**：`Id` / `GridPosition` / `AssignedRole` / `HasAssignedSideTask` / `AssignedSideTask`

**RoomGraphEdge**：`NodeA` / `NodeB` / `IsLoopEdge`

### RoomGraphBuilder

`RoomGraphBuilder.cs:11` — **环优先拓扑构建**。静态工具类。

算法策略：主环路 → 主路分支 → 次级分支 → 局部环路。

### RoomRoleAllocator

`RoomRoleAllocator.cs:16` — **房间角色分配**（详见 [RoomRoleAllocator功能解读](./RoomRoleAllocator功能解读.md)）。

静态工具类，核心方法 `AssignRoles()`：
1. 解析 PrimaryRing 节点（优先从 graph.PrimaryRingNodeIds，fallback 到 2-core 算法）
2. 环上选择 Start 节点（极端方位 + 评分）
3. BFS 计算距离 → 选择 OppositeRing 节点
4. 在 85%-95% 主路径深度选择 Boss 节点
5. 在 40%-60% 深度、空间两侧分配 SideTask 房间。若严格约束无解，则以放宽深度范围至 [0.20, 0.80] 且不要求侧向偏移的 relaxed 模式重试，确保任务角色必被分配
6. 在环拐角处分配 Shop 房间
7. 生成 TaskTriggerConnection 供 MissionManager 使用

若 PrimaryRing 不足 4 个节点则走 LinearFallback 策略。

### RoomStitcher

`RoomStitcher.cs:6` — **房间物理拼接**。静态工具类。

核心方法 `Stitch()`：
- BFS 从 Start 房间遍历图
- 匹配相邻房间的 `PcgConnectorMarker`（门）：
  - 得分系统（对齐方向 + 距离 + 朝向）
  - 创建 GameObject 连接点对齐两个房间
- `PcgConnectorMarker` 的法线轴优先来自门上 `BoxCollider` 的几何平面；`socketFrame` 仅作为平面外参考点选择法线正反面，不直接决定任意方向向量
- 树边拼接以 socket 对齐为准；重叠检测会忽略当前正在连接的 anchor 房间，只检查目标房间是否撞到其他已放置房间，避免合法门缝被整房间 AABB 误判为阻塞
- socket 对齐后会读取两侧 connector 的 `BoxCollider` 外侧厚度，将尚未放置的 target 房间沿连接法线小幅推开，避免两套门框紧密嵌合；该推开有上限，防止重新产生长桥
- 处理 unused exits（可选封门）
- 记录 `PcgRoomConnection` 和 `PcgClosedDoorRecord`

如果任何一对房间拼接失败则整个 Stitch 返回 false，生成器会重试。

逐步测试模式使用 `StitchStepwise()`，但成功条件仍以所有 `PcgPlacedRoom.IsPhysicallyPlaced == true` 为准；若只放置了 StartRoom 或部分房间，测试模式不会触发 `OnGenerationCompleted`。

### DeterministicRandom

`DeterministicRandom.cs:7` — **确定性随机数结构体**。基于 .NET `System.Random`，seed 相同则序列相同。

| 方法 | 用途 |
|------|------|
| `NextInt(max)` | [0, max) 整数 |
| `NextInt(min, max)` | [min, max) 整数 |
| `NextFloat01()` | [0, 1) 浮点 |
| `Chance(p)` | 按概率返回 true |

用于保证相同 Seed 生成相同地图。

### Rooms 标记组件

| 组件 | 职责 |
|------|------|
| `PcgRoomRoot` | 房间根节点，提供 ConnectorMarker/SpawnPoints/BossSpawnPoints/DefensePoints/ResourcePoints 列表 |
| `PcgRoomBounds` | 房间包围盒（`TryGetWorldBounds()`），暴露 `BoundsCollider` 供 MissionSystem 复用为进房触发区，并通过 `TryGetWorldFootprintCorners()` 向小地图提供带旋转的地面 footprint |
| `PcgConnectorMarker` | 门/连接器，标记 IsOutgoing/Size；若配置 `socketFrame`，运行时用它选择 BoxCollider 几何法线的外侧方向，并可提供 connector collider 的外侧厚度用于门框分离 |
| `PcgFunctionalNodes` | 功能节点集合 |

### PcgNavMeshAssembler

`Navigation/PcgNavMeshAssembler.cs` — 地图拼接完成后加载各房间的预烘焙 `NavMeshData`，并在房间连接处创建运行时 `NavMeshLink`。

运行时 Link 契约：

- 房间 `RoomPrebakedNavMeshAsset.AgentTypeId`、Assembler 的 `expectedAgentTypeId`、运行时 `NavMeshLink.agentTypeID` 必须一致
- Link 端点不会直接从重合的门缝中心采样，而是沿各连接器法线向对应房间内部缩进 `linkEndpointInset` 后采样
- 所有端点与刷怪点采样使用带 `agentTypeID` 的 `NavMeshQueryFilter`，避免吸附到其他 Agent Type 的 NavMesh
- 若端点吸附到门缝错误一侧，或吸附后 Link 长度小于 `minimumLinkLength`，该连接会被拒绝并输出警告
- `socketOffset` 可以继续用于房间拼接语义；运行时 NavMesh 端点分离由 Assembler 独立处理，不要求修改房间 Prefab

### RoomNavMeshBakeEditor 与小地图

`Navigation/Editor/RoomNavMeshBakeEditor.cs` 是当前房间 NavMesh 预烘焙的正式入口：

- 菜单入口：`Tools > PCG NavMesh > Bake Selected Prefab` / `Bake All PCG Room Prefabs`
- 输出：`Resources/Data/PrebakedNavMeshAssets/Rooms/` 下的 `NavMeshData`
- 回填：房间 Prefab 上的 `RoomPrebakedNavMeshAsset.navMeshData`

小地图缩略图不再复用旧 `NavMeshPreBaker` 的碰撞体提取逻辑，而是读取 `RoomPrebakedNavMeshAsset.NavMeshData` 生成 `PcgRoomRoot.minimapThumbnail`。运行时小地图贴图使用 `PcgRoomBounds.TryGetWorldFootprintCorners()` 按 Stitch 后的房间 Transform 进行旋转/位移映射；门口连接区域由 `PcgRoomConnection` / `PcgConnectorMarker` 在运行时补面，避免单房间 NavMesh 在门缝处断开造成小地图视觉断裂。

## 依赖关系

| 依赖模块 | 用途 |
|----------|------|
| `UnityEngine` | Transform, Instantiate, GameObject, Bounds |
| `DeterministicRandom` (内部) | 确定性随机 |
| `RoomGraph` (内部) | 图数据模型 |
| `PcgGenerationProfile` / `PcgGenerationProfileRegistry` (内部) | 配置系统 |

### PcgGenerationProfileRegistry

`PcgGenerationProfileRegistry.Entry` 现在除 `StyleKey` / `Profile` 外，还提供可选 `PreviewSprite` 字段，供 `MissionSelectWindow` 和 `LobbyWindow` 展示地图预览。该字段只作为局外 UI 入口，不参与 PCG 生成确定性。

需要人工确认：`PcgGenerationProfileRegistry.asset` 中各 Entry 的 `PreviewSprite` 需要在 Unity Inspector 中手动绑定；为空时 UI 会保留现有占位图。

**被依赖模块：**
- `RunSystem` (RunManager 作为主调用方)
- `MissionSystem` (MissionManager 提供 TaskInput)
- `Framework.AI.Navigation` (PCGMapTopologyProvider, NavMesh 集成)

## 关联资源

- **ScriptableObject**：`Resources/Configs/PCGCongfig/` 下的 Profile 配置
- **Prefab**：`Resources/Prefab/Rooms/` 下的 6 类房间预制体（Birth/Boss/Connector/Defence/Elimination/Shop）
- **NavMesh**：`Resources/Data/PrebakedNavMeshAssets/Rooms/` 预烘焙 NavMesh
- **Scene**：`PCGTest.unity`（PCG 测试场景）

## 关键契约

1. **IPCGMapTopologyProvider** 接口（`Framework/AI/Navigation/IPCGMapTopologyProvider.cs`）— PCG 生成完成后向 AI 系统提供地图拓扑信息
2. **PcgGeneratePackage → MapGenerationRequest** — RunManager/MissionManager 通过 Package 传递意图，Generator 解析为内部 Request
3. **OnGenerationCompleted event** — RunManager 订阅此事件接收 `PcgMapGenerationResult`，继续对局流程
4. **Seed 确定性** — 通过 `DeriveSeed(baseSeed, substream, budgetAttempt, graphAttempt)` 保证各阶段（Graph/Role/Stitch/Resource）使用独立但可复现的随机序列。同一 FixedSeed + 同一 Profile + 同一 Prefab + 同一 retry/降级规则下，所有客户端生成同一张地图
5. **房间 BoundCollider 复用** — `PcgRoomBounds.boundsCollider` 同时作为房间包围盒来源和 MissionSystem 的进房触发区来源。需要人工确认：可承载任务的房间 Prefab 必须绑定该 BoxCollider，且 collider 所在 GameObject 在运行时保持激活。
