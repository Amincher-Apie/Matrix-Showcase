# DamageCenter 伤害中心

## 1. 模块职责

纯静态工具模块，提供完整的伤害计算管线。包括：

- **武器伤害计算**（`CalculateDamage`）：从 `ServerWeaponRuntime` 读取武器面板 + 目标抗性 → 计算 `DamageInfo`
- **技能/泛用伤害计算**（`CalculateDamageFromProfile`）：从外部传入 `DamageProfile` + `PhysicalBulletType` → 计算 `DamageInfo`
- **伤害应用**（`ApplyDamage`）：将 `DamageInfo` 按物理子弹类型分配到护盾/生命值 → 输出 `DamageResult`
- **元素异常触发层数**（`ComputeElementProcLayers`）：基于 `ProcChance` 按元素伤害占比分配触发层数
- **数据结构定义**：`DamageInfo` / `DamageResult` / `DamageVfxEvent` / `PhysicalBulletType`

## 2. 模块边界

| 边界 | 说明 |
|------|------|
| **输入** | `ServerWeaponRuntime.GetModifiedDamageProfile()`（武器面板）/ 外部构造的 `DamageProfile` + `PhysicalBulletType` |
| **输出** | `DamageInfo`（完整伤害数据） → `ServerAttributeModule.TakeDamage()` / `DamageResult`（护盾+生命分配结果）→ `EventCenter.Trigger(UnitDamaged)` |
| **不负责** | 伤害的实际扣除（由 `ServerAttributeModule.TakeDamage()` 负责）；Buff 回调判断（由 `BuffHandler` 通过 `ServerCombatModule.ProcessValidatedHits()` 调用）；击退/硬直等控制效果 |

**文件分布**：

```
Framework/LogicLayer/DamageCenter/
├── DamageCalculator.cs       # 伤害计算器（静态工具类）
├── DamageStruct.cs           # 数据结构（DamageInfo/DamageResult/PhysicalBulletType）
└── DamageVfxEvent.cs         # 客户端伤害特效事件 (INetworkSerializable)
```

## 3. 核心流程

### 3.1 武器伤害计算（`CalculateDamage`）

```
输入: sourceActorId, targetActorId
    │
    ├─ NetworkObjectManager.TryGetNetworkProxy(双方)
    │   → sourceProxy.GetServerAttributeModule<ServerAttributeModule>()
    │   → targetProxy.GetServerAttributeModule<ServerAttributeModule>()
    │   → sourceProxy.GetServerWeaponRuntime<ServerWeaponRuntime>()
    │
    ├─ 1. 武器面板: weaponRuntime.GetModifiedDamageProfile()
    │    → solid/liquid/gas + ice/fire/toxic/electric
    │
    ├─ 2. 物理类型: weaponRuntime.GetPhysicalBulletType() → Solid/Liquid/Gas
    │
    ├─ 3. 抗性区: 每元素 × (1 - resistance)
    │    damageAfterRes = baseDamage × (1 - resistance)   (resistance ∈ [-1, 1])
    │
    ├─ 4. 物理基础 = solidAfterRes + liquidAfterRes + gasAfterRes
    │    元素合计 = iceAfterRes + fireAfterRes + toxicAfterRes + elecAfterRes
    │
    ├─ 5. 输出率(增伤): dmgOutputFactor = 1 + DamageOutPutRate
    │
    ├─ 6. 暴击区: critChance(Weapon), critMulti(Weapon) → isCrit? critMulti : 1
    │
    ├─ 7. 护甲区: 
    │    armorPenRate (+0.2 if Solid), effectiveArmor = armor×(1-penRate)
    │    armorFactor = 100 / (100 + effectiveArmor)
    │
    ├─ 8. 全局乘区: globalFactor = dmgOutput × crit × armor × special(1)
    │    → 分别乘到物理基础和各元素
    │
    ├─ 9. 元素触发: procChance → ComputeElementProcLayers
    │    → fullProcs + extraProc 随机 → 按伤害占比加权分配层数
    │
    └─ 输出: DamageInfo(amount + 四元素分量 + 触发层数)
```

### 3.2 技能/泛用伤害计算（`CalculateDamageFromProfile`）

与 `CalculateDamage` 同构，差异：
- 不依赖 `ServerWeaponRuntime`，伤害面板由外部传入 `DamageProfile`
- 物理类型由外部传入 `PhysicalBulletType`
- 暴击由外部参数控制（`enableCrit` + `extraCritChance/Multi`），默认不暴击
- 不做元素异常层数分配（`ProcChance` 无武器源）

### 3.3 伤害应用（`ApplyDamage`）

```
输入: DamageInfo + currentHealth + currentShield
    │
    ├─ 1. 毒元素穿盾 → 直接扣生命
    │    poisonHpDamage = info.poisonDamage → health -= poisonHpDamage
    │
    ├─ 2. 剩余伤害按物理类型处理:
    │    ├─ Liquid: 剩余伤害对护盾×2倍，折半溢出到生命
    │    │    intendedShieldDamage = remaining × 2
    │    │    盾不够吃 → 浪费部分折半为 baseOverflow → 扣血
    │    └─ Solid/Gas (default): 先打护盾→溢出到生命
    │
    └─ 输出: DamageResult(shieldDamage + healthDamage + targetDied)
```

