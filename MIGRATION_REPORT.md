# Matrix Showcase 迁移报告

- 源项目：`E:\UnityProjects\Matrix`
- 目标目录：`G:\2023University\简历\ShowreelBuild\Matrix-Showcase`
- 迁移策略：白名单复制，只复制 `.cs`、`.md`、`.asmdef`、`.inputactions` 与生成的 showcase 文档。
- 原始项目文件未被修改。

## 复制类别

| 来源路径 | 目标路径 | 文件数 | 复制理由 |
|---|---|---:|---|
| `生成文件` | `README.md` | 1 | Showcase 仓库入口说明 |
| `生成文件` | `NOTICE.md` | 1 | 说明第三方资源与完整工程不公开原因 |
| `生成文件` | `LICENSE` | 1 | Showcase 源码展示用途许可证建议文本 |
| `生成文件` | `.gitignore` | 1 | 防止 Unity 生成目录、IDE 文件和大体积资源进入公开仓库 |
| `生成文件` | `docs\ai-readable-project-brief.md` | 1 | 帮助其他 AI 或评审者快速理解 showcase 内容 |
| `生成文件` | `docs\public-scope.md` | 1 | 明确 showcase 公开范围和限制 |
| `AGENTS.md` | `docs\project\AGENTS.md` | 1 | 项目 AI 协作规则，可展示工程协作规范，公开前仍建议人工审阅 |
| `PROJECT_OVERVIEW.md` | `docs\project\PROJECT_OVERVIEW.md` | 1 | 项目定位、核心玩法流程和模块索引 |
| `ARCHITECTURE.md` | `docs\project\ARCHITECTURE.md` | 1 | 项目整体架构、模块关系和数据流说明 |
| `docs` | `docs` | 23 | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `Assets\Framework\EventCenter` | `src\Assets\Framework\EventCenter` | 3 | 自研事件中心，展示模块间解耦通信 |
| `Assets\Framework\LogicLayer` | `src\Assets\Framework\LogicLayer` | 129 | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\NetworkLayer` | `src\Assets\Framework\NetworkLayer` | 29 | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\RenderLayer` | `src\Assets\Framework\RenderLayer` | 11 | 表现层桥接代码，展示 DamageText 与 RenderActor 解耦方式 |
| `Assets\Framework\Singleton` | `src\Assets\Framework\Singleton` | 2 | 基础单例工具，多个核心管理器依赖 |
| `Assets\Framework\Json` | `src\Assets\Framework\Json` | 1 | 自研存档与配置序列化基础设施 |
| `Assets\Framework\Mono` | `src\Assets\Framework\Mono` | 1 | Mono 生命周期管理基础设施 |
| `Assets\Framework\Pool` | `src\Assets\Framework\Pool` | 1 | 自研普通对象池基础设施 |
| `Assets\Framework\Resource` | `src\Assets\Framework\Resource` | 4 | 资源加载封装，展示项目基础设施设计 |
| `Assets\Framework\UI` | `src\Assets\Framework\UI` | 14 | 自研 UI 框架与编辑器生成工具 |
| `Assets\Framework\Audio` | `src\Assets\Framework\Audio` | 1 | 自研音频管理基础设施 |
| `Assets\Framework\EffectRender` | `src\Assets\Framework\EffectRender` | 1 | 自研特效播放与回收基础设施 |
| `Assets\Scripts\PCG` | `src\Assets\Scripts\PCG` | 29 | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\RunSystem` | `src\Assets\Scripts\RunSystem` | 13 | 服务器权威对局状态机与结算流程 |
| `Assets\Scripts\MissionSystem` | `src\Assets\Scripts\MissionSystem` | 18 | 任务系统、任务语义输入、任务状态同步与房间绑定 |
| `Assets\Scripts\InventorySystem` | `src\Assets\Scripts\InventorySystem` | 22 | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\SO` | `src\Assets\Scripts\SO` | 12 | HeroSO、SkillDefinitionSO、WeaponSO、EnemySO 等数据驱动配置定义 |
| `Assets\Scripts\Excel` | `src\Assets\Scripts\Excel` | 8 | Excel 到 ScriptableObject 的数据工具链 |
| `Assets\Scripts\Managers` | `src\Assets\Scripts\Managers` | 5 | SOManager、EnemySpawnService、GameFlowBootstrapper 等运行时管理器 |
| `Assets\Scripts\Tools` | `src\Assets\Scripts\Tools` | 7 | 配置加载、坐标转换和编辑辅助工具 |
| `Assets\Scripts\PlayerControl` | `src\Assets\Scripts\PlayerControl` | 10 | 玩家输入、第三人称控制和技能输入绑定 |
| `Assets\Scripts\ArchiveSystem` | `src\Assets\Scripts\ArchiveSystem` | 6 | 对局结果与成长数据存档系统 |
| `Assets\Scripts\BuffSystem` | `src\Assets\Scripts\BuffSystem` | 6 | 元素 Buff 示例模块，配合 BuffSystem 展示扩展方式 |
| `Assets\Scripts\SpawnSystem` | `src\Assets\Scripts\SpawnSystem` | 3 | 玩家生成与怪物刷怪配置接口 |
| `Assets\Scripts\Enum` | `src\Assets\Scripts\Enum` | 4 | 核心枚举定义，支撑道具、品质、攻击类型等模块 |
| `Assets\Scripts\Interaction` | `src\Assets\Scripts\Interaction` | 5 | 交互与拾取系统，MissionSystem 捕获任务和玩家代理依赖 |

## 复制文件明细

