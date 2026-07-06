# 动画资源接入开发手册

> 项目：Matrix  
> 适用范围：敌人、玩家、Boss 的模型 Avatar、AnimatorController、AnimationClip 接入  
> 参考样板：`Assets/Resources/Prefab/Enemy/001.prefab`  
> 编写日期：2026-06-18  

## 1. 目标

本手册用于指导 AI Agent 或开发者在当前框架下接入动画资源，并保证联网行为正确。

接入完成后的目标是：

- 角色 prefab 保留现有网络、属性、战斗、AI、对象池结构。
- 模型资源通过 `Animator` 和 `Avatar` 挂到表现层，不改变服务端权威逻辑。
- 移动、攻击、受击、死亡等动画在 Host、Server、Client 多端表现一致。
- 动画事件只作为表现或向服务端发起请求，不直接做伤害、掉落、死亡、属性修改等权威结算。

## 2. 当前框架约束

接入动画前必须先理解以下分层：

| 层级 | 职责 | 动画接入原则 |
|------|------|--------------|
| `Framework/LogicLayer` | Actor、AI、Combat、Skill、Damage 逻辑 | 不直接依赖具体模型资源 |
| `Framework/NetworkLayer` | `NetworkObject`、Proxy、ServerAuthority、对象池 | 负责把服务端权威状态同步给客户端 |
| `Framework/RenderLayer` | 表现层、跳字、渲染对象 | 适合放动画表现桥接 |
| `Assets/Scripts` | Run/PCG/Spawn/UI/PlayerControl | 只负责游戏流程和输入，不承载敌人动画权威 |

联网原则：

1. 敌人由服务端权威驱动：AI、NavMeshAgent、攻击、死亡、掉落都只应由服务端决定。
2. 玩家移动是属主客户端驱动，当前项目已有 `ClientNetworkTransform` / `ClientNetworkAnimator` 作为客户端权威组件。
3. `NetworkVariable` / `NetworkList` 只能由服务端写入，客户端只读。
4. `ServerRpc` 必须校验调用者权限，玩家侧应检查 `OwnerClientId`。
5. 动画 Trigger 不能作为真实伤害结算入口；攻击命中仍走 Combat / DamageCenter / ServerAttributeModule。

## 3. `001.prefab` 当前结构

参考 prefab 路径：

```text
Assets/Resources/Prefab/Enemy/001.prefab
```

当前根对象 `001` 的核心组件如下：

| 组件 | 当前用途 | 动画接入时处理 |
|------|----------|----------------|
| `NetworkObject` | NGO 网络对象根 | 必须保留 |
| `ServerEnemyAttributeModule` | 服务端属性、受击、死亡、掉落、对象池回收 | 必须保留 |
| `EnemyNetworkProxy` | 注册网络对象，服务端启动 AI Tick | 必须保留 |
| `ServerBuffModule` | 服务端 Buff 同步 | 必须保留 |
| `ServerCombatModule` | 服务端开火 / 命中验证 | 必须保留 |
| `ServerWeaponRuntime` | 服务端武器属性运行时 | 必须保留 |
| `EnemyActor` | 敌人逻辑对象，组装 Attribute / Combat / AI | 必须保留 |
| `ServerEnemyMovementDriver` | 兼容层 | 保留，后续可清理 |
| `NetworkTransform` | 同步 Transform | 必须保留 |
| `NavMeshAgent` | 服务端敌人寻路 | 必须保留，客户端会禁用 |
| `EnemyNavAgentController` | 服务端 NavMeshAgent 包装，已有 `Speed` 参数写入入口 | 必须保留并扩展 |

当前子对象 `Capsule` 是临时物理/显示承载体：

| 子对象 | 当前组件 |
|--------|----------|
| `Capsule` | `MeshFilter`、`MeshRenderer`、`CapsuleCollider`、`Rigidbody` |

当前 `001.prefab` 尚未挂接实际模型 `Animator`，因此接入动画资源时应把 `Capsule` 替换或扩展为正式模型子树，而不是破坏根对象上的网络组件。

需要人工确认：

