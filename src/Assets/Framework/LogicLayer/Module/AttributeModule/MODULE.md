# Attribute 属性模块

## 1. 模块职责

为玩家和敌人提供统一的属性系统。包括：

- 28 种属性类型的定义与计算（基础/抗性/玩家特有/敌人特有）
- 属性修改器系统（Set/Add/Multiply/Percent + 叠层 + 来源追踪）
- 等级成长公式（base + growth × (level - 1)）
- Config → AttributeData 的初始化管线
- 逻辑层（LogicLayer）属性读取桥接（委托到网络层 NetworkVariable）
- 属性配置的资源加载与管理（AttributeManager）
- EventCenter 属性变化事件广播

> **网络层**（ServerAttributeModule / ServerPlayerAttributeModule / ServerEnemyAttributeModule）已有独立文档：[ServerAttributeModule功能解读](../../../NetworkLayer/ServerAuthority/AttributeSystem/ServerAttributeModule功能解读.md)

## 2. 模块边界

| 边界 | 说明 |
|------|------|
| **LogicLayer** | `AttributeModule` 子类提供只读 `GetAttribute()`，将调用委托到网络层的 `ServerAttributeModule.GetAttribute()` |
| **网络层** | `ServerAttributeModule` 持有完整的 `AttributeData` 字典 + `NetworkVariable` + 修改器，服务端权威 |
| **配置层** | `AttributeConfig` (SO) → `PlayerAttributeConfig` / `EnemyAttributeConfig`，定义基础值与成长值 |
| **管理** | `AttributeManager` (Singleton) 从 `Resources.LoadAll()` 加载全部配置并提供查询 |
| **不负责** | 武器属性（`WeaponAttributeType` + `ServerWeaponRuntime`，属 Combat 模块）；Buff 施加（属 BuffSystem） |

**文件分布**：

```
Framework/LogicLayer/Interfaces/
└── IAttribute.cs                      # 属性模块接口

Framework/LogicLayer/Module/AttributeModule/
├── AttributeEnum.cs                   # 枚举 (AttributeType/ModifyType/ElementType/FactionType/MonsterRank)
├── AttributeData.cs                   # 数据结构 (AttributeModifier + AttributeData)
├── AttributeConfig.cs                 # 属性配置基类 (SO)
├── PlayerAttributeConfig.cs           # 玩家属性配置 (SO)
├── EnemyAttributeConfig.cs            # 敌人属性配置 (SO)
├── AttributeManager.cs               # 配置管理器 (Singleton)
├── AttributeModule.cs                 # 逻辑层属性基类 (IModule→IAttribute)
├── PlayerAttributeModule.cs           # 玩家逻辑层属性模块
└── EnemyAttributeModule.cs            # 敌人逻辑层属性模块

Framework/NetworkLayer/ServerAuthority/AttributeSystem/
├── ServerAttributeModule.cs           # 服务器属性基类 (NetworkBehaviour, 已文档化)
├── ServerPlayerAttributeModule.cs     # 服务器玩家属性模块
└── ServerEnemyAttributeModule.cs      # 服务器敌人属性模块
```

## 3. 核心流程

### 3.1 属性初始化管线

```
AttributeManager.Instance.LoadAllAttributes()
    ├─ Resources.LoadAll<PlayerAttributeConfig>("Data/SO/AttributeSO/Player/")
    └─ Resources.LoadAll<EnemyAttributeConfig>("Data/SO/AttributeSO/Enemy/")
         │
         ▼ (运行时角色创建时)
PlayerActor.LocalInit()
    → PlayerAttributeModule.LocalInit()
        → _owner.GetComponent<ServerPlayerAttributeModule>()
        → serverModule.SetConfig(config)
            → InitializeAttributes()
                → 遍历 AttributeType 枚举
                → config.CalculateAttribute(type, level) → BaseValue
                → Health/Shield/Energy 标记 HasCurrentValue
            → SyncBaseAttributesToNetwork()
                → 服务端写入 16 + 10 = 26 个 NetworkVariable
            → RegisterNetworkVariableCallbacks()
                → 客户端注册 OnValueChanged → EventCenter 广播
```

