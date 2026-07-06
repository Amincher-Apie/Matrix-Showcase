# Matrix 项目 AI 速读文档

Matrix 是一个 Unity 2022.3 / URP / Netcode for GameObjects 的多人 Roguelike TPS 技术原型。本 showcase 仓库不是完整 Unity 工程，只保留核心自研代码与文档。

## 项目主线

玩家进入对局后，服务端创建 Run 流程，MissionSystem 生成任务语义输入，PCG 根据 Seed、地图风格和任务输入生成地图。玩家在开放地图中探索、刷怪、完成支线任务并进入 Boss 战。战斗、属性、技能、Buff、品质道具、AI 调度与结算均围绕服务器权威设计。

## 核心阅读顺序

1. `src/Assets/Framework/NetworkLayer`：网络代理、对象注册、对象池、ServerAuthority 模块。
2. `src/Assets/Scripts/PCG`：确定性地图生成、房间图、任务房间角色分配、房间拼接。
3. `src/Assets/Scripts/RunSystem` 与 `src/Assets/Scripts/MissionSystem`：对局状态机和任务状态同步。
4. `src/Assets/Framework/LogicLayer/Module/CombatModule`、`AttributeModule`、`DamageCenter`：战斗数值管线。
5. `src/Assets/Framework/LogicLayer/SkillSystem`、`BuffSystem`、`src/Assets/Scripts/InventorySystem/QualityEffects`：数据驱动效果系统。
6. `src/Assets/Framework/LogicLayer/Module/AIModule`：AI 状态机、感知、调度、InterestRegion、Boids。
7. `src/Assets/Scripts/SO` 与 `src/Assets/Scripts/Excel`：配置和工具链。

## 不包含的内容

本仓库不包含第三方插件、模型、贴图、音频、Shader、场景、Prefab、ScriptableObject 资产文件和 Unity 生成目录。因此它适合用于代码与架构评审，不适合直接运行完整游戏。
