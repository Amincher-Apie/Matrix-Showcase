# AGENTS.md — Matrix 项目 AI 协作规则

> 项目：Matrix（Unity 多人 Roguelike 第三人称射击）  
> 引擎：Unity 2022.3 | 网络：Netcode for GameObjects | 管线：URP  
> 文档版本：初版 v0.1 | 创建日期：2026-05-14

---

## 一、语言规则

1. **所有回复、计划、总结、修改说明必须使用中文。**
2. 代码注释可使用英文，但不强制。
3. 技术术语（类名、方法名、设计模式名）保留原文。

## 二、代码修改前置条件

在执行任何代码修改前，必须完成以下步骤：

1. **阅读项目级文档**：`AGENTS.md`、`PROJECT_OVERVIEW.md`、`ARCHITECTURE.md`
2. **阅读目标模块的 MODULE.md**：检查 `Assets/` 下对应目录是否存在 `MODULE.md` 或 `*功能解读.md`
3. **如果目标模块没有 MODULE.md**：先征求用户意见是否需要先生成文档，再进行修改
4. **如果涉及多个模块**：阅读所有相关模块的 MODULE.md

### 禁止行为

- 禁止一开始就扫描整个项目
- 禁止无关的大范围重构
- 禁止在没有阅读相关 MODULE.md 的情况下修改代码
- 禁止修改 `.unity`、`.prefab`、`.asset` 文件（除非用户明确要求）
- 禁止修改 `.cs` 的同时不做文档同步

## 三、代码修改后置条件

### 验证规则

1. 完成代码修改后，**如果检测到unityMCP，则通过MCP进行验证，否则默认不执行本地构建、编译、启动 Unity 或运行测试**。
2. 仅在用户明确要求时，才执行本地构建、运行或测试验证。
3. 任务完成时应提示用户在 Unity Editor 中自行编译并验证相关功能。

每次任务完成后必须明确说明：

| 项目 | 说明 |
|------|------|
| 修改了哪些文件 | 列出所有被修改的文件路径 |
| 修改了什么逻辑 | 用一句话概括改动 |
| 是否更新了 MODULE.md | 是 / 否 |
| 未更新 MODULE.md 的原因 | 如：本次为纯 Bug 修复，不影响模块契约 |
| 是否存在风险 | 如：可能影响其他模块的 xxx 功能 |

## 四、Unity 项目特殊注意事项

1. 在一轮新对话首次处理 Unity 项目任务时，应先提示用户：**可以尝试连接 MCP 服务器（UnityMCP），以便直接读取 Unity Editor 状态并进行验证。**
   - 该提示仅为建议，不作为继续分析或修改代码的前置条件。
   - 如果 UnityMCP 未连接，继续基于项目文件完成任务。
2. **代码和资源的绑定关系无法从 .cs 文件完全确认**：
   - `[SerializeField]` 字段的实际赋值发生在 Unity Inspector 中
   - `GetComponent<T>()` 的返回值依赖于 Prefab 上挂载的组件
   - `Resources.Load()` / `ScriptableObject` 引用依赖于资源路径
3. 如果代码逻辑依赖于 Inspector 中未赋值的字段，**必须在文档中标注 `需要人工确认`**
4. 涉及 Scene / Prefab / ScriptableObject / Inspector 字段的变更，必须提醒用户手动检查 Unity 编辑器
5. `NetworkVariable` / `NetworkList` 的值变更只应在 `IsServer` 的分支中执行
6. `ServerRpc` 内应始终校验调用者权限（`OwnerClientId`）

## 五、架构约定

1. **分层原则**：
   - `Framework/` — 框架层（网络/事件/UI/资源/对象池/音频），通用基础设施
   - `Framework/LogicLayer/` — 逻辑层（Module 组合 + Actor + Buff/Skill/Damage），不含 Unity 特定逻辑
   - `Framework/NetworkLayer/` — 网络层（ServerAuthority + Proxy + ClientAuthority），Netcode 同步
   - `Framework/RenderLayer/` — 渲染层（RenderActor + DamageText），表现层
   - `Scripts/` — 游戏层（RunSystem/PCG/MissionSystem/InventorySystem），具体游戏逻辑
2. **通信规则**：
   - 模块间通信优先通过 `EventCenter`（松耦合）
   - 直接引用仅在同一层或明确依赖方向时使用
3. **网络同步规则**：
   - 服务器权威（ServerAuthority），客户端通过 `ServerRpc` 请求
   - 状态同步通过 `NetworkVariable` / `NetworkList`
4. **单例模式**：
   - 非 MonoBehaviour 单例 → `SingletonBase<T>`
   - MonoBehaviour 单例 → `MonoSingletonBase<T>`
   - 网络对象池 → `SingletonBase<T>`

## 六、文档体系约定

| 文档类型 | 位置 | 用途 |
|---------|------|------|
| `AGENTS.md` | 项目根目录 | AI 协作规则 |
| `PROJECT_OVERVIEW.md` | 项目根目录 | 项目概览 |
| `ARCHITECTURE.md` | 项目根目录 | 架构说明 |
| `MODULE.md` | 模块目录下 | 单个模块的完整文档 |
| `*功能解读.md` | 模块目录下 | 大型文件的拆分解读 |

## CodingPlan 文档规则

当创建或修改 `CodingPlan/**/*.md` 时，必须遵守 `.Codex/rules/codingplan-markdown-links.md` 中的路径链接规则。

### 模块文档生成优先级

当前已完成文档的模块：
- EventCenter, RunSystem, PCG（含 RoomRoleAllocator 功能解读）, ServerAttributeModule 功能解读, MissionSystem
- AI 模块, Attribute / Combat 模块, InventorySystem / QualityEffects, ArchiveSystem
- UI Framework, NetworkLayer 整体, SkillSystem, BuffSystem, DamageCenter, PlayerControl
- HeroSO 体系（HeroSO + SkillAbilityDef + PassiveAbilityDef + IPassiveExecutor）— **2026-05-17 新增**
