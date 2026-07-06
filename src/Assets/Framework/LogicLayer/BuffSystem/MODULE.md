# BuffSystem Buff 系统

## 1. 模块职责

为玩家和敌人提供统一的 Buff 管理系统。包括：

- Buff 数据定义（`BuffData` SO）：基础属性 + 12 种回调点
- 运行时 Buff 管理（`BuffHandler`）：增/删/叠层 + 持续 Tick + 到期清除
- 属性修改 Buff 模块（`ModifyAttributeBBM`）：通过 `IAttributeProxy` 修改属性
- 叠层规则（`BuffUpdateTimeEnum` / `BuffRemoveStackUpdateEnum`）：控制层数增减行为
- 网络同步层（`ServerBuffModule`）：`NetworkList<BuffNetState>` 同步到客户端

**与 QualityEffects 的区别**：Buff 是**代码驱动**（每个 Buff 绑 `BaseBuffModule` SO 实例），QualityEffects 是**数据驱动**（Condition → Action 注册表）。Buff 由 BuffHandler 在服务端每帧驱动 Tick，QualityEffects 由战斗事件触发。

## 2. 模块边界

| 边界 | 说明 |
|------|------|
| **逻辑层** | `BuffHandler`（纯 C#，不继承 MonoBehaviour）— 服务端每帧 `OnLogicUpdate` 驱动 |
| **数据层** | `BuffData` (SO) — 定义单个 Buff 的所有参数和回调点 |
| **属性接口** | `IAttributeProxy` — Buff 通过此接口修改属性，解耦具体实现 |
| **网络层** | `ServerBuffModule` (NetworkBehaviour) — `NetworkList<BuffNetState>` 同步 |
| **不负责** | Buff 图标/UI 展示（UI 模块）；Buff 施加条件（由调用方自行判断） |

**文件分布**：

```
Framework/LogicLayer/BuffSystem/
├── BuffData.cs           # Buff 数据定义 (SO)
├── BuffDesign.cs         # 叠层策略枚举
├── BuffHandler.cs         # 运行时 Buff 管理器
├── BuffInfo.cs            # 单个 Buff 运行时实例
├── BuffOwnerContext.cs    # 拥有者上下文 (IAttributeProxy 桥接)
├── IBuffOwnerContext.cs   # 拥有者抽象接口 + IAttributeProxy 接口
└── BaseBuffModule.cs      # Buff 模块基类 (抽象 SO)

Scripts/BuffSystem/
└── ModifyAttributeBBM.cs  # 属性修改 Buff 模块 (具体实现)

NetworkLayer/ServerAuthority/BuffSystem/
└── ServerBuffModule.cs     # 服务端 Buff 网络同步 (已在 NetworkLayer MODULE.md 文档化)
```

## 3. 核心流程

### 3.1 Buff 生命周期

```
施加: ServerBuffModule.ApplyBuff(buffData, stacks)
    → BuffHandler.AddBuff(buffInfo)
        ├─ 已存在同 ID? → 叠层 (currentStack + 1) + 刷新 duration
        │   ├─ buffData.OnCreat?.Apply(buffInfo)
        │   └─ OnLayerAdd?.Invoke(buffInfo)
        └─ 首次添加? → 加入 BuffInfoSets + _idToBuff
            ├─ buffData.OnCreat?.Apply(buffInfo)
            └─ OnLayerAdd?.Invoke(buffInfo)
    → SyncRuntimeToNet() → NetworkList<BuffNetState> 同步客户端

Tick: BuffHandler.OnLogicUpdate(deltaTime) [每帧, 仅服务端]
    → 遍历 BuffInfoSets (优先级排序)
        ├─ defaultTickInterval > 0 → tickTimer 倒计时 → 到期触发 OnTick
        └─ !isForever → durationTime 递减 → 到期 → 加入移除列表
    → 移除到期 Buff → RemoveBuff()

移除: BuffHandler.RemoveBuff(buffInfo)
    → buffInfo.reverse = true
    → 逐层循环: buffData.OnRemove?.Apply(buffInfo) + currentStack--
    → RemoveBuffFromCollections() → OnLayerFallToZero?.Invoke()
```