- `ServerEnemyAttributeModule._prefabPath` 当前为 `Prefabs/Enemy/TestEnemy`，而资源实际路径是 `Assets/Resources/Prefab/Enemy/001.prefab`。对象池路径通常应使用 `Resources.Load` 风格路径，例如 `Prefab/Enemy/001`，不带 `Assets/Resources/` 和 `.prefab`。
- `EnemyActor._selfPrefabPath` 当前写作 `Prefab/Enemy/001.prefab`，是否与对象池路径统一需要人工确认。
- `ServerEnemyAttributeModule._deathAnimDuration` 已存在，但当前基类回收协程固定等待 1 秒，该字段未真正参与回收延迟。死亡动画长度大于 1 秒时需要同步修正代码。

## 4. 推荐的敌人 prefab 结构

敌人 prefab 推荐保持如下结构：

```text
EnemyRoot (NetworkObject)
├── Network / Logic / ServerAuthority 组件
├── NavMeshAgent
├── EnemyNavAgentController
├── Animator                  # 可放根节点，也可放 ModelRoot；推荐根节点，方便 NetworkAnimator 同对象工作
├── NetworkAnimator            # 敌人使用服务端权威 NetworkAnimator
└── ModelRoot
    ├── SkinnedMeshRenderer
    ├── 骨骼层级
    └── hitbox / socket / weapon point 等表现挂点
```

如果美术模型导入后自带 `Animator` 在模型子物体上，有两种做法：

| 做法 | 适用情况 | 说明 |
|------|----------|------|
| 把 `Animator` 提到根对象 | 敌人普通单位 | 推荐。`EnemyNavAgentController.GetComponent<Animator>()` 可直接拿到 |
| 保持 `Animator` 在子对象 | 复杂 Boss / 多模型组合 | 需要新增桥接脚本，通过 `[SerializeField] Animator animator` 显式绑定，不能依赖根对象 `GetComponent<Animator>()` |

不建议把 `NetworkObject` 放在模型子对象上。当前框架默认根对象就是网络对象、逻辑对象、对象池回收对象。

## 5. AnimatorController 参数标准

所有接入项目框架的角色 AnimatorController 应遵守统一参数名。这样 AI Agent 可以按约定写代码，而不需要逐个猜测 Controller。

### 5.1 通用参数

| 参数名 | 类型 | 来源 | 用途 |
|--------|------|------|------|
| `Speed` | `float` | 移动系统 | Idle / Walk / Run BlendTree |
| `IsDead` | `bool` | 服务端死亡状态 | 锁定死亡状态，防止回到 Idle |
| `Hit` | `trigger` | 服务端受击事件 | 播放受击表现 |
| `Die` | `trigger` | 服务端死亡事件 | 播放死亡动画 |
| `Attack` | `trigger` | 服务端攻击确认 | 播放普通攻击 |
| `Skill` | `trigger` | 技能确认或预测 | 播放技能动画 |

### 5.2 可选参数

| 参数名 | 类型 | 用途 |
|--------|------|------|
| `MoveX` / `MoveY` | `float` | 玩家八向移动或 strafe BlendTree |
| `AttackIndex` | `int` | 随机或连段攻击选择 |
| `SkillIndex` | `int` | 多技能动画选择 |
| `AimPitch` | `float` | 上下瞄准修正 |
| `IsGrounded` | `bool` | 玩家跳跃 / 落地 |
| `VerticalSpeed` | `float` | 玩家跳跃 / 下落 |

最低接入要求：

- 敌人至少提供 `Speed`、`Attack`、`Hit`、`Die`、`IsDead`。
- 玩家至少提供 `Speed`、`MoveX`、`MoveY`、`Attack`、`Skill`、`IsGrounded`。

## 6. Avatar 与动画资源导入规则

1. 模型导入后，在 Rig 页签确认 `Animation Type`。
   - 人形角色使用 `Humanoid`。
   - 非人形怪物、机器人、多足单位使用 `Generic`。
2. `Avatar Definition` 优先选择 `Create From This Model`。
3. 如果多个同骨架模型复用同一套动画，使用同一个兼容 Avatar。
4. AnimationClip 命名建议：

```text
Enemy_001_Idle
Enemy_001_Walk
Enemy_001_Run
Enemy_001_Attack_01
Enemy_001_Hit
Enemy_001_Die
```

5. 所有 Clip 应检查 Loop：
   - Idle / Walk / Run：Loop 开启。
   - Attack / Hit / Die：Loop 关闭。