**护盾/生命分配通则**：
- 非毒伤害 → 先扣护盾 → 溢出扣生命
- 毒伤害 → 直接穿盾扣生命
- 液体子弹 → 护盾 2 倍伤害效率 + 浪费部分折半溢出

## 4. 核心数据结构

### PhysicalBulletType

```csharp
enum PhysicalBulletType { Solid, Liquid, Gas }
```

| 类型 | 护甲/护盾特性 |
|------|-------------|
| `Solid` | 护甲穿透 +0.2；护盾正常处理 |
| `Liquid` | 护盾双倍伤害 + 浪费折半溢出 |
| `Gas` | 同 Solid（无特殊规则） |

### DamageInfo

```csharp
struct DamageInfo {
    float amount;                 // 总伤害
    PhysicalBulletType physicalBulletType;
    ulong sourceActorId / targetActorId;
    Vector3 hitWorldPos;
    bool isCritical / isSkill;
    // 四元素分量
    float iceDamage / fireDamage / poisonDamage / electricDamage;
    // 四元素触发层数
    int iceTriggerLayer / fireTriggerLayer / poisonTriggerLayer / electricTriggerLayer;
}
```

### DamageResult (INetworkSerializable)

```csharp
struct DamageResult : INetworkSerializable {
    float totalDamage;           // = shieldDamage + healthDamage
    float shieldDamage;          // 实际护盾扣除
    float healthDamage;          // 实际生命扣除
    bool targetDied;             // 目标是否死亡
    // + 同 DamageInfo 的四元素分量和触发层数
}
```

### DamageVfxEvent (INetworkSerializable)

```csharp
struct DamageVfxEvent : INetworkSerializable {
    ulong targetId;              // 受击方
    ulong sourceId;              // 攻击方
    Vector3 hitWorldPos;         // 权威命中点
    DamageResult damageResult;   // 伤害结果
}
```

**用途**：服务端 → 客户端单向传输，供 `DamageWorldTextManager` 显示跳字和命中特效。

## 5. 关键类与文件

### DamageCalculator（静态工具类）

`DamageCalculator.cs:11` — 全部公开方法为 `static`。

| 方法 | 用途 |
|------|------|
| `CalculateDamage(ulong source, ulong target)` | **武器直接攻击专用**，从 `ServerCombatModule.ProcessValidatedHits` 调用 |
| `CalculateDamageFromProfile(ulong source, ulong target, DamageProfile, PhysicalBulletType, ...)` | **技能/道具/环境伤害**，不依赖武器模块 |
| `ApplyDamage(DamageInfo, currentHealth, currentShield)` | 将 `DamageInfo` 分配到护盾/生命，输出 `DamageResult` |

**常量**：

| 常量 | 值 | 说明 |
|------|----|------|
| `ARMOR_CONSTANT` | 100 | 护甲公式常数 |
| `BASE_CRITICAL_MULTIPLIER` | 1.5x | 默认暴击倍率 |
| `SOLID_ARMOR_PENETRATION_BONUS` | 0.2 | 固体子弹额外穿透 |

**核心公式**：

```
抗性区:   damageAfterRes = baseDamage × (1 - resistance)
护甲区:   armorFactor = 100 / (100 + armor × (1 - armorPenRate))
全局乘区: globalFactor = (1 + DamageOutPutRate) × critFactor × armorFactor × specialFactor
最终伤害: finalDamage = (physicalBase + elementalAfterRes) × globalFactor
```

### DamageProfile

`DamageProfile` 定义于 `Assets/Scripts/Enum/WeaponSpecialize.cs:7`：

```csharp
sealed class DamageProfile {
    int solid/liquid/gas;    // 物理三态
    int ice/fire/toxic/electric; // 四元素
}
```

## 6. 对外接口

```csharp
// 武器伤害
public static DamageInfo CalculateDamage(ulong sourceActorId, ulong targetActorId);

// 技能/道具伤害
public static DamageInfo CalculateDamageFromProfile(
    ulong sourceActorId, ulong targetActorId,
    DamageProfile baseProfile, PhysicalBulletType bulletType,
    bool enableCrit = false, float extraCritChance = 0f, float extraCritMulti = 0f);

// 护盾/生命分配
public static DamageResult ApplyDamage(
    DamageInfo info, float currentHealth, float currentShield);
```

## 7. 依赖模块

| 依赖模块 | 用途 |
|----------|------|
| `NetworkObjectManager` | `TryGetNetworkProxy<T>()` 查找攻击/受击方 |
| `ServerAttributeModule` | `GetAttribute(Armor/Resistance_*/DamageOutPutRate/ArmorPenetrationRate)` |
| `ServerWeaponRuntime` | `GetModifiedDamageProfile()` + `GetPhysicalBulletType()` + `GetAttribute(WeaponAttributeType.*)` |

