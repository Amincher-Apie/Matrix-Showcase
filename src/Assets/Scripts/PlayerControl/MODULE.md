# PlayerControl 玩家控制系统

## 1. 模块职责

处理本地玩家的输入、移动、相机和武器瞄准。包括：

- **输入系统**：`PlayerInputSystem`（MonoSingleton）封装 Unity Input System，提供 11 个 C# 事件接口
- **第三人称控制**：`ThirdPersonPlayerController` — CharacterController 移动 + 相机旋转 + 跳跃/重力 + 静止转向 + 射击转向
- **武器瞄准**：`WeaponAimController` — 相机屏幕中心 → 射线命中点 → 枪口瞄准射线，并自动旋转武器瞄准根节点
- **玩家选择**：`IPlayerSelector` 接口 + `DefaultPlayerSelector`（本地多人分屏占位）

## 2. 模块边界

| 边界 | 说明 |
|------|------|
| **输入层** | `PlayerInputSystem` → 将 Unity Input System 转为 11 个 C# event，供其他模块订阅 |
| **控制器** | `ThirdPersonPlayerController` → `InitAsLocalPlayer()` / `SetInputEnabled(false)` 区分属主和非属主 |
| **瞄准层** | `WeaponAimController.GetAimRay()` → 屏幕中心射线 → 目标点 → FirePoint 方向；`LateUpdate()` 自动旋转 `aimRoot -> firePoint` 向量对准准心目标点 |
| **不负责** | 网络移动同步（`ClientNetworkTransform` 处理）；伤害/开火逻辑（Combat 模块接收 `FireContext`） |

**文件分布**：

```
Assets/Scripts/PlayerControl/
├── PlayerSkillInputBinder.cs        # 技能数字键输入绑定（运行时挂载）
├── Input/
│   ├── PlayerInputSystem.cs         # 输入系统封装 (MonoSingleton)
│   └── InputController.cs           # 自动生成 (Unity Input System)
├── Camera/
│   ├── ThirdPersonPlayerController.cs  # 第三人称控制器
│   └── WeaponAimController.cs          # 武器瞄准控制器
└── PlayerSelection/
    ├── IPlayerSelector.cs           # 玩家选择接口
    ├── DefaultPlayerSelector.cs     # 默认实现
    └── PlayerSelectionInfo.cs       # 选择信息数据
```

## 3. 核心流程

### 3.1 输入流程

```
Unity Input System (InputController.inputactions)
    → InputController (自动生成, partial class)
        → PlayerInputSystem : MonoSingletonBase<PlayerInputSystem>, IPlayerActions
            → _inputController.Player.AddCallbacks(this)
            → 11 个 OnXxx 方法 → 转发为 C# event:
                OnMovementInput / OnLookInput / OnJumpInput / OnFireInput
                OnReloadInput / OnCastSkillInput / OnCastUltimateInput
                OnUseActiveItemInput / OnOpenMapInput / OnOpenInventoryInput
                OnSprintInput
```

**按键绑定**（当前）：

| 动作 | 键位 | 类型 |
|------|------|------|
| Movement | WASD (2D Vector composite) | Value |
| Look | Mouse delta (ScaleVector2) | Value |
| Fire | J | Button |
| Jump | Space | Button |
| Sprint | LeftShift | Button |
| Reload | R | Button |
| CastSkill | Q | Button |
| CastUltimate | E | Button |
| UseActiveItem | C | Button |
| OpenMap | M | Button |
| OpenInventory | Tab | Button |

### 3.2 第三人称移动控制流程

```
Update() [仅 _inputEnabled=true]:
    CheckGroundedState()        → Physics.CheckSphere 地面检测
    HandleJumpAndGravity()       → Jump → _verticalVelocity = sqrt(H × -2G)
                                   → _verticalVelocity += Gravity × dt (下落加速)
    HandleCharacterMovement()    → Sprint? SprintSpeed : WalkSpeed
                                   → GetMovementInput → 相机相对方向 → SmoothDampAngle 转向
                                   → CharacterController.Move(direction × speed + gravity)
    (if idle) HandleIdleRotation() → 静止时随相机自动转向

LateUpdate():
    HandleCameraRotation()       → GetLookInput × MouseSensitivity
                                   → _cinemachineTargetYaw/Pitch → CinemachineCameraTarget.rotation

开火输入:
    OnFireInput() → HandleShootRotation() → CombatModule.TryFire(CreateFireContext())
                   → WeaponAimController.GetAimRay() → FireContext(origin, dir)
```

