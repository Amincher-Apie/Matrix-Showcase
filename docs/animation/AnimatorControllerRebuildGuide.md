# AnimatorController 重建手册

> 项目：Matrix  
> 用途：基于模型自带动画 Clip，重建支持代码参数驱动的 AnimatorController  
> 策略：**弃用原 Controller，新建含参数 + BlendTree 的 Controller，引用 FBX 中的 Clip**  
> 绑定步骤：另见 [AnimatorControllerBindingGuide.md](./AnimatorControllerBindingGuide.md)

## 0. 背景：为什么必须重建

模型附带的 Controller（如 `Arack_Controller.controller`）具有以下特征：

- **参数列表为空**（`m_AnimatorParameters: []`）：所有过渡仅靠 Exit Time 完成，无法接受代码驱动。
- **无 BlendTree**：Idle / Walk / Run 是独立状态，靠 Exit Time 循环，无法通过 `Speed` float 做平滑混合。
- **状态方向复杂**：Arack 有 28 个状态，Droid OII 有 80+ 个状态，大量方向/武器变体不适合当前项目。

**重建策略**：创建全新 Controller，手动添加参数和 BlendTree，从 FBX 中拖入所需 Clip。

## 敌人：Arack_Orange Controller 重建

### 1. 确认 FBX 中可用的 Clip

模型 FBX 路径：
`Assets/Resources/Modules/Protofactor/Sci Fi/Sci Fi Characters Mega Pack Vol 2/Sci Fi Creatures Vol 2/Arack/Mesh/Arack.fbx`

展开 FBX 即可看到所有 Clip 子资源。

### 2. 创建 Controller

1. 在 `Assets/Resources/Animation/Enemy/` 右键 → Create → Animator Controller
2. 命名为 `Arack_Orange.controller`

### 3. 添加参数

| 参数名 | 类型 | 默认值 |
|--------|------|--------|
| `Speed` | `float` | `0` |
| `IsDead` | `bool` | `false` |
| `Attack` | `trigger` | - |
| `Hit` | `trigger` | - |
| `Die` | `trigger` | - |

### 4. 创建 Locomotion BlendTree

1. 在 Base Layer 右键 → Create State → From New BlendTree
2. 命名为 `Locomotion`
3. BlendTree 参数 = `Speed`
4. 添加 3 个 Motion Field：

| Motion | Clip 来源（在 FBX 中找） | Threshold |
|--------|--------------------------|-----------|
| Idle | `IdleBreathe` | `0` |
| Walk | `Walk`（非 `_RM`） | `1.5` |
| Run | `Run`（非 `_RM`） | `3.5` |

> 从 FBX 拖拽 Clip 时：展开 `Arack.fbx` → 找到对应名称的 Clip 节点 → 拖入 Motion 槽位。

### 5. 创建 Attack / Hit / Die 状态

| 状态名 | Clip 来源（在 FBX 中找） |
|--------|--------------------------|
| `Attack` | `ClawsAttack` |
| `Hit` | `GetHitFront` |
| `Die` | `Death` |

### 6. 创建 Dead 冻结状态

1. 新建状态 `Dead`
2. Motion = 同一段 Death Clip
3. Speed = `0`（冻结）

> 不能用空 Motion，否则角色回到绑定姿态（T-pose/A-pose）。

### 7. 配置过渡

> 统一使用 `Any State` 作为入口。BlendTree 的退出条件不可靠。

#### Any State → Attack

- 方式：`Any State` → `Attack`
- 条件：`Attack` trigger **且** `IsDead = false`
- Has Exit Time：取消

#### Any State → Hit

- 方式：`Any State` → `Hit`
- 条件：`Hit` trigger **且** `IsDead = false`
- Has Exit Time：取消

#### Any State → Die

- 方式：`Any State` → `Die`
- 条件：`Die` trigger
- Has Exit Time：取消

#### Attack → Locomotion