### 3.2 属性修改 Buff 示例 (ModifyAttributeBBM)

```
施加 (正方向):
    buffData.OnCreat = ModifyAttributeBBM
    → Apply(buffInfo):
        IAttributeProxy.AddModifier(type, modifyType, value, sourceId=buffID)
        → ServerAttributeModule.AddModifier → 属性生效

移除 (反方向):
    buffData.OnRemove = ModifyAttributeBBM (同一个 SO)
    → Apply(buffInfo): buffInfo.reverse=true
        IAttributeProxy.RemoveModifiers(type, sourceId=buffID)
        → ServerAttributeModule.RemoveModifiers → 属性还原
```

### 3.3 伤害回调链

```
DamageCalculator → ServerCombatModule.ProcessValidatedHits
    → BuffHandler.ApplyOnUseNormalAtk(damageInfo)   [攻击前]
    → BuffHandler.ApplyOnCauseDamage(damageInfo)      [造成伤害后]
    → BuffHandler.ApplyOnHit(damageInfo)              [命中时]
    → BuffHandler.ApplyUponBeHurt(damageInfo)         [受击前 - 减伤/无敌帧]
    → BuffHandler.ApplyOnBeHurt(damageInfo)           [受击后]
    → BuffHandler.ApplyOnDeath(damageInfo)            [死亡时 - 受击方]
    → BuffHandler.ApplyOnKill(damageInfo)             [死亡时 - 攻击方]
```

## 4. 核心数据模型

### BuffData (ScriptableObject)

| 分组 | 字段 | 类型 | 说明 |
|------|------|------|------|
| 基础 | `buffID` | int | 唯一标识 |
| | `buffName` / `buffIcon` / `buffDescription` | string/Sprite | UI 展示 |
| | `priority` | int | BuffInfoSets 排序优先级 |
| | `tags` | string[] | 标签（备用） |
| 时间 | `isForever` | bool | 是否永久 |
| | `defaultDuration` | float | 默认持续（秒） |
| | `defaultTickInterval` | float | Tick 周期（0=无） |
| 更新策略 | `buffUpdateTime` | BuffUpdateTimeEnum | 层数变化时的持续时间策略 |
| | `buffRemoveStackUpdate` | BuffRemoveStackUpdateEnum | 移除时的层数策略 |
| 层数限制 | `defaultMaxStack` | int | 默认最大层数 |
| | `maxStackForPlayer/Normal/Elite/Boss` | int | 按 Owner 类别的层数上限 |
| **回调点** | `OnCreat/OnUpdate/OnTick/OnRemove` | BaseBuffModule | 基础回调 |
| | `OnHit/OnCauseDamage/UponBeHurt/OnBeHurt` | BaseBuffModule | 伤害回调 |
| | `OnDeath/OnKill` | BaseBuffModule | 死亡回调 |
| | `OnUseNormalAtk/OnUseSkill/AfterUseSkill` | BaseBuffModule | 战斗动作回调 |

### BuffInfo（运行时实例）

```csharp
class BuffInfo {
    BuffData buffData;
    IBuffOwnerContext Owner;
    int currentStack = 1;
    float durationTime;          // 剩余持续
    float tickTimer;             // 下次 Tick 剩余
    bool reverse;                // 是否处于移除流程
}
```

### BuffHandler（管理器）

```csharp
class BuffHandler {
    SortedSet<BuffInfo> BuffInfoSets;   // 按 priority ↓ buffID 排序
    Dictionary<int, BuffInfo> _idToBuff; // buffID → BuffInfo

    void AddBuff(BuffInfo) / RemoveBuff(BuffInfo) / ClearAll()
    void OnLogicUpdate(deltaTime)  // 每帧驱动
    int GetLayers(int buffID)      // 查询层数

    // 伤害回调
    ApplyOnUseNormalAtk / ApplyOnCauseDamage / ApplyOnHit
    ApplyUponBeHurt / ApplyOnBeHurt / ApplyOnDeath / ApplyOnKill
    ApplyOnUseSkill / ApplyAfterUseSkill
}
```

