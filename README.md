# Matrix Unity Roguelike TPS Showcase

## 项目名称

**Matrix** 是一个 Unity 多人 Roguelike 第三人称射击技术原型。  
本仓库是 `Matrix` 的 GitHub 技术展示版，重点呈现核心自研代码、系统架构、模块文档和个人工程实践。

## 项目定位

Matrix 面向多人合作 Roguelike TPS 场景，围绕“服务器权威 + 程序化地图 + 任务驱动对局 + 数据驱动战斗构筑”搭建技术原型。项目关注的不是美术包装或商业化内容，而是以下工程能力：

- 多人网络同步与服务器权威设计
- 确定性 PCG 地图生成
- Run / Mission 对局流程组织
- 战斗、属性、伤害、技能、Buff 和品质道具管线
- AI 状态机、感知、调度和群体移动
- ScriptableObject 配置与工具链

## 为什么不是完整可运行工程

本仓库**不是完整 Unity 工程**，也不承诺 clone 后可以直接运行完整游戏。完整项目未公开，主要原因是：

- 完整工程包含第三方或商业插件，公开再分发存在授权风险。
- 完整工程包含模型、贴图、音频、Shader、动画、Prefab、Scene 等大量资源，体积较大，不适合技术展示仓库。
- 原工程依赖 Unity Editor、Inspector 绑定、本机 Package 路径和项目内资源配置，直接公开无法保证他人环境可复现。
- Showcase 的目标是帮助面试官、导师或其他 AI 理解项目架构和核心实现，而不是发布完整游戏。

## 演示视频
b站:https://www.bilibili.com/video/BV1c8M86CEsx

百度网盘：https://pan.baidu.com/s/1-6d0EIUiknZmfu6E8BYEFA?pwd=9865

## 核心技术亮点

| 方向 | 亮点 |
|---|---|
| 服务器权威网络 | 基于 Netcode for GameObjects，使用 `ServerRpc`、`NetworkVariable`、`NetworkList`、`ClientRpc` 构建服务端权威链路 |
| 网络对象架构 | 使用 `NetworkProxyBase`、`PlayerNetworkProxy`、`EnemyNetworkProxy`、`NetworkObjectManager` 和 `NetworkObjectPool` 管理对象生命周期 |
| 确定性 PCG | 使用 Seed、`DeterministicRandom`、`RoomGraph`、`RoomRoleAllocator`、`RoomStitcher` 生成可复现地图 |
| 任务系统 | `MissionSystem` 将任务语义输入传递给 PCG，并通过 `MissionNetState` 同步任务状态 |
| 对局流程 | `RunSystem` 管理大厅、地图生成、探索、Boss 战、胜负和结算 |
| 战斗数值管线 | `CombatModule`、`AttributeModule`、`DamageCenter` 拆分开火、命中、伤害计算、属性变化和事件广播 |
| 技能与 Buff | 技能执行器、Buff 生命周期和服务器权威模块分层实现 |
| 品质道具系统 | 使用 Trigger / Condition / Action 模式配置道具效果，支持叠加规则和参数缩放 |
| AI 调度 | 状态机、感知系统、`AIScheduler`、LOD Tick、InterestRegion、Boids 和 PCG 拓扑寻路协作 |
| 数据驱动工具链 | 使用 HeroSO、SkillDefinitionSO、MissionConfig、PcgGenerationProfile、ExcelToSO 等配置系统 |

## 仓库目录结构

```text
Matrix-Showcase/
├── README.md
├── NOTICE.md
├── LICENSE
├── MIGRATION_REPORT.md
├── docs/
│   ├── architecture.md
│   ├── module_overview.md
│   ├── demo_script.md
│   ├── interview_notes.md
│   ├── github_release_notes.md
│   ├── risk_and_limitations.md
│   ├── ai-readable-project-brief.md
│   └── project/
├── diagrams/
├── examples/
├── media/
└── src/
    └── Assets/
        ├── Framework/
        │   ├── EventCenter/
        │   ├── LogicLayer/
        │   ├── NetworkLayer/
        │   ├── RenderLayer/
        │   └── UI/
        └── Scripts/
            ├── PCG/
            ├── RunSystem/
            ├── MissionSystem/
            ├── InventorySystem/
            ├── SO/
            ├── PlayerControl/
            ├── ArchiveSystem/
            ├── Excel/
            └── Tools/
```