6. Root Motion 默认关闭。敌人移动由服务端 `NavMeshAgent` 决定，玩家移动由 `CharacterController` / `ClientNetworkTransform` 决定。只有明确设计了根运动，并完成服务端位移校验时，才允许启用 Root Motion。

## 7. 敌人动画联网方案

### 7.1 推荐方案：服务端权威 `NetworkAnimator`

敌人是服务端权威对象，推荐挂 Unity Netcode 的标准 `NetworkAnimator`，不要使用 `ClientNetworkAnimator`。

原因：

- 敌人 AI 只在服务端 Tick。
- 敌人 NavMeshAgent 只在服务端启用，客户端禁用。
- 攻击、死亡、掉落都由服务端决定。
- 服务端写 Animator 参数，再由 `NetworkAnimator` 同步给客户端，符合当前权威模型。

接入步骤：

1. 在敌人根对象上挂 `Animator`。
2. 给 `Animator` 指定 Avatar 和 AnimatorController。
3. 在根对象上挂 `NetworkAnimator`。
4. 保留 `NetworkTransform`，用于同步根 Transform。
5. 保留 `EnemyNavAgentController.syncVelocityToAnimator = true`。
6. 确认 `EnemyNavAgentController.animatorSpeedParameter = Speed`。
7. 由服务端写入 `Speed`，由 `NetworkAnimator` 同步给客户端。

注意：当前 `EnemyNavAgentController` 的客户端 `LateUpdate()` 会尝试从本地 `NavMeshAgent.velocity` 写 `Speed`，但客户端 `agent.enabled = false`，因此该路径只能得到 0。接入 `NetworkAnimator` 后，应以服务端写入为准，避免客户端再覆盖参数。

### 7.2 可选方案：自定义动画网络状态

如果不希望使用 `NetworkAnimator`，可以新增一个轻量 `NetworkBehaviour`，例如 `EnemyAnimationNetState`：

```csharp
public sealed class EnemyAnimationNetState : NetworkBehaviour
{
    private readonly NetworkVariable<float> _speed = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> _isDead = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [SerializeField] private Animator animator;

    public void ServerSetSpeed(float value)
    {
        if (!IsServer) return;
        _speed.Value = value;
    }

    public void ServerSetDead()
    {
        if (!IsServer) return;
        _isDead.Value = true;
        PlayDieClientRpc();
    }

    [ClientRpc]
    private void PlayDieClientRpc()
    {
        animator.SetBool("IsDead", true);
        animator.SetTrigger("Die");
    }

    private void Update()
    {
        if (animator == null) return;
        animator.SetFloat("Speed", _speed.Value);
        animator.SetBool("IsDead", _isDead.Value);
    }
}
```

该方案更可控，但需要自己维护 Trigger、状态重置和对象池复用。

## 8. 玩家动画联网方案

玩家和敌人不同：玩家移动由属主客户端驱动。

推荐规则：

| 动画类型 | 权威方 | 推荐同步方式 |
|----------|--------|--------------|
| 移动 BlendTree | Owner 客户端 | `ClientNetworkAnimator` 或本地计算 + 网络同步 |
| 普通攻击 | Owner 发起，服务端验证 | 本地预测播放 + 服务端确认远端播放 |
| 技能前摇 | Owner 预测，服务端验证 | 本地预测播放，服务端确认后广播 |
| 受击 / 死亡 | 服务端 | 服务端同步 |

`ClientNetworkAnimator` 只适合玩家属主写入的表现参数，不允许承载安全敏感逻辑。

玩家当前状态：

- `ThirdPersonPlayerController` 已 `[RequireComponent(typeof(Animator))]` 并缓存 `_animator`。
- PlayerControl 文档标注 Animator 参数同步尚未实现。
- Skill 模块中 `TriggerAnimation()` 目前只是 Debug 预留。

因此玩家动画接入应作为后续单独任务，不要在敌人接入中混改玩家控制器。

## 9. 动画事件规则

Animation Event 可以使用，但必须遵守边界：

允许：

- 播放音效。
- 开关武器 Trail。
- 播放粒子。
- 打开/关闭非伤害 hitbox 的可视化提示。
- 向服务端发起已校验的请求，例如“玩家请求释放技能”。

禁止：

