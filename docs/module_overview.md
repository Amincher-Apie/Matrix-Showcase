# Matrix 模块说明

本文按技术展示维度介绍 Showcase 仓库中的核心模块。

## 1. NetworkLayer

路径：`src/Assets/Framework/NetworkLayer`

核心职责：

- 封装 Netcode for GameObjects 的网络对象代理。
- 用 `NetworkObjectManager` 建立 `NetworkObjectId` 到逻辑对象、渲染对象和代理对象的映射。
- 使用 `NetworkObjectPool` 管理网络对象生成和回收。
- 在 ServerAuthority 模块中处理属性、战斗、技能和 Buff 的服务端逻辑。

适合讲解的文件：

- `Proxy/NetworkProxyBase.cs`
- `Proxy/PlayerProxy/PlayerNetworkProxy.cs`
- `Proxy/EnemyProxy/EnemyNetworkProxy.cs`
- `NetworkObjectManager/NetworkObjectManager.cs`
- `NetworkObjectPool/NetworkObjectPoolManager.cs`
- `ServerAuthority/AttributeSystem/ServerAttributeModule.cs`
- `ServerAuthority/CombatSystem/ServerCombatModule.cs`

展示价值：体现服务器权威、网络生命周期管理、服务端结算和对象池设计。

## 2. PCG

路径：`src/Assets/Scripts/PCG`

核心职责：

- 使用 Seed 和 `DeterministicRandom` 生成可复现地图。
- 构建 `RoomGraph` 房间拓扑。
- 通过 `RoomRoleAllocator` 根据任务语义分配房间角色。
- 使用 `RoomStitcher` 完成房间物理拼接。
- 收集出生点、Boss 点、防守点、资源点等功能节点。

适合讲解的文件：

- `Core/DeterministicRandom.cs`
- `Data/RoomGraph.cs`
- `Data/PcgTaskModels.cs`
- `Generation/PcgMapGenerator.cs`
- `Generation/RoomGraphBuilder.cs`
- `Generation/RoomRoleAllocator.cs`
- `Generation/RoomStitcher.cs`
- `Generation/PcgGenerationFailureReporter.cs`

展示价值：体现算法设计、确定性、任务与地图生成的解耦，以及失败诊断能力。

## 3. RunSystem

路径：`src/Assets/Scripts/RunSystem`

核心职责：

- 管理单局对局生命周期。
- 推进 MainMenu、Lobby、RunInit、Exploring、BossFight、RunVictory、RunDefeat、RunSummary 等状态。
- 调用 MissionSystem 构建任务输入，再调用 PCG 生成地图。
- 监听玩家死亡、Boss 死亡和结算事件。

适合讲解的文件：

- `RunManager.cs`
- `RunStateModels.cs`
- `RunContext.cs`
- `RunSessionData.cs`
- `RunSummaryCalculator.cs`

展示价值：体现复杂流程状态机、网络同步状态和多个系统的编排能力。

## 4. MissionSystem

路径：`src/Assets/Scripts/MissionSystem`

核心职责：

- 从 `MissionLibrary` 中构建一组主线和支线任务。
- 将任务语义转换成 PCG 可消费的 `MapTaskInput`。
- 在 PCG 完成后，将任务绑定到正确房间。
- 使用 `MissionNetState` 同步任务状态。
- 实现 Boss、Eliminate、Defense、Capture、Destroy 等任务类型。

适合讲解的文件：

- `MissionManager.cs`
- `MissionRuntimeModels.cs`
- `MissionBase.cs`
- `MissionImplementations.cs`
- `MissionConfig.cs`
- `MissionLibrary.cs`

展示价值：体现任务语义设计、状态同步、任务与 PCG 的双向协作。

## 5. Combat / Attribute / DamageCenter

路径：

- `src/Assets/Framework/LogicLayer/Module/CombatModule`
- `src/Assets/Framework/LogicLayer/Module/AttributeModule`
- `src/Assets/Framework/LogicLayer/DamageCenter`

核心职责：

- CombatModule 处理玩家和敌人的开火逻辑。
- AttributeModule 管理属性、修改器和当前值。
- DamageCenter 定义伤害输入、结果和公式。
- ServerAttributeModule 负责服务端扣血、扣盾、死亡和事件广播。