### 3.2 属性值读取路径

```
外部调用 (如 AI/Skill/Combat)
    ↓
PlayerAttributeModule.GetAttribute(type)          ← LogicLayer
    ↓
_serverPlayerAttributeModule.GetAttribute(type)   ← 服务端/客户端双路径
    ├─ IsServer? → _attributes[type] → CalculateFinalValue() + cache
    └─ !IsServer? → NetworkVariable.Value (直接读取同步值)
```

### 3.3 属性修改公式

```
优先级: Set > Add + Multiply + Percent

计算:
  if (存在Set修改器) → 最后一个Set的值
  else:
    finalValue = (BaseValue + ΣAdd × stacks) × Π(Multiply^stacks) × (1 + Σ(Percent×stacks)/100)
```

**HasCurrentValue 属性**（Health/Shield/Energy）：修改器叠加后不直接应用到 NetworkVariable，而是通过 `CurrentValue` 追踪实时值。`Set` → 直接赋值 CurrentValue；`Add` → CurrentValue += value；Non-Set/Add → 重新计算并更新 CurrentValue。

### 3.4 等级成长公式

```
baseValue + growthValue × (level - 1)
```

`AttributeConfig.CalculateAttribute(type, level)` 实现。1 级时 `levelBonus=0`，无成长加成。

### 3.5 属性变化事件广播链

```
服务端 ModifyAttributeServerInternal()
    → _networkXxx.Value = newValue
         │ (NetworkVariable 自动同步到客户端)
         ▼
客户端 OnXxxChanged(old, new)
    → AttributeChangedEventTrigger(type, old, new)
        → EventCenter.Instance.Trigger(EventName.AttributeChanged, type, old, new)
            (裸参数 Trigger<AttributeType, float, float>)
```

## 4. 属性类型定义

### AttributeType 枚举 (28 个值)

| 分组 | 属性 | 说明 |
|------|------|------|
| **通用基础** | Health/MaxHealth | 生命值 |
| | Shield/MaxShield | 护盾值 |
| | Armor | 护甲 |
| | MoveSpeed | 移动速度 |
| | Level | 等级（1 起） |
| **通用抗性** | Resistance_Solid/Liquid/Gas | 固/液/气抗性 |
| | Resistance_Toxic/Fire/Ice/Electric | 毒/火/冰/电抗性 |
| **通用其他** | Resilience | 韧性 |
| | DamageOutPutRate | 伤害输出率（全局增伤） |
| | Faction | 派系（Neutral/Player/Enemy/Ally） |
| **玩家特有** | Energy/MaxEnergy | 能量值 |
| | EnergyRegen | 能量恢复 |
| | Luck | 幸运值（影响掉落率） |
| | CooldownReduction | 冷却缩减（0~80%） |
| | ArmorPenetrationRate | 护甲穿透率 |
| | SkillStrength/Duration/Range/Efficiency | 技能五维属性 |
| **敌人特有** | AggroRange | 仇恨范围 |
| | MonsterRank | 怪物等级（Normal/Elite/Boss） |
| | DropRate | 掉落率 |
| | InGameGoldReward | 局内金币奖励 |
| | OutGameCurrencyReward | 局外货币奖励 |
| | DetectionRange | 侦测范围 |

### AttributeModifyType 枚举

| 类型 | 运算符 | 叠加方式 |
|------|--------|---------|
| `Set` | `value` | 直接覆盖（最高优先级） |
| `Add` | `+ value × stacks` | 线性叠加 |
| `Multiply` | `× value^stacks` | 指数连乘 |
| `Percentage` | `+ value% × stacks` | 百分比累加 |

## 5. 关键类与文件

### AttributeConfig（配置基类 SO）