### 3.3 武器瞄准流程

```
WeaponAimController.GetAimRay():
    ① _mainCamera.ViewportPointToRay(0.5, 0.5) → 屏幕中心射线
    ② Physics.Raycast(cameraRay, maxAimDistance, aimLayerMask)
    ③ 命中自己? → 改用远点 (cameraRay.origin + dir × maxAimDistance)
    ④ 最终射线: firePoint.position → targetPoint
    返回: Ray(origin=firePoint, dir=targetPoint-origin)

WeaponAimController.LateUpdate():
    ① 解析 aimRoot（未配置时使用武器自身 transform）和 firePoint
    ② 复用准心目标点 targetPoint
    ③ 计算当前模型向量 firePoint.position - aimRoot.position
    ④ Quaternion.FromToRotation(currentVector, targetVector)
    ⑤ 旋转 aimRoot，使武器模型实际指向准心目标点
```

### 3.4 本地玩家初始化

```
PlayerNetworkProxy.OnNetworkSpawn():
    → IsOwner? → controller.InitAsLocalPlayer()
        → _inputEnabled=true, IsLocalPlayer=true
        → 虚拟相机 → Follow=CinemachineCameraTarget
        → WeaponAimController.BindCamera(_mainCamera)
        → HideAndLockMouse()

    → !IsOwner? → controller.SetInputEnabled(false)
```

## 4. 关键类与文件

### PlayerInputSystem (MonoSingleton)

`PlayerInputSystem.cs:10` — 继承了 Unity Input System 自动生成的 `IPlayerActions` 接口。

**11 个 C# event**（供外部模块订阅）：

| Event | 类型 |
|-------|------|
| `OnMovementInput` | `Action<Vector2>` |
| `OnLookInput` | `Action<Vector2>` |
| `OnJumpInput` / `OnFireInput` / `OnReloadInput` | `Action` |
| `OnCastSkillInput` / `OnCastUltimateInput` | `Action` |
| `OnUseActiveItemInput` / `OnOpenMapInput` / `OnOpenInventoryInput` | `Action` |
| `OnSprintInput` | `Action` |

**状态控制**：

| 方法 | 用途 |
|------|------|
| `SetInputLock(bool)` | 全局锁定/解锁所有输入 |
| `SetActionEnable(name, bool)` | 单独启用/禁用某个动作 |
| `GetMovementInput()` / `GetLookInput()` | 直接轮询读取（锁定时返回零向量） |

### ThirdPersonPlayerController（第三人称控制器）

`ThirdPersonPlayerController.cs:11` — `[RequireComponent(typeof(CharacterController))]`。

**Inspector 配置组**：

| 组 | 字段 | 默认 |
|----|------|------|
| 相机 | `_mainCamera` / `_virtualCamera` / `CinemachineCameraTarget` | — |
| | `_cameraCollisionLayers` | Default + NavMeshGeometry |
| | `_cameraCollisionRadius` | 0.3 |
| | `CameraTopClamp`=70 / `BottomClamp`=-30 / `MouseSensitivity`=1.0 | 70/-30/1 |
| 移动 | `WalkSpeed`=1.0 / `SprintSpeed`=5.0 / `SpeedChangeRate`=10 | 1/5/10 |
| | `NormalRotationSmoothTime`=0.12 / `Gravity`=-15 / `TerminalFallSpeed`=-53 | 0.12/-15/-53 |
| 射击 | `MaxShootAngleDifference`=15 / `ForceRotateOnShoot`=true | 15/true |
| | `ShootRotationSmoothTime`=0.05 | 0.05 |
| 静止 | `RotateWhenIdle`=true / `IdleMaxAngleDifference`=5 | true/5 |
| | `IdleRotationSmoothTime`=0.1 | 0.1 |
| 地面 | `GroundCheckYOffset`=0.14 / `GroundCheckSphereRadius`=0.3 | 0.14/0.3 |
| 跳跃 | `JumpHeight`=1.2 / `JumpCooldown`=0.5 / `FallJudgeDelay`=0.15 | 1.2/0.5/0.15 |

