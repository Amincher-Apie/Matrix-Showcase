# PROJECT_OVERVIEW.md — Matrix 项目概览

> 项目：Matrix | 引擎：Unity 2022.3 URP | 网络：Netcode for GameObjects 1.12.2  
> 文档版本：v0.4 | 更新日期：2026-05-17

---

## 1. 项目定位

**多人合作 Roguelike 第三人称射击游戏。** 玩家组成小队进入程序化生成的地图，完成主线 Boss 讨伐和支线任务（歼灭/防御/捕获/破坏），通过道具品质效果和武器系统构建角色能力，最终击败 Boss 完成一轮对局。

| 维度 | 当前状态 |
|------|---------|
| 开发阶段 | 早期开发 / 核心系统搭建阶段 |
| 网络模式 | 服务器权威（ServerAuthority），Netcode for GameObjects |
| 画面管线 | URP 14.0.12 |
| 核心工具链 | Odin Inspector、DOTween、Cinemachine 2.10.5、Amplify Shader Editor |

## 2. 当前已知核心玩法流程

```
开始页面 → 单人/多人选择
    → 任务选择（MissionSelectWindow：选地图/看敌人/选角色/配武器）
    → 进入战斗（单人 Host）/ 加入大厅（多人房主）
    → 地图程序化生成 → PlayerSpawnManager 批量生成玩家 → 进入出生房间
    → 自由探索（MonsterSpawnManager 按玩家位置刷怪）
    → 杀怪掉落（EnemyDropTableSO 查表概率掉落）
    → 进入 Boss 房间 → 触发 Boss 任务 → 击败 Boss → 对局胜利
    → 结算 → 返回大厅
```

**当前完成度**：
- 开始页面：`BeginWindow` 完整（单人/多人入口）
- 任务选择：`MissionSelectWindow` Phase 1 壳子（地图循环 + 敌人预览 + 占位）
- 大厅：`LobbyWindow` Phase 1 壳子 + `LobbyManager` P0 占位（自动跳过）
- 英雄选择：HeroSO 体系已实现（数据驱动：AttributeConfig + SkillDefinitionSO + PassiveExecutorSO + 英雄 Prefab）
- 地图生成：完整（PCG 管线），**已改为开放地图制**
- 玩家生成：`PlayerSpawnManager` 完整（PCG 完成后批量 + 中途加入支持）
- 刷怪系统：`MonsterSpawnManager` 完整（区域权重 + 玩家位置驱动），难度等级已接入
- Style ↔ 敌人映射：`PcgGenerationProfile.AvailableEnemies` 字段就绪，刷怪过滤 TODO
- 掉落系统：`ServerEnemyAttributeModule.HandleDeath()` → `TrySpawnDropsOnServer()` 完整
- Boss 战：`BossMission` → `BossDefeatedEvt` → `RunVictory` → `RunSummary` 完整
- 结算：基本完成（`RunSummaryCalculator` → `ArchiveManager`）

## 3. 当前已知模块列表

### 框架层（Framework）

| 模块 | 路径 | 状态 |
|------|------|------|
| Singleton 基础层 | `Assets/Framework/Singleton/` | 稳定 |
| EventCenter 事件中心 | `Assets/Framework/EventCenter/` | **已文档化** |
| Mono 管理器 | `Assets/Framework/Mono/` | 基础组件（驱动 Update） |
| Json 序列化 | `Assets/Framework/Json/` | 基础组件（ArchiveSystem 使用） |
| Resource 资源加载 | `Assets/Framework/Resource/` | 基础组件 |
| Pool 对象池 | `Assets/Framework/Pool/` | 基础组件 |
| Audio 音频 | `Assets/Framework/Audio/` | 基础组件 |
| EffectRender 特效 | `Assets/Framework/EffectRender/` | 基础组件 |
| UI Framework | `Assets/Framework/UI/` | **已文档化** |
| NetworkObjectPool | `Assets/Framework/NetworkLayer/NetworkObjectPool/` | 含于 NetworkLayer 文档 |

### 逻辑层（Framework/LogicLayer）