- 客户端 Animation Event 直接扣血。
- 客户端 Animation Event 直接生成掉落。
- 客户端 Animation Event 直接修改 `NetworkVariable`。
- 客户端 Animation Event 直接决定敌人死亡。

敌人攻击动画如果需要“出手帧”，推荐两种方式：

| 方式 | 说明 |
|------|------|
| 服务端计时 | `AttackState` 根据攻击前摇时间等待，然后调用 Combat。最稳，推荐 |
| 服务端动画事件 | 只在服务端 Animator 上接收 Animation Event，再调用 Combat。需要确保 Dedicated Server 也运行 Animator |

当前项目尚未验证 Dedicated Server 下 Animator Event 的可靠性，因此默认推荐服务端计时，不推荐依赖动画事件做权威结算。

## 10. 敌人接入标准流程

### 10.1 准备资源

1. 确认模型 FBX 已导入。
2. 配好 Avatar。
3. 配好 AnimatorController。
4. Controller 至少包含：
   - Idle
   - Walk / Run BlendTree
   - Attack
   - Hit
   - Die
5. 参数命名符合第 5 节。

### 10.2 复制样板 prefab

以 `001.prefab` 为模板复制新的敌人 prefab，不要从模型 prefab 直接开始做网络敌人。

保留根对象上的组件：

```text
NetworkObject
ServerEnemyAttributeModule
EnemyNetworkProxy
ServerBuffModule
ServerCombatModule
ServerWeaponRuntime
EnemyActor
NetworkTransform
NavMeshAgent
EnemyNavAgentController
```

然后替换表现子树：

1. 删除或禁用临时 `Capsule` 的 MeshRenderer。
2. 加入正式 `ModelRoot`。
3. 保留或重新配置碰撞体与 `physicsCarrier`。
4. 将 `EnemyActor.physicsCarrier` 指向实际物理承载体。

### 10.3 挂 Animator

推荐挂在根对象：

1. `EnemyRoot` 添加 `Animator`。
2. `Animator.avatar = 模型 Avatar`。
3. `Animator.runtimeAnimatorController = 对应 Controller`。
4. `Apply Root Motion = false`。
5. `Culling Mode` 需要人工确认。普通敌人建议 `Cull Update Transforms`，Boss 或强依赖事件的 Animator 建议 `Always Animate`。

如果 Animator 必须留在子对象：

1. 新增动画桥接脚本。
2. 桥接脚本用 `[SerializeField] private Animator animator;` 显式绑定。
3. 不要让 `EnemyNavAgentController` 继续通过 `GetComponent<Animator>()` 猜测。

### 10.4 挂网络动画组件

敌人使用：

```text
Unity.Netcode.Components.NetworkAnimator
```

不要使用：

```text
ClientNetworkAnimator
```

`ClientNetworkAnimator` 只适合玩家属主客户端写 Animator 参数。

### 10.5 配置移动动画

检查 `EnemyNavAgentController`：

```text
syncVelocityToAnimator = true
animatorSpeedParameter = Speed
```

服务端移动链路应为：

```text
EnemyNetworkProxy.ServerAITick()
  -> EnemyAIModule.ServerTick()
  -> EnemyNavAgentController.SetDestinationThrottled()
  -> NavMeshAgent.velocity
  -> Animator.SetFloat("Speed", value)
  -> NetworkAnimator 同步
```

客户端不应本地决定敌人速度动画。

### 10.6 配置攻击动画

敌人攻击权威链路应为：

```text
AI AttackState
  -> 服务端决定可以攻击
  -> Animator.SetTrigger("Attack")
  -> 到达攻击时机后调用 EnemyCombatModule.TryFire()
  -> ServerCombatModule.RequestFire()
  -> DamageCenter / ServerAttributeModule 结算
```

如果需要随机攻击动作：

```text
Animator.SetInteger("AttackIndex", index)
Animator.SetTrigger("Attack")
```

`AttackIndex` 必须由服务端决定并同步。

### 10.7 配置受击动画

受击来源是服务端 `ServerAttributeModule.TakeDamage()`。

推荐接入点：

- 在服务端伤害处理后触发 `Animator.SetTrigger("Hit")`。
- 或监听服务端确认后的 `UnitDamagedEvt`，只对 `targetId == NetworkObjectId` 的对象播放。

注意：`UnitDamagedEvt` 是 EventCenter 本地事件，不等于跨网络事件。客户端表现必须依赖 `ClientRpc`、`NetworkAnimator` 或 `NetworkVariable`。