### BuffUpdateTimeEnum（层数变化策略）

| 枚举 | 含义 |
|------|------|
| `add` | 叠加时累积时间 |
| `replace` | 叠加/掉落时刷新持续时间 |
| `keep` | 保持原有时间不变 |
| `single` | 逐层独立计时 |

### BuffRemoveStackUpdateEnum（移除策略）

| 枚举 | 含义 |
|------|------|
| `clear` | 失效即清空全部层数 |
| `reduce` | 逐层掉落 |
| `single` | 独立计算 |
| `half` | 掉落至一半 |
| `none` | 特殊（9001 专用） |

## 5. 关键类与文件

### IBuffOwnerContext + IAttributeProxy（接口层）

`IBuffOwnerContext.cs:14` — `IBuffOwnerContext` 定义 Buff 拥有者抽象：`NetworkObjectId` + `OwnerCategory` + `IAttributeProxy`。

`IAttributeProxy` — Buff 修改属性的统一接口：
```csharp
public interface IAttributeProxy {
    float GetAttribute(AttributeType type);
    void AddModifier(AttributeType type, AttributeModifyType modifyType, float value, ulong sourceId, int stacks);
    void RemoveModifiers(AttributeType type, ulong sourceId, int stacks);
}
```

`ServerAttributeModule` 直接作为 `IAttributeProxy` 实现（`ServerAttributeModule : NetworkBehaviour, IAttributeProxy`）。

### BuffOwnerContext（上下文实现）

`BuffOwnerContext.cs:4` — 简单数据容器，持有 `NetworkObjectId` / `OwnerCategory` / `IAttributeProxy`。

### ModifyAttributeBBM（具体 Buff 模块）

`ModifyAttributeBBM.cs:7` — 继承 `BaseBuffModule`，通用的属性修改 Buff 模块：

- **正方向**（施加 Buff）：`IAttributeProxy.AddModifier(type, modifyType, value, sourceId=buffID)`
- **反方向**（移除 Buff）：`IAttributeProxy.RemoveModifiers(type, sourceId=buffID)`

通过 `ModifyAttributeTemplate` 配置修改哪个属性（`AttributeType`）、方式（`AttributeModifyType`）、数值。

### ServerBuffModule（网络同步层）

`ServerBuffModule.cs:12` — 已在 [NetworkLayer MODULE.md](../../../NetworkLayer/MODULE.md) 文档化。核心：

- 持有 `BuffHandler` + `NetworkList<BuffNetState>`
- `Update()` 每帧调用 `Handler.OnLogicUpdate(deltaTime)` + `SyncRuntimeToNet()`
- `ApplyBuff(BuffData, stacks, durationOverride)` / `RemoveBuff(buffId)` / `HasBuff(buffId)`
- `NetBuffs` 使用 `[HideInInspector]`，避免 Netcode 1.12.2 Inspector 反射创建 public `NetworkList<>` 时产生 Native Collection 泄漏警告。

## 6. 对外接口

### BuffHandler

| 方法 | 用途 |
|------|------|
| `AddBuff(BuffInfo)` | 添加/叠层 Buff |
| `RemoveBuff(BuffInfo)` | 移除 Buff（逐层 OnRemove 回调） |
| `ClearAll()` | 清空全部 Buff |
| `FindBuff(buffID)` / `GetLayers(buffID)` | 查询 Buff 存在性/层数 |
| `OnLogicUpdate(deltaTime)` | 每帧 Tick + 持续到期检查 |
| `ApplyOnUseNormalAtk(damage)` / `ApplyOnHit(damage)` / ... | 伤害事件回调（8 种） |

### IAttributeProxy

| 方法 | 用途 |
|------|------|
| `AddModifier(type, modifyType, value, sourceId, stacks)` | 施加属性修改器 |
| `RemoveModifiers(type, sourceId, stacks)` | 移除属性修改器 |

## 7. 依赖模块

