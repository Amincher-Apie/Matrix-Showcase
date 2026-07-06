# AnimatorController 绑定手册

> 项目：Matrix  
> 用途：指导开发/美术将模型挂入 prefab、绑定 Animator 和网络动画组件  
> Controller 重建：另见 [AnimatorControllerRebuildGuide.md](./AnimatorControllerRebuildGuide.md)

## 敌人绑定步骤（以 Arack_Orange + 001.prefab 为例）

### 1. 准备 001.prefab

1. 在 Project 窗口打开 `Assets/Resources/Prefab/Enemy/`
2. 选中 `001.prefab`，在 Inspector 中点击 "Open Prefab"
3. 禁用或删除子对象 `Capsule` 的 `MeshRenderer`

### 2. 挂入模型

1. 将模型 prefab 拖入 001 的 Hierarchy，作为根对象的子对象：
   `Assets/Resources/Modules/Protofactor/Sci Fi/Sci Fi Characters Mega Pack Vol 2/Sci Fi Creatures Vol 2/Arack/Prefabs/Arack_Orange.prefab`
2. 重命名子对象为 `Model`
3. 确保 `Model` 的 `Transform` 本地位置/旋转/缩放与模型对齐需求一致

### 3. 移除模型自带的 Animator

模型 prefab 自带 `Animator` 组件。因为 Animator 统一放在根对象上，需从 `Model` 子对象上移除：

1. 选中 `Model` GameObject
2. 在 Inspector 中找到 `Animator` 组件
3. 右键 → Remove Component

> 只移除 Animator 组件，保留骨骼层级和 SkinnedMeshRenderer。

### 4. 在根对象添加 Animator

1. 选中根对象（如 `001`）
2. Add Component → `Animator`
3. 配置：
   - **Controller**：`Arack_Orange.controller`（重建步骤见 [AnimatorControllerRebuildGuide.md](./AnimatorControllerRebuildGuide.md)）
   - **Avatar**：拖入 Arack FBX 的 Avatar（来自模型 Rig 配置）
   - **Apply Root Motion**：取消勾选
   - **Update Mode**：`Normal`
   - **Culling Mode**：`Cull Update Transforms`

### 5. 在根对象添加 NetworkAnimator

1. 选中根对象
2. Add Component → `NetworkAnimator`（`Unity.Netcode.Components`）
3. 不需要额外配置，`NetworkAnimator` 自动绑定同 GameObject 上的 `Animator`

> 不要使用 `ClientNetworkAnimator`，敌人是服务端权威。

### 6. 赋值 Controller 并保存

1. 选中根对象 `Animator` 组件
2. 将对应的 `.controller` 文件拖入 `Controller` 字段
3. 将模型 Avatar 拖入 `Avatar` 字段
4. 保存 prefab

---

## 玩家绑定步骤（以 Droid_OII + TestPlayer.prefab 为例）

玩家流程与敌人类似，差异点：

| 差异项 | 敌人 | 玩家 |
|--------|------|------|
| 模型路径 | `.../Arack/Prefabs/Arack_Orange.prefab` | `.../Droid OII/Prefabs/Droid_OII_DesertCamo.prefab` |
| 目标 prefab | `Assets/Resources/Prefab/Enemy/001.prefab` | `Assets/Resources/Prefab/Player/TestPlayer.prefab` |
| 网络动画组件 | `NetworkAnimator` | `ClientNetworkAnimator` |
| Controller | `Arack_Orange.controller` | `Droid_OII.controller` |

其余步骤（移除模型 Animator、在根对象挂 Animator、配置 Avatar）相同。

---

## Clip 设置检查清单

- [ ] Idle / Walk / Run 系列：**Loop Time = true**
- [ ] Attack / Hit / Die / Skill：**Loop Time = false**
- [ ] 所有 Clip 的 **Root Motion** 默认关闭
- [ ] Avatar 来源于对应模型 FBX 的 Rig 页签配置
