# HeroSO 英雄数据模块

## 1. 模块职责

HeroSO 用于描述一个可选英雄的静态配置，包括英雄 Prefab、初始属性配置、主动技能列表和被动能力列表。

本模块只负责数据声明，不负责玩家生成、技能结算、Buff 结算或网络同步。

## 2. 文件边界

| 文件 | 职责 |
|------|------|
| `HeroSO.cs` | 英雄模板 ScriptableObject，持有 Prefab、属性、主动技能、被动能力 |
| `SkillAbilityDef.cs` | 主动技能条目，持有 UI 描述与 `SkillDefinitionSO` 引用 |
| `PassiveAbilityDef.cs` | 被动能力条目，持有 UI 描述与 `PassiveExecutorSO` 引用 |

## 3. 主动技能配置

`HeroSO.skills` 按列表顺序填入 `PlayerSkillModule` 的技能槽。

主动技能不在 HeroSO 上直接引用 `MonoBehaviour` 执行器。执行逻辑由 `SkillDefinitionSO.executeHandler` 枚举指向 `SkillExecuteRegistry` 中注册的 `ISkillExecute`。

需要人工确认：

- `SkillDefinitionSO.executeHandler` 已选择正确枚举，例如 `BombardArea`、`PiercingShot`、`KineticBoost`。
- `SkillExecuteRegistry.EnsureInitialized()` 中已注册对应 ID 的执行器。
- HeroSO 的技能列表顺序符合 UI/输入槽位预期。

## 4. 被动能力配置

`HeroSO.passives` 使用 `PassiveExecutorSO` 资产引用。被动执行器资源需实现 `IPassiveExecutor`，并在 `PlayerActor.RegisterPassives()` / `UnregisterPassives()` 生命周期中执行注册与清理。

被动执行器不能配置为普通脚本文件或场景 `MonoBehaviour`。需要先通过对应执行器的 `CreateAssetMenu` 创建 `.asset` 资源，再拖入 HeroSO 的 `Passive Executor` 字段。

当前代表实现：

- `KineticBurstPassiveExecutor`：通过 `游戏配置/角色系统/被动执行器/动能爆发` 创建，周期性为玩家添加 `MoveSpeed` 百分比增益。

需要人工确认：

- HeroSO 被动列表中的 `Passive Executor` 已拖入具体 `PassiveExecutorSO` 资产。
- 被动执行器依赖的玩家 Prefab 上存在 `PlayerNetworkProxy`，因为部分被动需要借用它启动协程。
- Inspector 中未赋值的被动不会生效。

## 5. 运行时流程

```text
PlayerSpawnManager
    -> Instantiate HeroSO.heroPrefab
    -> PlayerInitializer.SetHeroSO()
    -> PlayerActor.SetHeroSO()
    -> PlayerActor.RegisterModules()
    -> RegisterPassives()
    -> PlayerInitializer.ApplyHeroSkills()
```

## 5.1 默认英雄解析

局外 UI 使用 `HeroSOLoader.LoadOrDefault()` / `LoadDefault()` 获取默认英雄。加载器会优先按 `Resources/Configs/HeroSO/{heroId}` 直接加载，失败后扫描 `Resources/Configs/HeroSO` 下所有 `HeroSO`，按 `BaseSO.id` 或 `BaseSO.name` 匹配；未传入或匹配失败时返回该目录下第一个可用 `HeroSO`。

需要人工确认：默认 HeroSO 资源必须位于 `Resources/Configs/HeroSO/`，且建议配置稳定的 `id`。当前 UI 保存到 `MissionSessionConfig.SelectedHeroId` 的值会优先使用 `HeroSO.id`，为空才回退 `HeroSO.name`。

## 6. 依赖关系

| 依赖 | 用途 |
|------|------|
| `PlayerAttributeConfig` | 初始化玩家属性模块 |
| `SkillDefinitionSO` | 主动技能数值、目标、伤害与执行器 ID |
| `SkillExecuteRegistry` | 根据 `ExecuteHandlerId` 查找主动技能执行器 |
| `PassiveExecutorSO` / `IPassiveExecutor` | 被动能力生命周期回调 |
| `PlayerInitializer` | 注入 HeroSO 并填充技能槽 |
| `PlayerActor` | 读取 HeroSO 属性并注册被动 |

## 7. 维护规则

- 新增主动技能执行器时，优先在 `SkillExecuteRegistry.EnsureInitialized()` 注册，同步 `SkillExecuteHandlerId`，并在 `SkillDefinitionSO.executeHandler` 中选择对应枚举。
- 新增被动能力执行器时，继承 `PassiveExecutorSO`，提供 `CreateAssetMenu`，并在 `OnHeroDestroyed()` 中清理运行时状态。
- 修改 HeroSO 字段时，同步更新本文件和 SkillSystem 文档中与 HeroSO 相关的说明。

## 8. 文档维护信息

| 项目 | 内容 |
|------|------|
| 创建日期 | 2026-06-30 |
| 覆盖文件数 | 3 个 .cs |
| 关联模块文档 | SkillSystem, Attribute, NetworkLayer |
