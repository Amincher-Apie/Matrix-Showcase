# Phase 1 技能系统接入 — 配置手册

> 对应 ADR：`docs/adr/0004-skill-system-integration-plan.md`（2026-06-29 Accepted）  
> 代码实施日期：2026-06-29  
> 目标：正式对局中通过数字键 1-0 释放技能，HeroSO 技能自动入槽

---

## 一、代码改动清单

| 文件 | 改动类型 | 改动说明 |
|------|---------|----------|
| `Assets/Scripts/Test/PlayerInitializer.cs` | 修改 | `ApplyHeroSkills` 按 HeroSO.skills 填入技能槽 + 运行时挂载 `PlayerSkillInputBinder` |
| `Assets/Scripts/PlayerControl/PlayerSkillInputBinder.cs` | **新建** | 数字键 1-0 → 技能槽 0-9；相机中心射线构建 SkillCastContext |
| `Assets/Framework/LogicLayer/SkillSystem/PlayerSkillRuntime.cs` | 修改 | 新增 `maxCharges` + `RestoreCharge`（冷却恢复）；`IsReady` 懒检测充能恢复 |
| `Assets/Framework/LogicLayer/Module/SkillModule/PlayerSkillModule.cs` | 修改 | `PredictConsumeCost`：EnergyOnly 技能不消耗充能、不进入冷却 |
| `Assets/Framework/NetworkLayer/ServerAuthority/SkillSystem/ServerSkillModule.cs` | **重写** | 6 项校验链 + 按 `skillId` 服务端冷却 + `CheckAndConsumeEnergy` 走内部方法 + `BroadcastSkillCastConfirmed` 最小实现 |
| `Assets/Framework/NetworkLayer/ServerAuthority/AttributeSystem/ServerAttributeModule.cs` | 修改 | `ModifyAttributeServerInternal` 访问级从 `private` → `protected` |
| `Assets/Framework/NetworkLayer/ServerAuthority/AttributeSystem/ServerPlayerAttributeModule.cs` | 修改 | 新增 `TryConsumeEnergyServerInternal`（纯服务端，绕过 RPC） |
| `Assets/Framework/EventCenter/EventName.cs` | 修改 | 新增 `SkillCastConfirmed` 事件枚举 + `SkillCastConfirmedEvt` 结构体 |
| `Assets/Scripts/SO/SkillSO/SkillDefinitionSO.cs` | 修改 | 新增 `spreadAngle`（扩散角）和 `spreadRadius`（终点球体半径）字段 |

---

## 二、Unity Editor 人工配置清单

以下项目**必须**由人工在 Unity Editor 中确认/配置。不配置将导致技能系统无法正常工作。

### 2.1 玩家 Prefab（HeroSO.heroPrefab）

| 配置项 | 位置 | 说明 |
|--------|------|------|
| `ServerSkillModule` | Prefab 根节点 | 已挂载组件 `ServerSkillModule`（要求 `ServerPlayerAttributeModule`） |
| `ServerPlayerAttributeModule` | Prefab 根节点 | 已挂载组件 `ServerPlayerAttributeModule` + `PlayerAttributeConfig` 赋值 |
| `PlayerNetworkProxy.ServerSkillModule` | Inspector 字段 | 必须引用到 Prefab 上的 `ServerSkillModule` 组件 |
| `PlayerNetworkProxy.ServerPlayerAttributeModule` | Inspector 字段 | 必须引用到 Prefab 上的 `ServerPlayerAttributeModule` 组件 |
| `PlayerActor` | Prefab 根节点 | 已挂载 `PlayerActor` 组件 |

### 2.2 HeroSO 配置

| 配置项 | 说明 |
|--------|------|
| `HeroSO.skills` 列表 | 每项对应一个主动技能。`skillDefinition` 引用 `SkillDefinitionSO`；不再配置 `skillExecutor` |
| 技能槽数量 | `PlayerInitializer` 按 `skills` 列表索引顺序填入 `_skills`。`skills[0]` → 数字键 1，`skills[1]` → 数字键 2 … |
| `HeroSO.passives` 列表 | 每项对应一个被动能力。`passiveExecutor` 需拖入具体 `PassiveExecutorSO` 资产 |