| 依赖模块 | 用途 |
|----------|------|
| `IAttributeProxy` (ServerAttributeModule) | Buff 通过其修改属性 |
| `ScriptableObject` (Unity) | BuffData / BaseBuffModule 为 SO 实例 |
| `Unity.Netcode` (ServerBuffModule) | NetworkList 同步 |
| `EventCenter` (间接) | ServerBuffModule.OnLayerAdd/Zero 预留 |

## 8. 被哪些模块依赖

| 依赖方 | 用途 |
|--------|------|
| `ServerCombatModule` (Combat) | `ApplyOnUseNormalAtk` / `ApplyOnCauseDamage` / `ApplyOnHit` |
| `ServerAttributeModule.TakeDamage()` | `ApplyUponBeHurt` / `ApplyOnBeHurt` / `ApplyOnDeath` / `ApplyOnKill` |
| `ServerSkillModule` (Skill) | `ApplyOnUseSkill` / `ApplyAfterUseSkill` |
| `QualityEffectModule` (InventorySystem) | 施加 Buff 作为 QualityEffect Action (`ApplyBuff`/`ApplyDebuff` 执行器) |

## 9. 事件订阅与广播

### BuffHandler 事件

| 事件 | 触发时机 | 订阅方 |
|------|---------|--------|
| `OnLayerAdd` (Action\<BuffInfo\>) | 添加/叠层时 | `ServerBuffModule`（触发 SyncRuntimeToNet） |
| `OnLayerFallToZero` (Action\<BuffInfo\>) | 层数归零/移除时 | `ServerBuffModule`（触发 SyncRuntimeToNet） |

### EventCenter

Buff 系统**不直接订阅或广播 EventCenter 事件**。Buff 通过伤害回调链（`ApplyOnHit` / `ApplyUponBeHurt` 等）被 Combat/Attribute 模块调用，而非通过 EventCenter 订阅。

## 10. Inspector 字段

### BuffData (SO)

全部 `public` 字段在 Inspector 中可编辑（详见数据模型部分）。

### ModifyAttributeBBM (SO)

| 字段 | 类型 | 说明 |
|------|------|------|
| `ModifyTemplates` | `ModifyAttributeTemplate` | 属性修改模板（type + modifyType + value） |

### ServerBuffModule

| 字段 | 类型 | 用途 |
|------|------|------|
| `attrModule` | `ServerAttributeModule` | 绑定的属性模块（用于构建 BuffOwnerContext） |

## 11. Prefab / Scene / ScriptableObject 依赖

| 类型 | 路径/名称 | 用途 |
|------|----------|------|
| SO | `BuffData.asset` (路径待确认) | Buff 数据配置 |
| SO | `ModifyAttributeBBM.asset` | 属性修改 Buff 模块 |
| Prefab | 玩家/敌人 Prefab | 挂载 `ServerBuffModule` |

**TODO**：`BuffData` 当前通过 `[CreateAssetMenu]` 手动创建。后续接入 ExcelToSOGenerator 自动生成。

## 12. 常见问题

**Q: Buff 和 QualityEffects 有什么区别？**
A: Buff 是**代码驱动**（每个 Buff 绑定 SO `BaseBuffModule` 实例），QualityEffects 是**数据驱动**（Condition → Action 注册表，无需为每种效果写代码）。Buff 适合需要复杂逻辑的效果（如 DOT、复活），QualityEffects 适合通用效果（如命中时概率造成额外伤害）。

**Q: ModifyAttributeBBM 如何工作？**
A: 同一个 `ModifyAttributeBBM` 同时绑定到 `OnCreat` 和 `OnRemove`。施加 Buff 时调用 `AddModifier`，移除时 `reverse=true` → 调用 `RemoveModifiers`。通过 `sourceId=buffData.buffID` 追踪来源。

**Q: Buff 的 Tick 和 Duration 是否同时生效？**
A: 是的。对于设置了 `defaultTickInterval > 0` 的 Buff（如 DOT），每 Tick 触发 `OnTick` 回调；同时 `durationTime` 递减，到期后整个 Buff 移除。

**Q: 叠层时持续时间如何变化？**
A: 由 `BuffUpdateTimeEnum` 控制：
- `add` → 累积时间
- `replace` → 刷新为 defaultDuration
- `keep` → 保持原有时间
- `single` → 每层独立计时

