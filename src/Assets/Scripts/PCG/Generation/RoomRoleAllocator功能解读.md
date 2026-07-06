# RoomRoleAllocator 功能解读

> 文件：[RoomRoleAllocator.cs](./RoomRoleAllocator.cs) | 约 1435 行 | 静态工具类 `Matrix.PCG.RoomRoleAllocator`

## 整体定位

将 RoomGraphBuilder 生成的无语义拓扑图，按任务需求（MapTaskInput）和图结构特征，为每个节点分配游戏功能角色（RoomRole），并生成任务触发连接供 MissionManager 使用。

**输入**：`RoomGraph` + `MapTaskInput` + `DeterministicRandom`
**输出**：图中每个节点的 `AssignedRole` 被设置 + `TaskTriggerConnection` 列表

---

## 一、功能模块划分

### 模块 1：入口与策略选择 (行 16-51)

```csharp
public static void AssignRoles(graph, taskInput, ref random, taskTriggerConnections)
```

**逻辑**：
1. 重置所有节点的 Role 为 `Connector`
2. 解析 PrimaryRing 节点列表（≥4 个为有效环）
3. **分支决策**：PrimaryRing ≥ 4 → 走 **RingTopology 策略**；否则走 **LinearFallback 策略**

---

### 模块 2：PrimaryRing 解析 (行 800-917)

**方法**：`ResolvePrimaryRingNodes()` + `BuildTwoCore()`

**核心算法**：K-core 分解的 2-core 变体

1. 优先使用 `graph.PrimaryRingNodeIds`（由 RoomGraphBuilder 设置）
2. Fallback：运行 2-core 算法 — 反复删除度 ≤ 1 的节点，剩下即核心环路
3. 如果核心包含节点 0，BFS 遍历输出有序环

**用途**：找到图的主环路，作为 Start/Boss 位置和路径深度计算的参考基准。

---

### 模块 3：RingTopology 策略 (行 63-131)

**核心流程**（7 个步骤，每个步骤有独立的评分选择方法）：

```
SelectStartRingNode → SelectOppositeRingNode → SelectBossNodeForRing
→ AssignSideTasksForRing → AssignShopRoomsForRing
```

#### 3.1 Start 节点选择 (SelectStartRingNode, 行 133-195)

- 随机选一个方向（上下左右）
- 在该方向上取最极端的环节点作为候选
- **评分因素**：随机扰动(+0~23) + 低度数加分(+16 if degree≤3 else -12) + 少非环邻居加分(-14 per)

#### 3.2 OppositeRing 节点选择 (行 197-240)

- 计算环几何中心
- 选择与 Start 方向最相反（dot 最小）且距离最远的环节点
- **评分**：随机(+0~19) + 距离加分(+20 per dist) + 反方向加分(-dot×110)

#### 3.3 Boss 节点选择 (SelectBossNodeForRing, 行 242-312)

- 约束：Boss 必须在路径深度的 **85%~95%** 区间
- **评分**（环上节点）：Opposite 位置加分(+95)，远处扣分(-8 per dist from opposite)
- **评分**（分支节点）：分支深度加分(+52 per depth)，叶子节点加分(+30 if degree==1)
- 若最佳节点恰好是 Start → fallback 到 OppositeRing

#### 3.4 SideTask 分配 (AssignSideTasksForRing, 行 314-370)

**参数约束**（常量）：
- 深度区间：`SideTaskMinProgress=0.40` ~ `SideTaskMaxProgress=0.60`
- 横向偏移：`SideTaskMinLateralRatio=0.18` ~ `SideTaskMaxLateralRatio=0.85`

**流程**：
1. `BuildSideTaskSlots(count)` — 为每个 SideTask 生成目标槽位：
   - 1 个 → depth=0.50，左右随机
   - ≥2 个 → depth=0.50±jitter，左右分置（-1/+1）
2. `SelectSideTaskNodeForRing()` — 针对每个槽位评分选择：
   - 深度匹配（距目标 depth 越近分越高，×260 权重）
   - 横向偏移匹配（距 0.50 越近分越高，×80 权重）
   - 环上节点扣分(-110)，分支节点加分(+44)
   - 分支深度加分(+34 per ringDepth)
   - 叶子节点加分(+32 if degree==1)
   - 邻接环节点加分(+12)
   - 随机抖动(+0~25)
3. `MapSideTaskToRole()` — SideTaskType → RoomRole 映射：
   - `Defense` → `SideDefense`
   - `Capture` → `SideCapture`
   - `Destroy` → `SideDestroy`
   - `Elimination` → `SideElimination`（默认）

#### 3.5 Shop 分配 (AssignShopRoomsForRing, 行 544-660)

**数量**：`EstimateShopCount(nodeCount)` — <10 个房间→1 店，<18→2 店，≥18→3 店

