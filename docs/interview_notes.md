# Matrix 面试讲解要点

本文用于准备求职或升学面试中的项目讲解。

## 1. 30 秒版本

> Matrix 是一个 Unity 多人 Roguelike TPS 技术原型。我主要展示的是核心工程系统：基于 Netcode for GameObjects 的服务器权威网络架构、确定性 PCG 地图生成、任务系统、战斗数值管线、技能 Buff 和品质道具系统、AI 调度以及 ScriptableObject 数据驱动工具链。公开仓库是 Showcase 版，只包含自研代码和文档，不包含完整资源工程。

## 2. 2 分钟版本

> 这个项目的核心是把多人 Roguelike TPS 拆成几条稳定管线。第一条是网络管线，客户端只提交输入意图，服务端通过 ServerAuthority 模块验证和结算，再用 NetworkVariable、NetworkList 和 ClientRpc 同步结果。第二条是 PCG 和任务管线，MissionSystem 先生成任务语义输入，PCG 根据 Seed、地图风格和任务输入生成房间拓扑，并把 Boss、歼灭、防守、捕获、破坏等任务分配到地图上。第三条是战斗和构筑管线，开火、命中、伤害、属性、Buff、技能和品质道具效果被拆分成独立模块，通过事件中心和服务端模块协作。AI 部分则用状态机、感知系统、AIScheduler、兴趣区域和 Boids 做敌人行为和性能控制。这个仓库不是完整运行工程，主要用于展示架构和核心代码。

## 3. 重点可讲模块

### NetworkLayer

可讲问题：

- 为什么要做 `NetworkProxyBase`。
- 为什么需要 `NetworkObjectManager`。
- `ServerRpc` 如何做权限和状态验证。
- 网络对象池如何避免频繁 Instantiate / Destroy。

回答方向：

> 我把网络对象和游戏逻辑之间加了一层 Proxy。Proxy 负责 NGO 生命周期和 RPC 入口，LogicLayer 负责业务逻辑。这样网络层和逻辑层不会完全混在一起，也方便通过 NetworkObjectId 查找对象。

### PCG

可讲问题：

- 如何保证地图生成可复现。
- 任务如何影响地图生成。
- 房间如何拼接。
- 失败时如何诊断。

回答方向：

> PCG 的输入包括 Seed、StyleKey 和 Mission TaskInput。生成流程分为拓扑构建、房间角色分配、房间实例化、物理拼接和功能点收集。每个阶段使用派生子 Seed，保证同一输入下结果可复现，同时不同阶段互不干扰。

### MissionSystem

可讲问题：

- 为什么任务系统不直接控制地图。
- 如何同步任务状态。
- 如何支持多种任务类型。

回答方向：

> MissionSystem 先表达“这局需要什么任务”，PCG 再决定“这些任务放到哪些房间”。任务状态用 `MissionNetState` 同步，具体任务继承 `MissionBase`，不同类型只实现各自完成条件。

### Combat / Attribute / Damage

可讲问题：

- 伤害计算在哪里发生。
- 如何处理 Buff 和品质道具的触发。
- 如何广播死亡事件。

回答方向：

> 服务端收到开火或技能请求后，先完成命中判断，再生成 DamageInfo，经过 DamageCalculator 得到 DamageResult，最后由 ServerAttributeModule 扣血、扣盾、触发 Buff 和品质道具回调，并通过 EventCenter 广播 UnitDamaged 或 UnitDied。

### QualityEffects

可讲问题：

- 为什么用 Trigger / Condition / Action。
- 如何扩展新效果。
- 如何处理叠加。

回答方向：

> TCA 模式能把“何时触发、是否满足、执行什么”拆开。新增条件或动作时只需要注册一个执行器，之后多个道具都可以复用。叠加规则通过独立 StackingRule 处理，避免写死在道具逻辑里。

### AI

可讲问题：

- AI 为什么需要调度器。
- 如何减少大量敌人的性能开销。
- PCG 地图如何影响 AI 导航。

回答方向：

> 普通敌人使用状态机和感知系统。服务端的 AIScheduler 会根据距离、兴趣区域和战斗状态分配不同仿真级别，减少远处敌人的 Tick 频率。导航上还适配了 PCG 生成的房间拓扑和 NavMeshLink。

## 4. 不要夸大的点

- 不要说这是完整商业游戏。
- 不要说公开仓库可以直接运行完整工程。
- 不要说第三方模型、贴图、插件是自己原创。
- 不要说所有系统都达到生产级完成度。
- 不要隐藏当前有 TODO、占位逻辑或 Inspector 绑定依赖。

## 5. 可以主动说明的限制

- Showcase 是代码和文档展示版。
- 完整工程因资源授权、体积和本地依赖不公开。
- 部分系统需要 Unity Inspector 中的 Prefab 和 SO 绑定。
- 部分 Boss 行为依赖商业插件 Behavior Designer。
- 部分模块还处于原型阶段，后续可继续打磨。

## 6. 推荐讲解顺序

1. 一句话说明项目定位。
2. 说明 Showcase 边界。
3. 展示整体架构图。
4. 讲 NetworkLayer。
5. 讲 PCG 和 MissionSystem。
6. 讲战斗、技能、Buff、品质道具。
7. 讲 AI 调度。
8. 总结个人工作和后续计划。

## 7. 面试官可能追问

### 如果没有资源，怎么证明项目不是空架子？

回答：

> 仓库中保留了核心源码、模块文档和迁移报告，能看到系统之间的调用关系、网络同步结构、PCG 管线、任务状态机和效果系统设计。资源不公开是授权和体积原因，不影响对核心工程能力的评估。

### 为什么不用完整开源？

回答：

> 完整工程包含第三方商业插件和大量资源资产，我不能将这些内容重新分发。公开 Showcase 是更合适的方式，既能展示自研系统，也能避免授权风险。

### 这个项目最大技术难点是什么？

回答方向：

> 难点不在单个功能，而在多个系统之间的边界。比如 MissionSystem 要影响 PCG，但不能和地图实例强耦合；客户端要有即时反馈，但核心状态必须由服务端结算；AI 要在 PCG 地图里移动，还要控制大量敌人的 Tick 成本。