| 模块 | 路径 | 状态 |
|------|------|------|
| LogicActor 逻辑角色 | `.../LogicLayer/LogicActor/` | 基础结构（含于各模块文档） |
| PlayerActor / EnemyActor | `.../LogicLayer/PlayerLogic/` `.../EnemyLogic/` | 含于 AI/Combat/Skill 文档 |
| IPassiveExecutor 被动接口 | `.../LogicLayer/Interfaces/IPassiveExecutor.cs` | **新模块**（HeroSO 系统联动） |
| Attribute 属性模块 | `.../Module/AttributeModule/` | **已文档化** |
| Combat 战斗模块 | `.../Module/CombatModule/` | **已文档化** |
| Skill 技能模块 | `.../SkillSystem/` | **已文档化** |
| Buff 系统 | `.../BuffSystem/` | **已文档化** |
| AI 模块 | `.../Module/AIModule/` | **已文档化**（48 文件） |
| DamageCenter 伤害 | `.../DamageCenter/` | **已文档化** |

### 网络层（Framework/NetworkLayer）

| 模块 | 路径 | 状态 |
|------|------|------|
| ServerAttributeModule | `.../ServerAuthority/AttributeSystem/` | **已文档化**（功能解读） |
| ServerCombatModule | `.../ServerAuthority/CombatSystem/` | 含于 Combat 文档 |
| ServerBuffModule | `.../ServerAuthority/BuffSystem/` | 含于 NetworkLayer + BuffSystem 文档 |
| ServerSkillModule | `.../ServerAuthority/SkillSystem/` | 含于 Skill 文档 |
| PlayerProxy / EnemyProxy | `.../Proxy/` | 含于 NetworkLayer 文档 |
| ClientAuthority | `.../ClientAuthority/` | 含于 NetworkLayer 文档 |
| NetworkLayer 整体 | `.../NetworkLayer/` | **已文档化** |

### 游戏层（Scripts）

| 模块 | 路径 | 状态 |
|------|------|------|
| RunSystem 对局系统 | `Assets/Scripts/RunSystem/` | **已文档化** |
| MissionSystem 任务系统 | `Assets/Scripts/MissionSystem/` | **已文档化** |
| PCG 程序化生成 | `Assets/Scripts/PCG/` | **已文档化**（含 RoomRoleAllocator 功能解读） |
| InventorySystem 背包 | `Assets/Scripts/InventorySystem/` | **已文档化**（含 QualityEffects） |
| QualityEffects 品质效果 | `.../InventorySystem/QualityEffects/` | **已文档化**（另有策划自写中文文档） |
| ArchiveSystem 存档 | `Assets/Scripts/ArchiveSystem/` | **已文档化** |
| PlayerControl 玩家控制 | `Assets/Scripts/PlayerControl/` | **已文档化** |
| SpawnSystem 刷怪/玩家生成 | `Assets/Scripts/SpawnSystem/` | 轻量模块（PlayerSpawnManager + 配置） |
| HeroSO 英雄数据 | `Assets/Scripts/SO/HeroSO/` | **新模块**（HeroSO + SkillAbilityDef + PassiveAbilityDef） |
| SO 定义 | `Assets/Scripts/SO/` | 数据定义（EnemySO / WeaponSO / SkillDefinitionSO 等） |
| Excel 导入 | `Assets/Scripts/Excel/` | 工具链 |
| UI 游戏界面 | `Assets/Scripts/UI/` | 含于各窗口实现中 |
| Managers / Tools / Enum | `Assets/Scripts/Managers/` 等 | 工具类（含 `GameFlowBootstrapper` 启动器） |
| ShopSystem 商店 | `Assets/Scripts/ShopSystem/` | 后端完整（ShopManager），UI 基类已建 |

## 4. 已完成文档的模块（16 篇，全核心覆盖）