`AttributeConfig.cs:6` — 抽象 ScriptableObject，定义通用属性的基础值与成长值。

| 字段组 | 字段 | 类型 |
|--------|------|------|
| 基础值 | `baseHealth/Shield/Armor/MoveSpeed` | float |
| 成长值 | `healthGrowth/shieldGrowth/armorGrowth/moveSpeedGrowth` | float |
| 抗性 | `baseResistanceSolid/Liquid/Gas/Toxic/Fire/Ice/Electric` | float |
| 其他 | `damageOutputRate` / `baseFaction` | float / FactionType |

**`CalculateAttribute(type, level)`** — 等级成长公式：`baseValue + growthValue × (level - 1)`。抗性等无成长属性直接返回 baseValue。

### PlayerAttributeConfig（玩家配置 SO）

`PlayerAttributeConfig.cs:7` — 继承 `AttributeConfig`，追加 10 个玩家特有属性：`baseEnergy`、`baseEnergyRegen`、`baseLuck`、`baseCooldownReduction`、`baseResilience`、`baseArmorPenetrationRate`、`baseSkillStrength/Duration/Range/Efficiency`（每项均有对应的 `Growth` 字段）。`CalculateAttribute()` 先调基类再 switch 玩家特有属性。

### EnemyAttributeConfig（敌人配置 SO）

`EnemyAttributeConfig.cs:7` — 继承 `AttributeConfig`，追加敌人特有字段（`baseAggroRange`、`baseMonsterRank`、`baseDropRate` 等）。

### AttributeManager（配置管理器 Singleton）

`AttributeManager.cs:11` — 单例，负责从 Resources 加载全部属性配置。

| 方法 | 用途 |
|------|------|
| `GetPlayerAttributeConfig(playerId)` | 查询玩家属性配置，无则返回默认（id="0"） |
| `GetEnemyAttributeConfig(enemyId)` | 查询敌人属性配置；若传入 `Normal/002` 这类 Prefab 分层地址，会回退使用最后一段 `002` 查询属性 id |
| `GetRandomEnemyByRank(MonsterRank)` | 按怪物等级随机返回敌人配置 |
| `GetAvailablePlayerIds()` | 获取所有可用玩家 ID（UI 角色选择界面） |
| `ReloadAllAttributes()` | 热重载所有配置 |

**加载路径**：`Resources.LoadAll("Data/SO/AttributeSO/Player/")` / `"Data/SO/AttributeSO/Enemy/"`

### AttributeModule（逻辑层基类）

`AttributeModule.cs:7` — 抽象类，实现 `IAttribute`，持有 `LogicActor _owner` 和 `ServerAttributeModule _serverAttributeModule` 引用。

> **设计说明**：基类 `GetAttribute()` 代码已被注释保留供参考（直接返回 0 并打印 Error），实际逻辑在子类 `PlayerAttributeModule` / `EnemyAttributeModule` 中各自实现完整的 switch-case 代理到具体类型的 `ServerAttributeModule`。

### PlayerAttributeModule（逻辑层·玩家）

`PlayerAttributeModule.cs:7` — 继承 `AttributeModule`。

`LocalInit()` → `_owner.GetComponent<ServerPlayerAttributeModule>()` → `serverModule.SetConfig(config)`。

`GetAttribute(type)` → 28 项的 switch-case 代理到 `_serverPlayerAttributeModule.GetAttribute(type)`。

**玩家特有方法**：`HasEnoughEnergy(amount)` / `GetReducedCooldown(baseCd)` / `GetLuckyDropRate(baseRate)` / `IsEnergyExhausted()`

### EnemyAttributeModule（逻辑层·敌人）

`EnemyAttributeModule.cs:7` — 继承 `AttributeModule`，同构于 PlayerAttributeModule。

**敌人特有方法**：`GetMonsterRank()` / `GetActualDropRate()` / `IsInAggroRange(pos)` / `IsInDetectionRange(pos)`