**公开方法**：

| 方法 | 用途 |
|------|------|
| `InitAsLocalPlayer()` | 属主初始化（绑相机 + 武器瞄准 + 锁鼠标） |
| `SetInputEnabled(bool)` | 启用/禁用输入（非属主禁用） |
| `BindWeaponController(WeaponAimController)` | 绑定武器瞄准控制器 |
| `HandleShootRotation()` | 射击时朝向相机方向转向 |
| `Silence()` | 静默输入 + 解锁鼠标（测试用） |

本地玩家初始化时会调用 `ConfigureCameraCollision()`，将
`Cinemachine3rdPersonFollow.CameraCollisionFilter` 统一设置为
`Default + NavMeshGeometry`，并设置碰撞球半径。PCG 房间墙体主要位于
`NavMeshGeometry`（Layer 8）；如果只检测 `Default`，相机会绕过房间墙体并穿墙。

### WeaponAimController（武器瞄准）

`WeaponAimController.cs:3` — 挂载在武器 Prefab 上。

**公开方法**：

| 方法 | 用途 |
|------|------|
| `GetAimRay()` | 返回 `Ray(origin=firePoint, dir=targetPoint-firePoint)` |
| `BindCamera(Camera)` | 绑定相机引用 |
| `SetOwnerRoot(Transform)` | 绑定角色根节点（用于判断自瞄射线是否命中自己） |
| `SetAimRoot(Transform)` | 绑定武器瞄准根节点（用于自动旋转 `aimRoot -> firePoint` 向量） |
| `GetFirePointPosition()` | 获取枪口世界坐标 |

### IPlayerSelector + DefaultPlayerSelector

`IPlayerSelector.cs:7` — 角色选择接口，供本地多人分屏使用：

```csharp
public interface IPlayerSelector {
    List<PlayerSelectionInfo> GetSelectablePlayers();
    bool SelectPlayer(string playerId);
    PlayerSelectionInfo GetSelectedPlayer();
    event Action<PlayerSelectionInfo> OnPlayerSelected;
}
```

`DefaultPlayerSelector.cs` — 默认实现（P0 占位，单玩家模式）。

## 5. 对外接口

| 接口/类 | 关键 API |
|---------|---------|
| `PlayerInputSystem` | 11 个 C# event + `GetMovementInput()` / `GetLookInput()` + `SetInputLock()` |
| `PlayerSkillInputBinder` | 数字键 `1-0` → 技能槽 `0-9`，构建 `SkillCastContext` 并调用 `PlayerSkillModule.TryCastSkill()` |
| `ThirdPersonPlayerController` | `InitAsLocalPlayer()` / `SetInputEnabled(bool)` / `BindWeaponController()` |
| `WeaponAimController` | `GetAimRay()` / `BindCamera()` / `SetOwnerRoot()` / `SetAimRoot()` |
| `IPlayerSelector` | `GetSelectablePlayers()` / `SelectPlayer()` / `OnPlayerSelected` |

## 6. 依赖模块

| 依赖模块 | 用途 |
|----------|------|
| `MonoSingletonBase<T>` | PlayerInputSystem 单例 |
| `Unity Input System` | InputController 自动生成 + InputActionAsset |
| `Cinemachine` | 虚拟相机（CinemachineVirtualCamera） |
| `UnityEngine.CharacterController` | 角色移动 |
| `PlayerActor.CombatModule` | `TryFire(FireContext)` 开火 |
| `PlayerNetworkProxy` | `IsOwner` 判断 + `InitAsLocalPlayer()` |
| `PlayerActor.PlayerRender` | `BindWeaponController()` |