| 文档 | 路径 | 类型 |
|------|------|------|
| EventCenter | `Assets/Framework/EventCenter/MODULE.md` | MODULE.md |
| RunSystem | `Assets/Scripts/RunSystem/MODULE.md` | MODULE.md |
| PCG | `Assets/Scripts/PCG/MODULE.md` | MODULE.md |
| RoomRoleAllocator | `Assets/Scripts/PCG/Generation/RoomRoleAllocator功能解读.md` | 功能解读 |
| ServerAttributeModule | `Assets/Framework/NetworkLayer/ServerAuthority/AttributeSystem/ServerAttributeModule功能解读.md` | 功能解读 |
| MissionSystem | `Assets/Scripts/MissionSystem/MODULE.md` | MODULE.md |
| AI | `Assets/Framework/LogicLayer/Module/AIModule/MODULE.md` | MODULE.md |
| Combat | `Assets/Framework/LogicLayer/Module/CombatModule/MODULE.md` | MODULE.md |
| Skill | `Assets/Framework/LogicLayer/SkillSystem/MODULE.md` | MODULE.md |
| InventorySystem + QualityEffects | `Assets/Scripts/InventorySystem/MODULE.md` | MODULE.md |
| ArchiveSystem | `Assets/Scripts/ArchiveSystem/MODULE.md` | MODULE.md |
| Attribute | `Assets/Framework/LogicLayer/Module/AttributeModule/MODULE.md` | MODULE.md |
| UI Framework | `Assets/Framework/UI/MODULE.md` | MODULE.md |
| NetworkLayer | `Assets/Framework/NetworkLayer/MODULE.md` | MODULE.md |
| BuffSystem | `Assets/Framework/LogicLayer/BuffSystem/MODULE.md` | MODULE.md |
| DamageCenter | `Assets/Framework/LogicLayer/DamageCenter/MODULE.md` | MODULE.md |
| PlayerControl | `Assets/Scripts/PlayerControl/MODULE.md` | MODULE.md |

## 5. 轻量模块（文档随主模块覆盖，不单独建立 MODULE.md）

- `SpawnSystem`（接口 + MonsterSpawnConfig SO + PlayerSpawnManager 玩家生成管理）
- `SO 定义`（数据定义，含于 InventorySystem/Combat/Skill）
- `Excel 导入`（工具链，含于 InventorySystem 文档）
- `Managers / Tools / Enum`（工具类）
- `Scripts/UI/` 窗口实现（继承 UI Framework WindowBase）
- `Scripts/Test/`（测试代码）
- `ThirdParty/`（第三方资源）

## 6. 当前已确认的核心架构原则

1. **分层架构**：Framework（基础设施/逻辑/网络/渲染）→ Scripts（游戏逻辑）
2. **事件驱动解耦**：`EventCenter` 是模块间通信的唯一松耦合通道
3. **服务器权威**：所有状态变更在服务端执行，客户端通过 `ServerRpc` 发起请求，通过 `NetworkVariable`/`NetworkList` 接收同步
4. **确定性 PCG**：基于 `DeterministicRandom` 保证相同 Seed → 相同地图
5. **Module 组合模式**：`PlayerActor`/`EnemyActor` 通过组合 `IModule` 接口实例组装行为
6. **Attribute → ServerAttribute → EventCenter**：属性变更由 `ServerAttributeModule` 驱动，经 `NetworkVariable` 回调和 `EventCenter` 广播到客户端
7. **单例分层**：非 MonoBehaviour 用 `SingletonBase`，MonoBehaviour 用 `MonoSingletonBase`

## 7. 当前仍需要人工确认的问题

| # | 问题 | 严重程度 | 状态 |
|---|------|---------|------|
| 1 | `NetworkProxyBase.OnNetworkDespawn` 未调用 UnregisterNetworkObject | 中 | **已知 Bug** |
| 2 | `BuffUpdateTimeEnum` / `BuffRemoveStackUpdateEnum` 未实现 | 低 | **已知 Bug** |
| 3 | `ServerPlayerAttributeModule.OnMaxEnergyChanged` 触发 AggroRange 事件 | 低 | **已知 Bug** |
| 4 | `DefaultPlayerSelector` 未联动 NetworkObjectManager | 低 | **已知 Bug** |
| 5 | `TaskTriggerConnection` 未被 MissionManager 使用 | 低 | **TODO** |
| 6 | `PropsEffectScripts` 为遗留代码 | 低 | 待清理 |

### 2026-05-16 架构变更记录