### 10.8 配置死亡动画

当前死亡链路：

```text
ServerEnemyAttributeModule.HandleDeath()
  -> EnemyNavAgentController.RetireForDeath()
  -> TrySpawnDropsOnServer()
  -> base.HandleDeath()
  -> EventCenter.Trigger(UnitDied)
  -> 1 秒后对象池回收
```

推荐调整为：

```text
ServerEnemyAttributeModule.HandleDeath()
  -> if _deathHandled return
  -> _deathHandled = true
  -> Animator.SetBool("IsDead", true)
  -> Animator.SetTrigger("Die")
  -> EnemyNavAgentController.RetireForDeath()
  -> TrySpawnDropsOnServer()
  -> EventCenter.Trigger(UnitDied)
  -> 等待 deathAnimDuration
  -> NetworkObjectPoolManager.DespawnAndRecycle()
```

需要人工确认：

- `_deathAnimDuration` 应与死亡 Clip 时长一致。
- 如果死亡动画有倒地后残留展示需求，不应立即对象池回收。
- 如果对象池复用，`OnNetworkSpawn` / `OnEnable` 必须重置 `IsDead=false`、清 Trigger、恢复 Collider / Rigidbody。

## 11. 对象池复用注意事项

敌人会从对象池反复生成和回收，动画状态必须可重置。

每次 Spawn 时需要确保：

- `Animator.Rebind()` 或显式重置关键参数。
- `IsDead = false`。
- `Speed = 0`。
- 清理攻击/受击/死亡 Trigger 残留。
- `EnemyNavAgentController` 已恢复碰撞和 NavMeshAgent 状态。
- `ServerEnemyAttributeModule._deathHandled = false`。

当前代码中：

- `EnemyNavAgentController.OnNetworkSpawn()` 已调用 `ResetRuntimeState()` 和 `RestoreCollisionState()`。
- `ServerEnemyAttributeModule.OnNetworkSpawn()` 已重置 `_deathHandled = false`。
- Animator 参数重置尚未统一实现，需要在动画桥接中补齐。

## 12. 建议新增的代码职责

如果要正式接入，不建议把动画逻辑散落进 AI、Combat、Attribute 多个模块。建议新增一个小型桥接组件。

### 12.1 敌人动画桥接

建议路径：

```text
Assets/Framework/RenderLayer/Animation/EnemyAnimationBridge.cs
```

职责：

- 持有 `Animator` 引用。
- 提供 `ServerSetSpeed(float)`、`ServerPlayAttack(int)`、`ServerPlayHit()`、`ServerPlayDie()`。
- 内部使用 `NetworkAnimator` 或自定义 `NetworkVariable` / `ClientRpc`。
- 在对象池复用时重置 Animator 参数。
- 不直接计算伤害、不直接移动角色、不直接修改属性。

### 12.2 玩家动画桥接

建议路径：

```text
Assets/Framework/RenderLayer/Animation/PlayerAnimationBridge.cs
```

职责：

- 从 `ThirdPersonPlayerController` 或输入状态读取本地移动参数。
- 属主客户端写移动表现参数。
- 攻击/技能先本地预测播放，再等待服务端确认处理远端表现。
- 不承担 Combat / Skill 权威结算。

## 13. 需要同步的文档

如果新增动画桥接脚本，需要同步更新：

| 变更 | 需要同步 |
|------|----------|
| 新增 `RenderLayer/Animation` | 需要为 RenderLayer 补 `MODULE.md`，或在现有文档体系中新增动画接入文档引用 |
| 修改 `EnemyNavAgentController` 动画同步策略 | 更新 AI 模块 `MODULE.md` 的移动/动画同步说明 |
| 修改 `ServerEnemyAttributeModule` 死亡回收延迟 | 更新 ServerAttributeModule 功能解读 |
| 修改 PlayerControl 动画参数 | 更新 PlayerControl `MODULE.md` |

如果只做资源绑定，不改 `.cs`，不需要更新 MODULE.md，但必须在提交说明中标注“需要人工确认 Inspector 绑定”。

## 14. AI Agent 实施顺序

后续 AI Agent 若要按本手册实际落地，请按以下顺序执行：