## 7. 被哪些模块依赖

| 依赖方 | 用途 |
|--------|------|
| `PlayerNetworkProxy.OnNetworkSpawn()` | 属主调用 `InitAsLocalPlayer()` |
| `PlayerActor` | 同 GameObject，`GetComponent<>()` 获取 |
| UI 模块 | `ThirdPersonPlayerController` 订阅 `OnOpenInventoryInput` 切换 `InventoryWindow`；其他 UI 可订阅 `OnOpenMapInput` / `OnOpenInventoryInput` |
| Combat 模块 | 订阅 `OnFireInput` / `OnReloadInput` |
| Skill 模块 | Phase 1 使用 `PlayerSkillInputBinder` 直接读取数字键 `1-0`；旧的 `OnCastSkillInput` / `OnCastUltimateInput` 仍保留给后续键位方案 |

## 8. 事件订阅与广播

PlayerControl **不通过 EventCenter 通信**。使用 C# event：

| 发布方 | 事件 | 订阅方 |
|--------|------|--------|
| `PlayerInputSystem` | `OnFireInput` | `ThirdPersonPlayerController` |
| `PlayerInputSystem` | `OnMovementInput` / `OnLookInput` / `OnJumpInput` / `OnSprintInput` | `ThirdPersonPlayerController`（内部调用） |
| `PlayerInputSystem` | `OnReloadInput` / `OnCastSkillInput` / ... | 外部模块（Combat/Skill/UI） |

## 9. Inspector 字段

主要在 `ThirdPersonPlayerController`（详见第 4 节），分为 7 组：相机/移动/射击/静止/地面/跳跃/组件。

`WeaponAimController` 额外包含武器瞄准字段：

| 字段 | 用途 |
|------|------|
| `firePoint` | 枪口点；未配置时尝试查找子节点 `FirePoint`，仍失败则回退到武器自身 |
| `ownerRoot` | 玩家角色根节点，用于过滤准心射线命中自己 |
| `aimRoot` | 武器瞄准根节点；未配置时回退到武器自身 `transform` |
| `autoRotateRootToCrosshair` | 是否自动旋转 `aimRoot -> firePoint` 模型向量到准心目标 |
| `aimRotationLerpSpeed` | 自动瞄准旋转插值速度；`0` 表示瞬间对齐 |
| `maxAimDistance` | 准心射线未命中时的远点距离 |

## 10. Prefab / Scene / ScriptableObject 依赖

| 类型 | 路径/名称 | 用途 |
|------|----------|------|
| .inputactions | `Assets/Scripts/PlayerControl/Input/InputController.inputactions` | InputActionAsset 源文件 |
| Prefab | `Resources/NetworkPlayer.prefab` | 挂载 `ThirdPersonPlayerController` + `CharacterController` + `Animator` |
| Prefab | `Resources/Prefab/Control/VirtualCamera.prefab` | CinemachineVirtualCamera 预制体 |
| Scene | `SampleScene.unity` | 主场景（需 UICamera + MainCamera） |
| Scene | `ControllerTestScene` | 控制器测试场景 |

## 11. 常见问题

**Q: 为什么 InputController.cs 禁止手动修改？**
A: `InputController.cs` 是 Unity Input System 自动生成的 `partial class`（来自 `InputController.inputactions`），重新生成后所有手动修改会被覆盖。所有逻辑应写在 `PlayerInputSystem` 中。

**Q: 非属主玩家的控制器如何工作？**
A: `PlayerNetworkProxy.OnNetworkSpawn()` 中根据 `IsOwner` 判断：属主调用 `InitAsLocalPlayer()` 启用输入；非属主调用 `SetInputEnabled(false)` 禁用。非属主的移动由 `ClientNetworkTransform` 同步。

**Q: Cinemachine 相机为何在 Start 而非 InitAsLocalPlayer 中绑定？**
A: `InitAsLocalPlayer` 中绑定（`_virtualCamera.Follow = CinemachineCameraTarget`）。`Start` 中的绑定代码被注释（当前仅做引用获取）。这避免了非属主客户端也绑定相机的错误。