### ServerPlayerAttributeModule（网络层·玩家）

`ServerPlayerAttributeModule.cs:8` — `sealed class`，继承 `ServerAttributeModule`。

**追加 10 个 NetworkVariable**：Energy/MaxEnergy/EnergyRegen/Luck/CDR/ArmorPenetration/SkillStrength × 4。

**ServerRpc**：`ConsumeEnergyServerRpc(amount)` / `RestoreEnergyServerRpc(amount)`

**`OnEnergyChanged`** — 当 `newValue <= 0` 时触发 `EventName.PlayerEnergyExhaust` 事件。

**护盾自动回复** — 玩家在服务端实际受伤后记录最后受伤时间；当处于 `Alive` 且超过 `_shieldRegenDelay` 未受伤时，按 `_shieldRegenRate` 每秒恢复护盾至 `MaxShield`。

### ServerEnemyAttributeModule（网络层·敌人）

`ServerEnemyAttributeModule.cs` — 继承 `ServerAttributeModule`，追加敌人特有 NetworkVariable 和回调（与 ServerPlayerAttributeModule 同构）。

Boss 使用 `ServerBossAttributeModule : ServerEnemyAttributeModule` 复用敌人属性与伤害管线。由于 Boss prefab 不挂 `EnemyActor`，属性配置不走普通敌人的 `EnemyAttributeModule.SetConfig()` 链路，而由 `BossNetworkProxy` 在运行时注入；服务端注入后写入 NetworkVariable，客户端注入后只初始化本地缓存并注册 NetworkVariable 回调，供血条刷新。`ServerEnemyAttributeModule.OnNetworkSpawn()` 遇到 Boss 且配置尚未注入时会等待后续 `SetConfig()`，避免组件顺序导致生成时误报配置错误。

### AttributeData / AttributeModifier（数据结构）

```csharp
class AttributeModifier {
    AttributeModifyType ModifyType;   // Set/Add/Multiply/Percent
    float Value;
    object Source;                    // 来源 ID (itemId/buffId/skillId)
    int StackCount = 1;              // 叠层数
}

class AttributeData {
    float BaseValue;         // 配置基础值
    float CurrentValue;      // 当前值 (HP/护盾/能量用)
    List<AttributeModifier> Modifiers;
    bool HasCurrentValue;    // 是否有最大值限制
    float CachedValue;       // 缓存值
    bool IsCacheDirty;       // 缓存脏标记
}
```

## 6. 对外接口

### IAttribute

```csharp
public interface IAttribute : IModule
{
    float GetAttribute(AttributeType type);
    int GetLevel();
}
```

### AttributeManager（查询接口）

| 方法 | 调用方 |
|------|--------|
| `GetPlayerAttributeConfig(id)` | 角色选择 / 初始化 |
| `GetEnemyAttributeConfig(id)` | EnemySpawnService |
| `GetRandomEnemyByRank(rank)` | 随机刷怪 |
| `GetAvailablePlayerIds()` | UI 角色选择 |
| `ReloadAllAttributes()` | 热重载 / 调试 |

## 7. 依赖模块

| 依赖模块 | 用途 |
|----------|------|
| `SingletonBase<T>` | AttributeManager 单例 |
| `ServerAttributeModule` (NetworkBehaviour) | Player/Enemy 属性模块的父类，NetworkVariable + 修改器系统 |
| `EventCenter` | AttributeChanged / PlayerEnergyExhaust 事件广播 |
| `Unity.Netcode` | NetworkVariable / ServerRpc |
| `UnityEngine.Resources` | `Resources.LoadAll()` 加载 SO 配置 |

## 8. 被哪些模块依赖