### 2.3 SkillDefinitionSO 配置

> **注意**：`id`、`description`、`icon`、`name` 四个字段继承自 `BaseSO`（Odin `[FoldoutGroup]` 组织），**不在** `SkillDefinitionSO` 本身上声明。`SkillDefinitionSO` 原先重复声明了 `id` 和 `description`，已修复为只保留 `displayName`（本类特有字段）。

| 配置项 | 位置 | 说明 |
|--------|------|------|
| `id` | `BaseSO` → `[FoldoutGroup("基础属性")]` | 全局唯一字符串 ID。与 `executeHandler` 配合使用 |
| `icon` | `BaseSO` → `[FoldoutGroup("渲染属性")]` | 技能图标 Sprite。若为空则 HUD 显示空白 |
| `description` | `BaseSO` → `[FoldoutGroup("基础属性")]` | 技能描述文本（TextArea） |
| `displayName` | `SkillDefinitionSO` → `[Header("基础信息")]` | 技能显示名称（用于 UI） |
| `targetType` | `SkillDefinitionSO` → `[Header("技能类型")]` | `Self` / `Direction` / `Point`。不要使用 `Actor`（未实现） |
| `costType` | `SkillDefinitionSO` | `CooldownOnly` / `EnergyOnly` / `CooldownAndEnergy` |
| `baseCooldown` | `SkillDefinitionSO` | 冷却时间基准（秒）。`EnergyOnly` 忽略此值 |
| `baseEnergyCost` | `SkillDefinitionSO` | 能量消耗基准（仅 `EnergyOnly` / `CooldownAndEnergy`） |
| `spreadAngle`（新） | `SkillDefinitionSO` | Direction 技能扩散角（度，0=精确射击） |
| `spreadRadius`（新） | `SkillDefinitionSO` | Direction 技能终点球体半径（米，0=单点命中） |
| `executeHandler` | `SkillDefinitionSO` | 从枚举下拉框选择，与 `SkillExecuteRegistry` 中注册的 `ISkillExecute` 对应 |

### 2.3.1 PassiveExecutorSO 配置

| 配置项 | 说明 |
|--------|------|
| 创建被动执行器资产 | 右键 Project 窗口，通过 `游戏配置/角色系统/被动执行器/动能爆发` 创建 `KineticBurstPassiveExecutor` 资产 |
| 拖入 HeroSO | 将创建出的 `PassiveExecutorSO` 资产拖入 HeroSO 的 `passiveExecutor` 字段 |

### 2.4 输入配置（Input System）

| 配置项 | 说明 |
|--------|------|
| 数字键 1-0 绑定 | `InputController.inputactions` 中需要为数字键 1-9+0 配置 `CastSkill_X` 动作（或直接使用 Keyboard 设备读取，当前 `PlayerSkillInputBinder` 已使用 `Keyboard.current.digitXKey`） |
| Input System 包 | 必须安装 `com.unity.inputsystem` 包（`#if ENABLE_INPUT_SYSTEM` 已做兼容） |

**当前方案**：`PlayerSkillInputBinder.Update()` 直接通过 `Keyboard.current.digitXKey.wasPressedThisFrame` 读取，**不依赖** `InputController.inputactions` 中的技能绑定。这意味着不需要额外配置 Input Action。

### 2.5 相机

| 配置项 | 说明 |
|--------|------|
| 场景中必须有 `Camera.main` | `PlayerSkillInputBinder` 通过 `Camera.main` 获取相机，构建技能上下文。如果使用自定义相机标签，需要修改代码 |

---

## 三、运行时链路说明

