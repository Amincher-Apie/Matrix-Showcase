# 动画参数速查表

> 项目：Matrix  
> 用途：美术 / 动画师制作 AnimatorController 时的参数命名约定  
> 关联：[AnimationResourceIntegrationManual.md](./AnimationResourceIntegrationManual.md) 第 5 节（完整说明）

## 敌人（至少提供）

| 参数名 | 类型 | 用途 |
|--------|------|------|
| `Speed` | `float` | Idle / Walk / Run BlendTree |
| `IsDead` | `bool` | 锁定死亡状态，防止回到 Idle |
| `Attack` | `trigger` | 播放普通攻击 |
| `Hit` | `trigger` | 播放受击表现 |
| `Die` | `trigger` | 播放死亡动画 |

## 玩家（至少提供）

| 参数名 | 类型 | 用途 |
|--------|------|------|
| `Speed` | `float` | Idle / Walk / Run BlendTree |
| `MoveX` | `float` | 水平 strafe BlendTree 输入 |
| `MoveY` | `float` | 前后移动 BlendTree 输入 |
| `Attack` | `trigger` | 播放普通攻击 |
| `Skill` | `trigger` | 播放技能动画 |
| `IsGrounded` | `bool` | 跳跃 / 落地切换 |

## 可选参数

| 参数名 | 类型 | 用途 |
|--------|------|------|
| `AttackIndex` | `int` | 随机或连段攻击选择 |
| `SkillIndex` | `int` | 多技能动画选择 |
| `AimPitch` | `float` | 上下瞄准修正 |
| `VerticalSpeed` | `float` | 跳跃 / 下落速度 |
