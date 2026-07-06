# 敌人 AI 目标系统从 PlayerActor 泛化为 IAttackableObject

> 状态：Proposed / 待审核
> 日期：2026-06-29
> 关联模块：EnemyAIModule、PerceptionSystem、AttackState、ChaseState、AttackableObjectManager、MissionSystem
> 前置条件：防御任务/Destroy 任务需要非玩家可攻击目标

## 背景

当前敌人 AI 的目标系统全部硬编码为 `PlayerActor`：

- `EnemyAIModule._currentTarget` — 声明类型 `PlayerActor`
- `PerceptionSystem.DetectTarget()` — 返回值 `PlayerActor`，内部 `TryGetPlayerActor()` 强转过滤
- `AttackState.PerformAttack(PlayerActor target)` — 参数签名锁定
- ChaseState、PatrolState、IdleState 全部消费 `GetCurrentTarget()` 返回的 `PlayerActor`
- 仅为防御任务（Ally 阵营的防御目标）这类非玩家目标提供攻击能力，就需要动整个 AI 状态机

`AttackableObjectType` 枚举已预留 `MissionTarget`、`Building`、`EscortTarget` 位，但没有路径让这些类型进入敌人 AI 的决策管道。

## 决策

将敌人 AI 的目标系统从 `PlayerActor` 泛化为 `IAttackableObject` 接口。

具体变更：

1. `EnemyAIModule._currentTarget` 改为 `IAttackableObject`
2. `PerceptionSystem.DetectTarget()` 返回值改为 `IAttackableObject`，去掉 `TryGetPlayerActor` 类型过滤，只保留 `IsAliveForAI` 和距离/视线筛选
3. `AttackState` / `ChaseState` / `PatrolState` / `IdleState` 中消费目标的方法签名同步跟进
4. 移除 `TryGetPlayerActor` 方法，感知系统不再有类型偏见

## 替选方案

**A. 保留 PlayerActor 不改，防御目标实现为 PlayerActor 子类或冒充**
可以绕过类型系统，但会污染 PlayerActor 的语义（防御目标不应拥有技能、背包、品质效果），且后续每个新目标类型都需要侵入 PlayerActor 体系。

**B. 在 EnemyAIModule 中维护两份目标列表（PlayerActor + IAttackableObject）**
减少单次改动面，但引入双轨维护成本，且状态机转换逻辑翻倍。

**C. 泛化为 IAttackableObject（选定方案）**
改动集中、语义清晰、符合接口隔离原则。后续新增任何 `IAttackableObject` 实现（EscortTarget、Building）不需要改动 AI 核心链路。

## 影响

正面：
- 消除 AI 感知层的 PlayerActor 硬编码绑定，目标类型扩展不需要改 AI 管道
- 防御目标、护送目标、建筑等可直接走统一感知路径
- `AttackableObjectManager` 的预留枚举值变为可消费状态

代价：
- 所有引用 `GetCurrentTarget()` 的状态机和 AI 调度代码需要更新签名
- 现有代码中 `target as PlayerActor` 的隐式假设需要审计
- 回归测试覆盖：敌人仍能正确攻击玩家

## 实施建议

建议在防御任务 OnActivated 逻辑之前完成此改造。改造可增量执行：

1. 改 `_currentTarget` 类型 → 编译器错误清单暴露所有消费点
2. 逐个修改状态机文件中的签名和 `PlayerActor` 特定调用
3. 修改 `PerceptionSystem.DetectTarget()` 移除类型过滤
4. 写一个简易的 `IAttackableObject` 实现（如 `DebugTarget`）做冒烟测试
5. 确认原玩家追踪逻辑不变