| 依赖方 | 用途 |
|--------|------|
| `PlayerActor` / `EnemyActor` | 组合 `PlayerAttributeModule` / `EnemyAttributeModule` |
| `AI 模块` | `GetAttribute(MoveSpeed/MonsterRank/AggroRange)` |
| `Skill 模块` | `GetAttribute(SkillStrength/Duration/Range/Efficiency/CDR/Energy)` |
| `Combat 模块` | `GetAttribute(Armor/Faction)` 伤害计算参考 |
| `ServerAttributeModule` | 伤害处理 `TakeDamage()`、`HealServerRpc()` 等 |
| `InventorySystem` (QualityEffects) | `AddStat` / `RemoveStat` 修改属性 |
| `ArchiveSystem` (RunSummaryCalculator) | `MonsterRegistry.TotalKilledCount` 统计击杀 |

## 9. 事件订阅与广播

### 广播事件

| 事件 | 触发方 | 触发时机 |
|------|--------|---------|
| `AttributeChanged` | ServerAttributeModule | 任意 NetworkVariable 值变化 → 客户端回调 |
| `PlayerEnergyExhaust` | `ServerPlayerAttributeModule.OnEnergyChanged()` | Energy ≤ 0 时 |

### 订阅事件

Attribute 模块**不订阅 EventCenter 事件**（仅作为事件生产者）。

## 10. Inspector 字段

### AttributeConfig (SO)

所有 `public` 字段在 Inspector 中可编辑（详见数据结构部分）。

### ServerPlayerAttributeModule

| 字段 | 类型 | 用途 |
|------|------|------|
| `_prefabPath` | `string` | 预制体路径（用于对象池回收，默认 `"Players/"`） |
| `_shieldRegenDelay` | `float` | 受伤后开始自动回盾前的延迟，默认 5 秒 |
| `_shieldRegenRate` | `float` | 护盾每秒恢复量，默认 10 点/秒 |
| `_shieldRegenTickInterval` | `float` | 自动回盾的服务端结算间隔，默认 0.2 秒 |

### PlayerAttributeModule / EnemyAttributeModule / AttributeModule

均为纯 C# 类，无 Inspector 字段。

## 11. Prefab / Scene / ScriptableObject 依赖

| 类型 | 路径/名称 | 用途 |
|------|----------|------|
| SO | `Resources/Data/SO/AttributeSO/Player/*.asset` | 玩家属性配置 |
| SO | `Resources/Data/SO/AttributeSO/Enemy/*.asset` | 敌人属性配置 |
| SO | `Resources/Configs/AI/EnemyAI_Default.asset` | AI 配置（引用 AttributeModule 属性） |
| Prefab | 玩家 Prefab | 挂载 `ServerPlayerAttributeModule` |
| Prefab | 敌人 Prefab | 挂载 `ServerEnemyAttributeModule` |

> **注意**：`AttributeConfig`（基类）和 `PlayerAttributeConfig`/`EnemyAttributeConfig` 均由 ExcelToSOGenerator 自动生成，存放于 `Resources/Data/SO/AttributeSO/` 目录。

敌人 Prefab 可以按 `Prefab/Enemy/{Rank}/{id}` 分层，例如 `Prefab/Enemy/Normal/002.prefab`。属性配置仍使用原始短 id（如 `002`），`AttributeManager.GetEnemyAttributeConfig()` 会在直接查找失败时自动取路径最后一段作为属性 id，避免 Prefab 模型分层影响属性配置命名。

## 12. 常见问题

**Q: LogicLayer AttributeModule 和 NetworkLayer ServerAttributeModule 的关系？**
A: 继承关系：`ServerAttributeModule : NetworkBehaviour`（网络层，持有完整数据），`AttributeModule : IAttribute`（逻辑层，只读代理）。LogicLayer 通过 `_owner.GetComponent<ServerXxxAttributeModule>()` 获取网络层引用，所有 `GetAttribute()` 调用委托到网络层。

**Q: AttributeModule 基类的 GetAttribute 为何被注释？**
A: 基类 `AttributeModule.GetAttribute()` 的 switch-case 已被注释（直接返回 0 并打印 Error），实际逻辑在子类 `PlayerAttributeModule` / `EnemyAttributeModule` 中各自实现。原因：基类持有的 `_serverAttributeModule` 是泛型父类，无法直接访问子类的特定 NetworkVariable，子类通过持有具体类型的引用（`_serverPlayerAttributeModule`）直接访问。