**Bug**：当前 `AddBuff` 实现中，叠层时**写死使用 `replace` 策略**（`existing.durationTime = buffInfo.buffData.defaultDuration`），未从 `buffData.buffUpdateTime` 枚举读取。后续需按枚举值分别实现 add/replace/keep/single 逻辑。

## 13. 当前完成度

| 功能 | 状态 |
|------|------|
| BuffData SO 定义（12 回调 + 时间/叠层参数） | 完成 |
| BuffHandler 增/删/叠层/Tick/Duration | 完成 |
| BuffInfo 运行时数据结构 | 完成 |
| IAttributeProxy 抽象接口 | 完成 |
| ModifyAttributeBBM（属性修改模块） | 完成 |
| ServerBuffModule 网络同步 | 完成 |
| 伤害回调链（8 种 ApplyOnXxx） | 完成 |
| Priority 排序（SortedSet） | 完成 |
| BuffUpdateTimeEnum 策略实现 | **Bug** — AddBuff 中写死 `replace`，未读取 `buffData.buffUpdateTime` 枚举 |
| BuffRemoveStackUpdateEnum 策略实现 | **未实现** — RemoveBuff 直接循环到 0，未区分 clear/reduce/single/half/none 策略 |
| 客户端 Buff UI 展示（图标/层数/时间） | **未实现** — NetBuffs 同步已有，UI 层未对接 |

## 14. 修改本模块时必须同步更新的内容

- **BuffData 新增回调点** → 同步更新 `BuffHandler.AddBuff/RemoveBuff` 调用链 + `BuffHandler` 对应 ApplyXxx 方法
- **BaseBuffModule.Apply 签名变更** → 同步更新 `ModifyAttributeBBM` 和所有其他 BBM 子类
- **BuffUpdateTimeEnum / BuffRemoveStackUpdateEnum 策略实现** → 更新 `AddBuff/RemoveBuff` 中的硬编码逻辑

## 15. 文档维护信息

| 项目 | 内容 |
|------|------|
| 创建日期 | 2026-05-14 |
| 覆盖文件数 | 8 个 .cs |
| 关联模块文档 | NetworkLayer (ServerBuffModule), Attribute (IAttributeProxy/ServerAttributeModule), Combat (Buff 回调链调用方), Skill, InventorySystem (QualityEffects ApplyBuff) |

## 16. 2026-06-30 元素 Buff 与施加者维度落地

- `BuffData` 新增 `stackKeyMode` 与 `maxAppliersPerTarget`：旧 Buff 默认 `BuffIdOnly`，火/毒/电/冰等元素异常应配置为 `BuffIdAndApplier`。
- `BuffInfo` 新增施加者快照：`applierObjectId`、`applierClientId`、`sourceDamageInfo`、`elementType`、`elementDamageSnapshot`。其中 `Owner` 仍表示 Buff 承载者，不表示施加者。
- `BuffHandler` 已支持 `(buffId, applierId)` 复合键；`FindBuff(int)` 保持兼容，`GetLayers(int)` 会聚合同 ID 的所有施加者层数。
- `ModifyAttributeBBM` 的修改器来源改为 `buffInfo.RuntimeSourceId`，避免不同施加者的同类 Buff 属性修改互相覆盖。
- 新增 `ElementBuffMappingAsset`，服务端元素触发优先读取映射资源；未配置映射资源时，会按 `buffID=2001/2002/2003/2004` 或 `tags=fire/poison/electric/ice` 兜底查找 `BuffData`。
- 新增元素 BBM：`FireIgniteBBM`、`PoisonDotBBM`、`ElectricArcBBM`、`ChillSlowBBM`。它们分别用于火 DOT、毒穿盾 DOT、电弧、冰减速。
- 需要人工确认：`Assets/Resources/BuffData/` 下的四个元素 `BuffData` 资源仍需在 Unity Inspector 中创建/配置，并把对应 BBM 资产绑定到 `OnTick` 或 `OnCreat/OnRemove`。本次未修改 `.asset`。