1. 阅读根目录 `AGENTS.md`、`PROJECT_OVERVIEW.md`、`ARCHITECTURE.md`。
2. 阅读相关模块文档：
   - `Assets/Framework/NetworkLayer/MODULE.md`
   - `Assets/Framework/LogicLayer/Module/AIModule/MODULE.md`
   - `Assets/Framework/LogicLayer/Module/CombatModule/MODULE.md`
   - `Assets/Framework/LogicLayer/Module/AttributeModule/MODULE.md`
   - `Assets/Framework/NetworkLayer/ServerAuthority/AttributeSystem/ServerAttributeModule功能解读.md`
   - 若涉及玩家，再读 `Assets/Scripts/PlayerControl/MODULE.md`
3. 以 `Assets/Resources/Prefab/Enemy/001.prefab` 为结构参考，不直接文本编辑 prefab。
4. 先在 Unity Editor 中复制 prefab 并接入模型、Avatar、AnimatorController。
5. 若需要代码支持，先确认目标模块是否有 MODULE.md；没有则先询问是否补文档。
6. 新增桥接代码时保持职责单一：动画桥接只做表现同步。
7. 多端测试通过后，再批量接入更多敌人。

## 15. 联网测试清单

每个接入动画的敌人至少完成以下测试：

| 测试 | 预期 |
|------|------|
| Host 单人运行 | 敌人 Idle / Walk / Attack / Die 正常 |
| Host + Client | 两端看到同一敌人的移动动画一致 |
| Client 旁观敌人移动 | 客户端不能因为本地 NavMeshAgent 禁用而一直 Idle |
| 攻击动画 | 攻击表现与服务端 Combat 结算顺序一致 |
| 受击动画 | 只有服务端确认受击后播放 |
| 死亡动画 | 两端播放死亡，播放完成前不被过早回收 |
| 对象池复用 | 复活后的同一池对象不会保持死亡姿态 |
| 延迟环境 | 客户端本地表现可以延迟，但不能提前产生伤害 |
| Dedicated Server | 不依赖客户端 Animator Event 做权威结算 |

## 16. 常见问题

### Q1：为什么敌人不能用 `ClientNetworkAnimator`？

因为敌人不是客户端属主驱动。敌人 AI、攻击、死亡都是服务端决定，客户端只能接收同步后的表现。`ClientNetworkAnimator` 会让属主客户端写 Animator，不适合敌人。

### Q2：为什么客户端上的敌人不能自己从 `NavMeshAgent.velocity` 算 `Speed`？

当前 `EnemyNavAgentController.OnNetworkSpawn()` 在客户端会禁用 `NavMeshAgent`。客户端本地 `agent.velocity` 不可靠，通常会是 0。敌人移动动画应由服务端同步参数，或由客户端根据 `NetworkTransform` 位移差计算纯表现速度。

### Q3：动画 Clip 已经都在 AnimatorController 里，为什么还要统一参数？

代码只能通过参数名驱动 Animator。统一 `Speed`、`Attack`、`Hit`、`Die` 等参数后，AI Agent 可以批量接入不同模型，而不需要针对每个 Controller 写特殊逻辑。

### Q4：死亡动画和对象池回收冲突怎么办？

以服务端死亡流程为准。先播放死亡表现，再按死亡 Clip 时长延迟回收。当前代码固定 1 秒回收，需要在正式接入死亡动画时改为使用 `_deathAnimDuration` 或动画配置数据。

### Q5：Root Motion 能不能用？

默认不能。当前敌人位移由服务端 `NavMeshAgent` 和 `NetworkTransform` 同步，玩家位移由 `CharacterController` / 客户端权威 Transform 同步。Root Motion 会引入第二套位移来源，容易导致服务器和客户端位置不一致。

## 17. 最小落地方案

如果只想最快把 `001.prefab` 接入一个敌人动画资源，推荐最小方案如下：

1. 在 Unity Editor 中复制 `001.prefab`。
2. 在根对象挂 `Animator`，指定 Avatar 和 AnimatorController。
3. 在根对象挂服务端权威 `NetworkAnimator`。
4. Controller 中建立 `Speed` float、`Attack` trigger、`Hit` trigger、`Die` trigger、`IsDead` bool。
5. 保持 `EnemyNavAgentController.syncVelocityToAnimator = true`，参数名为 `Speed`。
6. 暂时只验证 Idle / Walk / Run。
7. 再补攻击、受击、死亡 Trigger。
8. 修正死亡回收延迟，使其匹配死亡 Clip。
9. 做 Host + Client 测试，确认客户端不是永远 Idle。