- Has Exit Time：勾选（Attack 播完自动退回 BlendTree）

#### Hit → Locomotion

- Has Exit Time：勾选

#### Die → Dead

- Has Exit Time：勾选
- Exit Time：`0.95`
- Transition Duration：`0`（无混合，立即切）
- Transition Offset：`0.95`

> `Transition Offset` 让 Dead 进入时从 Clip 的 95% 位置开始，叠加 `Speed = 0` 即冻结在最后一帧。

### 8. IsDead 防护

`Die` → `IsDead = true` 后，`Any State → Attack/Hit` 的 `IsDead = false` 条件阻断二次触发。

---

## 玩家：Droid_OII Controller 重建

### 1. 确认 FBX

`Assets/Resources/Modules/Protofactor/Sci Fi/Sci Fi Characters Mega Pack Vol 2/SciFi Robots Pack Vol 2/Droid OII/Mesh/Droid_OII.fbx`

### 2. 创建 Controller

`Assets/Resources/Animation/Player/Droid_OII.controller`

### 3. 添加参数

| 参数名 | 类型 | 默认值 | 用途 |
|--------|------|--------|------|
| `Speed` | `float` | `0` | 移动速度模长（用于 Walk/Run 判定） |
| `MoveX` | `float` | `0` | 水平 strafe |
| `MoveY` | `float` | `0` | 前后移动 |
| `IsDead` | `bool` | `false` | 死亡锁定 |
| `IsGrounded` | `bool` | `true` | 跳跃/落地 |
| `Attack` | `trigger` | - | 普通攻击 |
| `Skill` | `trigger` | - | 技能动画 |
| `Hit` | `trigger` | - | 受击 |
| `Die` | `trigger` | - | 死亡 |

### 4. 创建 2D Locomotion BlendTree

1. 新建 BlendTree 状态 `Locomotion`
2. Blend Type = `2D Freeform Directional`
3. 参数 = `MoveX` + `MoveY`

添加 Motion Field（均取自 `Droid_OII.fbx`，选非 `_RM` 版本）：

| 方向 | Pos X | Pos Y | Droid_OII.fbx 中的 Clip 名 |
|------|-------|-------|----------------------------|
| Idle | 0 | 0 | `IdleUnarmed` |
| 前进 Walk | 0 | 1 | `WalkForwardCombat` |
| 前进 Run | 0 | 2 | `RunForwardCombat` |
| 后退 Walk | 0 | -1 | `WalkBackwardsCombat` |
| 后退 Run | 0 | -2 | `RunBackwardsCombat` |
| 右 Walk | 1 | 0 | `WalkRightCombat` |
| 右 Run | 2 | 0 | `RunRightCombat` |
| 左 Walk | -1 | 0 | `WalkLeftCombat` |
| 左 Run | -2 | 0 | `RunLeftCombat` |

> `Speed` 参数由代码端计算 `sqrt(MoveX² + MoveY²)` 填入，用于速度相关的状态判定。

### 5. 创建 Attack / Skill / Hit / Die 状态

| 状态名 | Clip 来源 |
|--------|-----------|
| `Attack` | `Attack1Combat` |
| `Skill` | 暂留空（TODO：接入具体技能 Clip） |
| `Hit` | `GetHitFrontCombat` |
| `Die` | `DeathFrontCombat` |

过渡规则与敌人相同：
- 全部使用 `Any State` 入口（BlendTree 退出条件不可靠）
- Attack / Hit / Skill 带 `IsDead = false`
- Die → Dead：Exit Time=0.95，Duration=0，Offset=0.95，Dead 用同段 Clip + Speed=0 冻结最后一帧

---

## Clip 设置检查清单

- [ ] Idle / Walk / Run 系列：**Loop Time = true**
- [ ] Attack / Hit / Die / Skill：**Loop Time = false**
- [ ] 所有 Clip 的 **Root Motion** 相关选项保持默认关闭
