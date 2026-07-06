using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 聚合玩家所有品质道具效果，并根据战斗事件执行触发。
/// </summary>
public class PlayerQualityEffectModule : IModule
{
    private readonly PlayerActor _owner;
    private readonly Dictionary<string, float> _cooldowns = new(); // key: effectId, value: nextReadyTime
    private readonly List<(QualityEffectDefinition def, int stacks, int quality)> _activeEffects = new();

    public ulong ObjectId => _owner.ObjectId;

    public PlayerQualityEffectModule(PlayerActor owner)
    {
        _owner = owner;
    }

    public void LocalInit()
    {
        var inv = _owner?.InventoryModule;
        if (inv == null)
        {
            Debug.LogWarning($"[PlayerQualityEffectModule] LocalInit: InventoryModule 为空");
            return;
        }
        
        var net = _owner.InventoryModule?.InventoryData; // 只用来初始化，不直接依赖 NetworkList
        Debug.Log($"[PlayerQualityEffectModule] LocalInit: 开始初始化，owner={_owner?.ObjectId}");
        RebuildFromInventory();

        // 订阅网络背包变化
        var networkInventory = _owner.networkProxy?.GetComponent<NetworkInventory>();
        if (networkInventory != null)
        {
            Debug.Log($"[PlayerQualityEffectModule] 订阅 OnInventoryChanged 事件 (IsServer={networkInventory.IsServer}, IsClient={networkInventory.IsClient})");
            networkInventory.OnInventoryChanged += OnInventoryChangedHandler;
            
            // 验证订阅是否成功（注意：事件不能直接访问 GetInvocationList，所以注释掉）
            // var invocationList = networkInventory.OnInventoryChanged?.GetInvocationList();
            // Debug.Log($"[PlayerQualityEffectModule] 订阅后，OnInventoryChanged 订阅数: {invocationList?.Length ?? 0}");
        }
        else
        {
            Debug.LogWarning($"[PlayerQualityEffectModule] 未找到 NetworkInventory，无法订阅事件");
        }

        // 注册所有堆叠规则
        StackingRuleRegistry.Register("Add", new AddStackingRule());
        StackingRuleRegistry.Register("Max", new MaxStackingRule());
        StackingRuleRegistry.Register("Min", new MinStackingRule());
        StackingRuleRegistry.Register("NoStack", new NoStackingRule());
        StackingRuleRegistry.Register("Average", new AverageStackingRule());
        StackingRuleRegistry.Register("StackByQuality", new StackByQualityRule());
        
        // 注册所有 Condition 执行器
        QualityEffectRegistry.RegisterCondition("RandomChance", new RandomChanceEvaluator());
        QualityEffectRegistry.RegisterCondition("HPRatioLessThan", new HPRatioLessThanEvaluator());
        QualityEffectRegistry.RegisterCondition("HPRatioGreaterThan", new HPRatioGreaterThanEvaluator());
        QualityEffectRegistry.RegisterCondition("HasStatus", new HasStatusEvaluator());
        QualityEffectRegistry.RegisterCondition("NoStatus", new NoStatusEvaluator());
        QualityEffectRegistry.RegisterCondition("IsElite", new IsEliteEvaluator());
        QualityEffectRegistry.RegisterCondition("StackGreaterThan", new StackGreaterThanEvaluator());
        QualityEffectRegistry.RegisterCondition("RecentKills", new RecentKillsEvaluator());
        QualityEffectRegistry.RegisterCondition("DistanceLessThan", new DistanceLessThanEvaluator());
        QualityEffectRegistry.RegisterCondition("IsFacing", new IsFacingEvaluator());
        QualityEffectRegistry.RegisterCondition("AttributeGreater", new AttributeGreaterEvaluator());

        // 注册所有 Action 执行器
        QualityEffectRegistry.RegisterAction("AddStat", new AddStatExecutor());
        QualityEffectRegistry.RegisterAction("AddWeaponStat", new AddWeaponStatExecutor());
        QualityEffectRegistry.RegisterAction("ApplyDot", new ApplyDotExecutor());
        QualityEffectRegistry.RegisterAction("Summon", new SummonExecutor());
        QualityEffectRegistry.RegisterAction("ApplyHoT", new ApplyHoTExecutor());
        QualityEffectRegistry.RegisterAction("RemoveStat", new RemoveStatExecutor());
        QualityEffectRegistry.RegisterAction("RemoveWeaponStat", new RemoveWeaponStatExecutor());
        QualityEffectRegistry.RegisterAction("ApplyBuff", new ApplyBuffExecutor());
        QualityEffectRegistry.RegisterAction("ApplyDebuff", new ApplyDebuffExecutor());
        QualityEffectRegistry.RegisterAction("AddDamage", new AddDamageExecutor());
        QualityEffectRegistry.RegisterAction("Heal", new HealExecutor());
        QualityEffectRegistry.RegisterAction("Teleport", new TeleportExecutor());
        QualityEffectRegistry.RegisterAction("ModifyCooldown", new ModifyCooldownExecutor());
        QualityEffectRegistry.RegisterAction("DropItem", new DropItemExecutor());
    }

    public void OnActivate() { }