```
[数字键按下]
  → PlayerSkillInputBinder.Update()
    → Keyboard.current.digitXKey.wasPressedThisFrame
      → TryCastSkillInSlot(keyIndex - 1)
        → BuildCastContext(def, slotIndex)
          → Camera.main.ViewportPointToRay(0.5, 0.5)  // 屏幕中心射线
          → Physics.Raycast → hit.point 或 origin + dir * baseRange
        → PlayerSkillModule.TryCastSkill(slotIndex, ctx)
          → runtime.IsReady() → 懒检测冷却恢复
          → ClientCheckEnergyCost → 软校验
          → ClientPredictExecute → 本地预测表现
          → PredictConsumeCost → 冷却预测（EnergyOnly 跳过）
          → SendCastRequestToServer → PlayerNetworkProxy.CastSkillServerRpc
            → ServerSkillModule.ServerTryCastSkill (6 项校验)
                → TryConsumeEnergyServerInternal → ModifyAttributeServerInternal (内部方法)
                → WriteServerCooldown (按 skillId)
                → ISkillExecute.ServerExecute
                → BroadcastSkillCastConfirmed
                  → EventCenter.Trigger(SkillCastConfirmed)
                  → OnSkillCastConfirmedClientRpc (Phase 3 扩展)
```

---

## 四、验证清单

完成上述配置后，按以下步骤验证：

1. **Host 单人模式进入对局**
2. **按数字键 1**：应释放 HeroSO.skills[0] 定义的技能
3. **按数字键 2**：应释放 HeroSO.skills[1] 定义的技能
4. **冷却结束后**按同一按键：技能应再次可用
5. **能量不足时**：技能不应释放，HUD 应有相应反馈
6. **按超出 skills 列表的键**（如 skills 只有 2 个时按 3）：应静默忽略
7. **Console 日志**：
   - `[PlayerInitializer] HeroSO[xxx] 共 N 个技能已入槽`
   - `[PlayerSkillInputBinder] 输入绑定就绪`
   - `[ServerSkillModule] 技能 xxx 释放确认已广播`

---

## 五、常见问题

| 问题 | 可能原因 | 排查方法 |
|------|---------|----------|
| 按键无响应 | `PlayerSkillInputBinder.enabled = false`（非 Owner） | 检查 Console 中是否有 `非 Owner` 日志 |
| 技能不释放 | `_skills` 列表为空 | 检查 `HeroSO.skills` 是否已配置 `skillDefinition` |
| 释放后技能永不可用 | `EnergyOnly` 技能调用了 `ConsumeOneCharge` | 确认已使用新代码（`PredictConsumeCost` 中 EnergyOnly 跳过充能消耗） |
| 服务端拒绝技能 | 冷却未通过 / 能量不足 | Console 中搜索 `[ServerSkillModule] 拒绝` |
| 编译错误 | 新文件缺少 .meta | 删除 `PlayerSkillInputBinder.cs` 再重新创建（或手动触发 AssetDatabase.Refresh） |
| 相机为 null | 场景中无 Camera.main | 确保 MainCamera 标签已设置，或场景中存在激活的 Camera |
| 射线未命中点 | 场景无碰撞体 | `point` 会使用兜底值 `origin + direction * baseRange`，技能仍可释放 |

---

## 六、Phase 3/4 待办提醒

以下项目在 Phase 1 中未实现，将在后续阶段处理：

- [ ] **动画接入**：`TriggerAnimation` 仍是 Debug.Log 占位 → Phase 3 接入 AnimationBridge
- [ ] **拒绝回滚**：服务端拒绝时客户端冷却/能量不回滚 → Phase 2 实现 `SkillCastRejectedEvt`
- [ ] **HUD 冷却滑条修正**：冷却缩减时滑条分母用 `baseCooldown` → Phase 3 改为实际冷却时长
- [ ] **远端表现同步**：`OnSkillCastConfirmedClientRpc` 仅日志 → Phase 3 播放远端特效/动画
- [ ] **PlayerInitializer 目录迁移**：从 `Test/` 迁入正式目录 → Phase 4
- [ ] **SkillExecuteRegistry per-player**：防止全局覆盖 → Phase 4
- [ ] **主动道具技能**：`ActiveSkillItemSO` 技能定义 + 自动入槽 → Phase 4
- [ ] **PiercingShotSkillExecutor**：内容补完 → Phase 4
