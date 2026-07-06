# GameHUDWindow 小地图配置指南

> 面向：需要配置或排查 GameHUDWindow 中小地图系统的开发者  
> 最后更新：2026-06-27  
> 相关文档：[ADR-0001](adr/0001-minimap-pre-baked-thumbnails.md) | [UI MODULE.md](../Assets/Scripts/UI/MODULE.md) | [PCG MODULE.md](../Assets/Scripts/PCG/MODULE.md)

---

## 一、架构概览

```
GameHUDWindow (WindowBase)
  ├─ MinimapLogic (纯逻辑，非 MonoBehaviour)
  │     ├─ 全图纹理预分配 (Texture2D, RGBA32)
  │     ├─ 房间缩略图拼接（读取 PcgRoomRoot.minimapThumbnail 的 RGB/alpha）
  │     ├─ 连接区域补面（深灰色矩形填充门缝）
  │     ├─ 角色图标纹入（RoomRole → Texture2D，从 Style 配置注入）
  │     ├─ 坐标转换（World → UV → MinimapLocal）
  │     └─ 实体追踪（PlayerActor / EnemyActor，事件驱动注册/注销）
  │
  └─ MinimapView (MonoBehaviour，挂在 Prefab 子节点)
        ├─ RawImage（地形层，uvRect 裁切跟随玩家）
        ├─ MarkerContainer（动态标记层）
        ├─ 圆形 Mask + 边框
        └─ Update()：每帧读 TrackedPlayers/Enemies，更新标记位置/朝向
```

---

## 二、配置清单

### 2.1 GameHUDWindow Prefab — Inspector 绑定

#### GameHUDWindowDataComponent（根节点）

| 字段 | 绑定目标 | 文件 |
|------|----------|------|
| `MinimapView` | Prefab 子节点上的 `MinimapView` 组件 | `GameHUDWindowDataComponent.cs:22` |

代码路径：`GameHUDWindow.InitMinimap()` → `dataComponent.MinimapView`。

若此字段未绑定，`InitMinimap()` 会直接 `return`，小地图不初始化。

#### MinimapView 组件（子节点）

| SerializeField | 类型 | 绑定目标 | 说明 |
|----------------|------|---------|------|
| `minimapRoot` | `RectTransform` | 小地图圆形区域根节点 | 用于计算小地图像素尺寸 |
| `minimapImage` | `RawImage` | 静态地形层 RawImage | 运行时通过 `uvRect` 裁切 |
| `markerContainer` | `RectTransform` | 动态标记父容器 | 玩家/敌人标记 Instantiate 到此 |
| `borderImage` | `Image` | 圆形 Mask 边框 | 小地图外圈装饰 |
| `playerMarkerPrefab` | `GameObject` | 玩家标记预制体 | 需带 RectTransform |
| `enemyMarkerPrefab` | `GameObject` | 敌人标记预制体 | 需带 RectTransform |
| `viewWorldRadius` | `float`（默认 30） | — | 小地图视野半径（米） |

