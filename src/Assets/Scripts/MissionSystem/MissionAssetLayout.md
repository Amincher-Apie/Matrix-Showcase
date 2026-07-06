# Mission Asset Layout

推荐把任务资产固定放在 `Assets/Resources/Configs/Missions` 下，目录建议如下：

```text
Assets/Resources/Configs/Missions
├─ Libraries
│  └─ MissionLibrary_Default.asset
├─ Primary
│  └─ Boss
│     └─ Mission_Boss_001_Guardian.asset
└─ Secondary
   ├─ Eliminate
   │  └─ Mission_Eliminate_001_Sweep.asset
   ├─ Defense
   │  └─ Mission_Defense_001_Core.asset
   ├─ Capture
   │  └─ Mission_Capture_001_Relay.asset
   └─ Destroy
      └─ Mission_Destroy_001_Reactor.asset
```

命名建议：

- `MissionLibrary_<用途>.asset`
- `Mission_<类型>_<三位编号>_<语义名>.asset`
- `类型` 统一使用 `Boss / Eliminate / Defense / Capture / Destroy`
- `语义名` 用英语短词，方便脚本、表格和资源检索保持一致

推荐职责划分：

- `Libraries` 只放任务库，不放单任务配置
- `Primary/Boss` 只放主任务配置
- `Secondary/*` 每个子目录只放一种次任务类型
- 正式项目里如果有多套主题地图，可以继续按主题在每个类型目录下再加一层，比如 `Industrial`、`Lab`

推荐接入方式：

1. 在 Unity 菜单执行 `Tools/Matrix/Missions/Generate Sample Mission Assets`
2. 生成器会自动创建一套示例 `MissionConfig` 和一个 `MissionLibrary_Default`
3. 把 `MissionManager` 上的 `missionLibrary` 指向 `MissionLibrary_Default`
4. 再按正式内容补全 `objectivePrefab`、`destroyRounds.TargetPrefab`、奖励和敌人配置

当前示例资产包含：

- 1 个主任务 Boss
- 4 个次任务模板：歼灭、防御、捕获、破坏
- 1 个默认任务库，已把上述 5 个任务全部收录

后续扩展建议：

- 如果同一玩法有多套数值难度，直接复制 `MissionConfig`，只改编号和参数
- 如果同一地图风格要绑定固定任务池，可以再建 `MissionLibrary_Industrial`、`MissionLibrary_Lab`
- 如果后续服务器下发的是任务 ID，只要保证服务器 ID 和 `missionId` 对齐即可