适合讲解的文件：

- `CombatModule/PlayerCombatModule.cs`
- `CombatModule/FireMethods/HitScanFireMethod.cs`
- `CombatModule/FireMethods/ProjectileFireMethod.cs`
- `AttributeModule/AttributeModule.cs`
- `AttributeModule/AttributeManager.cs`
- `DamageCenter/DamageCalculator.cs`
- `DamageCenter/DamageStruct.cs`

展示价值：体现战斗管线拆分、数值计算和事件解耦。

## 6. SkillSystem / BuffSystem / QualityEffects

路径：

- `src/Assets/Framework/LogicLayer/SkillSystem`
- `src/Assets/Framework/LogicLayer/BuffSystem`
- `src/Assets/Scripts/InventorySystem/QualityEffects`

核心职责：

- SkillSystem 通过执行器注册表执行不同技能。
- BuffSystem 负责 Buff 数据、生命周期、叠层和回调。
- QualityEffects 使用 Trigger / Condition / Action 模式配置品质道具效果。

适合讲解的文件：

- `SkillSystem/SkillRuntimeContext.cs`
- `SkillSystem/SkillExcuteRegistry.cs`
- `SkillSystem/Excutors/`
- `BuffSystem/BuffHandler.cs`
- `BuffSystem/BuffData.cs`
- `QualityEffects/QualityEffectDefinitions.cs`
- `QualityEffects/Executors/ConditionExecutors.cs`
- `QualityEffects/Executors/ActionExecutors.cs`
- `QualityEffects/StackingRules/`

展示价值：体现数据驱动玩法扩展能力。注意：部分动作执行器是扩展点，不能夸大为全部完成。

## 7. AI

路径：`src/Assets/Framework/LogicLayer/Module/AIModule`

核心职责：

- 状态机驱动 Idle、Patrol、Chase、Attack。
- 感知系统选择目标。
- AIScheduler 根据距离和兴趣区域降低 Tick 成本。
- Steering 和 Boids 改善群体移动。
- Navigation 模块适配 PCG 地图拓扑。

适合讲解的文件：

- `EnemyAIModule.cs`
- `AIStateMachine.cs`
- `States/`
- `Perception/PerceptionSystem.cs`
- `Scheduling/AIScheduler.cs`
- `Interest/InterestRegionManager.cs`
- `Movement/SteeringSystem.cs`
- `Movement/Boids/`

展示价值：体现 AI 行为、性能调度和 PCG 场景适配。

## 8. SO 与工具链

路径：

- `src/Assets/Scripts/SO`
- `src/Assets/Scripts/Excel`
- `src/Assets/Scripts/Tools`
- `src/Assets/Scripts/Managers`

核心职责：

- 使用 ScriptableObject 定义英雄、技能、武器、敌人、任务、道具等数据。
- 使用 `SOManager` 统一加载和查询配置。
- 使用 Excel 工具链辅助配置生成。

适合讲解的文件：

- `SO/HeroSO/HeroSO.cs`
- `SO/HeroSO/SkillAbilityDef.cs`
- `SO/HeroSO/PassiveAbilityDef.cs`
- `SO/SkillSO/SkillDefinitionSO.cs`
- `Managers/SOManager.cs`
- `Excel/ExcelToSOGenerator.cs`

展示价值：体现内容生产工具链和数据驱动设计。

## 9. 辅助模块

| 模块 | 路径 | 说明 |
|---|---|---|
| EventCenter | `src/Assets/Framework/EventCenter` | 模块间事件通信 |
| ArchiveSystem | `src/Assets/Scripts/ArchiveSystem` | 对局结果、成长数据和 Json 存档 |
| PlayerControl | `src/Assets/Scripts/PlayerControl` | 输入、第三人称控制和技能输入绑定 |
| Interaction | `src/Assets/Scripts/Interaction` | 拾取和交互逻辑 |
| UI Framework | `src/Assets/Framework/UI` | 窗口基类和 UI 生成工具 |

这些模块不是 README 的主叙事，但能支撑完整工程感。