## 核心模块说明

| 模块 | 路径 | 说明 |
|---|---|---|
| NetworkLayer | `src/Assets/Framework/NetworkLayer` | 网络代理、对象注册、对象池、ServerAuthority 模块 |
| PCG | `src/Assets/Scripts/PCG` | 确定性地图生成、房间图、任务房间分配和房间拼接 |
| RunSystem | `src/Assets/Scripts/RunSystem` | 对局生命周期状态机 |
| MissionSystem | `src/Assets/Scripts/MissionSystem` | 主线 / 支线任务、任务状态同步和 PCG 绑定 |
| Combat / Attribute / Damage | `src/Assets/Framework/LogicLayer/Module`、`src/Assets/Framework/LogicLayer/DamageCenter` | 战斗输入、属性修改、伤害计算和死亡事件 |
| Skill / Buff | `src/Assets/Framework/LogicLayer/SkillSystem`、`src/Assets/Framework/LogicLayer/BuffSystem` | 技能执行器、Buff 数据和回调管线 |
| QualityEffects | `src/Assets/Scripts/InventorySystem/QualityEffects` | 数据驱动品质道具效果系统 |
| AI | `src/Assets/Framework/LogicLayer/Module/AIModule` | 状态机、感知、调度、兴趣区域、Boids 与导航 |
| SO / Tools | `src/Assets/Scripts/SO`、`src/Assets/Scripts/Excel`、`src/Assets/Scripts/Tools` | ScriptableObject 配置和数据工具链 |

详细说明见 [`docs/module_overview.md`](docs/module_overview.md)。

## 运行限制

- 本仓库未包含 Unity `ProjectSettings`、`Packages`、Scene、Prefab、ScriptableObject 资产和第三方资源。
- 部分代码依赖 Unity Inspector 中的序列化字段绑定，无法仅从源码确认运行时对象引用。
- 部分模块引用商业插件或第三方运行时，例如 Behavior Designer、Odin、Amplify Shader Editor 等，Showcase 不包含这些插件。
- 本仓库适合阅读、评审和讲解，不适合作为完整游戏工程运行。

## 第三方资源说明

完整工程中使用过第三方插件、示例资源、模型、贴图、音频、Shader 或字体资源。本仓库不声明这些资源为原创，也不重新分发这些资源。相关说明见 [`NOTICE.md`](NOTICE.md)。

## 我的主要工作

本项目中，我主要围绕核心玩法系统和工程架构进行设计、实现与整理：

- 设计多人 Roguelike TPS 的核心模块拆分与代码结构。
- 实现服务器权威网络架构中的代理层、对象注册、对象池和服务端模块。
- 搭建确定性 PCG 生成管线，包括房间图、角色分配、任务房间绑定和拼接流程。
- 实现 RunSystem / MissionSystem，将任务语义、地图生成和对局流程串联起来。
- 设计战斗、属性、伤害、技能、Buff 和品质道具效果的可扩展管线。
- 实现 AI 状态机、感知、调度和群体移动相关系统。
- 整理模块文档、架构说明和对外展示材料。


## 后续计划

- 将 `diagrams/` 中的架构图补齐为 Mermaid 或 PNG。
- 对公开代码中的调试日志和 TODO 做一次展示向整理。
- 为关键模块补充更短的“读代码入口说明”。
- 如需可运行 Demo，另行制作精简 Unity 工程，并使用占位资源替换第三方资源。

## 阅读建议

- 快速了解项目：[`docs/ai-readable-project-brief.md`](docs/ai-readable-project-brief.md)
- 看系统架构：[`docs/architecture.md`](docs/architecture.md)
- 看模块拆分：[`docs/module_overview.md`](docs/module_overview.md)
- 了解公开边界：[`docs/risk_and_limitations.md`](docs/risk_and_limitations.md)