文件：[MinimapView.cs:11-25](../Assets/Scripts/UI/MinimapView.cs#L11-L25)

### 2.2 前置条件 — 房间资产

每个房间 Prefab 必须完成两步：

**第一步：NavMesh 烘焙**

菜单：`Tools > PCG NavMesh > Bake Selected Prefab`（或 `Bake All PCG Room Prefabs`）

产出：`RoomPrebakedNavMeshAsset.NavMeshData`（存储在 `Resources/Data/PrebakedNavMeshAssets/Rooms/`）

**第二步：小地图缩略图烘焙**

菜单：`Tools > PCG NavMesh > Minimap Thumbnail Baker`

流程：
1. 拖入房间 Prefab
2. 调整光栅化参数（`PixelsPerMeter`=10, `MaxTextureSize`=512）
3. 点击 `[Bake]` 预览
4. 点击 `[Save]` 保存并赋值

产出：`{PrefabDir}/MinimapThumbnails/{name}_minimap.png`，自动赋值到 `PcgRoomRoot.minimapThumbnail`

支持 `[Batch]` 按钮批量处理整个房间目录。

> 缩略图颜色就是运行时小地图的最终颜色。Baker 默认内部填充 `RGB(64,64,64)`，外轮廓纯白。美术可自行替换缩略图 PNG 调整风格。

### 2.3 可选配置 — 房间角色图标

在 `PcgGenerationProfile` 的 `StyleOptions.MinimapIcons` 中配置：

| 字段 | 类型 | 说明 |
|------|------|------|
| `Role` | `RoomRole` | 房间角色枚举 |
| `Icon` | `Texture2D` | 图标纹理（纹入房间世界原点） |

文件：[PcgGenerationModels.cs:77-83](../Assets/Scripts/PCG/Data/PcgGenerationModels.cs#L77-L83)

不需要图标时可保持列表为空。

---

## 三、运行时数据流

```
GameHUDWindow.OnAwake()
  └─ _minimapLogic = new MinimapLogic()

GameHUDWindow.OnShow()
  ├─ InitHUDComponents()          // 血量/弹药/技能
  └─ InitMinimap()
       ├─ MissionManager.CurrentMapResult → mapResult
       ├─ logic.Initialize(mapResult, mapResult.Request.MinimapIcons)
       │    ├─ BuildIconMap()      // 建立 RoomRole → Texture2D 映射
       │    ├─ BuildFullMapTexture()  // 遍历 PlacedRooms 计算包围盒，分配全图纹理
       │    └─ RegisterEvents()    // 订阅 5 个 EventCenter 事件
       └─ view.Initialize(logic)
```

**渐进探索（运行时）**：

```
玩家进入房间 →
  EventCenter.RoomEntered(event) →
    MinimapLogic.OnRoomEntered(nodeId)
      ├─ StampTerrainTexture(thumbnail, roomRoot, bounds)
      │    └─ 有 RoomBounds → 仿射变换定向拼接
      │    └─ 无 RoomBounds → 轴对称回退拼接
      ├─ StampConnectionPatchesForRoom(placedRoom)
      │    └─ 遍历 mapResult.Connections，对匹配房间的每条连接
      │        用 ConnectorFrom/ConnectorTo 计算门宽矩形
      │        填充 RGB(64,64,64) + 白色轮廓
      └─ StampIconAtWorldPos(icon, roomCenter)
```

**每帧更新（MinimapView.Update）**：

```
UpdatePlayerMarkers(viewCenter)
  ├─ 移除已注销玩家的标记
  ├─ 为新增玩家 Instantiate markerPrefab
  └─ 计算 WorldToMinimapLocal 位置 + 朝向旋转

UpdateEnemyMarkers(viewCenter)
  └─ 同上，使用 enemyMarkerPrefab

UpdateMinimapUV(viewCenter)
  └─ 按本地玩家 WorldToFullMapUV 裁切 RawImage.uvRect
```

---

## 四、GameHUDWindow 生命周期

| 阶段 | 操作 | 说明 |
|------|------|------|
| `OnAwake()` | `new MinimapLogic()` | 创建逻辑层，不依赖 PCG 数据 |
| `OnShow()` | `InitMinimap()` | 需 `MissionManager.CurrentMapResult` 已就绪 |
| `OnHide()` | 无操作 | 小地图保持状态 |
| `OnDestroy()` | `logic.Dispose()` | 销毁纹理、注销事件、清空字典 |

---

## 五、排查指南

| 症状 | 原因 | 检查位置 |
|------|------|----------|
| 小地图完全不显示 | `dataComponent.MinimapView` 未在 Inspector 绑定 | GameHUDWindow Prefab |
| 地形层空白 | 房间 `minimapThumbnail` 未烘焙 | 房间 Prefab Inspector → PcgRoomRoot |
| 地形层全透明 | 缩略图 alpha 通道全 ≤ 0.1 | 检查缩略图 PNG 的 alpha 通道 |
| 玩家标记不出现 | `playerMarkerPrefab` 未赋值或 PlayerSpawned 事件未触发 | MinimapView Inspector / EventCenter |
| 标记闪烁/跳跃 | `viewWorldRadius` 与实际地图比例不匹配 | `MinimapView.viewWorldRadius`（默认 30） |
| 房间连接处断裂 | `PcgRoomConnection` 未记录或 `ConnectorFrom/To` 为空 | `mapResult.Connections` |
| InitMinimap 直接 return | `missionManager?.CurrentMapResult` 为 null | PCG 未完成或 GameHUDWindow 在 PCG 前 Show |
| 图标未显示 | `MinimapIcons` 列表为空或 Icon 纹理未赋值 | `PcgGenerationProfile.StyleOptions` |
| 编辑器预览无数据 | 场景缺少 `FullFlowBootstrap` 或引用未配置 | `Tools > Minimap > Minimap Full Map Debug` |

### 时序问题

`InitMinimap()` 依赖 `MissionManager.CurrentMapResult`。如果 GameHUDWindow 在 PCG 完成之前 Show，小地图不会初始化。当前架构**没有延迟重试机制**，需确保 PCG 先于 HUD Show 完成。

---

## 六、关键代码引用

| 文件 | 职责 |
|------|------|
| [GameHUDWindow.cs](../Assets/Scripts/UI/WindowComponent/GameHUDWindow.cs) | 小地图创建、初始化、销毁 |
| [GameHUDWindowDataComponent.cs](../Assets/Scripts/UI/DataComponent/GameHUDWindowDataComponent.cs) | MinimapView Inspector 字段声明 |
| [MinimapLogic.cs](../Assets/Scripts/UI/MinimapLogic.cs) | 纹理拼接、坐标转换、实体追踪 |
| [MinimapView.cs](../Assets/Scripts/UI/MinimapView.cs) | 每帧 UI 更新、标记管理 |
| [MinimapThumbnailBaker.cs](../Assets/Editor/MinimapThumbnailBaker.cs) | Editor 缩略图烘焙工具 |
| [PcgGenerationModels.cs](../Assets/Scripts/PCG/Data/PcgGenerationModels.cs) | MinimapIconEntry / MinimapIcons 数据模型 |
| [PcgRoomRoot.cs](../Assets/Scripts/PCG/Rooms/PcgRoomRoot.cs) | minimapThumbnail 字段声明与公开访问器 |