| 来源 | 目标 | 理由 |
|---|---|---|
| `生成文件` | `.gitignore` | 防止 Unity 生成目录、IDE 文件和大体积资源进入公开仓库 |
| `docs\adr\0001-minimap-pre-baked-thumbnails.md` | `docs\adr\0001-minimap-pre-baked-thumbnails.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `docs\adr\0002-mission-system-trigger-and-spawn-orchestration.md` | `docs\adr\0002-mission-system-trigger-and-spawn-orchestration.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `docs\adr\0003-ai-targeting-generalization-interface.md` | `docs\adr\0003-ai-targeting-generalization-interface.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `docs\adr\0004-skill-system-integration-plan.md` | `docs\adr\0004-skill-system-integration-plan.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `生成文件` | `docs\ai-readable-project-brief.md` | 帮助其他 AI 或评审者快速理解 showcase 内容 |
| `docs\animation\AnimationParametersStandard.md` | `docs\animation\AnimationParametersStandard.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `docs\animation\AnimationResourceIntegrationManual.md` | `docs\animation\AnimationResourceIntegrationManual.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `docs\animation\AnimatorControllerBindingGuide.md` | `docs\animation\AnimatorControllerBindingGuide.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `docs\animation\AnimatorControllerRebuildGuide.md` | `docs\animation\AnimatorControllerRebuildGuide.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `docs\CodingPlan\5.18-GameFlowTest.md` | `docs\CodingPlan\5.18-GameFlowTest.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `docs\CodingPlan\5.18-GameFlowTestSence.md` | `docs\CodingPlan\5.18-GameFlowTestSence.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `docs\CodingPlan\BUGS.md` | `docs\CodingPlan\BUGS.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `docs\CodingPlan\CodeReview-GapAnalysis-2026-05-16.md` | `docs\CodingPlan\CodeReview-GapAnalysis-2026-05-16.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `docs\CodingPlan\Gap-5.17.md` | `docs\CodingPlan\Gap-5.17.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `docs\CodingPlan\Gap-5.17-Self.md` | `docs\CodingPlan\Gap-5.17-Self.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `docs\CodingPlan\LobbyWindow.md` | `docs\CodingPlan\LobbyWindow.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `docs\CodingPlan\MissionSelectWindow.md` | `docs\CodingPlan\MissionSelectWindow.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `docs\CodingPlan\PCGProfile_EnemyMapping.md` | `docs\CodingPlan\PCGProfile_EnemyMapping.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `docs\CodingPlan\PlayerSpawnManager.md` | `docs\CodingPlan\PlayerSpawnManager.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `docs\CodingPlan\RunDataFlow.md` | `docs\CodingPlan\RunDataFlow.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `docs\CodingPlan\ThreeMissionTypes_ImplementationPlan.md` | `docs\CodingPlan\ThreeMissionTypes_ImplementationPlan.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `docs\minimap-gamehudwindow-config.md` | `docs\minimap-gamehudwindow-config.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `docs\phase1-configuration-guide.md` | `docs\phase1-configuration-guide.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `AGENTS.md` | `docs\project\AGENTS.md` | 项目 AI 协作规则，可展示工程协作规范，公开前仍建议人工审阅 |
| `ARCHITECTURE.md` | `docs\project\ARCHITECTURE.md` | 项目整体架构、模块关系和数据流说明 |
| `PROJECT_OVERVIEW.md` | `docs\project\PROJECT_OVERVIEW.md` | 项目定位、核心玩法流程和模块索引 |
| `生成文件` | `docs\public-scope.md` | 明确 showcase 公开范围和限制 |
| `docs\刷怪系统运行逻辑说明.md` | `docs\刷怪系统运行逻辑说明.md` | 项目既有设计文档、ADR、配置说明和开发计划文档 |
| `生成文件` | `LICENSE` | Showcase 源码展示用途许可证建议文本 |
| `生成文件` | `NOTICE.md` | 说明第三方资源与完整工程不公开原因 |
| `生成文件` | `README.md` | Showcase 仓库入口说明 |
| `Assets\Framework\Audio\AudioManager.cs` | `src\Assets\Framework\Audio\AudioManager.cs` | 自研音频管理基础设施 |
| `Assets\Framework\EffectRender\EffectRenderManager.cs` | `src\Assets\Framework\EffectRender\EffectRenderManager.cs` | 自研特效播放与回收基础设施 |
| `Assets\Framework\EventCenter\EventCenter.cs` | `src\Assets\Framework\EventCenter\EventCenter.cs` | 自研事件中心，展示模块间解耦通信 |
| `Assets\Framework\EventCenter\EventName.cs` | `src\Assets\Framework\EventCenter\EventName.cs` | 自研事件中心，展示模块间解耦通信 |
| `Assets\Framework\EventCenter\MODULE.md` | `src\Assets\Framework\EventCenter\MODULE.md` | 自研事件中心，展示模块间解耦通信 |
| `Assets\Framework\Json\JsonManager.cs` | `src\Assets\Framework\Json\JsonManager.cs` | 自研存档与配置序列化基础设施 |
| `Assets\Framework\LogicLayer\BuffSystem\BaseBuffModule.cs` | `src\Assets\Framework\LogicLayer\BuffSystem\BaseBuffModule.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\BuffSystem\BuffData.cs` | `src\Assets\Framework\LogicLayer\BuffSystem\BuffData.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\BuffSystem\BuffDesign.cs` | `src\Assets\Framework\LogicLayer\BuffSystem\BuffDesign.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\BuffSystem\BuffHandler.cs` | `src\Assets\Framework\LogicLayer\BuffSystem\BuffHandler.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\BuffSystem\BuffInfo.cs` | `src\Assets\Framework\LogicLayer\BuffSystem\BuffInfo.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\BuffSystem\BuffOwnerContext.cs` | `src\Assets\Framework\LogicLayer\BuffSystem\BuffOwnerContext.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\BuffSystem\ElementBuffMappingAsset.cs` | `src\Assets\Framework\LogicLayer\BuffSystem\ElementBuffMappingAsset.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\BuffSystem\IBuffOwnerContext.cs` | `src\Assets\Framework\LogicLayer\BuffSystem\IBuffOwnerContext.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\BuffSystem\MODULE.md` | `src\Assets\Framework\LogicLayer\BuffSystem\MODULE.md` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\DamageCenter\DamageCalculator.cs` | `src\Assets\Framework\LogicLayer\DamageCenter\DamageCalculator.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\DamageCenter\DamageStruct.cs` | `src\Assets\Framework\LogicLayer\DamageCenter\DamageStruct.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\DamageCenter\DamageVfxEvent.cs` | `src\Assets\Framework\LogicLayer\DamageCenter\DamageVfxEvent.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\DamageCenter\MODULE.md` | `src\Assets\Framework\LogicLayer\DamageCenter\MODULE.md` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\EnemyLogic\BossActor.cs` | `src\Assets\Framework\LogicLayer\EnemyLogic\BossActor.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\EnemyLogic\BossBTBridge.cs` | `src\Assets\Framework\LogicLayer\EnemyLogic\BossBTBridge.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\EnemyLogic\BossModule.cs` | `src\Assets\Framework\LogicLayer\EnemyLogic\BossModule.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossAdvanceAttack.cs` | `src\Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossAdvanceAttack.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossAdvanceCombo.cs` | `src\Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossAdvanceCombo.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossBackward.cs` | `src\Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossBackward.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossDeath.cs` | `src\Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossDeath.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossFaceToPlayer.cs` | `src\Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossFaceToPlayer.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossHealthRange.cs` | `src\Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossHealthRange.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossIsVariables.cs` | `src\Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossIsVariables.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossNear.cs` | `src\Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossNear.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossRemote.cs` | `src\Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossRemote.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossShoot.cs` | `src\Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossShoot.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossSkillA.cs` | `src\Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossSkillA.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossSprintAttack.cs` | `src\Assets\Framework\LogicLayer\EnemyLogic\BossTasks\BossSprintAttack.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\EnemyLogic\EnemyActor.cs` | `src\Assets\Framework\LogicLayer\EnemyLogic\EnemyActor.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\EnemyLogic\EnemyDropTableSO.cs` | `src\Assets\Framework\LogicLayer\EnemyLogic\EnemyDropTableSO.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\EnemyLogic\MagicRay.cs` | `src\Assets\Framework\LogicLayer\EnemyLogic\MagicRay.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\EnemyLogic\ShockWave.cs` | `src\Assets\Framework\LogicLayer\EnemyLogic\ShockWave.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Interfaces\IAttribute.cs` | `src\Assets\Framework\LogicLayer\Interfaces\IAttribute.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Interfaces\ICombat.cs` | `src\Assets\Framework\LogicLayer\Interfaces\ICombat.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Interfaces\IFireMethod.cs` | `src\Assets\Framework\LogicLayer\Interfaces\IFireMethod.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Interfaces\IHitScanService.cs` | `src\Assets\Framework\LogicLayer\Interfaces\IHitScanService.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Interfaces\ILogicObject.cs` | `src\Assets\Framework\LogicLayer\Interfaces\ILogicObject.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Interfaces\IModule.cs` | `src\Assets\Framework\LogicLayer\Interfaces\IModule.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Interfaces\IPassiveExecutor.cs` | `src\Assets\Framework\LogicLayer\Interfaces\IPassiveExecutor.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Interfaces\ISkilll.cs` | `src\Assets\Framework\LogicLayer\Interfaces\ISkilll.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Interfaces\ITimeSource.cs` | `src\Assets\Framework\LogicLayer\Interfaces\ITimeSource.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\LogicActor\LogicActor.cs` | `src\Assets\Framework\LogicLayer\LogicActor\LogicActor.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\LogicObject.cs` | `src\Assets\Framework\LogicLayer\LogicObject.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\AIDebug.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\AIDebug.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\AIStateMachine.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\AIStateMachine.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Config\AISchedulerConfig.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Config\AISchedulerConfig.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Config\EnemyAIConfig.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Config\EnemyAIConfig.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Debug\EnemyAIDebugOverlay.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Debug\EnemyAIDebugOverlay.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\EnemyAIModule.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\EnemyAIModule.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\EnemyAI模块介绍.md` | `src\Assets\Framework\LogicLayer\Module\AIModule\EnemyAI模块介绍.md` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Interest\AIInterestHotspotService.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Interest\AIInterestHotspotService.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Interest\InterestRegionDebugInfo.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Interest\InterestRegionDebugInfo.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Interest\InterestRegionManager.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Interest\InterestRegionManager.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Interest\InterestRegionSourceType.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Interest\InterestRegionSourceType.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\MODULE.md` | `src\Assets\Framework\LogicLayer\Module\AIModule\MODULE.md` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Movement\AIMoveIntent.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Movement\AIMoveIntent.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Movement\AISteeringDebugInfo.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Movement\AISteeringDebugInfo.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Movement\Boids\BoidsAgentData.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Movement\Boids\BoidsAgentData.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Movement\Boids\BoidsCentralController.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Movement\Boids\BoidsCentralController.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Movement\Boids\BoidsConfig.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Movement\Boids\BoidsConfig.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Movement\Boids\BoidsDebugInfo.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Movement\Boids\BoidsDebugInfo.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Movement\Boids\BoidsUpdater.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Movement\Boids\BoidsUpdater.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Movement\EnemyNavAgentController.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Movement\EnemyNavAgentController.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Movement\SteeringSystem.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Movement\SteeringSystem.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Navigation\AIPathRequestType.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Navigation\AIPathRequestType.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Navigation\AIPathResultType.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Navigation\AIPathResultType.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Navigation\IPCGMapTopologyProvider.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Navigation\IPCGMapTopologyProvider.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Navigation\PcgMapTopologyProviderBridge.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Navigation\PcgMapTopologyProviderBridge.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Navigation\PCGMapTopologyService.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Navigation\PCGMapTopologyService.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Navigation\PCGNavMeshLinkBuilder.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Navigation\PCGNavMeshLinkBuilder.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Navigation\PCGNavMeshManager.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Navigation\PCGNavMeshManager.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Perception\AttackableObjectManager.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Perception\AttackableObjectManager.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Perception\AttackableObjectType.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Perception\AttackableObjectType.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Perception\IAttackableObject.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Perception\IAttackableObject.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Perception\PerceptionSystem.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Perception\PerceptionSystem.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Scheduling\AIScheduler.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Scheduling\AIScheduler.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Scheduling\AISchedulerDebugInfo.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Scheduling\AISchedulerDebugInfo.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\Scheduling\AISimulationLevel.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\Scheduling\AISimulationLevel.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\States\AIStateBase.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\States\AIStateBase.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\States\AttackState.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\States\AttackState.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\States\ChaseState.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\States\ChaseState.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\States\IdleState.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\States\IdleState.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AIModule\States\PatrolState.cs` | `src\Assets\Framework\LogicLayer\Module\AIModule\States\PatrolState.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AttributeModule\AttributeConfig.cs` | `src\Assets\Framework\LogicLayer\Module\AttributeModule\AttributeConfig.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AttributeModule\AttributeData.cs` | `src\Assets\Framework\LogicLayer\Module\AttributeModule\AttributeData.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AttributeModule\AttributeEnum.cs` | `src\Assets\Framework\LogicLayer\Module\AttributeModule\AttributeEnum.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AttributeModule\AttributeManager.cs` | `src\Assets\Framework\LogicLayer\Module\AttributeModule\AttributeManager.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AttributeModule\AttributeModule.cs` | `src\Assets\Framework\LogicLayer\Module\AttributeModule\AttributeModule.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AttributeModule\EnemyAttributeConfig.cs` | `src\Assets\Framework\LogicLayer\Module\AttributeModule\EnemyAttributeConfig.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AttributeModule\EnemyAttributeModule.cs` | `src\Assets\Framework\LogicLayer\Module\AttributeModule\EnemyAttributeModule.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AttributeModule\MODULE.md` | `src\Assets\Framework\LogicLayer\Module\AttributeModule\MODULE.md` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AttributeModule\PlayerAttributeConfig.cs` | `src\Assets\Framework\LogicLayer\Module\AttributeModule\PlayerAttributeConfig.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\AttributeModule\PlayerAttributeModule.cs` | `src\Assets\Framework\LogicLayer\Module\AttributeModule\PlayerAttributeModule.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\CombatModule\DamageContributionTracker.cs` | `src\Assets\Framework\LogicLayer\Module\CombatModule\DamageContributionTracker.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\CombatModule\EnemyCombatModule.cs` | `src\Assets\Framework\LogicLayer\Module\CombatModule\EnemyCombatModule.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\CombatModule\FireMethods\HitScanFireMethod.cs` | `src\Assets\Framework\LogicLayer\Module\CombatModule\FireMethods\HitScanFireMethod.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\CombatModule\FireMethods\ProjectileFireMethod.cs` | `src\Assets\Framework\LogicLayer\Module\CombatModule\FireMethods\ProjectileFireMethod.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\CombatModule\MODULE.md` | `src\Assets\Framework\LogicLayer\Module\CombatModule\MODULE.md` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\CombatModule\PlayerCombatModule.cs` | `src\Assets\Framework\LogicLayer\Module\CombatModule\PlayerCombatModule.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\CombatModule\Services\ClientHitScanService.cs` | `src\Assets\Framework\LogicLayer\Module\CombatModule\Services\ClientHitScanService.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\CombatModule\Services\HitScanTargetResolver.cs` | `src\Assets\Framework\LogicLayer\Module\CombatModule\Services\HitScanTargetResolver.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\CombatModule\WeaponAttributeEnum.cs` | `src\Assets\Framework\LogicLayer\Module\CombatModule\WeaponAttributeEnum.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\CombatModule\WeaponConfig.cs` | `src\Assets\Framework\LogicLayer\Module\CombatModule\WeaponConfig.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\CombatModule\WeaponModifier.cs` | `src\Assets\Framework\LogicLayer\Module\CombatModule\WeaponModifier.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\PlayerBuffModule.cs` | `src\Assets\Framework\LogicLayer\Module\PlayerBuffModule.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\PlayerInventoryModule.cs` | `src\Assets\Framework\LogicLayer\Module\PlayerInventoryModule.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\PlayerQualityEffectModule.cs` | `src\Assets\Framework\LogicLayer\Module\PlayerQualityEffectModule.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\PlayerTestModule.cs` | `src\Assets\Framework\LogicLayer\Module\PlayerTestModule.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\SkillModule\PlayerSkillModule.cs` | `src\Assets\Framework\LogicLayer\Module\SkillModule\PlayerSkillModule.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\SpawnSystem\MODULE.md` | `src\Assets\Framework\LogicLayer\Module\SpawnSystem\MODULE.md` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\SpawnSystem\MonsterRegistry.cs` | `src\Assets\Framework\LogicLayer\Module\SpawnSystem\MonsterRegistry.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\SpawnSystem\MonsterSpawnManager.cs` | `src\Assets\Framework\LogicLayer\Module\SpawnSystem\MonsterSpawnManager.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\SpawnSystem\RoomGraphExtensions.cs` | `src\Assets\Framework\LogicLayer\Module\SpawnSystem\RoomGraphExtensions.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\Module\SpawnSystem\SpawnPointData.cs` | `src\Assets\Framework\LogicLayer\Module\SpawnSystem\SpawnPointData.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\PlayerLogic\PlayerActor.cs` | `src\Assets\Framework\LogicLayer\PlayerLogic\PlayerActor.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\PlayerLogic\PlayerBossMeleeHitReceiver.cs` | `src\Assets\Framework\LogicLayer\PlayerLogic\PlayerBossMeleeHitReceiver.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\SkillSystem\Excutors\BombardAreaSkillExecutor.cs` | `src\Assets\Framework\LogicLayer\SkillSystem\Excutors\BombardAreaSkillExecutor.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\SkillSystem\Excutors\KineticBoostSkillExecutor.cs` | `src\Assets\Framework\LogicLayer\SkillSystem\Excutors\KineticBoostSkillExecutor.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\SkillSystem\Excutors\KineticBurstPassiveExecutor.cs` | `src\Assets\Framework\LogicLayer\SkillSystem\Excutors\KineticBurstPassiveExecutor.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\SkillSystem\Excutors\PiercingShotSkillExecutor.cs` | `src\Assets\Framework\LogicLayer\SkillSystem\Excutors\PiercingShotSkillExecutor.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\SkillSystem\MODULE.md` | `src\Assets\Framework\LogicLayer\SkillSystem\MODULE.md` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\SkillSystem\PassiveExecutorSO.cs` | `src\Assets\Framework\LogicLayer\SkillSystem\PassiveExecutorSO.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\SkillSystem\PlayerSkillRuntime.cs` | `src\Assets\Framework\LogicLayer\SkillSystem\PlayerSkillRuntime.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\SkillSystem\SkillEnums.cs` | `src\Assets\Framework\LogicLayer\SkillSystem\SkillEnums.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\SkillSystem\SkillExcuteRegistry.cs` | `src\Assets\Framework\LogicLayer\SkillSystem\SkillExcuteRegistry.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\SkillSystem\SkillExecuteHandlerId.cs` | `src\Assets\Framework\LogicLayer\SkillSystem\SkillExecuteHandlerId.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\SkillSystem\SkillRuntimeContext.cs` | `src\Assets\Framework\LogicLayer\SkillSystem\SkillRuntimeContext.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\SkillSystem\SkillStatBuilder.cs` | `src\Assets\Framework\LogicLayer\SkillSystem\SkillStatBuilder.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\LogicLayer\SkillSystem\SkillTargetingUtility.cs` | `src\Assets\Framework\LogicLayer\SkillSystem\SkillTargetingUtility.cs` | 自研逻辑层，包含 Actor、AI、Combat、Attribute、Damage、Skill、Buff、Spawn 等核心模块 |
| `Assets\Framework\Mono\MonoManager.cs` | `src\Assets\Framework\Mono\MonoManager.cs` | Mono 生命周期管理基础设施 |
| `Assets\Framework\NetworkLayer\ClientAuthority\ClientNetworkAnimator.cs` | `src\Assets\Framework\NetworkLayer\ClientAuthority\ClientNetworkAnimator.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\ClientAuthority\ClientNetworkTransform.cs` | `src\Assets\Framework\NetworkLayer\ClientAuthority\ClientNetworkTransform.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\Enemy\ServerEnemyMovementDriver.cs` | `src\Assets\Framework\NetworkLayer\Enemy\ServerEnemyMovementDriver.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\Interfaces\INetworkProxy.cs` | `src\Assets\Framework\NetworkLayer\Interfaces\INetworkProxy.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\MODULE.md` | `src\Assets\Framework\NetworkLayer\MODULE.md` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\NetworkObjectManager\NetworkObjectManager.cs` | `src\Assets\Framework\NetworkLayer\NetworkObjectManager\NetworkObjectManager.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\NetworkObjectPool\NetworkDropItem.cs` | `src\Assets\Framework\NetworkLayer\NetworkObjectPool\NetworkDropItem.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\NetworkObjectPool\NetworkObjectPool.cs` | `src\Assets\Framework\NetworkLayer\NetworkObjectPool\NetworkObjectPool.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\NetworkObjectPool\NetworkObjectPoolBootstrap.cs` | `src\Assets\Framework\NetworkLayer\NetworkObjectPool\NetworkObjectPoolBootstrap.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\NetworkObjectPool\NetworkObjectPoolManager.cs` | `src\Assets\Framework\NetworkLayer\NetworkObjectPool\NetworkObjectPoolManager.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\NetworkObjectPool\PooledNetworkObject.cs` | `src\Assets\Framework\NetworkLayer\NetworkObjectPool\PooledNetworkObject.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\NetworkObjectPool\PooledNetworkPrefabHandler.cs` | `src\Assets\Framework\NetworkLayer\NetworkObjectPool\PooledNetworkPrefabHandler.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\Proxy\BossProxy\BossNetworkProxy.cs` | `src\Assets\Framework\NetworkLayer\Proxy\BossProxy\BossNetworkProxy.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\Proxy\EnemyProxy\EnemyNetworkProxy.cs` | `src\Assets\Framework\NetworkLayer\Proxy\EnemyProxy\EnemyNetworkProxy.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\Proxy\NetworkProxyBase.cs` | `src\Assets\Framework\NetworkLayer\Proxy\NetworkProxyBase.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\Proxy\PlayerProxy\PlayerNetworkProxy.cs` | `src\Assets\Framework\NetworkLayer\Proxy\PlayerProxy\PlayerNetworkProxy.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\ServerAuthority\AttributeSystem\PlayerDeathHandling功能解读.md` | `src\Assets\Framework\NetworkLayer\ServerAuthority\AttributeSystem\PlayerDeathHandling功能解读.md` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\ServerAuthority\AttributeSystem\ServerAttributeModule.cs` | `src\Assets\Framework\NetworkLayer\ServerAuthority\AttributeSystem\ServerAttributeModule.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\ServerAuthority\AttributeSystem\ServerAttributeModule功能解读.md` | `src\Assets\Framework\NetworkLayer\ServerAuthority\AttributeSystem\ServerAttributeModule功能解读.md` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\ServerAuthority\AttributeSystem\ServerBossAttributeModule.cs` | `src\Assets\Framework\NetworkLayer\ServerAuthority\AttributeSystem\ServerBossAttributeModule.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\ServerAuthority\AttributeSystem\ServerEnemyAttributeModule.cs` | `src\Assets\Framework\NetworkLayer\ServerAuthority\AttributeSystem\ServerEnemyAttributeModule.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\ServerAuthority\AttributeSystem\ServerPlayerAttributeModule.cs` | `src\Assets\Framework\NetworkLayer\ServerAuthority\AttributeSystem\ServerPlayerAttributeModule.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\ServerAuthority\BuffSystem\ServerBuffModule.cs` | `src\Assets\Framework\NetworkLayer\ServerAuthority\BuffSystem\ServerBuffModule.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\ServerAuthority\CombatSystem\FireDataStruct.cs` | `src\Assets\Framework\NetworkLayer\ServerAuthority\CombatSystem\FireDataStruct.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\ServerAuthority\CombatSystem\FireServerServices\ServerHitScanService.cs` | `src\Assets\Framework\NetworkLayer\ServerAuthority\CombatSystem\FireServerServices\ServerHitScanService.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\ServerAuthority\CombatSystem\ServerCombatModule.cs` | `src\Assets\Framework\NetworkLayer\ServerAuthority\CombatSystem\ServerCombatModule.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\ServerAuthority\CombatSystem\ServerWeaponRuntime.cs` | `src\Assets\Framework\NetworkLayer\ServerAuthority\CombatSystem\ServerWeaponRuntime.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\ServerAuthority\SkillSystem\ServerSkillModule.cs` | `src\Assets\Framework\NetworkLayer\ServerAuthority\SkillSystem\ServerSkillModule.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\NetworkLayer\ServerAuthority\SkillSystem\SkillCastContext.cs` | `src\Assets\Framework\NetworkLayer\ServerAuthority\SkillSystem\SkillCastContext.cs` | 自研网络层，包含 NetworkProxy、ServerAuthority、NetworkObjectManager、NetworkObjectPool |
| `Assets\Framework\Pool\PoolManager.cs` | `src\Assets\Framework\Pool\PoolManager.cs` | 自研普通对象池基础设施 |
| `Assets\Framework\RenderLayer\DamageText\DamageSprite.cs` | `src\Assets\Framework\RenderLayer\DamageText\DamageSprite.cs` | 表现层桥接代码，展示 DamageText 与 RenderActor 解耦方式 |
| `Assets\Framework\RenderLayer\DamageText\DamageWorldText.cs` | `src\Assets\Framework\RenderLayer\DamageText\DamageWorldText.cs` | 表现层桥接代码，展示 DamageText 与 RenderActor 解耦方式 |
| `Assets\Framework\RenderLayer\DamageText\DamageWorldTextManager.cs` | `src\Assets\Framework\RenderLayer\DamageText\DamageWorldTextManager.cs` | 表现层桥接代码，展示 DamageText 与 RenderActor 解耦方式 |
| `Assets\Framework\RenderLayer\Interfaces\IRenderModule.cs` | `src\Assets\Framework\RenderLayer\Interfaces\IRenderModule.cs` | 表现层桥接代码，展示 DamageText 与 RenderActor 解耦方式 |
| `Assets\Framework\RenderLayer\Interfaces\IRenderObject.cs` | `src\Assets\Framework\RenderLayer\Interfaces\IRenderObject.cs` | 表现层桥接代码，展示 DamageText 与 RenderActor 解耦方式 |
| `Assets\Framework\RenderLayer\MODULE.md` | `src\Assets\Framework\RenderLayer\MODULE.md` | 表现层桥接代码，展示 DamageText 与 RenderActor 解耦方式 |
| `Assets\Framework\RenderLayer\PlayerRender\PlayerRender.cs` | `src\Assets\Framework\RenderLayer\PlayerRender\PlayerRender.cs` | 表现层桥接代码，展示 DamageText 与 RenderActor 解耦方式 |
| `Assets\Framework\RenderLayer\RenderActor\RenderActor.cs` | `src\Assets\Framework\RenderLayer\RenderActor\RenderActor.cs` | 表现层桥接代码，展示 DamageText 与 RenderActor 解耦方式 |
| `Assets\Framework\RenderLayer\RenderModule\PlayerTestRenderModule.cs` | `src\Assets\Framework\RenderLayer\RenderModule\PlayerTestRenderModule.cs` | 表现层桥接代码，展示 DamageText 与 RenderActor 解耦方式 |
| `Assets\Framework\RenderLayer\RenderObject.cs` | `src\Assets\Framework\RenderLayer\RenderObject.cs` | 表现层桥接代码，展示 DamageText 与 RenderActor 解耦方式 |
| `Assets\Framework\RenderLayer\WeaponRender\WeaponAnimationController.cs` | `src\Assets\Framework\RenderLayer\WeaponRender\WeaponAnimationController.cs` | 表现层桥接代码，展示 DamageText 与 RenderActor 解耦方式 |
| `Assets\Framework\Resource\IResourceLoader.cs` | `src\Assets\Framework\Resource\IResourceLoader.cs` | 资源加载封装，展示项目基础设施设计 |
| `Assets\Framework\Resource\ResourcesLoader.cs` | `src\Assets\Framework\Resource\ResourcesLoader.cs` | 资源加载封装，展示项目基础设施设计 |
| `Assets\Framework\Resource\ResourcesManager.cs` | `src\Assets\Framework\Resource\ResourcesManager.cs` | 资源加载封装，展示项目基础设施设计 |
| `Assets\Framework\Resource\ResPathConfig.cs` | `src\Assets\Framework\Resource\ResPathConfig.cs` | 资源加载封装，展示项目基础设施设计 |
| `Assets\Framework\Singleton\MonoSingletonBase.cs` | `src\Assets\Framework\Singleton\MonoSingletonBase.cs` | 基础单例工具，多个核心管理器依赖 |
| `Assets\Framework\Singleton\SingletonBase.cs` | `src\Assets\Framework\Singleton\SingletonBase.cs` | 基础单例工具，多个核心管理器依赖 |
| `Assets\Framework\UI\Base\UISetting.cs` | `src\Assets\Framework\UI\Base\UISetting.cs` | 自研 UI 框架与编辑器生成工具 |
| `Assets\Framework\UI\Base\WindowBase.cs` | `src\Assets\Framework\UI\Base\WindowBase.cs` | 自研 UI 框架与编辑器生成工具 |
| `Assets\Framework\UI\Base\WindowBehavior.cs` | `src\Assets\Framework\UI\Base\WindowBehavior.cs` | 自研 UI 框架与编辑器生成工具 |
| `Assets\Framework\UI\Base\WindowConfig.cs` | `src\Assets\Framework\UI\Base\WindowConfig.cs` | 自研 UI 框架与编辑器生成工具 |
| `Assets\Framework\UI\Core\UIManager.cs` | `src\Assets\Framework\UI\Core\UIManager.cs` | 自研 UI 框架与编辑器生成工具 |
| `Assets\Framework\UI\Editor\AnalysisControlTool.cs` | `src\Assets\Framework\UI\Editor\AnalysisControlTool.cs` | 自研 UI 框架与编辑器生成工具 |
| `Assets\Framework\UI\Editor\ControlData.cs` | `src\Assets\Framework\UI\Editor\ControlData.cs` | 自研 UI 框架与编辑器生成工具 |
| `Assets\Framework\UI\Editor\GenerateBindComponentTool.cs` | `src\Assets\Framework\UI\Editor\GenerateBindComponentTool.cs` | 自研 UI 框架与编辑器生成工具 |
| `Assets\Framework\UI\Editor\GenerateItemComponentTool.cs` | `src\Assets\Framework\UI\Editor\GenerateItemComponentTool.cs` | 自研 UI 框架与编辑器生成工具 |
| `Assets\Framework\UI\Editor\GenerateWindowComponentTool.cs` | `src\Assets\Framework\UI\Editor\GenerateWindowComponentTool.cs` | 自研 UI 框架与编辑器生成工具 |
| `Assets\Framework\UI\Editor\ReverseGeneratePrefabTool.cs` | `src\Assets\Framework\UI\Editor\ReverseGeneratePrefabTool.cs` | 自研 UI 框架与编辑器生成工具 |
| `Assets\Framework\UI\Editor\ScriptDisplayWindow.cs` | `src\Assets\Framework\UI\Editor\ScriptDisplayWindow.cs` | 自研 UI 框架与编辑器生成工具 |
| `Assets\Framework\UI\MODULE.md` | `src\Assets\Framework\UI\MODULE.md` | 自研 UI 框架与编辑器生成工具 |
| `Assets\Framework\UI\UI框架设计文档.md` | `src\Assets\Framework\UI\UI框架设计文档.md` | 自研 UI 框架与编辑器生成工具 |
| `Assets\Scripts\ArchiveSystem\Core\ArchiveManager.cs` | `src\Assets\Scripts\ArchiveSystem\Core\ArchiveManager.cs` | 对局结果与成长数据存档系统 |
| `Assets\Scripts\ArchiveSystem\Core\ArchiveUpdatePayloads.cs` | `src\Assets\Scripts\ArchiveSystem\Core\ArchiveUpdatePayloads.cs` | 对局结果与成长数据存档系统 |
| `Assets\Scripts\ArchiveSystem\DataModel\PlayerArchiveData.cs` | `src\Assets\Scripts\ArchiveSystem\DataModel\PlayerArchiveData.cs` | 对局结果与成长数据存档系统 |
| `Assets\Scripts\ArchiveSystem\MODULE.md` | `src\Assets\Scripts\ArchiveSystem\MODULE.md` | 对局结果与成长数据存档系统 |
| `Assets\Scripts\ArchiveSystem\Storage\IArchiveStorage.cs` | `src\Assets\Scripts\ArchiveSystem\Storage\IArchiveStorage.cs` | 对局结果与成长数据存档系统 |
| `Assets\Scripts\ArchiveSystem\Storage\JsonArchiveStorage.cs` | `src\Assets\Scripts\ArchiveSystem\Storage\JsonArchiveStorage.cs` | 对局结果与成长数据存档系统 |
| `Assets\Scripts\BuffSystem\ChillSlowBBM.cs` | `src\Assets\Scripts\BuffSystem\ChillSlowBBM.cs` | 元素 Buff 示例模块，配合 BuffSystem 展示扩展方式 |
| `Assets\Scripts\BuffSystem\ElectricArcBBM.cs` | `src\Assets\Scripts\BuffSystem\ElectricArcBBM.cs` | 元素 Buff 示例模块，配合 BuffSystem 展示扩展方式 |
| `Assets\Scripts\BuffSystem\ElementalBuffModules.cs` | `src\Assets\Scripts\BuffSystem\ElementalBuffModules.cs` | 元素 Buff 示例模块，配合 BuffSystem 展示扩展方式 |
| `Assets\Scripts\BuffSystem\FireIgniteBBM.cs` | `src\Assets\Scripts\BuffSystem\FireIgniteBBM.cs` | 元素 Buff 示例模块，配合 BuffSystem 展示扩展方式 |
| `Assets\Scripts\BuffSystem\ModifyAttributeBBM.cs` | `src\Assets\Scripts\BuffSystem\ModifyAttributeBBM.cs` | 元素 Buff 示例模块，配合 BuffSystem 展示扩展方式 |
| `Assets\Scripts\BuffSystem\PoisonDotBBM.cs` | `src\Assets\Scripts\BuffSystem\PoisonDotBBM.cs` | 元素 Buff 示例模块，配合 BuffSystem 展示扩展方式 |
| `Assets\Scripts\Enum\EnemyAttackType.cs` | `src\Assets\Scripts\Enum\EnemyAttackType.cs` | 核心枚举定义，支撑道具、品质、攻击类型等模块 |
| `Assets\Scripts\Enum\EnumQualityLevel.cs` | `src\Assets\Scripts\Enum\EnumQualityLevel.cs` | 核心枚举定义，支撑道具、品质、攻击类型等模块 |
| `Assets\Scripts\Enum\ItemTypeEnum.cs` | `src\Assets\Scripts\Enum\ItemTypeEnum.cs` | 核心枚举定义，支撑道具、品质、攻击类型等模块 |
| `Assets\Scripts\Enum\WeaponSpecialize.cs` | `src\Assets\Scripts\Enum\WeaponSpecialize.cs` | 核心枚举定义，支撑道具、品质、攻击类型等模块 |
| `Assets\Scripts\Excel\ColumnMapping.cs` | `src\Assets\Scripts\Excel\ColumnMapping.cs` | Excel 到 ScriptableObject 的数据工具链 |
| `Assets\Scripts\Excel\ColumnMappingType.cs` | `src\Assets\Scripts\Excel\ColumnMappingType.cs` | Excel 到 ScriptableObject 的数据工具链 |
| `Assets\Scripts\Excel\ExcelFileConfig.cs` | `src\Assets\Scripts\Excel\ExcelFileConfig.cs` | Excel 到 ScriptableObject 的数据工具链 |
| `Assets\Scripts\Excel\ExcelToSOConfig.cs` | `src\Assets\Scripts\Excel\ExcelToSOConfig.cs` | Excel 到 ScriptableObject 的数据工具链 |
| `Assets\Scripts\Excel\ExcelToSOConfigEditor.cs` | `src\Assets\Scripts\Excel\ExcelToSOConfigEditor.cs` | Excel 到 ScriptableObject 的数据工具链 |
| `Assets\Scripts\Excel\ExcelToSOGenerator.cs` | `src\Assets\Scripts\Excel\ExcelToSOGenerator.cs` | Excel 到 ScriptableObject 的数据工具链 |
| `Assets\Scripts\Excel\MultiFieldItem.cs` | `src\Assets\Scripts\Excel\MultiFieldItem.cs` | Excel 到 ScriptableObject 的数据工具链 |
| `Assets\Scripts\Excel\WorksheetConfig.cs` | `src\Assets\Scripts\Excel\WorksheetConfig.cs` | Excel 到 ScriptableObject 的数据工具链 |
| `Assets\Scripts\Interaction\IInteractable.cs` | `src\Assets\Scripts\Interaction\IInteractable.cs` | 交互与拾取系统，MissionSystem 捕获任务和玩家代理依赖 |
| `Assets\Scripts\Interaction\InteractionDetector.cs` | `src\Assets\Scripts\Interaction\InteractionDetector.cs` | 交互与拾取系统，MissionSystem 捕获任务和玩家代理依赖 |
| `Assets\Scripts\Interaction\MODULE.md` | `src\Assets\Scripts\Interaction\MODULE.md` | 交互与拾取系统，MissionSystem 捕获任务和玩家代理依赖 |
| `Assets\Scripts\Interaction\PickupItem.cs` | `src\Assets\Scripts\Interaction\PickupItem.cs` | 交互与拾取系统，MissionSystem 捕获任务和玩家代理依赖 |
| `Assets\Scripts\Interaction\WorldBillboardUI.cs` | `src\Assets\Scripts\Interaction\WorldBillboardUI.cs` | 交互与拾取系统，MissionSystem 捕获任务和玩家代理依赖 |
| `Assets\Scripts\InventorySystem\InGameInventoryData.cs` | `src\Assets\Scripts\InventorySystem\InGameInventoryData.cs` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\InGameInventoryManager.cs` | `src\Assets\Scripts\InventorySystem\InGameInventoryManager.cs` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\InventoryItem.cs` | `src\Assets\Scripts\InventorySystem\InventoryItem.cs` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\InventorySlot.cs` | `src\Assets\Scripts\InventorySystem\InventorySlot.cs` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\MODULE.md` | `src\Assets\Scripts\InventorySystem\MODULE.md` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\NetworkInventory.cs` | `src\Assets\Scripts\InventorySystem\NetworkInventory.cs` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\QualityEffects\EnumQualityEffectType.cs` | `src\Assets\Scripts\InventorySystem\QualityEffects\EnumQualityEffectType.cs` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\QualityEffects\Executors\ActionExecutors.cs` | `src\Assets\Scripts\InventorySystem\QualityEffects\Executors\ActionExecutors.cs` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\QualityEffects\Executors\ConditionExecutors.cs` | `src\Assets\Scripts\InventorySystem\QualityEffects\Executors\ConditionExecutors.cs` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\QualityEffects\QualityEffectConstants.cs` | `src\Assets\Scripts\InventorySystem\QualityEffects\QualityEffectConstants.cs` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\QualityEffects\QualityEffectDefinitions.cs` | `src\Assets\Scripts\InventorySystem\QualityEffects\QualityEffectDefinitions.cs` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\QualityEffects\QualityEffectRegistry.cs` | `src\Assets\Scripts\InventorySystem\QualityEffects\QualityEffectRegistry.cs` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\QualityEffects\StackingRules\AddStackingRule.cs` | `src\Assets\Scripts\InventorySystem\QualityEffects\StackingRules\AddStackingRule.cs` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\QualityEffects\StackingRules\AverageStackingRule.cs` | `src\Assets\Scripts\InventorySystem\QualityEffects\StackingRules\AverageStackingRule.cs` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\QualityEffects\StackingRules\IStackingRule.cs` | `src\Assets\Scripts\InventorySystem\QualityEffects\StackingRules\IStackingRule.cs` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\QualityEffects\StackingRules\MaxStackingRule.cs` | `src\Assets\Scripts\InventorySystem\QualityEffects\StackingRules\MaxStackingRule.cs` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\QualityEffects\StackingRules\MinStackingRule.cs` | `src\Assets\Scripts\InventorySystem\QualityEffects\StackingRules\MinStackingRule.cs` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\QualityEffects\StackingRules\NoStackingRule.cs` | `src\Assets\Scripts\InventorySystem\QualityEffects\StackingRules\NoStackingRule.cs` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\QualityEffects\StackingRules\StackByQualityRule.cs` | `src\Assets\Scripts\InventorySystem\QualityEffects\StackingRules\StackByQualityRule.cs` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\QualityEffects\StackingRules\StackingRuleRegistry.cs` | `src\Assets\Scripts\InventorySystem\QualityEffects\StackingRules\StackingRuleRegistry.cs` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\QualityEffects\品质道具系统完整文档.md` | `src\Assets\Scripts\InventorySystem\QualityEffects\品质道具系统完整文档.md` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\InventorySystem\SynthesisRecipeManager.cs` | `src\Assets\Scripts\InventorySystem\SynthesisRecipeManager.cs` | 背包、网络同步背包与品质道具 TCA 效果系统 |
| `Assets\Scripts\Managers\DebugLog.cs` | `src\Assets\Scripts\Managers\DebugLog.cs` | SOManager、EnemySpawnService、GameFlowBootstrapper 等运行时管理器 |
| `Assets\Scripts\Managers\DebugManager.cs` | `src\Assets\Scripts\Managers\DebugManager.cs` | SOManager、EnemySpawnService、GameFlowBootstrapper 等运行时管理器 |
| `Assets\Scripts\Managers\EnemySpawnService.cs` | `src\Assets\Scripts\Managers\EnemySpawnService.cs` | SOManager、EnemySpawnService、GameFlowBootstrapper 等运行时管理器 |
| `Assets\Scripts\Managers\GameFlowBootstrapper.cs` | `src\Assets\Scripts\Managers\GameFlowBootstrapper.cs` | SOManager、EnemySpawnService、GameFlowBootstrapper 等运行时管理器 |
| `Assets\Scripts\Managers\SOManager.cs` | `src\Assets\Scripts\Managers\SOManager.cs` | SOManager、EnemySpawnService、GameFlowBootstrapper 等运行时管理器 |
| `Assets\Scripts\MissionSystem\DefenseObjective.cs` | `src\Assets\Scripts\MissionSystem\DefenseObjective.cs` | 任务系统、任务语义输入、任务状态同步与房间绑定 |
| `Assets\Scripts\MissionSystem\Editor\MissionSampleAssetGenerator.cs` | `src\Assets\Scripts\MissionSystem\Editor\MissionSampleAssetGenerator.cs` | 任务系统、任务语义输入、任务状态同步与房间绑定 |
| `Assets\Scripts\MissionSystem\MissionAssetLayout.md` | `src\Assets\Scripts\MissionSystem\MissionAssetLayout.md` | 任务系统、任务语义输入、任务状态同步与房间绑定 |
| `Assets\Scripts\MissionSystem\MissionBase.cs` | `src\Assets\Scripts\MissionSystem\MissionBase.cs` | 任务系统、任务语义输入、任务状态同步与房间绑定 |
| `Assets\Scripts\MissionSystem\MissionConfig.cs` | `src\Assets\Scripts\MissionSystem\MissionConfig.cs` | 任务系统、任务语义输入、任务状态同步与房间绑定 |
| `Assets\Scripts\MissionSystem\MissionConfigurationGuide.md` | `src\Assets\Scripts\MissionSystem\MissionConfigurationGuide.md` | 任务系统、任务语义输入、任务状态同步与房间绑定 |
| `Assets\Scripts\MissionSystem\MissionContext.cs` | `src\Assets\Scripts\MissionSystem\MissionContext.cs` | 任务系统、任务语义输入、任务状态同步与房间绑定 |
| `Assets\Scripts\MissionSystem\MissionDamageableTarget.cs` | `src\Assets\Scripts\MissionSystem\MissionDamageableTarget.cs` | 任务系统、任务语义输入、任务状态同步与房间绑定 |
| `Assets\Scripts\MissionSystem\MissionImplementations.cs` | `src\Assets\Scripts\MissionSystem\MissionImplementations.cs` | 任务系统、任务语义输入、任务状态同步与房间绑定 |
| `Assets\Scripts\MissionSystem\MissionLibrary.cs` | `src\Assets\Scripts\MissionSystem\MissionLibrary.cs` | 任务系统、任务语义输入、任务状态同步与房间绑定 |
| `Assets\Scripts\MissionSystem\MissionManager.cs` | `src\Assets\Scripts\MissionSystem\MissionManager.cs` | 任务系统、任务语义输入、任务状态同步与房间绑定 |
| `Assets\Scripts\MissionSystem\MissionPointer.cs` | `src\Assets\Scripts\MissionSystem\MissionPointer.cs` | 任务系统、任务语义输入、任务状态同步与房间绑定 |
| `Assets\Scripts\MissionSystem\MissionPointerManager.cs` | `src\Assets\Scripts\MissionSystem\MissionPointerManager.cs` | 任务系统、任务语义输入、任务状态同步与房间绑定 |
| `Assets\Scripts\MissionSystem\MissionRuntimeModels.cs` | `src\Assets\Scripts\MissionSystem\MissionRuntimeModels.cs` | 任务系统、任务语义输入、任务状态同步与房间绑定 |
| `Assets\Scripts\MissionSystem\MissionTrackedTarget.cs` | `src\Assets\Scripts\MissionSystem\MissionTrackedTarget.cs` | 任务系统、任务语义输入、任务状态同步与房间绑定 |
| `Assets\Scripts\MissionSystem\MissionTriggerZone.cs` | `src\Assets\Scripts\MissionSystem\MissionTriggerZone.cs` | 任务系统、任务语义输入、任务状态同步与房间绑定 |
| `Assets\Scripts\MissionSystem\MissionUIConfigSO.cs` | `src\Assets\Scripts\MissionSystem\MissionUIConfigSO.cs` | 任务系统、任务语义输入、任务状态同步与房间绑定 |
| `Assets\Scripts\MissionSystem\MODULE.md` | `src\Assets\Scripts\MissionSystem\MODULE.md` | 任务系统、任务语义输入、任务状态同步与房间绑定 |
| `Assets\Scripts\PCG\Config\PcgGeneratePackage.cs` | `src\Assets\Scripts\PCG\Config\PcgGeneratePackage.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Config\PcgGenerationProfile.cs` | `src\Assets\Scripts\PCG\Config\PcgGenerationProfile.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Config\PcgGenerationProfileRegistry.cs` | `src\Assets\Scripts\PCG\Config\PcgGenerationProfileRegistry.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Core\DeterministicRandom.cs` | `src\Assets\Scripts\PCG\Core\DeterministicRandom.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Data\PcgGenerationModels.cs` | `src\Assets\Scripts\PCG\Data\PcgGenerationModels.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Data\PcgRequestCloneUtility.cs` | `src\Assets\Scripts\PCG\Data\PcgRequestCloneUtility.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Data\PcgTaskModels.cs` | `src\Assets\Scripts\PCG\Data\PcgTaskModels.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Data\RoomGraph.cs` | `src\Assets\Scripts\PCG\Data\RoomGraph.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Generation\PcgGenerationFailureReporter.cs` | `src\Assets\Scripts\PCG\Generation\PcgGenerationFailureReporter.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Generation\PcgGenerationResult.cs` | `src\Assets\Scripts\PCG\Generation\PcgGenerationResult.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Generation\PcgMapGenerator.cs` | `src\Assets\Scripts\PCG\Generation\PcgMapGenerator.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Generation\RoomGraphBuilder.cs` | `src\Assets\Scripts\PCG\Generation\RoomGraphBuilder.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Generation\RoomRoleAllocator.cs` | `src\Assets\Scripts\PCG\Generation\RoomRoleAllocator.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Generation\RoomRoleAllocator功能解读.md` | `src\Assets\Scripts\PCG\Generation\RoomRoleAllocator功能解读.md` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Generation\RoomStitcher.cs` | `src\Assets\Scripts\PCG\Generation\RoomStitcher.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\MODULE.md` | `src\Assets\Scripts\PCG\MODULE.md` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Navigation\Editor\NavMeshDataPreviewTool.cs` | `src\Assets\Scripts\PCG\Navigation\Editor\NavMeshDataPreviewTool.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Navigation\Editor\RoomNavMeshBakeEditor.cs` | `src\Assets\Scripts\PCG\Navigation\Editor\RoomNavMeshBakeEditor.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Navigation\PcgNavMeshAssembler.cs` | `src\Assets\Scripts\PCG\Navigation\PcgNavMeshAssembler.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Navigation\RoomPrebakedNavMeshAsset.cs` | `src\Assets\Scripts\PCG\Navigation\RoomPrebakedNavMeshAsset.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\PCG_Phase2_TaskAnchor_Plan.md` | `src\Assets\Scripts\PCG\PCG_Phase2_TaskAnchor_Plan.md` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Rooms\Instances\PcgBossSpawnPointMarker.cs` | `src\Assets\Scripts\PCG\Rooms\Instances\PcgBossSpawnPointMarker.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Rooms\Instances\PcgDefenseObjectivePointMarker.cs` | `src\Assets\Scripts\PCG\Rooms\Instances\PcgDefenseObjectivePointMarker.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Rooms\Instances\PcgResourcePointMarker.cs` | `src\Assets\Scripts\PCG\Rooms\Instances\PcgResourcePointMarker.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Rooms\Instances\PcgSpawnPointMarker.cs` | `src\Assets\Scripts\PCG\Rooms\Instances\PcgSpawnPointMarker.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Rooms\PcgConnectorMarker.cs` | `src\Assets\Scripts\PCG\Rooms\PcgConnectorMarker.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Rooms\PcgFunctionalNodes.cs` | `src\Assets\Scripts\PCG\Rooms\PcgFunctionalNodes.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Rooms\PcgRoomBounds.cs` | `src\Assets\Scripts\PCG\Rooms\PcgRoomBounds.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PCG\Rooms\PcgRoomRoot.cs` | `src\Assets\Scripts\PCG\Rooms\PcgRoomRoot.cs` | 确定性 PCG 地图生成核心模块 |
| `Assets\Scripts\PlayerControl\Camera\ThirdPersonPlayerController.cs` | `src\Assets\Scripts\PlayerControl\Camera\ThirdPersonPlayerController.cs` | 玩家输入、第三人称控制和技能输入绑定 |
| `Assets\Scripts\PlayerControl\Camera\WeaponAimController.cs` | `src\Assets\Scripts\PlayerControl\Camera\WeaponAimController.cs` | 玩家输入、第三人称控制和技能输入绑定 |
| `Assets\Scripts\PlayerControl\Input\InputController.cs` | `src\Assets\Scripts\PlayerControl\Input\InputController.cs` | 玩家输入、第三人称控制和技能输入绑定 |
| `Assets\Scripts\PlayerControl\Input\InputController.inputactions` | `src\Assets\Scripts\PlayerControl\Input\InputController.inputactions` | 玩家输入、第三人称控制和技能输入绑定 |
| `Assets\Scripts\PlayerControl\Input\PlayerInputSystem.cs` | `src\Assets\Scripts\PlayerControl\Input\PlayerInputSystem.cs` | 玩家输入、第三人称控制和技能输入绑定 |
| `Assets\Scripts\PlayerControl\MODULE.md` | `src\Assets\Scripts\PlayerControl\MODULE.md` | 玩家输入、第三人称控制和技能输入绑定 |
| `Assets\Scripts\PlayerControl\PlayerSelection\DefaultPlayerSelector.cs` | `src\Assets\Scripts\PlayerControl\PlayerSelection\DefaultPlayerSelector.cs` | 玩家输入、第三人称控制和技能输入绑定 |
| `Assets\Scripts\PlayerControl\PlayerSelection\IPlayerSelector.cs` | `src\Assets\Scripts\PlayerControl\PlayerSelection\IPlayerSelector.cs` | 玩家输入、第三人称控制和技能输入绑定 |
| `Assets\Scripts\PlayerControl\PlayerSelection\PlayerSelectionInfo.cs` | `src\Assets\Scripts\PlayerControl\PlayerSelection\PlayerSelectionInfo.cs` | 玩家输入、第三人称控制和技能输入绑定 |
| `Assets\Scripts\PlayerControl\PlayerSkillInputBinder.cs` | `src\Assets\Scripts\PlayerControl\PlayerSkillInputBinder.cs` | 玩家输入、第三人称控制和技能输入绑定 |
| `Assets\Scripts\RunSystem\HeroSelector.cs` | `src\Assets\Scripts\RunSystem\HeroSelector.cs` | 服务器权威对局状态机与结算流程 |
| `Assets\Scripts\RunSystem\LobbyManager.cs` | `src\Assets\Scripts\RunSystem\LobbyManager.cs` | 服务器权威对局状态机与结算流程 |
| `Assets\Scripts\RunSystem\MODULE.md` | `src\Assets\Scripts\RunSystem\MODULE.md` | 服务器权威对局状态机与结算流程 |
| `Assets\Scripts\RunSystem\PlayerDeathDefeat功能解读.md` | `src\Assets\Scripts\RunSystem\PlayerDeathDefeat功能解读.md` | 服务器权威对局状态机与结算流程 |
| `Assets\Scripts\RunSystem\RunConfig.cs` | `src\Assets\Scripts\RunSystem\RunConfig.cs` | 服务器权威对局状态机与结算流程 |
| `Assets\Scripts\RunSystem\RunContext.cs` | `src\Assets\Scripts\RunSystem\RunContext.cs` | 服务器权威对局状态机与结算流程 |
| `Assets\Scripts\RunSystem\RunManager.cs` | `src\Assets\Scripts\RunSystem\RunManager.cs` | 服务器权威对局状态机与结算流程 |
| `Assets\Scripts\RunSystem\RunResultUIBridge.cs` | `src\Assets\Scripts\RunSystem\RunResultUIBridge.cs` | 服务器权威对局状态机与结算流程 |
| `Assets\Scripts\RunSystem\RunResultWindow.cs` | `src\Assets\Scripts\RunSystem\RunResultWindow.cs` | 服务器权威对局状态机与结算流程 |
| `Assets\Scripts\RunSystem\RunRoomTrigger.cs` | `src\Assets\Scripts\RunSystem\RunRoomTrigger.cs` | 服务器权威对局状态机与结算流程 |
| `Assets\Scripts\RunSystem\RunSessionData.cs` | `src\Assets\Scripts\RunSystem\RunSessionData.cs` | 服务器权威对局状态机与结算流程 |
| `Assets\Scripts\RunSystem\RunStateModels.cs` | `src\Assets\Scripts\RunSystem\RunStateModels.cs` | 服务器权威对局状态机与结算流程 |
| `Assets\Scripts\RunSystem\RunSummaryCalculator.cs` | `src\Assets\Scripts\RunSystem\RunSummaryCalculator.cs` | 服务器权威对局状态机与结算流程 |
| `Assets\Scripts\SO\BaseSO.cs` | `src\Assets\Scripts\SO\BaseSO.cs` | HeroSO、SkillDefinitionSO、WeaponSO、EnemySO 等数据驱动配置定义 |
| `Assets\Scripts\SO\EnemySO\EnemySO.cs` | `src\Assets\Scripts\SO\EnemySO\EnemySO.cs` | HeroSO、SkillDefinitionSO、WeaponSO、EnemySO 等数据驱动配置定义 |
| `Assets\Scripts\SO\HeroSO\HeroSO.cs` | `src\Assets\Scripts\SO\HeroSO\HeroSO.cs` | HeroSO、SkillDefinitionSO、WeaponSO、EnemySO 等数据驱动配置定义 |
| `Assets\Scripts\SO\HeroSO\MODULE.md` | `src\Assets\Scripts\SO\HeroSO\MODULE.md` | HeroSO、SkillDefinitionSO、WeaponSO、EnemySO 等数据驱动配置定义 |
| `Assets\Scripts\SO\HeroSO\PassiveAbilityDef.cs` | `src\Assets\Scripts\SO\HeroSO\PassiveAbilityDef.cs` | HeroSO、SkillDefinitionSO、WeaponSO、EnemySO 等数据驱动配置定义 |
| `Assets\Scripts\SO\HeroSO\SkillAbilityDef.cs` | `src\Assets\Scripts\SO\HeroSO\SkillAbilityDef.cs` | HeroSO、SkillDefinitionSO、WeaponSO、EnemySO 等数据驱动配置定义 |
| `Assets\Scripts\SO\InventoryItemSO\ActiveSkillItemSO.cs` | `src\Assets\Scripts\SO\InventoryItemSO\ActiveSkillItemSO.cs` | HeroSO、SkillDefinitionSO、WeaponSO、EnemySO 等数据驱动配置定义 |
| `Assets\Scripts\SO\InventoryItemSO\BaseInventoryItemSO.cs` | `src\Assets\Scripts\SO\InventoryItemSO\BaseInventoryItemSO.cs` | HeroSO、SkillDefinitionSO、WeaponSO、EnemySO 等数据驱动配置定义 |
| `Assets\Scripts\SO\InventoryItemSO\ConsumableItemSO.cs` | `src\Assets\Scripts\SO\InventoryItemSO\ConsumableItemSO.cs` | HeroSO、SkillDefinitionSO、WeaponSO、EnemySO 等数据驱动配置定义 |
| `Assets\Scripts\SO\InventoryItemSO\QualityItemSO.cs` | `src\Assets\Scripts\SO\InventoryItemSO\QualityItemSO.cs` | HeroSO、SkillDefinitionSO、WeaponSO、EnemySO 等数据驱动配置定义 |
| `Assets\Scripts\SO\InventoryItemSO\WeaponSO.cs` | `src\Assets\Scripts\SO\InventoryItemSO\WeaponSO.cs` | HeroSO、SkillDefinitionSO、WeaponSO、EnemySO 等数据驱动配置定义 |
| `Assets\Scripts\SO\SkillSO\SkillDefinitionSO.cs` | `src\Assets\Scripts\SO\SkillSO\SkillDefinitionSO.cs` | HeroSO、SkillDefinitionSO、WeaponSO、EnemySO 等数据驱动配置定义 |
| `Assets\Scripts\SpawnSystem\IMonsterSpawnSystem.cs` | `src\Assets\Scripts\SpawnSystem\IMonsterSpawnSystem.cs` | 玩家生成与怪物刷怪配置接口 |
| `Assets\Scripts\SpawnSystem\MonsterSpawnConfig.cs` | `src\Assets\Scripts\SpawnSystem\MonsterSpawnConfig.cs` | 玩家生成与怪物刷怪配置接口 |
| `Assets\Scripts\SpawnSystem\PlayerSpawnManager.cs` | `src\Assets\Scripts\SpawnSystem\PlayerSpawnManager.cs` | 玩家生成与怪物刷怪配置接口 |
| `Assets\Scripts\Tools\CoordinateConverter.cs` | `src\Assets\Scripts\Tools\CoordinateConverter.cs` | 配置加载、坐标转换和编辑辅助工具 |
| `Assets\Scripts\Tools\HeroSOLoader.cs` | `src\Assets\Scripts\Tools\HeroSOLoader.cs` | 配置加载、坐标转换和编辑辅助工具 |
| `Assets\Scripts\Tools\ItemParameterTools.cs` | `src\Assets\Scripts\Tools\ItemParameterTools.cs` | 配置加载、坐标转换和编辑辅助工具 |
| `Assets\Scripts\Tools\ParentFinder.cs` | `src\Assets\Scripts\Tools\ParentFinder.cs` | 配置加载、坐标转换和编辑辅助工具 |
| `Assets\Scripts\Tools\TextTools.cs` | `src\Assets\Scripts\Tools\TextTools.cs` | 配置加载、坐标转换和编辑辅助工具 |
| `Assets\Scripts\Tools\WeaponConfigLoader.cs` | `src\Assets\Scripts\Tools\WeaponConfigLoader.cs` | 配置加载、坐标转换和编辑辅助工具 |
| `Assets\Scripts\Tools\WeaponSOLoader.cs` | `src\Assets\Scripts\Tools\WeaponSOLoader.cs` | 配置加载、坐标转换和编辑辅助工具 |

## 跳过内容

- Unity 生成目录：Library/、Temp/、Logs/、obj/、UserSettings/、UIElementsSchema/
- IDE 生成文件：.idea/、.vs/、.vscode/、*.csproj、*.sln、*.lscache
- 第三方与商业插件目录：Assets/ThirdParty/、Assets/Plugins/、Assets/Behavior Designer/、Assets/AmplifyShaderEditor/、Assets/DOTween/ 等
- 大体积或授权不明资源：Assets/Resources/Modules/、Assets/Bullet_Impact_FX/、Assets/EnemyModel/、Assets/TirgamesAssets/、Assets/PolygonDungeon/
- Unity 二进制资源：*.unity、*.prefab、*.asset、*.mat、*.controller、*.anim
- 美术与音频资源：*.fbx、*.obj、*.png、*.jpg、*.tga、*.tif、*.wav、*.mp3、*.ogg、*.shader、*.shadergraph
- 源项目根目录 CLAUDE.md、CONTEXT.md：偏内部上下文，本次未纳入公开 showcase
- Assets/Scripts/Test/：测试与调试启动脚本，不作为公开核心代码迁移
- Assets/Scripts/UI/ 与 Assets/Scripts/ShopSystem/：不属于本次核心技术筛选主线，后续可人工选择性补充

## 需要人工确认

- `src/Assets/Framework/NetworkLayer/Proxy/BossProxy/BossNetworkProxy.cs` 与 `src/Assets/Framework/LogicLayer/EnemyLogic/BossTasks/` 引用了 Behavior Designer，Showcase 不包含该商业插件。
- `src/Assets/Framework/LogicLayer/Module/AIModule/Navigation/*` 与 PCG/NavMesh/场景资源绑定较强，公开仓库仅作代码阅读。
- `src/Assets/Framework/NetworkLayer/ServerAuthority/AttributeSystem/ServerAttributeModule.cs` 文件较大且包含较多调试日志，正式公开前建议整理注释与日志。
- `src/Assets/Scripts/InventorySystem/QualityEffects/Executors/ActionExecutors.cs` 中部分召唤、Tick 类能力属于扩展点，应在 README 标注当前实现边界。
- `docs/CodingPlan/**` 属于开发计划与差距分析文档，公开前建议人工确认表达是否适合对外展示。
- 源项目 `Packages/manifest.json` 存在本机 file: 依赖，本次未复制；如后续需要 Unity 包信息，应制作脱敏版 manifest。

## 下一步建议

- 为 README 补充演示视频链接、核心截图、个人职责说明和模块阅读路线图。
- 如需让仓库更像正式 GitHub 项目，可将 Mermaid 架构图补充到 `diagrams/`。
- 若希望提供可运行 Demo，应另建精简 Unity 工程并替换所有第三方资源为占位资源。