该方案能最小成本接入现有动画资源，同时不破坏当前服务端权威框架。

## 18. 集成测试基线：FullFlowTestScene

动画接入的集成测试以 `Assets/Scenes/GameFlowTest/FullFlowTestScene.unity` 为**唯一测试基线**。

### 18.1 完整链路

```
FullFlowTestBootstrapper → 大厅 UI → 点击开始
  → NetworkManager.StartHost()
  → RunManager.TransitionTo(Lobby → RunInit → RoomEnter)
  → PcgMapGenerator.Generate() 生成地图
  → PlayerSpawnManager.SpawnAllConnectedPlayers() 生成玩家
  → MissionManager.TryBootstrapRuntimeMissions() 初始化任务
    → BossMission.OnActivated() / EliminateMission.OnActivated()
      → EnemySpawnService.SpawnEnemy(enemyId, pos, rot)
        → "Prefab/Enemy/{enemyId}"  ← 敌人资源路径
        → NetworkObjectPoolManager.GetAndSpawn()
        → EnemyActor.ConfigureForSpawn() + ActivateAfterSpawn()
  → 战斗 → ServerAttributeModule.TakeDamage() → HP=0
    → HandleDeath() → SetBool("IsDead") + SetTrigger("Die")
    → UnitDied 事件 → 多方监听 (RunManager / MissionManager / MonsterRegistry)
    → 回收计时 → NetworkObjectPoolManager.DespawnAndRecycle()
  → 任务完成 → RunSummaryCalculator → 结算
```

### 18.2 接入测试时的关键配置

| 配置点 | 位置 | 说明 |
|--------|------|------|
| 对象池注册 | `NetworkObjectPoolBootstrap` 的 `_prefabPaths` | 需包含 `"Prefab/Enemy/{测试ID}"` |
| Spawn enemyId | `MonsterSpawnManager` / Mission Config SO | 需指向测试用的 enemyId |
| 预制体 | `Assets/Resources/Prefab/Enemy/{ID}.prefab` | 包含正确挂载的 Animator + NetworkAnimator |

### 18.3 动画接入验证清单（集成版）

在 FullFlowTestScene 中以 Host + Client 运行，逐项确认：

- [ ] PGC 地图生成后，Ambient 刷怪敌人正常激活，带完整动画组件
- [ ] 敌人从生成起即播放 Idle / Walk / Run（由 NavMeshAgent 寻路驱动 Speed）
- [ ] 敌人攻击时播放 Attack 动画（`AttackState.OnEnter` → `SetTrigger("Attack")`）
- [ ] 敌人受击时播放 Hit 动画（`TakeDamage` → `OnHitAnimation`）
- [ ] 敌人死亡时播放 Die 并冻结在最后一帧（`HandleDeath` → `IsDead=true` + `Die` trigger）
- [ ] 死亡后延迟回收，播放期间不提前消失
- [ ] 对象池复用后 Animation 参数被重置（无残留 IsDead / Speed / Trigger）
- [ ] Client 端 Trigger 动画同步正确（Attack / Hit / Die）
- [ ] Client 端移动动画（Speed）由 NetworkAnimator 或 Transform 插值正确驱动

### 18.4 核心脚本引用

| 脚本 | 路径 |
|------|------|
| FullFlowTestBootstrapper | `Assets/Scripts/Test/GameProcess/FullFlowTestBootstrapper.cs` |
| EnemySpawnService | `Assets/Scripts/Managers/EnemySpawnService.cs` |
| MonsterSpawnManager | `Assets/Framework/LogicLayer/Module/SpawnSystem/MonsterSpawnManager.cs` |
| NetworkObjectPoolBootstrap | `Assets/Framework/NetworkLayer/NetworkObjectPool/NetworkObjectPoolBootstrap.cs` |
| RunManager | `Assets/Scripts/RunSystem/RunManager.cs` |
| MissionManager | `Assets/Scripts/MissionSystem/MissionManager.cs` |
| EnemyActor | `Assets/Framework/LogicLayer/EnemyLogic/EnemyActor.cs` |