**Q: 属性修改器和武器修改器有何区别？**
A: 核心公式相同（BaseValue + Add/Multiply/Percent + Set），但数据结构独立：
- 角色属性：`AttributeModifier` + `AttributeData`，由 `ServerAttributeModule` 管理
- 武器属性：`WeaponModifier` + `WeaponAttributeData`，由 `ServerWeaponRuntime` 管理
武器独有暴击溢出转换规则和独立的 `WeaponModifyOperator` 枚举。

**Q: 伤害计算中抗性如何生效？**
A: `DamageCalculator.CalculateDamage()` 公式：
- 元素抗性区：`damageAfterRes = baseDamage × (1 - resistance)`（resistance ∈ [-1, 1]）
- 护甲区：`armorFactor = 100 / (100 + effectiveArmor)`，其中 `effectiveArmor = armor × (1 - armorPenRate)`
- 全局区：`finalDamage = (physicalBase + elemental) × dmgOutputFactor × critFactor × armorFactor`
- 毒元素穿透护盾直接扣血；液体子弹对护盾双倍伤害

**Q: 如何新增一个属性类型？**
A: 需要在以下位置同步添加：
1. `AttributeEnum.cs` → `AttributeType` 枚举新值
2. 对应的 `XxxAttributeConfig` → 基础值 + 成长值字段 + `CalculateAttribute()` switch
3. `ServerXxxAttributeModule` → NetworkVariable（若需同步 + RPC 操作）
4. `PlayerAttributeModule.GetAttribute()` / `EnemyAttributeModule.GetAttribute()` → switch-case

## 13. 当前完成度

| 功能 | 状态 |
|------|------|
| 28 种属性类型定义 | 完成 |
| 修改器系统（Set/Add/Multiply/Percent） | 完成 |
| 等级成长公式 | 完成 |
| AttributeManager 配置加载 | 完成 |
| LogicLayer↔NetworkLayer 桥接 | 完成 |
| PlayerAttributeModule / EnemyAttributeModule | 完成 |
| ServerPlayerAttributeModule（10 个玩家特有 NV） | 完成 |
| ServerEnemyAttributeModule（敌人特有 NV） | 完成 |
| EventCenter 属性变化广播 | 完成 |
| PlayerEnergyExhaust 事件 | 完成 |
| AttributeModule 基类 GetAttribute | **未实现**（已注释，子类各自实现） |
| OutGameCurrencyReward 属性 | **暂闲置** — 枚举已定义，但局外经济系统不做，该属性暂不使用 |
| 抗性计算减伤公式 | **已完成** — `DamageCalculator` 中实现：`damageAfterRes = baseDamage × (1 - resistance)` |

## 14. 修改本模块时必须同步更新的内容

- **AttributeType 新增值** → 同步更新：
  - `AttributeConfig.CalculateAttribute()` switch-case
  - `PlayerAttributeConfig.CalculateAttribute()` / `EnemyAttributeConfig.CalculateAttribute()` 对应 switch
  - `PlayerAttributeModule.GetAttribute()` 28 项 switch-case
  - `EnemyAttributeModule.GetAttribute()` switch-case
  - `ServerAttributeModule.UpdateNetworkVariable()` switch-case（若需同步）
  - `ServerPlayerAttributeModule` / `ServerEnemyAttributeModule` 的 NetworkVariable 声明 + 回调注册 + `SyncBaseAttributesToNetwork()`
- **AttributeModifyType 新增值** → 同步更新 `CalculateFinalValue()` 公式
- **Config 新增字段** → 同步更新 ExcelToSOGenerator 的 ColumnMapping
- **修改 AttributeManager 加载路径** → 确认 Resources 目录下的对应 SO 存在