**分布策略**：沿主路径在 42%~74% 深度均匀分布

**评分因素**（`SelectShopNodeForRing`）：
- 深度匹配(×220 权重)
- 环上节点加分(+86)，1 跳分支加分(+36)，更深分支扣分(-56)
- 环拐角加分(+24)
- 高度数加分(+20 if degree≥3)
- 距 Boss 太近扣分(-40 if dist<2)
- 随机(+0~29)

#### 3.6 TaskTriggerConnection 生成 (行 662-736)

为每个 SideTask 房间生成与相邻 Connector 节点的触发连接：
- 排除 LoopEdge
- 若唯一 Connector 邻居或 60% chance → `IsPrimaryTrigger=true`
- 输出到 `taskTriggerConnections` 列表供 MissionManager 使用

---

### 模块 4：LinearFallback 策略 (行 1100-1429)

当 PrimaryRing < 4 个节点时的降级方案。

**简化逻辑**：
- `SelectStartNodeLinear` → `SelectBossNodeLinear`（简单 BFS 距离取最远）→ `AssignSideTasksLinear` / `AssignShopRoomsLinear`
- 不做环几何分析，只基于 BFS 距离 depth ratio 分配

**核心方法**：
- `AssignRolesLinearFallback()` (行 1100)—— 主入口
- `SelectSideTaskNodeLinear()` (行 1227) —— 按深度±横向评分
- `SelectShopNodeLinear()` (行 1332) —— 按深度+连接性评分

---

### 模块 5：通用图算法工具 (行 919-1099)

| 方法 | 用途 |
|------|------|
| `CalculateShortestDistance()` | BFS 单源最短距离（同时输出 predecessor 数组） |
| `CalculateDistanceToSet()` | BFS 多源最短距离 |
| `FindMaxDistance()` | 找距离数组中最大值 |
| `FindFarthestNode()` | 找距离最远的节点 |
| `BuildShortestPath()` | 通过 predecessor 回溯路径 |
| `ComputeMainAxisDirection()` | 计算 Start→Boss 主轴方向 |
| `ComputeLateralRatio()` | 计算节点在主轴法线方向的横向偏移比 |
| `ResolveCenter()` | 计算环节点组的几何中心 |
| `IsRingCorner()` | 判断环上一个节点是否为拐角（邻居向量不共线） |
| `IsAdjacentToRing()` | 判断节点是否有环上邻居 |
| `CountNonRingNeighbors()` | 统计非环邻居数量 |

---

## 二、数据结构

### TaskTriggerConnection (行 1435)

```csharp
public struct TaskTriggerConnection
{
    public int TaskNodeId;        // 任务房间节点ID
    public RoomRole TaskRole;     // 任务房间角色
    public int ConnectedNodeId;   // 连接的 Connector 节点ID
    public bool IsPrimaryTrigger; // 是否为主要触发器
}
```

### SideTaskSlot (行 497)

```csharp
private struct SideTaskSlot
{
    public float TargetProgress;      // 目标深度比例 (0~1)
    public float TargetLateralSign;   // 目标横向方向 (-1左/+1右)
}
```

---

## 三、评分函数设计原则

所有选择方法遵循**数据驱动打分 + 随机扰动**模式：

```
score = Σ(几何特征 × 权重) + random(0, maxJitter)
best = argmax(score)
```

**常见特征**：深度比例、横向偏移、是否环上、分支深度、度数、邻居类型
**权重范围**：±10 ~ ±260（深度匹配权重最高）

这种设计保证：
- 同 Seed 同结果（确定性）
- 视觉多样性（随机扰动）
- 可调整（权重可改为配置参数）

---

## 四、常量参数汇总

| 常量 | 值 | 用途 |
|------|----|------|
| `BossMinProgress` | 0.85 | Boss 最小路径深度比 |
| `BossMaxProgress` | 0.95 | Boss 最大路径深度比 |
| `SideTaskMinProgress` | 0.40 | SideTask 最小深度比 |
| `SideTaskMaxProgress` | 0.60 | SideTask 最大深度比 |
| `SideTaskMinLateralRatio` | 0.18 | SideTask 最小横向偏移 |
| `SideTaskMaxLateralRatio` | 0.85 | SideTask 最大横向偏移 |

---

## 五、调用上下文

```
PcgMapGenerator.GenerateResolvedRequest()
    │
    ├─ RoomGraphBuilder.Build()           → 生成裸拓扑图 (节点+边+PrimaryRing)
    │
    └─ RoomRoleAllocator.AssignRoles()    → [本类] 为每个节点打角色标签
        │
        ├─ graph.GetNode(i).AssignedRole  被直接写入
        └─ taskTriggerConnections         输出给 PcgMapGenerationResult
            │
            └─ 最终被 MissionManager 使用，决定哪些任务在哪些房间触发
```
