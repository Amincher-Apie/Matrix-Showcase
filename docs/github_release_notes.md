# GitHub 公开仓库说明

本文用于发布 Matrix Showcase 仓库时附在 GitHub Release、仓库说明或置顶 Issue 中。

## 1. 仓库性质

这是 Matrix 项目的技术展示仓库，不是完整可运行 Unity 工程。

仓库内容主要包括：

- 核心自研 C# 代码
- 架构说明
- 模块文档
- 公开范围说明
- 演示视频脚本
- 面试讲解要点

## 2. 适合谁阅读

- 游戏客户端开发面试官
- Unity / C# / 网络游戏方向导师
- 对多人 Roguelike、PCG、任务系统、AI 调度感兴趣的开发者
- 需要快速理解项目结构的 AI 工具

## 3. 包含内容

| 类型 | 内容 |
|---|---|
| 网络架构 | Netcode for GameObjects、NetworkProxy、ServerAuthority、NetworkObjectPool |
| PCG | Seed、RoomGraph、RoomRoleAllocator、RoomStitcher、NavMesh 适配 |
| 对局流程 | RunSystem 状态机、结算、任务流程串联 |
| 任务系统 | MissionConfig、MissionManager、MissionNetState、任务类型实现 |
| 战斗系统 | Combat、Attribute、DamageCenter、事件广播 |
| 构筑系统 | SkillSystem、BuffSystem、QualityEffects |
| AI | 状态机、感知、AIScheduler、InterestRegion、Boids |
| 数据工具 | HeroSO、SkillDefinitionSO、ExcelToSO、SOManager |

## 4. 不包含内容

仓库不包含：

- `Library/`、`Temp/`、`Logs/`、`obj/`、`UserSettings/`
- `.csproj`、`.sln`、`.idea`、`.vs`
- 模型、贴图、音频、动画、Shader
- Prefab、Scene、ScriptableObject 资产
- 第三方资源包或商业插件完整包
- 本机路径依赖和私有仓库元数据

## 5. 不公开完整工程的原因

完整工程不公开，主要是因为：

1. 资源授权：工程中包含第三方或商业资源，不能再分发。
2. 体积控制：完整 Unity 工程和资源体积过大，不适合技术展示仓库。
3. 本地依赖：完整工程包含 Unity Inspector 绑定、本机 Package 路径和私有配置。
4. 展示目标：该仓库用于评审核心系统，不用于发布完整游戏。

## 6. 使用建议

推荐阅读顺序：

1. `README.md`
2. `docs/architecture.md`
3. `docs/module_overview.md`
4. `docs/ai-readable-project-brief.md`
5. `MIGRATION_REPORT.md`
6. `src/Assets/Framework/NetworkLayer`
7. `src/Assets/Scripts/PCG`
8. `src/Assets/Scripts/MissionSystem`

## 7. 发布前检查清单

- README 中是否补充了演示视频链接。
- 是否确认 `docs/CodingPlan/` 中的措辞适合公开。
- 是否确认没有本机路径、账号、Token、邮箱或手机号。
- 是否确认没有误上传二进制资源。
- 是否确认没有误上传商业插件或第三方资源。
- 是否确认 LICENSE 符合预期。

## 8. Release 描述模板

```md
Matrix Showcase v1.0

这是 Matrix Unity 多人 Roguelike TPS 技术原型的公开展示版。仓库包含核心自研代码、架构说明和模块文档，重点展示服务器权威网络架构、确定性 PCG、任务系统、战斗数值管线、技能/Buff/品质道具系统、AI 调度和数据驱动工具链。

注意：本仓库不是完整可运行 Unity 工程，不包含第三方资源、商业插件、模型、贴图、音频、Scene、Prefab 或 ScriptableObject 资产。完整工程因资源授权、体积和本地依赖原因不公开。
```
