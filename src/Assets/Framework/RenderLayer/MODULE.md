# RenderLayer 渲染表现层

## 1. 模块职责

RenderLayer 负责客户端表现逻辑，包括玩家表现对象、武器表现、伤害跳字和特效触发。表现层通过 `EventCenter` 订阅逻辑/网络层事件，不直接参与伤害、弹药、属性等权威逻辑。

## 2. 文件结构

```
Assets/Framework/RenderLayer/
├── RenderObject.cs
├── Interfaces/
│   ├── IRenderObject.cs
│   └── IRenderModule.cs
├── RenderActor/
│   └── RenderActor.cs
├── PlayerRender/
│   └── PlayerRender.cs
├── RenderModule/
│   └── PlayerTestRenderModule.cs
├── WeaponRender/
│   └── WeaponAnimationController.cs
└── DamageText/
```

## 3. 核心类

| 类 | 职责 |
|----|------|
| `RenderObject` | 渲染对象基类，提供 Initialize / OnActivate / Destroy 生命周期 |
| `RenderActor` | 渲染模块组合基类，管理 `IRenderModule` |
| `PlayerRender` | 玩家表现入口，绑定武器瞄准与武器动画控制器 |
| `PlayerTestRenderModule` | 监听 `LocalWeaponFired` / `RemoteWeaponFired` 播放枪口特效 |
| `WeaponAnimationController` | 监听开火/换弹事件，驱动武器 Animator 的 `IsFiring` 与 `Reload` |
| `DamageWorldTextManager` | 客户端伤害跳字管理 |

## 4. 武器动画接入

`WeaponAnimationController` 挂载在武器 Prefab 根节点或 Animator 所在节点，推荐与 `WeaponAimController` 位于同一武器根节点。当前约定的 Animator 参数：

| 参数 | 类型 | 用途 |
|------|------|------|
| `IsFiring` | Bool | 控制射击循环动画，例如 Gatling 的 `Spin` |
| `Reload` | Trigger | 触发换弹动画，例如临时使用 `MegaBlast` |
| `FireSpinSpeed` | Float | 可选，用于按射速调节武器动画速度 |

事件订阅：

| EventName | 行为 |
|-----------|------|
| `LocalWeaponFired` | actorId / weaponId 匹配时播放本地武器射击脉冲 |
| `RemoteWeaponFired` | 非本地玩家且 actorId / weaponId 匹配时播放远端同步射击脉冲，避免本地预测后重复触发 |
| `ReloadStarted` | actorId / weaponId 匹配时停止射击并触发 `Reload` |
| `ReloadFinished` | actorId / weaponId 匹配时确保 `IsFiring=false` |

## 5. 依赖关系

| 依赖 | 用途 |
|------|------|
| `EventCenter` | 订阅战斗表现事件 |
| `WeaponAimController` | 获取武器 FirePoint，供特效和瞄准使用 |
| `PlayerActor` | `PlayerRender` 通过 ActorId 过滤表现事件 |
| `Animator` | 驱动武器/角色表现状态 |

## 6. 需要人工确认

- 武器 Prefab 上必须挂载 `WeaponAnimationController` 和 Animator，并正确配置 Controller。
- Gatling 武器 Animator 需要包含 `IsFiring` Bool 和 `Reload` Trigger。
- `Spin` Clip 需要开启 Loop Time，`MegaBlast` Clip 需要关闭 Loop Time。
- `WeaponFollowPoint`、`FirePoint`、`MuzzleVfxPoint` 的实际位置需要在 Unity Editor 中检查。
