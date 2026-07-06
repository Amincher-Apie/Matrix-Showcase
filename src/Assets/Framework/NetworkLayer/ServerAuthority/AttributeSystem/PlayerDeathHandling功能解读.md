# PlayerDeathHandling 功能解读

## 变更目的

玩家当前不使用倒地、救助或自救流程。玩家 Health 归零后应立即视为死亡，并触发对局失败链路。

## 当前逻辑

- `ServerPlayerAttributeModule.HandleDeath()` 在服务端执行。
- 当玩家 Health <= 0 时，首次进入死亡处理就会把 `_lifeState` 设置为 `PlayerLifeState.Dead`。
- 随后向拥有者客户端播放死亡动画，并触发 `EventName.UnitDied`。
- 玩家对象不会调用 `base.HandleDeath()`，因此不会被对象池回收。

## 对外契约

- `PlayerLifeState.Downed` 当前不再作为正式流程使用。
- `SelfReviveServerRpc()` 与 `RescueByTeammate()` 保留兼容入口，但常规死亡链路不会进入 `Downed`，因此不会触发自救或队友救助。
- `RunManager` 通过 `UnitDied` 复查全员存活数，全部玩家死亡后进入 `RunDefeat` 和 `RunSummary`。

## 需要人工确认

- 玩家 Prefab 的 Animator 是否存在 `IsDead` bool 与 `Die` trigger。
- `GameFailedWindow` 是否已在 `WindowConfig` 中配置并能通过 `Resources` 路径加载。
