# PlayerDeathDefeat 功能解读

## 变更目的

玩家血量归零后直接失败，不再等待倒地、救助或二次死亡。

## 当前链路

1. `ServerPlayerAttributeModule.TakeDamage()` 扣血后调用死亡检查。
2. 玩家 Health <= 0 时，`ServerPlayerAttributeModule.HandleDeath()` 直接设置 `PlayerLifeState.Dead` 并触发 `UnitDied`。
3. `RunManager` 在 `Exploring` / `BossFight` 注册 `UnitDied` 监听。
4. `RunManager.OnUnitDied()` 调用 `RunContext.GetAlivePlayerCount()` 复查仍存活玩家。
5. 存活玩家数为 0 时，状态切换到 `RunDefeat`，随后进入 `RunSummary`。
6. `RunManager` 通过 `ClientRpc` 将 `RunSummaryReady` 分发到客户端本地 `EventCenter`。
7. `RunResultUIBridge` 在客户端收到 `RunSummaryReady` 后转换为 `GameFailedWindow` 弹窗。

## 存活统计规则

`RunContext.GetAlivePlayerCount()` 只统计满足以下条件的玩家：

- `PlayerActor.IsActiveForAI == true`
- `PlayerActor.IsAliveForAI == true`
- 如果存在 `ServerPlayerAttributeModule`，则 Health > 0

这避免了仅注册在 `AttackableObjectManager` 中、但已死亡的玩家继续被算作存活。

## 风险

`UnitDied` 是通用死亡事件，也被任务系统、刷怪系统和 AI 移动监听。当前修复保持事件语义为“真实死亡”，没有把倒地伪装成死亡事件。

`RunSummaryReady` 依赖客户端本地 `EventCenter` 唤醒 UI；服务端触发后必须通过 `RunManager` 的 `ClientRpc` 转发给远端客户端，否则非 Host 客户端不会弹出失败窗口。
