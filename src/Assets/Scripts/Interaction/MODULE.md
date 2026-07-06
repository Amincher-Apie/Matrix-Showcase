# Interaction 交互系统

## 1. 模块职责

为运行时世界物体提供最小可用的玩家交互能力，当前服务于 Capture 任务的任务拾取物：

- `IInteractable` 定义可交互对象的提示、范围与服务端执行入口
- `InteractionDetector` 挂载在本地玩家上，检测最近可交互对象并按 F 触发
- `PickupItem` 表示网络拾取物，服务端校验请求者并写入 `NetworkInventory`
- `WorldBillboardUI` 提供简单的 3D 世界提示文本

## 2. 模块边界

| 边界 | 说明 |
|------|------|
| 上游 | `PlayerNetworkProxy.OnNetworkSpawn()` 为本地玩家运行时添加 `InteractionDetector` |
| 下游 | `NetworkInventory.TryAddItemServer()`、`MissionManager.ReportCapturePickupCollected()` |
| 不负责 | 背包 UI 展示、拾取音效、Prefab 资产绑定 |

## 3. 网络规则

- 客户端只负责检测附近交互物并发送请求。
- `PickupItem.RequestPickupServerRpc()` 使用 `ServerRpcParams.Receive.SenderClientId` 作为请求者，不信任客户端传入的玩家 ID。
- 物品发放只在服务端调用 `NetworkInventory.TryAddItemServer()`。

## 4. 需要人工确认

- Capture 拾取物 Prefab 需要挂载 `NetworkObject`、`PickupItem`、触发用 `Collider`，并在 Netcode NetworkPrefabs 中注册。
- 玩家 Prefab 可不手动挂 `InteractionDetector`，当前由 `PlayerNetworkProxy` 在本地属主生成时运行时补齐。

## 5. 文档维护信息

| 项目 | 内容 |
|------|------|
| 创建日期 | 2026-06-29 |
| 覆盖文件数 | 4 个 .cs |
| 关联模块文档 | MissionSystem, InventorySystem, PlayerControl, NetworkLayer |
## 2026-06-29 实现备注

- `InteractionDetector` 通过 `Physics.OverlapSphereNonAlloc()` 查找附近 Collider，并遍历父级 `MonoBehaviour` 判断 `IInteractable`，避免 Unity 泛型组件查询对接口类型的限制。
- `PickupItem` 的服务端拾取流程为：校验请求者背包 -> `SOManager.GetSOById<BaseInventoryItemSO>()` -> `NetworkInventory.TryAddItemServer()` -> `MissionManager.ReportCapturePickupCollected()` -> Despawn。
- `PickupItem.ServerInit()` 仅在服务端写入任务 Slot、ItemId 与数量；Prefab 上的提示文本、触发 Collider 和 NetworkPrefabs 注册仍需要人工确认。