## 15. 文档维护信息

| 项目 | 内容 |
|------|------|
| 创建日期 | 2026-05-14 |
| 覆盖文件数 | 13 个 .cs（含 3 个接口层） |
| 关联模块文档 | EventCenter, Combat, Skill, AI, InventorySystem (QualityEffects), ServerAttributeModule功能解读 |

## 16. 2026-06-29 Skill Phase 1 能量扣除接入

- `ServerAttributeModule.ModifyAttributeServerInternal()` 已调整为 `protected`，供服务端权威子类在不走 RPC 的情况下复用属性修改、NetworkVariable 同步与事件广播逻辑。
- `ServerPlayerAttributeModule.TryConsumeEnergyServerInternal(float amount)` 新增为服务器内部扣能量入口，供 `ServerSkillModule` 在技能释放确认时调用。
- 该内部入口只允许 `IsServer` 分支执行；能量不足返回 `false`，成功后委托 `ModifyAttributeServerInternal(AttributeType.Energy, ...)` 完成同步。
- 客户端仍不能直接调用该内部方法；外部客户端请求继续通过各模块自己的 `ServerRpc` 入口做 Owner 校验。

## 17. 2026-06-30 元素 Buff 与属性修改器移除接入

- `ServerAttributeModule.RemoveModifiers()` 现在与 `AddModifier()` 对称：服务端调用时走 `RemoveModifiersServerInternal()`，客户端请求才走 `RemoveModifiersServerRpc()`。
- `ServerAttributeModule.TakeDamage()` 会把 `DamageResult` 和原始 `DamageInfo` 一起传给元素触发流程，用于确定施加者、ClientId 与元素伤害 α 快照。
- `ApplyElementTriggers()` 已从日志占位改为调用目标身上的 `ServerBuffModule.ApplyBuff(...)`，并通过 `ElementBuffMappingAsset` 查找火/冰/毒/电 BuffData。
- 需要人工确认：目标 Prefab 必须挂载并正确绑定 `ServerBuffModule.attrModule`，元素 BuffData 必须存在于 Resources 或映射资源中，否则只会输出 warning。

## 18. 2026-06-30 玩家 HUD 初始数值修正

- `PlayerAttributeConfig.CalculateAttribute()` 现在对 `Energy` 和 `MaxEnergy` 都返回 `baseEnergy + energyGrowth × (level - 1)`，避免当前能量在初始化时保持 0。
- `ServerAttributeModule.GetAttribute()` 在客户端优先读取已同步的 `NetworkVariable`，即使本地没有完整 `_attributes` 字典，也能为 HUD / 表现层返回 Health、Shield、Energy 等同步值。
- `ServerPlayerAttributeModule` 在缺少直接注入配置时，会尝试从 `PlayerActor.HeroSO.attributeConfig` 兜底解析；客户端即使没有本地配置，也会注册 NetworkVariable 回调用于 UI 刷新。

## 19. 2026-07-01 玩家护盾自动回复

- `ServerAttributeModule.TakeDamage()` 在服务端实际扣除 Shield / Health 后调用 `OnDamageApplied(DamageInfo, DamageResult)`，玩家属性模块据此记录最后受伤时间。
- `RestoreShieldServerRpc()` 委托到 `RestoreShieldServerInternal(float amount)`，自动回盾在服务端内部直接复用同步与事件广播逻辑。
- `ServerPlayerAttributeModule.Update()` 仅在服务端、玩家 `Alive`、护盾未满且超过 `_shieldRegenDelay` 未受伤时回盾；倒地、死亡或观战状态不会自动恢复。
- 自动回盾按 `_shieldRegenTickInterval` 累积结算，恢复量仍按 `_shieldRegenRate × 实际累积时间` 计算。
- 需要人工确认：玩家 Prefab 上 `_shieldRegenDelay`、`_shieldRegenRate`、`_shieldRegenTickInterval` 的正式调参值是否符合手感。
