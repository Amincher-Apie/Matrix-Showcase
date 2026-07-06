# Matrix 演示视频脚本

建议视频长度：5 到 8 分钟。  
目标观众：面试官、导师、同学或技术评审者。  
目标表达：这是一个多人 Roguelike TPS 技术原型，重点展示系统设计与工程能力。

## 0. 开场说明

时间：20 秒

画面建议：

- 展示项目标题页或 GitHub README。
- 显示一句话定位。

讲解示例：

> 大家好，这是 Matrix，一个基于 Unity 和 Netcode for GameObjects 的多人 Roguelike TPS 技术原型。这个展示仓库不是完整可运行工程，而是公开了核心自研代码和架构文档，用来展示服务器权威网络、确定性 PCG、任务系统、战斗管线、技能 Buff、品质道具、AI 调度和数据驱动工具链。

## 1. 说明 Showcase 边界

时间：30 秒

画面建议：

- 打开 `README.md` 的“为什么不是完整可运行工程”部分。
- 打开 `NOTICE.md`。

讲解要点：

- 完整工程不公开。
- 原因是第三方资源授权、工程体积和本地依赖。
- 当前仓库用于技术评审和代码阅读。

讲解示例：

> 这里我先说明边界：这个仓库不包含模型、贴图、音频、商业插件、完整场景和 Prefab，所以不能直接 clone 后运行完整游戏。公开内容主要是自研 C# 代码、模块文档和架构说明。

## 2. 展示整体架构

时间：60 秒

画面建议：

- 打开 `docs/architecture.md`。
- 展示架构图或目录树。

讲解要点：

- `Assets/Scripts` 是游戏层。
- `Assets/Framework` 是框架层。
- NetworkLayer 负责网络同步。
- LogicLayer 负责战斗、属性、技能、AI 等逻辑。
- EventCenter 负责模块解耦。

讲解示例：

> 整体架构分成 Game Layer 和 Framework Layer。游戏层负责 Run、Mission、PCG、Inventory 等具体玩法系统。框架层包含网络层、逻辑层、表现层和基础设施。核心原则是客户端提交意图，服务端验证和结算，表现层通过事件响应结果。

## 3. 展示服务器权威网络

时间：60 到 90 秒

画面建议：

- 打开 `src/Assets/Framework/NetworkLayer/Proxy/PlayerProxy/PlayerNetworkProxy.cs`。
- 打开 `NetworkObjectManager.cs`。
- 打开 `ServerAuthority/AttributeSystem/ServerAttributeModule.cs`。

讲解要点：

- `PlayerNetworkProxy` 是客户端请求入口。
- `ServerRpc` 提交开火、技能、武器切换等意图。
- 服务端模块负责验证和执行。
- `NetworkObjectManager` 用网络 ID 查询逻辑对象。

讲解示例：

> 这里是玩家网络代理。客户端不会直接修改核心状态，而是通过 ServerRpc 提交请求。服务端在 ServerAuthority 模块里做验证、结算和同步。为了避免到处 GetComponent，我做了 NetworkObjectManager，用 NetworkObjectId 统一查找网络代理、逻辑对象和表现对象。

## 4. 展示 PCG 地图生成

时间：90 秒

画面建议：

- 展示游戏中的地图生成结果，或者展示 PCG 代码。
- 打开 `PcgMapGenerator.cs`、`RoomGraphBuilder.cs`、`RoomRoleAllocator.cs`、`RoomStitcher.cs`。

讲解要点：

- 输入是 Seed、StyleKey、TaskInput。
- `RoomGraphBuilder` 构建拓扑。
- `RoomRoleAllocator` 分配 Start、Boss、支线任务房间。
- `RoomStitcher` 负责物理拼接。
- 失败时有诊断报告。

讲解示例：

> PCG 的输入不是单纯随机，而是包含任务语义。MissionSystem 会告诉 PCG 本局需要什么主线和支线任务，PCG 再把这些任务映射到房间角色上。这样任务和地图生成是解耦的，但又能互相影响。

## 5. 展示 RunSystem / MissionSystem

时间：60 到 90 秒

画面建议：

- 打开 `RunManager.cs`。
- 打开 `MissionManager.cs` 和 `MissionRuntimeModels.cs`。

讲解要点：

- RunManager 管理对局状态。
- MissionManager 构建任务组。
- `MissionNetState` 同步任务状态。
- Boss、歼灭、防守、捕获、破坏任务共用任务基类。

讲解示例：

> RunSystem 负责大的流程，比如大厅、生成地图、探索、Boss 战和结算。MissionSystem 负责具体任务，它先生成任务组，再转换为 PCG 输入，地图生成后再把任务绑定回具体房间。任务状态通过 NetworkList 同步给客户端。

## 6. 展示战斗、技能、Buff 和品质道具

时间：90 秒

画面建议：

- 展示开火、命中、扣血、跳字。
- 打开 `DamageCalculator.cs`、`BuffHandler.cs`、`QualityEffectDefinitions.cs`。

讲解要点：

- 开火请求由服务端验证。
- 伤害结构和结果结构可网络序列化。
- Buff 有生命周期和回调。
- QualityEffects 使用 Trigger / Condition / Action。

讲解示例：

> 战斗系统拆成请求、命中、伤害计算、属性变化和表现事件。品质道具系统采用 TCA 模式，也就是 Trigger、Condition、Action。这样一个道具效果可以由触发时机、判断条件和执行动作组合出来，而不是每个道具写一份硬编码逻辑。

## 7. 展示 AI 调度

时间：60 到 90 秒

画面建议：

- 展示敌人巡逻、追击或群体移动。
- 打开 `EnemyAIModule.cs`、`AIScheduler.cs`、`PerceptionSystem.cs`、`BoidsCentralController.cs`。

讲解要点：

- 敌人使用状态机。
- 感知系统选择目标。
- Scheduler 根据距离、热点和战斗状态调整 Tick。
- Boids 和 Steering 处理群体移动。

讲解示例：

> AI 不只是一个简单状态机。普通敌人有感知、追击、攻击等状态，同时服务端用 AIScheduler 对敌人进行分级 Tick，远离玩家或不重要的敌人会降低更新频率。为了改善群体移动，还加入了 Steering 和 Boids。

## 8. 展示数据驱动工具链

时间：45 秒

画面建议：

- 打开 `HeroSO.cs`、`SkillDefinitionSO.cs`、`MissionConfig.cs`、`PcgGenerationProfile.cs`。

讲解要点：

- 英雄、技能、任务、地图风格、道具均由 SO 驱动。
- ExcelToSO 用于配置导入。
- Showcase 不包含真实 `.asset` 资源。

讲解示例：

> 项目的内容配置主要通过 ScriptableObject 表达。比如英雄配置、技能配置、任务配置、地图风格和品质道具效果。这里展示的是代码定义和工具链，真实资源资产没有公开。

## 9. 结尾

时间：20 秒

讲解示例：

> 总结一下，Matrix 不是一个已经商业上线的游戏，而是一个围绕多人 Roguelike TPS 搭建的技术原型。这个 Showcase 主要展示我在网络架构、PCG、任务系统、战斗管线、AI 调度和数据驱动工具链上的设计和实现能力。

## 视频录制注意事项

- 不展示本机绝对路径、账号、云仓库名或插件授权界面。
- 不要把第三方模型、贴图、音频称为原创。
- 不要说 GitHub 仓库可以直接运行完整项目。
- 讲解时把“已完成”和“原型阶段 / 扩展点”分清楚。
- 如果展示 Unity Editor，尽量聚焦运行效果和代码结构，不展开第三方资源目录。