    public void LocalDestroy()
    {
        var networkInventory = _owner?.networkProxy?.GetComponent<NetworkInventory>();
        if (networkInventory != null)
        {
            networkInventory.OnInventoryChanged -= OnInventoryChangedHandler;
        }
        _activeEffects.Clear();
        _cooldowns.Clear();
    }

    /// <summary>
    /// 从背包当前装备的品质道具中重建品质道具效果
    /// 品质道具不允许堆叠，每个道具占一个槽位。
    /// 
    /// 效果叠加策略（通过标签控制）：
    /// - 无标签或 "NoMerge"：每个道具单独执行，数值累加（如攻速加成）
    /// - "MergeByQuality"：相同效果ID按品质分组合并，层数=该品质的道具数量（如概率效果）
    /// - "MergeAll"：相同效果ID合并所有品质，层数=所有道具总数（如持续时间效果）
    /// 
    /// 通过 perStack 参数控制哪些属性随层数增加：
    /// - 配置了 perStack 的参数会随层数增加（如 chance.perStack, duration.perStack）
    /// - 未配置 perStack 的参数保持基础值不变
    /// </summary>
    /// <summary>
    /// 事件处理器：当背包变化时调用
    /// </summary>
    private void OnInventoryChangedHandler()
    {
        Debug.Log($"[PlayerQualityEffectModule] ★ OnInventoryChangedHandler 被调用");
        RebuildFromInventory();
    }
    