## 8. 被哪些模块依赖

| 依赖方 | 方式 |
|--------|------|
| `ServerCombatModule.ProcessValidatedHits()` | 调用 `CalculateDamage` + Buff 回调链 |
| `BombardAreaSkillExecutor.ApplyOneBombardTick()` | 调用 `CalculateDamageFromProfile` |
| `ServerAttributeModule.TakeDamage()` | 接收 `DamageInfo` → 调用 `ApplyDamage` → 扣盾/血 |

## 9. 事件订阅与广播

DamageCenter **不订阅也不广播 EventCenter 事件**。它是纯函数模块，输入数据 → 输出 `DamageInfo`/`DamageResult`，事件广播由调用方负责（`ServerCombatModule` / `ServerAttributeModule`）。

## 10. Inspector 字段

DamageCenter 无 `[SerializeField]` 字段。`DamageCalculator` 是静态类，`DamageInfo`/`DamageResult`/`DamageVfxEvent` 是纯数据结构。

## 11. Prefab / Scene / ScriptableObject 依赖

无直接依赖。`DamageProfile` 由 `ServerWeaponRuntime.GetModifiedDamageProfile()` 内部从 `WeaponSO` (SO) 读取。

## 12. 常见问题

**Q: `CalculateDamage` 和 `CalculateDamageFromProfile` 的区别？**
A: 前者用于武器直接攻击，从 `ServerWeaponRuntime` 读取武器面板和暴击率；后者用于技能/道具/环境伤害，伤害面板由外部构造。两者共用相同的抗性/护甲/全局乘区公式。

**Q: 毒元素为什么能穿盾？**
A: 设计意图。`ApplyDamage` 中 `poisonDamage` 直接扣生命，不经过护盾。体现在 `remaining = amount - poisonDamage` → 只对非毒部分做护盾分配。

**Q: 液体子弹"双倍护盾伤害"的溢出如何计算？**
A: `intendedShieldDamage = remaining × 2` → 先打到护盾上 → 实际盾损 ≤ 护盾值 → 超出部分折半：`usedBase = actualShieldLoss / 2` → 剩余基础伤害 `remaining - usedBase` 溢出到生命。

**Q: 元素触发层数（Proc）如何分配？**
A: `ComputeElementProcLayers` 中 `fullProcs = floor(procChance)` + `extraProc 随机 ±1` → 按四元素伤害占比加权随机分配到对应元素层数。

## 13. 当前完成度

| 功能 | 状态 |
|------|------|
| `CalculateDamage`（武器伤害） | 完成 |
| `CalculateDamageFromProfile`（技能伤害） | 完成 |
| `ApplyDamage`（护盾/生命分配） | 完成 |
| 元素触发层数分配 | 完成 |
| 抗性减伤公式 | 完成 |
| 护甲公式 | 完成 |
| 暴击计算 | 完成 |
| 液体双倍护盾 + 毒穿盾 | 完成 |
| 固体额外护甲穿透 | 完成 |
| `specialFactor` 扩展（Buff/派系克制） | **预留**（当前恒为 1） |

## 14. 修改本模块时必须同步更新的内容

- **物理公式常量变更**（`ARMOR_CONSTANT` 等）→ 关注所有调用方（Combat + Skill 的 Executor）
- **DamageInfo 新增字段** → 同步更新 `DamageResult` 构造器 + `ApplyDamage` 输出
- **新增 PhysicalBulletType** → 在 `ApplyDamage` 的 switch-case 中增加新类型的护盾/护甲规则
- **DamageProfile 新增元素** → 同步更新 `CalculateDamage` / `CalculateDamageFromProfile` 中的伤害分量读取

## 15. 文档维护信息

| 项目 | 内容 |
|------|------|
| 创建日期 | 2026-05-14 |
| 覆盖文件数 | 3 个 .cs + 1 个外部引用 (`DamageProfile` in WeaponSpecialize.cs) |
| 关联模块文档 | Combat (调用方), Skill (BombardArea), ServerAttributeModule (TakeDamage 消费方), Attribute (抗性/护甲公式) |

## 16. 2026-06-30 技能元素触发接入

- `DamageCalculator.CalculateDamageFromProfile()` 新增可选参数 `procChance`，默认值为 `0f`，因此旧的技能/道具/环境伤害调用保持不触发元素异常。
- 当 `procChance > 0` 时，技能伤害会复用 `ComputeElementProcLayers()`，按最终四元素伤害占比分配 `ice/fire/poison/electricTriggerLayer`。
- `BombardAreaSkillExecutor` 与 `PiercingShotSkillExecutor` 会把 `SkillDefinitionSO.skillProcChance` 传入该参数。
- 需要人工确认：技能 SO 的 `skillProcChance` 若保持 0，则即使存在元素伤害也只造成伤害，不会自动施加元素 Buff。