**Q: 为什么第三人称相机会穿过 PCG 房间墙体？**
A: `Cinemachine3rdPersonFollow` 通过 `CameraCollisionFilter` 做 SphereCast。PCG 房间墙体主要使用 `NavMeshGeometry`（Layer 8），旧配置只检测 `Default`（Layer 0），因此相机不会命中墙体。控制器现在会在本地玩家初始化时统一覆盖为 `Default + NavMeshGeometry`。

## 12. 当前完成度

| 功能 | 状态 |
|------|------|
| PlayerInputSystem（11 事件） | 完成 |
| PlayerSkillInputBinder（数字键技能槽） | **Phase 1 完成** — 运行时由 `PlayerInitializer` 挂载，仅 Owner 启用，直接读取 Keyboard 数字键 |
| InputController（自动生成） | 完成 |
| ThirdPersonPlayerController（移动+相机+跳跃） | 完成 |
| 射击转向 + 静止转向 | 完成 |
| WeaponAimController（屏幕中心射线 + 武器根节点自动对准） | 完成 |
| InitAsLocalPlayer（属主初始化） | 完成 |
| 第三人称相机墙体碰撞（Default + NavMeshGeometry） | 完成 |
| SetInputEnabled（非属主禁用） | 完成 |
| IPlayerSelector 接口 | 完成 |
| DefaultPlayerSelector | **Bug** — 当前为 P0 占位（单玩家），应联动 `NetworkObjectManager` 获取本机 ID 做定向选择 |
| 多人分屏选择 | **未实现** |
| 手柄/移动端输入 | **不做** — 项目仅支持键鼠 |
| Animator 参数同步（Speed/State） | **未实现** — Animation 控制脚本待后续开发计划 |

## 13. 修改本模块时必须同步更新的内容

- **InputController.inputactions 变更** → 自动重新生成 `InputController.cs` → 同步更新 `PlayerInputSystem` 的 `IPlayerActions` 实现
- **新增 C# event** → 在 `PlayerInputSystem` 中声明 + `InputController.PlayerActions.AddCallbacks/RemoveCallbacks` 中注册
- **IPlayerSelector 接口变更** → 同步更新 `DefaultPlayerSelector`

## 14. 文档维护信息

| 项目 | 内容 |
|------|------|
| 创建日期 | 2026-05-14 |
| 覆盖文件数 | 7 个 .cs + 1 个 .inputactions |
| 关联模块文档 | Combat (FireContext), Skill (CastSkill), NetworkLayer (PlayerNetworkProxy), UI Framework (OpenMap/OpenInventory) |

## 15. 需要人工确认

- 在 Unity Editor 中确认 `AnimatedTestPlayer.prefab` 的虚拟相机 Body 仍为
  `Cinemachine3rdPersonFollow`；否则运行时会输出警告且无法应用本修复。
- 确认需要阻挡相机的 PCG 墙体带有 3D Collider，并位于 `Default` 或
  `NavMeshGeometry` Layer。没有 Collider 的纯渲染网格仍不会参与相机碰撞。
- 确认正式武器 Prefab 的 `WeaponAimController.firePoint` 与 `aimRoot`
  关系正确；`aimRoot -> firePoint` 应代表武器模型实际枪口方向，否则自动对准会出现模型偏转。

## 16. 2026-06-29 Skill Phase 1 输入接入

- `PlayerSkillInputBinder` 不依赖 `InputController.inputactions` 中的技能动作；它直接读取 `Keyboard.current.digit1Key` 到 `digit0Key`，并回退到旧 Input Manager 的 `KeyCode.Alpha1` 到 `Alpha0`。
- 数字键映射：`1 -> slot 0`，`2 -> slot 1`，...，`0 -> slot 9`。
- `BuildCastContext()` 使用 `Camera.main.ViewportPointToRay(0.5, 0.5)` 生成屏幕中心射线；若射线未命中，则使用 `origin + direction * SkillDefinitionSO.baseRange` 作为兜底点。
- 需要人工确认：场景中存在带 `MainCamera` Tag 的激活相机；如果改用自定义相机标签，需要同步修改 `PlayerSkillInputBinder`。