    public void RebuildFromInventory()
    {
        Debug.Log($"[PlayerQualityEffectModule] ★ RebuildFromInventory 被调用");
        
        // 确保堆叠规则已注册（防止在 LocalInit 之前调用）
        if (!StackingRuleRegistry.HasRule("Add"))
        {
            Debug.LogWarning($"[PlayerQualityEffectModule] 堆叠规则未注册，正在初始化...");
            StackingRuleRegistry.Register("Add", new AddStackingRule());
            StackingRuleRegistry.Register("Max", new MaxStackingRule());
            StackingRuleRegistry.Register("Min", new MinStackingRule());
            StackingRuleRegistry.Register("NoStack", new NoStackingRule());
            StackingRuleRegistry.Register("Average", new AverageStackingRule());
            StackingRuleRegistry.Register("StackByQuality", new StackByQualityRule());
        }
        
        // 确保 Action 和 Condition 执行器已注册（防止在 LocalInit 之前调用）
        if (!QualityEffectRegistry.TryGetAction("AddStat", out _))
        {
            Debug.LogWarning($"[PlayerQualityEffectModule] Action 执行器未注册，正在初始化...");
            // 注册所有 Condition 执行器
            QualityEffectRegistry.RegisterCondition("RandomChance", new RandomChanceEvaluator());
            QualityEffectRegistry.RegisterCondition("HPRatioLessThan", new HPRatioLessThanEvaluator());
            QualityEffectRegistry.RegisterCondition("HPRatioGreaterThan", new HPRatioGreaterThanEvaluator());
            QualityEffectRegistry.RegisterCondition("HasStatus", new HasStatusEvaluator());
            QualityEffectRegistry.RegisterCondition("NoStatus", new NoStatusEvaluator());
            QualityEffectRegistry.RegisterCondition("IsElite", new IsEliteEvaluator());
            QualityEffectRegistry.RegisterCondition("StackGreaterThan", new StackGreaterThanEvaluator());
            QualityEffectRegistry.RegisterCondition("RecentKills", new RecentKillsEvaluator());
            QualityEffectRegistry.RegisterCondition("DistanceLessThan", new DistanceLessThanEvaluator());
            QualityEffectRegistry.RegisterCondition("IsFacing", new IsFacingEvaluator());
            QualityEffectRegistry.RegisterCondition("AttributeGreater", new AttributeGreaterEvaluator());
            
            // 注册所有 Action 执行器
            QualityEffectRegistry.RegisterAction("AddStat", new AddStatExecutor());
            QualityEffectRegistry.RegisterAction("AddWeaponStat", new AddWeaponStatExecutor());
            QualityEffectRegistry.RegisterAction("ApplyDot", new ApplyDotExecutor());
            QualityEffectRegistry.RegisterAction("Summon", new SummonExecutor());
            QualityEffectRegistry.RegisterAction("ApplyHoT", new ApplyHoTExecutor());
            QualityEffectRegistry.RegisterAction("RemoveStat", new RemoveStatExecutor());
            QualityEffectRegistry.RegisterAction("RemoveWeaponStat", new RemoveWeaponStatExecutor());
            QualityEffectRegistry.RegisterAction("ApplyBuff", new ApplyBuffExecutor());
            QualityEffectRegistry.RegisterAction("ApplyDebuff", new ApplyDebuffExecutor());
            QualityEffectRegistry.RegisterAction("AddDamage", new AddDamageExecutor());
            QualityEffectRegistry.RegisterAction("Heal", new HealExecutor());
            QualityEffectRegistry.RegisterAction("Teleport", new TeleportExecutor());
            QualityEffectRegistry.RegisterAction("ModifyCooldown", new ModifyCooldownExecutor());
            QualityEffectRegistry.RegisterAction("DropItem", new DropItemExecutor());
        }
        
        var networkInventory = _owner?.networkProxy?.GetComponent<NetworkInventory>(); //获取当前角色的背包
        if (networkInventory == null)
        {
            Debug.LogWarning($"[PlayerQualityEffectModule] RebuildFromInventory: networkInventory 为空");
            return;
        }

        Debug.Log($"[PlayerQualityEffectModule] 开始重建效果列表，当前激活效果数量: {_activeEffects.Count}");

        // 记录旧效果用于检测变化
        var oldEffects = new Dictionary<string, int>();
        var oldEffectDefinitions = new Dictionary<string, (QualityEffectDefinition def, int stacks, int quality)>();
        foreach (var (def, stacks, quality) in _activeEffects) //遍历当前角色装备的所有品质道具效果
        {
            if (!string.IsNullOrEmpty(def.id))
            {
                oldEffects[def.id] = stacks; //记录当前角色装备的品质道具效果的ID和堆叠层数
                oldEffectDefinitions[def.id] = (def, stacks, quality); //保存旧效果定义，用于撤销属性修改
            }
        }

        _activeEffects.Clear(); //清空当前角色装备的所有品质道具效果

        // 用于收集所有效果，后续按类型处理
        var rawEffects = new List<(QualityEffectDefinition def, int quality, int slotIndex)>();
        
        // 第一步：收集所有槽位的效果
        for (int i = 0; i < networkInventory.QualityItemSlots.Count; i++)
        {
            var slot = networkInventory.QualityItemSlots[i];
            if (slot.isNull) continue;

            string itemId = slot.item.itemId.ToString();
            int quality = (int)slot.item.qualityLevel; // 获取道具品质（0=白色, 1=绿色, 2=蓝色, 3=紫色, 4=红色）

            // 从SO管理器拿到对应道具的SO配置，读出该道具的效果列表
            var so = SOManager.Instance?.GetSOById<QualityItemSO>(itemId);
            if (so == null || so.effects == null) continue;

            // 遍历该道具的所有效果
            foreach (var eff in so.effects)
            {
                rawEffects.Add((eff, quality, i));
            }
        }

        // 第二步：按效果ID和合并策略分组
        // 相同效果ID的效果会被合并，合并策略由标签决定
        var mergeGroups = new Dictionary<string, Dictionary<int, int>>(); // key: effectId, value: (quality -> count) - 用于 MergeByQuality
        var mergeAllGroups = new Dictionary<string, (QualityEffectDefinition def, int totalCount, int maxQuality)>(); // key: effectId - 用于 MergeAll
        var noMergeEffects = new List<(QualityEffectDefinition def, int quality)>(); // 不合并的效果
        
        foreach (var (def, quality, _) in rawEffects)
        {
            if (string.IsNullOrEmpty(def.id))
            {
                // 没有效果ID，直接添加，不合并
                _activeEffects.Add((def, 1, quality));
                continue;
            }
            
            // 检查合并策略标签
            string tags = def.tags ?? "";
            bool isNoMerge = string.IsNullOrEmpty(tags) || 
                            tags.Contains("NoMerge", StringComparison.OrdinalIgnoreCase);
            bool isMergeByQuality = tags.Contains("MergeByQuality", StringComparison.OrdinalIgnoreCase);
            bool isMergeAll = tags.Contains("MergeAll", StringComparison.OrdinalIgnoreCase);
            
            if (isNoMerge)
            {
                // 不合并：每个道具单独执行，数值累加
                // 例如：2个绿色士兵注射器（每个+15%攻速）+ 1个蓝色士兵注射器（+20%攻速）
                // 最终效果：50%攻速（15+15+20），每个效果分别执行
                noMergeEffects.Add((def, quality));
            }
            else if (isMergeByQuality)
            {
                // 按品质合并：相同效果ID按品质分组合并
                // 例如：2个绿色（20%概率）+ 1个蓝色（30%概率）
                // → 绿色合并为层数2，蓝色层数1，分别执行
                if (!mergeGroups.ContainsKey(def.id))
                    mergeGroups[def.id] = new Dictionary<int, int>();
                
                if (!mergeGroups[def.id].ContainsKey(quality))
                    mergeGroups[def.id][quality] = 0;
                
                mergeGroups[def.id][quality]++;
            }
            else if (isMergeAll)
            {
                // 合并所有：相同效果ID合并所有品质的道具
                // 例如：2个道具（减速2秒）→ 合并为层数2，duration.perStack=2 → 最终4秒
                if (!mergeAllGroups.ContainsKey(def.id))
                {
                    mergeAllGroups[def.id] = (def, 0, quality);
                }
                
                var (_, count, maxQ) = mergeAllGroups[def.id];
                mergeAllGroups[def.id] = (def, count + 1, Math.Max(maxQ, quality));
            }
            else
            {
                // 默认行为：不合并（向后兼容）
                noMergeEffects.Add((def, quality));
            }
        }

        // 第三步：处理不合并的效果（直接添加）
        foreach (var (def, quality) in noMergeEffects)
        {
            _activeEffects.Add((def, 1, quality));
        }

        // 第四步：处理按品质合并的效果
        foreach (var kvp in mergeGroups)
        {
            string effectId = kvp.Key;
            var qualityGroups = kvp.Value;
            
            // 找到原始效果定义
            QualityEffectDefinition baseDef = default;
            bool found = false;
            foreach (var (def, _, _) in rawEffects)
            {
                if (def.id == effectId)
                {
                    baseDef = def;
                    found = true;
                    break;
                }
            }
            
            if (!found) continue;
            
            // 对每个品质组，创建一个合并后的效果（层数 = 该品质的道具数量）
            foreach (var qualityGroup in qualityGroups)
            {
                int quality = qualityGroup.Key;
                int count = qualityGroup.Value; // 该品质的道具数量
                
                // 层数等于该品质的道具数量
                // 通过 perStack 参数控制哪些属性随层数增加
                // 例如：chance.perStack = 0.2，层数2 → 概率 = base + 0.2 * 2
                _activeEffects.Add((baseDef, count, quality));
            }
        }

        // 第五步：处理合并所有品质的效果
        foreach (var kvp in mergeAllGroups)
        {
            string effectId = kvp.Key;
            var (def, totalCount, maxQuality) = kvp.Value;
            
            // 层数等于所有品质的道具总数
            // 通过 perStack 参数控制哪些属性随层数增加
            // 例如：duration.perStack = 2，层数2 → 持续时间 = base + 2 * 2 = base + 4
            _activeEffects.Add((def, totalCount, maxQuality));
        }

        // 第六步：应用参数堆叠规则，合并参数值
        ApplyParamStackingRules(rawEffects);
        
        // 第七步：检测效果变化并触发相应事件
        var newEffectIds = new HashSet<string>();
        foreach (var (def, _, _) in _activeEffects)
        {
            if (!string.IsNullOrEmpty(def.id))
                newEffectIds.Add(def.id);
        }

        // 先处理卸载：撤销所有被移除的效果的属性修改
        foreach (var oldId in oldEffects.Keys)
        {
            if (!newEffectIds.Contains(oldId))
            {
                Debug.Log($"[PlayerQualityEffectModule] 效果 {oldId} 被移除，触发 OnUnequip");
                RaiseOnUnequip();
                
                // 自动撤销该效果的属性修改（如果配置了 AddStat 动作）
                if (oldEffectDefinitions.TryGetValue(oldId, out var oldEffectInfo))
                {
                    var (oldDef, oldStacks, oldQuality) = oldEffectInfo;
                    if (oldDef.actions != null)
                    {
                        // 查找 AddStat 动作并执行反向操作
                        foreach (var action in oldDef.actions)
                        {
                            if (action.actionId == "AddStat")
                            {
                                // 执行反向操作：RemoveStat
                                var evt = new QualityEventContext { owner = _owner, target = null, baseDamage = 0f };
                                var ctx = new QualityEffectRuntimeContext
                                {
                                    stacks = oldStacks,
                                    quality = oldQuality,
                                    baseDamage = 0f,
                                    ownerLevel = 0,
                                    targetMaxHp = 0,
                                    effectId = oldId
                                };
                                
                                if (QualityEffectRegistry.TryGetAction("RemoveStat", out var removeStatExecutor))
                                {
                                    Debug.Log($"[PlayerQualityEffectModule] 自动撤销效果 {oldId} 的属性修改");
                                    removeStatExecutor.Execute(action, ctx, evt);
                                }
                            }
                        }
                    }
                }
            }
        }

        // 然后处理新装备和层数变化：应用新效果的属性修改
        foreach (var newId in newEffectIds)
        {
            if (!oldEffects.ContainsKey(newId))
            {
                // 新效果被添加
                Debug.Log($"[PlayerQualityEffectModule] 效果 {newId} 被添加，触发 OnEquip");
                RaiseOnEquip();
            }
            else
            {
                // 效果已存在，检查层数是否变化
                int oldStacks = oldEffects[newId];
                int newStacks = 0;
                foreach (var (def, stacks, _) in _activeEffects)
                {
                    if (def.id == newId)
                    {
                        newStacks = stacks;
                        break;
                    }
                }
                
                if (newStacks != oldStacks)
                {
                    // 层数变化了，需要重新应用效果
                    Debug.Log($"[PlayerQualityEffectModule] 效果 {newId} 层数变化: {oldStacks} -> {newStacks}，重新应用效果");
                    
                    // 先撤销旧效果的属性修改
                    if (oldEffectDefinitions.TryGetValue(newId, out var oldEffectInfo))
                    {
                        var (oldDef, _, oldQuality) = oldEffectInfo;
                        if (oldDef.actions != null)
                        {
                            foreach (var action in oldDef.actions)
                            {
                                if (action.actionId == "AddStat")
                                {
                                    var evt = new QualityEventContext { owner = _owner, target = null, baseDamage = 0f };
                                    var ctx = new QualityEffectRuntimeContext
                                    {
                                        stacks = oldStacks,
                                        quality = oldQuality,
                                        baseDamage = 0f,
                                        ownerLevel = 0,
                                        targetMaxHp = 0,
                                        effectId = newId
                                    };
                                    
                                    if (QualityEffectRegistry.TryGetAction("RemoveStat", out var removeStatExecutor))
                                    {
                                        Debug.Log($"[PlayerQualityEffectModule] 撤销效果 {newId} 的旧属性修改（层数 {oldStacks}）");
                                        removeStatExecutor.Execute(action, ctx, evt);
                                    }
                                }
                            }
                        }
                    }
                    
                    // 然后应用新效果的属性修改（使用新层数）
                    // 只针对这个效果重新应用，而不是触发所有效果
                    foreach (var (def, stacks, quality) in _activeEffects)
                    {
                        if (def.id == newId && def.triggers != null && def.triggers.Contains("OnEquip"))
                        {
                            var evt = new QualityEventContext { owner = _owner, target = null, baseDamage = 0f };
                                    var ctx = new QualityEffectRuntimeContext
                                    {
                                        stacks = newStacks,
                                        quality = quality,
                                        baseDamage = 0f,
                                        ownerLevel = 0,
                                        targetMaxHp = 0,
                                        effectId = newId
                                    };
                            
                            // 执行所有动作
                            if (def.actions != null)
                            {
                                foreach (var action in def.actions)
                                {
                                    if (QualityEffectRegistry.TryGetAction(action.actionId, out var executor))
                                    {
                                        Debug.Log($"[PlayerQualityEffectModule] 重新应用效果 {newId} 的属性修改（层数 {newStacks}）");
                                        executor.Execute(action, ctx, evt);
                                    }
                                }
                            }
                            break;
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 应用参数堆叠规则，合并参数值
    /// </summary>
    private void ApplyParamStackingRules(List<(QualityEffectDefinition def, int quality, int slotIndex)> rawEffects)
    {
        // 按效果ID分组原始效果
        var effectsByEffectId = new Dictionary<string, List<(QualityEffectDefinition def, int quality, int stacks)>>();
        
        foreach (var (def, quality, _) in rawEffects)
        {
            if (string.IsNullOrEmpty(def.id)) continue;
            
            if (!effectsByEffectId.ContainsKey(def.id))
                effectsByEffectId[def.id] = new List<(QualityEffectDefinition def, int quality, int stacks)>();
            
            effectsByEffectId[def.id].Add((def, quality, 1));
        }
        
        // 处理每个激活的效果
        var newActiveEffects = new List<(QualityEffectDefinition def, int stacks, int quality)>();
        
        foreach (var (def, stacks, quality) in _activeEffects)
        {
            if (string.IsNullOrEmpty(def.id))
            {
                // 没有效果ID，直接添加
                newActiveEffects.Add((def, stacks, quality));
                continue;
            }
            
            // 检查合并策略
            string tags = def.tags ?? "";
            bool isMergeByQuality = tags.Contains("MergeByQuality", StringComparison.OrdinalIgnoreCase);
            bool isMergeAll = tags.Contains("MergeAll", StringComparison.OrdinalIgnoreCase);
            
            if (!effectsByEffectId.ContainsKey(def.id))
            {
                newActiveEffects.Add((def, stacks, quality));
                continue;
            }
            
            var sourceEffects = effectsByEffectId[def.id];
            
            if (isMergeByQuality)
            {
                // 按品质分组：只合并相同品质的效果
                var qualityGroup = sourceEffects.Where(e => e.quality == quality).ToList();
                if (qualityGroup.Count > 0)
                {
                    var mergedDef = MergeEffectDefinition(qualityGroup, def);
                    newActiveEffects.Add((mergedDef, stacks, quality));
                }
            }
            else if (isMergeAll)
            {
                // 合并所有品质：合并所有效果
                var mergedDef = MergeEffectDefinition(sourceEffects, def);
                newActiveEffects.Add((mergedDef, stacks, quality));
            }
            else
            {
                // NoMerge：不合并，保持原样
                newActiveEffects.Add((def, stacks, quality));
            }
        }
        
        _activeEffects.Clear();
        _activeEffects.AddRange(newActiveEffects);
    }
    
    /// <summary>
    /// 合并效果定义：根据参数堆叠规则合并参数值
    /// </summary>
    private QualityEffectDefinition MergeEffectDefinition(
        List<(QualityEffectDefinition def, int quality, int stacks)> sourceEffects,
        QualityEffectDefinition baseDef)
    {
        // 创建新的效果定义（深拷贝）
        var stackingRuleMap = baseDef.BuildParamStackingRuleMap();
        
        var mergedDef = new QualityEffectDefinition
        {
            id = baseDef.id,
            tags = baseDef.tags,
            maxStacks = baseDef.maxStacks,
            cooldown = baseDef.cooldown,
            triggers = new List<string>(baseDef.triggers),
            conditions = new List<ConditionBlock>(),
            actions = new List<ActionBlock>(),
            paramStackingRules = baseDef.paramStackingRules?
                .Where(entry => entry != null)
                .Select(entry => entry.Clone())
                .ToList() ?? new List<ParamStackingRuleEntry>()
        };
        
        // 复制条件（条件参数也需要合并）
        foreach (var condition in baseDef.conditions)
        {
            var mergedCondition = new ConditionBlock
            {
                conditionId = condition.conditionId,
                @params = MergeParams(sourceEffects, condition.@params, stackingRuleMap, true, condition.conditionId)
            };
            mergedDef.conditions.Add(mergedCondition);
        }
        
        // 复制动作（动作参数也需要合并）
        foreach (var action in baseDef.actions)
        {
            var mergedAction = new ActionBlock
            {
                actionId = action.actionId,
                @params = MergeParams(sourceEffects, action.@params, stackingRuleMap, false, action.actionId)
            };
            mergedDef.actions.Add(mergedAction);
        }
        
        return mergedDef;
    }
    
    /// <summary>
    /// 合并参数：根据堆叠规则合并参数值
    /// </summary>
    private SerializableStringFloatMap MergeParams(
        List<(QualityEffectDefinition def, int quality, int stacks)> sourceEffects,
        SerializableStringFloatMap baseParams,
        Dictionary<string, string> stackingRuleMap,
        bool isCondition,
        string contextId)
    {
        var mergedParams = new SerializableStringFloatMap
        {
            contextId = contextId,
            paramEntries = new List<EffectParamEntry>()
        };
        
        // 收集所有参数名（从所有源效果中）
        var allParamKeys = new HashSet<string>();
        foreach (var (def, _, _) in sourceEffects)
        {
            SerializableStringFloatMap sourceParams = null;
            if (isCondition && def.conditions != null)
            {
                // 找到对应的条件
                foreach (var cond in def.conditions)
                {
                    if (cond.conditionId == contextId)
                    {
                        sourceParams = cond.@params;
                        break;
                    }
                }
            }
            else if (!isCondition && def.actions != null)
            {
                // 找到对应的动作
                foreach (var act in def.actions)
                {
                    if (act.actionId == contextId)
                    {
                        sourceParams = act.@params;
                        break;
                    }
                }
            }
            
            if (sourceParams != null)
            {
                foreach (var entry in sourceParams.paramEntries)
                {
                    allParamKeys.Add(entry.key);
                }
            }
        }
        
        // 如果没找到参数，使用 baseParams 作为参考
        if (allParamKeys.Count == 0 && baseParams != null)
        {
            foreach (var entry in baseParams.paramEntries)
            {
                allParamKeys.Add(entry.key);
            }
        }
        
        // 对每个参数应用堆叠规则
        foreach (var paramKey in allParamKeys)
        {
            var floatValues = new List<(float value, int quality, int stacks)>();
            var stringValues = new List<(string value, int quality, int stacks)>();
            EffectParamType entryType = EffectParamType.Float;
            
            foreach (var (def, quality, stacks) in sourceEffects)
            {
                SerializableStringFloatMap sourceParams = null;
                if (isCondition && def.conditions != null)
                {
                    foreach (var cond in def.conditions)
                    {
                        if (cond.conditionId == contextId)
                        {
                            sourceParams = cond.@params;
                            break;
                        }
                    }
                }
                else if (!isCondition && def.actions != null)
                {
                    foreach (var act in def.actions)
                    {
                        if (act.actionId == contextId)
                        {
                            sourceParams = act.@params;
                            break;
                        }
                    }
                }
                
                float value = 0f;
                if (sourceParams != null)
                {
                    value = sourceParams.GetOrDefault(paramKey, 0f);
                }
                else if (baseParams != null)
                {
                    value = baseParams.GetOrDefault(paramKey, 0f);
                }
                
                EffectParamEntry paramEntry = null;
                if (sourceParams != null)
                {
                    paramEntry = sourceParams.paramEntries.FirstOrDefault(e => e.key == paramKey);
                }
                if (paramEntry == null && baseParams != null)
                {
                    paramEntry = baseParams.paramEntries.FirstOrDefault(e => e.key == paramKey);
                }
                
                if (paramEntry != null && paramEntry.type == EffectParamType.String)
                {
                    entryType = EffectParamType.String;
                    string stringValue = sourceParams != null
                        ? paramEntry.stringValue
                        : baseParams.GetStringOrDefault(paramKey, paramEntry.stringValue);
                    stringValues.Add((stringValue, quality, stacks));
                }
                else
                {
                    entryType = EffectParamType.Float;
                    floatValues.Add((value, quality, stacks));
                }
            }
            
            EffectParamEntry entry;
            if (entryType == EffectParamType.String)
            {
                // 字符串参数：默认取最高品质（或第一个非空值）
                string mergedString = stringValues
                    .OrderByDescending(v => v.quality)
                    .ThenByDescending(v => v.stacks)
                    .Select(v => v.value)
                    .FirstOrDefault(v => !string.IsNullOrEmpty(v));
                
                if (string.IsNullOrEmpty(mergedString) && baseParams != null)
                {
                    mergedString = baseParams.GetStringOrDefault(paramKey, "");
                }
                
                entry = new EffectParamEntry
                {
                    key = paramKey,
                    type = EffectParamType.String,
                    stringValue = mergedString,
                    contextId = contextId
                };
            }
            else
            {
                // 获取该参数的堆叠规则（默认使用 Add）
                string ruleName = "Add";
                if (stackingRuleMap != null && stackingRuleMap.TryGetValue(paramKey, out var customRule))
                {
                    ruleName = customRule;
                }
                
                var rule = StackingRuleRegistry.Get(ruleName);
                if (rule == null)
                {
                    rule = StackingRuleRegistry.Get("Add"); // 回退到默认规则
                }
                
                // 如果仍然为 null（说明规则未注册），使用默认的 Add 规则
                if (rule == null)
                {
                    Debug.LogWarning($"[MergeParams] 堆叠规则 '{ruleName}' 和 'Add' 都未注册，使用默认 Add 规则");
                    rule = new AddStackingRule();
                }
                
                if (floatValues.Count == 0 && baseParams != null)
                {
                    float baseValue = baseParams.GetOrDefault(paramKey, 0f);
                    floatValues.Add((baseValue, quality: 0, stacks: 1));
                }
                
                float mergedValue = rule.MergeValues(floatValues, paramKey);
                
                entry = new EffectParamEntry
                {
                    key = paramKey,
                    type = EffectParamType.Float,
                    floatValue = mergedValue,
                    contextId = contextId
                };
            }
            
            mergedParams.paramEntries.Add(entry);
        }
        
        return mergedParams;
    }

    // ========== 对外触发API：由战斗系统在相应时机调用 ==========
    
    /// <summary>
    /// 当击中敌人时触发
    /// </summary>
    /// <param name="target">击中的敌人</param>
    /// <param name="baseDamage">击中敌人造成的伤害</param>
    public void RaiseOnHitDealt(object target, float baseDamage)
    {
        var evt = new QualityEventContext { owner = _owner, target = target, baseDamage = baseDamage };
        HandleTrigger("OnHitDealt", evt);
    }

    /// <summary>
    /// 当受到伤害时触发
    /// </summary>
    /// <param name="attacker">受到伤害的来源</param>
    /// <param name="damage">受到的伤害</param>
    public void RaiseOnHitReceived(object attacker, float damage)
    {
        var evt = new QualityEventContext { owner = _owner, target = attacker, baseDamage = damage };
        HandleTrigger("OnHitReceived", evt);
    }


    /// <summary>
    /// 当造成暴击伤害时触发
    /// </summary>
    /// <param name="target">受到暴击伤害的敌人</param>
    /// <param name="critDamage">造成的暴击伤害</param>
    public void RaiseOnCrit(object target, float critDamage)
    {
        var evt = new QualityEventContext { owner = _owner, target = target, baseDamage = critDamage };
        HandleTrigger("OnCrit", evt);
    }

    /// <summary>
    /// 当击杀敌人时触发
    /// </summary>
    /// <param name="target">击杀的敌人</param>
    public void RaiseOnKill(object target)
    {
        var evt = new QualityEventContext { owner = _owner, target = target, baseDamage = 0f };
        HandleTrigger("OnKill", evt);
    }

    /// <summary>
    /// 当自身死亡时触发
    /// </summary>
    public void RaiseOnDeath()
    {
        var evt = new QualityEventContext { owner = _owner, target = null, baseDamage = 0f };
        HandleTrigger("OnDeath", evt);
    }

    /// <summary>
    /// 当释放技能时触发
    /// </summary>
    /// <param name="skillId">释放的技能ID</param>
    public void RaiseOnSkillCast(string skillId)
    {
        var evt = new QualityEventContext { owner = _owner, target = null, baseDamage = 0f };
        HandleTrigger("OnSkillCast", evt);
    }

    /// <summary>
    /// 当技能命中敌人时触发
    /// </summary>
    /// <param name="target">命中的敌人</param>
    /// <param name="skillDamage">技能造成的伤害</param>
    public void RaiseOnSkillHit(object target, float skillDamage)
    {
        var evt = new QualityEventContext { owner = _owner, target = target, baseDamage = skillDamage };
        HandleTrigger("OnSkillHit", evt);
    }

    /// <summary>
    /// 当受到治疗时触发
    /// </summary>
    /// <param name="healAmount">受到的治疗量</param>
    public void RaiseOnHeal(float healAmount)
    {
        var evt = new QualityEventContext { owner = _owner, target = null, baseDamage = healAmount };
        HandleTrigger("OnHeal", evt);
    }

    /// <summary>
    /// 当装备品质道具时触发
    /// </summary>
    public void RaiseOnEquip()
    {
        Debug.Log($"[PlayerQualityEffectModule] ★ RaiseOnEquip 被调用，当前激活效果数量: {_activeEffects.Count}");
        var evt = new QualityEventContext { owner = _owner, target = null, baseDamage = 0f };
        HandleTrigger("OnEquip", evt);
        Debug.Log($"[PlayerQualityEffectModule] ★ RaiseOnEquip 处理完成");
    }

    /// <summary>
    /// 当卸下品质道具时触发
    /// </summary>
    public void RaiseOnUnequip()
    {
        var evt = new QualityEventContext { owner = _owner, target = null, baseDamage = 0f };
        HandleTrigger("OnUnequip", evt);
    }

    /// <summary>
    /// 当品质道具堆叠层数变化时触发
    /// </summary>
    /// <param name="newStacks"></param>
    public void RaiseOnStackChanged(int newStacks)
    {
        var evt = new QualityEventContext { owner = _owner, target = null, baseDamage = newStacks };
        HandleTrigger("OnStackChanged", evt);
    }

    /// <summary>
    /// 当进入区域时触发
    /// </summary>
    /// <param name="areaId">进入的区域ID</param>
    public void RaiseOnEnterArea(string areaId)
    {
        var evt = new QualityEventContext { owner = _owner, target = null, baseDamage = 0f };
        HandleTrigger("OnEnterArea", evt);
    }

    /// <summary>
    /// 当使用主动道具时触发
    /// </summary>
    /// <param name="itemId"></param>
    public void RaiseOnUseItem(string itemId)
    {
        var evt = new QualityEventContext { owner = _owner, target = null, baseDamage = 0f };
        HandleTrigger("OnUseItem", evt);
    }

    /// <summary>
    /// 定时触发
    /// </summary>
    /// <param name="interval">触发间隔</param>
    public void RaiseTick(float interval)
    {
        var evt = new QualityEventContext { owner = _owner, target = null, baseDamage = 0f };
        HandleTrigger($"OnTick:{interval}", evt);
    }

    /// <summary>
    /// 处理触发器
    /// </summary>
    /// <param name="triggerId">触发器ID</param>
    /// <param name="evt">触发事件上下文</param>
    private void HandleTrigger(string triggerId, in QualityEventContext evt)
    {
        float now = Time.time;

        //遍历当前角色装备的所有品质道具效果
        for (int i = 0; i < _activeEffects.Count; i++)
        {
            //获取当前品质道具效果的定义、道具堆叠层数和品质
            var (def, stacks, quality) = _activeEffects[i];
            if (def.triggers == null) continue;

            //遍历当前品质道具效果的定义的触发器列表
            bool matched = false;
            for (int t = 0; t < def.triggers.Count; t++)
            {
                var trig = def.triggers[t];
                //如果触发器ID匹配，则认为当前品质道具效果符合触发时机
                if (trig == triggerId) { matched = true; break; }
                //如果触发器ID是定时触发，则认为当前品质道具效果符合触发时机
                if (triggerId.StartsWith("OnTick:") && trig.StartsWith("OnTick:")) matched = true;
            }
            if (!matched) continue; //如果当前品质道具效果的定义的触发器列表中不包含该触发器，则认为当前品质道具效果不符合触发时机，跳过

            // 冷却检查
            if (!string.IsNullOrEmpty(def.id) && def.cooldown > 0f)
            {
                if (_cooldowns.TryGetValue(def.id, out var nextReady) && nextReady > now) continue; //如果当前品质道具效果的下次可触发时间大于当前时间 则冷却还没结束，跳过
            }

            //创建当前品质道具效果的运行时上下文
            var ctx = new QualityEffectRuntimeContext
            {
                stacks = stacks,
                quality = quality, // 使用实际品质值（0=白色, 1=绿色, 2=蓝色, 3=紫色, 4=红色）
                baseDamage = evt.baseDamage,
                ownerLevel = 0,
                targetMaxHp = 0,
                effectId = def.id ?? ""
            };

            // 触发条件检查
            bool pass = true;
            if (def.conditions != null)
            {
                //遍历当前品质道具效果的定义的条件列表 所有触发条件必须全部通过才能执行
                for (int c = 0; c < def.conditions.Count; c++)
                {
                    var cond = def.conditions[c]; //获取当前品质道具效果的定义的条件列表中的第c个条件
                    if (string.IsNullOrEmpty(cond.conditionId)) continue;
                    if (!QualityEffectRegistry.TryGetCondition(cond.conditionId, out var evaluator)) continue;
                    if (!evaluator.Evaluate(cond, ctx, evt)) //拿到对应的ConditionEvaluator，并执行评估
                    {
                        //如果条件评估返回false 则认为当前品质道具效果不符合触发条件，跳过
                        pass = false; 
                        break; 
                    } 
                }
            }
            if (!pass) continue;

            // 触发时机 触发条件通过 执行动作
            if (def.actions != null)
            {
                Debug.Log($"[HandleTrigger] ★ 触发器 '{triggerId}' 匹配成功，准备执行 {def.actions.Count} 个动作");
                for (int a = 0; a < def.actions.Count; a++) //遍历当前品质道具效果的定义的动作列表
                {
                    var action = def.actions[a]; //获取当前品质道具效果的定义的动作列表中的第a个动作
                    if (string.IsNullOrEmpty(action.actionId))
                    {
                        Debug.LogWarning($"[HandleTrigger] 动作 #{a} 的 actionId 为空，跳过");
                        continue; //如果动作ID为空，则跳过
                    }
                    if (!QualityEffectRegistry.TryGetAction(action.actionId, out var exec))
                    {
                        Debug.LogWarning($"[HandleTrigger] 未找到动作执行器: {action.actionId}");
                        continue; //拿到对应的ActionExecutor，并执行
                    }
                    Debug.Log($"[HandleTrigger] ★ 执行动作: {action.actionId}");
                    exec.Execute(action, ctx, evt); //执行动作
                }
            }

            // 更新当前道具效果的冷却
            if (!string.IsNullOrEmpty(def.id) && def.cooldown > 0f)
            {
                _cooldowns[def.id] = now + def.cooldown;
            }
        }
    }
}