| 变更 | 说明 |
|------|------|
| 删除 `RoomCombatController` | 房间制波次战斗不再适用，刷怪全权由 `MonsterSpawnManager` 管理 |
| 删除 `PathChoiceManager` | 开放地图无需路线选择 |
| RunState 新增 `Exploring` | 自由探索状态，取代 RoomCombat/RoomClear/PathChoice 三个废弃状态 |
| `MonsterSpawnManager` 接入难度 | `SpawnMonsterAt()` 根据 `_difficultyTier` 设置怪物等级 |
| `BossMission.Complete()` 触发 `BossDefeatedEvt` | 串联 RunManager → RunVictory 完整链路 |
| `BirthRoomNodeId` 动态解析 | `ResolveBirthNodeId()` 从 PCG 结果查找 Start 房间，不再硬编码 |
| 新增 `GameFlowBootstrapper` | 正式启动器（SerializeField 注入，无反射），替代 FlowTestBootstrap |
| 新增 `GameHUDWindow` | 游戏内 HUD 基类窗口（血量/护盾/能量/弹药/小地图占位） |
| 新增 `ShopWindow` | 商店 UI 基类窗口（对接 ShopManager 网络同步数据） |
| 新增 `ResultWindow` | 结算 UI 基类窗口（监听 RunSummaryReady 事件自动弹出） |
| 现有 Dependencies 字段改为 `internal` | PcgMapGenerator / RunManager / MissionManager / MonsterSpawnManager 的 SerializeField 字段改为 internal，供 Bootstrapper 直接赋值 |
| 新增 `PlayerSpawnManager` | 正式玩家生成系统，PCG 完成后为所有已连接客户端生成玩家（`SpawnAsPlayerObject`），支持多人 + 中途加入。替代 FlowTestBootstrap / GameFlowBootstrapper 中的旧生成逻辑 |
| 新增 `LobbyWindow` | 大厅 UI 窗口（继承 WindowBase），Phase 1 壳子。覆盖玩家卡片 / 任务选择 / 角色武器 / 准备状态 完整布局 |
| 新增 `MissionSelectWindow` | 任务选择 UI 窗口（继承 WindowBase），Phase 1 壳子。5 模块：地图循环 / 敌人预览 / 角色选择 / 武器装配 / 战斗入口 |
| `PcgGenerationProfile` 扩展 Enemy Pool | 新增 `availableEnemies` 字段（`List<EnemySO>`），建立 Style ↔ 敌人类型映射，供 UI 预览和刷怪过滤 |
| 新增 `CodingPlan/` 文档目录 | 存放实现计划文档（PlayerSpawnManager / LobbyWindow / MissionSelectWindow / PCGProfile_EnemyMapping）+ BUGS.md |

### 2026-05-17 架构变更记录

| 变更 | 说明 |
|------|------|
| 新增 `HeroSO` | 英雄数据模板 SO（属性 + 主动技能定义 + 被动执行器资源 + 英雄 Prefab），数据驱动英雄差异化 |
| 新增 `SkillAbilityDef` / `PassiveAbilityDef` | 主动技能定义（文字描述 + SkillDefinitionSO）与被动能力定义（文字描述 + PassiveExecutorSO 资源），存放于 HeroSO 的技能/被动列表中 |
| 新增 `IPassiveExecutor` | 被动能力执行器接口（OnHeroSpawned / OnHeroDestroyed），与 ISkillExecute 同构 |
| PlayerActor 支持 HeroSO | `RegisterModules()` 优先读取 `HeroSO.attributeConfig`；新增 `RegisterPassives()`/`UnregisterPassives()` |
| PlayerInitializer 重写 | `[DefaultExecutionOrder(-100)]` + `SetHeroSO()` + HeroSO 主动技能入槽 |
| PlayerSpawnManager 注入 HeroSO | 生成玩家时从 `defaultHeroSO` 读取并传入 PlayerInitializer.SetHeroSO() |
| DefaultPlayerSelector 集成 HeroSO | 优先从 `SOManager.GetSOList<HeroSO>()` 加载角色列表，回退 AttributeManager |
| PlayerSelectionInfo 扩展 | 新增 `HeroSO` 字段，桥接 DefaultPlayerSelector ↔ PlayerSpawnManager |

## 8. 文档建设状态

**全部 16 篇核心模块文档已完成。** 后续新增模块按需建立 MODULE.md，轻量模块随主模块文档覆盖。