## 17. 2026-06-29 武器动画与 Reload 输入接入

- `ThirdPersonPlayerController` 现在订阅 `PlayerInputSystem.OnReloadInput`，仅 Owner 可调用 `PlayerActor.CombatModule.Reload()`。
- `Update()` 中会在输入启用判断前调用 `PlayerCombatModule.TickReloadState(Time.time)`，使换弹完成事件不再依赖下一次 `CanFire()` 检查，也不因临时禁用输入而卡住。
- 开火输入仍走 `OnFireInput -> CombatModule.TryFire(CreateFireContext())`，人物 Animator 不直接播放近战 `Attack`。
- 武器自身动画由 RenderLayer 的 `WeaponAnimationController` 监听 `LocalWeaponFired` / `RemoteWeaponFired` / `ReloadStarted` / `ReloadFinished` 驱动。
- 需要人工确认：武器 Prefab 已挂载 `WeaponAnimationController`，并且 Animator Controller 包含 `IsFiring` 与 `Reload` 参数。

## 18. 2026-06-30 FireMode Auto / Charge 输入链路

### 事件扩展

- `PlayerInputSystem` 新增两个 C# event：`OnFireStarted`（`started` 阶段）和 `OnFireCanceled`（`canceled` 阶段）。
- `OnFire(context)` 现在区分 `started` / `performed` / `canceled` 三种阶段分别触发事件。
- 新增 `IsFireHeld` 属性：读取 `_inputController.Player.Fire.IsPressed()`，供 Auto 持续开火判断。

### 开火模式分发

`ThirdPersonPlayerController` 根据 `WeaponSO.fireMode` 做模式分派：

| FireMode | 输入行为 | 驱动位置 |
|----------|---------|---------|
| `Semi` | `performed` 时打一发（原有行为） | `OnFireInput()` |
| `Auto` | 按住期间每帧 `TryFire()`，射速由 `NextFireTime` 冷却控制 | `Update()` |
| `Charge` | 按下 `StartCharge()`，按住期间 `UpdateCharge()`，松手 `ReleaseCharge(ctx)` | `OnFireStarted()` / `Update()` / `OnFireCanceled()` |

### 边界保护

- Auto 的 `TryFire()` 由 `CanFire()` 内部 `NextFireTime` 冷却自然节流，不均匀 while 循环。
- 弹药耗尽 → `CanFire()` 返回 false → 自动停火。
- 换弹锁 → `CanFire()` 已含 `TickReloadState` + `_isReloading` 检查。
- 非 Owner → `_inputEnabled` / `_networkProxy.IsOwner` 双重拦截。
- 武器切换 → 每帧实时读取 `CurrentConfig.WeaponSO.fireMode`。
- 蓄力最大自动释放 → `UpdateCharge()` 已有 `chargeLevel >= 0.99f` 触发 `ReleaseCharge`。

## 19. 2026-06-30 跳跃与第三人称相机微调

- `ThirdPersonPlayerController` 不再在运行时反复调用 `SetActionEnable("Jump", ...)` 禁用/启用 Jump action；跳跃限制只由 `IsGrounded` 与 `JumpCooldown` 控制，避免 Input System 按键状态被中途打断。
- `GroundLayers` 默认使用 `Default + NavMeshGeometry`，并在运行时检测到 LayerMask 为空时自动使用同一兜底；`CheckGroundedState()` 同时参考 `Physics.CheckSphere` 与 `CharacterController.isGrounded`。
- Runtime Camera Target 现在支持 `_cameraTargetLocalOffset`，默认向右肩轻微偏移，让屏幕中心准心避开玩家背部中心线。
- 需要人工确认：玩家 Prefab / Hero Prefab 的地面碰撞层仍建议明确配置到 `GroundLayers`；虚拟相机 Body 仍应为 `Cinemachine3rdPersonFollow`，并在运行时观察右肩偏移是否需要按具体模型调整。
