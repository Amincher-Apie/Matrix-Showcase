using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 负责服务器权威的背包容器管理与网络同步，抽象全部背包操作。
/// </summary>
public class NetworkInventory : NetworkBehaviour
{
    /// <summary>
    /// 武器槽固定容量（主武器 + 副武器）。
    /// </summary>
    private const int WeaponSlotCapacity = 2;

    /// <summary>
    /// 消耗品槽位数量的同步变量（默认 3，最大 8）。
    /// </summary>
    public NetworkVariable<int> ConsumableSlotCapacity = new NetworkVariable<int>(3);

    /// <summary>
    /// 品质道具槽位数量的同步变量（默认 6，最大 12）。
    /// </summary>
    public NetworkVariable<int> QualityItemSlotCapacity = new NetworkVariable<int>(6);

    /// <summary>
    /// 局内货币（服务器权威）。商店购买、出售、刷新均通过此字段校验和增减。
    /// </summary>
    public NetworkVariable<int> InGameCurrency = new NetworkVariable<int>(0);

    public void AddCurrencyServerInternal(int amount)
    {
        if (!IsServer || amount <= 0)
            return;

        InGameCurrency.Value = Mathf.Max(0, InGameCurrency.Value + amount);
    }

    /// <summary>
    /// 武器槽容器（固定长度）。
    /// </summary>
    public NetworkList<InventorySlot> WeaponSlots { get; private set; }

    /// <summary>
    /// 消耗品槽容器（随容量动态调整）。
    /// </summary>
    public NetworkList<InventorySlot> ConsumableSlots { get; private set; }

    /// <summary>
    /// 品质道具槽容器（随容量动态调整）。
    /// </summary>
    public NetworkList<InventorySlot> QualityItemSlots { get; private set; }

    /// <summary>
    /// 主动道具槽（唯一）。
    /// </summary>
    public NetworkVariable<InventorySlot> ActiveItemSlot { get; private set; }

    /// <summary>
    /// 备选区容器（无限容量）。道具可在此与装备区之间移动。
    /// </summary>
    public NetworkList<InventorySlot> BackupSlots { get; private set; }

    private bool _eventsRegistered;

    /// <summary>
    /// 背包任意容器变化时触发（包含扩容与物品更新）。
    /// </summary>
    public event Action OnInventoryChanged;

    /// <summary>
    /// 指定容器发生变化时触发，参数为容器类型。
    /// </summary>
    public event Action<EnumItemType> OnContainerChanged;

    /// <summary>
    /// 初始化网络容器实例。
    /// </summary>
    private void Awake()
    {
        EnsureContainersInitialized();
    }

    public void EnsureContainersInitialized()
    {
        WeaponSlots ??= new NetworkList<InventorySlot>();
        ConsumableSlots ??= new NetworkList<InventorySlot>();
        QualityItemSlots ??= new NetworkList<InventorySlot>();
        ActiveItemSlot ??= new NetworkVariable<InventorySlot>(InventorySlot.Empty);
        BackupSlots ??= new NetworkList<InventorySlot>();
    }

    /// <summary>
    /// NGO 生成回调，服务器端初始化槽位并注册监听。
    /// </summary>
    public override void OnNetworkSpawn()
    {
        EnsureContainersInitialized();
        base.OnNetworkSpawn();

        if (IsServer)
        {
            InitializeWeaponSlots();
            AdjustContainerSize(ConsumableSlots, ConsumableSlotCapacity.Value);
            AdjustContainerSize(QualityItemSlots, QualityItemSlotCapacity.Value);
        }

        RegisterEvents();
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// NGO 销毁回调，注销事件并释放引用。
    /// </summary>
    public override void OnNetworkDespawn()
    {
        UnregisterEvents();
        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        UnregisterEvents();
        base.OnDestroy();
    }

    private void RegisterEvents()
    {
        if (_eventsRegistered)
            return;

        if (IsServer)
        {
            ConsumableSlotCapacity.OnValueChanged += OnConsumableCapacityChanged;
            QualityItemSlotCapacity.OnValueChanged += OnQualityCapacityChanged;
        }

        WeaponSlots.OnListChanged += OnWeaponListChanged;
        ConsumableSlots.OnListChanged += OnConsumableListChanged;
        QualityItemSlots.OnListChanged += OnQualityListChanged;
        ActiveItemSlot.OnValueChanged += OnActiveSlotChanged;
        _eventsRegistered = true;
    }

    private void UnregisterEvents()
    {
        if (!_eventsRegistered)
            return;

        if (IsServer)
        {
            ConsumableSlotCapacity.OnValueChanged -= OnConsumableCapacityChanged;
            QualityItemSlotCapacity.OnValueChanged -= OnQualityCapacityChanged;
        }

        if (WeaponSlots != null)
            WeaponSlots.OnListChanged -= OnWeaponListChanged;
        if (ConsumableSlots != null)
            ConsumableSlots.OnListChanged -= OnConsumableListChanged;
        if (QualityItemSlots != null)
            QualityItemSlots.OnListChanged -= OnQualityListChanged;
        if (ActiveItemSlot != null)
            ActiveItemSlot.OnValueChanged -= OnActiveSlotChanged;

        _eventsRegistered = false;
    }

    /// <summary>
    /// 初始化武器槽为固定的空槽。
    /// </summary>
    private void InitializeWeaponSlots()
    {
        WeaponSlots.Clear();
        for (int i = 0; i < WeaponSlotCapacity; i++)
        {
            WeaponSlots.Add(InventorySlot.Empty);
        }
    }

    /// <summary>
    /// 武器槽列表发生变化时的处理。
    /// </summary>
    private void OnWeaponListChanged(NetworkListEvent<InventorySlot> change)
    {
        RaiseContainerChanged(EnumItemType.Weapon);
    }

    /// <summary>
    /// 消耗品槽发生变化时的处理。
    /// </summary>
    private void OnConsumableListChanged(NetworkListEvent<InventorySlot> change)
    {
        RaiseContainerChanged(EnumItemType.Consumable);
    }

    /// <summary>
    /// 品质道具槽发生变化时的处理。
    /// </summary>
    private void OnQualityListChanged(NetworkListEvent<InventorySlot> change)
    {
        Debug.Log($"[NetworkInventory] ★ OnQualityListChanged 被触发: EventType={change.Type}, IsServer={IsServer}");
        RaiseContainerChanged(EnumItemType.QualityPassive);
    }

    /// <summary>
    /// 主动槽发生变化时的处理。
    /// </summary>
    private void OnActiveSlotChanged(InventorySlot previousValue, InventorySlot newValue)
    {
        RaiseContainerChanged(EnumItemType.Active);
    }

    /// <summary>
    /// 统一触发容器变化与背包变化事件。
    /// </summary>
    /// <param name="type">发生变化的容器类型。</param>
    private void RaiseContainerChanged(EnumItemType type)
    {
        Debug.Log($"[NetworkInventory] ★ RaiseContainerChanged: type={type}, OnInventoryChanged订阅数={(OnInventoryChanged?.GetInvocationList().Length ?? 0)}");
        OnContainerChanged?.Invoke(type);
        OnInventoryChanged?.Invoke();
        Debug.Log($"[NetworkInventory] ★ OnInventoryChanged 事件已触发");
    }

    /// <summary>
    /// 清理所有网络容器引用，避免内存泄露。
    /// </summary>
    private void DisposeContainers()
    {
        WeaponSlots?.Dispose();
        ConsumableSlots?.Dispose();
        QualityItemSlots?.Dispose();
        BackupSlots?.Dispose();
    }

    /// <summary>
    /// 消耗品容量值改变时，重新调整容器并广播事件。
    /// </summary>
    private void OnConsumableCapacityChanged(int previousValue, int newValue)
    {
        AdjustContainerSize(ConsumableSlots, newValue);
        RaiseContainerChanged(EnumItemType.Consumable);
    }

    /// <summary>
    /// 品质道具容量值改变时，重新调整容器并广播事件。
    /// </summary>
    private void OnQualityCapacityChanged(int previousValue, int newValue)
    {
        AdjustContainerSize(QualityItemSlots, newValue);
        RaiseContainerChanged(EnumItemType.QualityPassive);
    }

    /// <summary>
    /// 根据容量上限在服务器端调整容器长度。
    /// </summary>
    /// <param name="container">目标容器。</param>
    /// <param name="newCapacity">最新容量值。</param>
    private void AdjustContainerSize(NetworkList<InventorySlot> container, int newCapacity)
    {
        if (!IsServer)
        {
            return;
        }

        while (container.Count < newCapacity)
        {
            container.Add(InventorySlot.Empty);
        }

        while (container.Count > newCapacity)
        {
            int lastIndex = container.Count - 1;
            var slot = container[lastIndex];
            if (slot.IsEmpty())
            {
                container.RemoveAt(lastIndex);
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// 服务器端添加物品到指定容器。
    /// </summary>
    /// <param name="item">待添加的物品实例。</param>
    /// <param name="amount">数量（消耗品堆叠使用）。</param>
    /// <param name="rpcParams">RPC 参数。</param>
    [ServerRpc(RequireOwnership = true)]
    public void AddItemServerRpc(InventoryItem item, long amount = 1, ServerRpcParams rpcParams = default)
    {
        TryAddItemServer(item, amount);
    }

    /// <summary>
    /// 服务端内部添加物品入口，供掉落、商店、任务奖励等服务器系统直接调用。
    /// </summary>
    public bool TryAddItemServer(InventoryItem item, long amount = 1)
    {
        if (!IsServer)
        {
            return false;
        }

        amount = Math.Max(1, amount);

        if (item.itemType == EnumItemType.Active)
        {
            SetActiveSlot(item);
            RaiseItemPickedUp(item, amount);
            return true;
        }

        var container = GetContainerByType(item.itemType);
        if (container == null)
        {
            Debug.LogWarning($"未找到对应容器：{item.itemType}");
            return false;
        }

        int capacity = GetContainerCapacity(item.itemType);
        if (container.Count > capacity)
        {
            AdjustContainerSize(container, capacity);
        }

        if (item.isStackable && TryStackItem(container, item, amount))
        {
            RaiseItemPickedUp(item, amount);
            return true;
        }

        if (TryFillEmptySlot(container, item, amount, capacity))
        {
            RaiseItemPickedUp(item, amount);
            // 成功添加道具，事件会在 NetworkList 变化时自动触发
            // 但为了确保效果系统能立即响应，我们也在服务器端手动触发一次
            if (item.itemType == EnumItemType.QualityPassive)
            {
                Debug.Log($"[NetworkInventory] 品质道具已添加，手动触发效果重建");
                RaiseContainerChanged(EnumItemType.QualityPassive);
                
                // 额外：直接查找并调用 PlayerQualityEffectModule 的效果重建
                var playerProxy = GetComponent<PlayerNetworkProxy>();
                if (playerProxy != null && playerProxy.PlayerActor != null)
                {
                    var qualityModule = playerProxy.PlayerActor.QualityEffectModule;
                    if (qualityModule != null)
                    {
                        Debug.Log($"[NetworkInventory] 直接调用 PlayerQualityEffectModule.RebuildFromInventory");
                        qualityModule.RebuildFromInventory();
                    }
                    else
                    {
                        Debug.LogWarning($"[NetworkInventory] PlayerActor.QualityEffectModule 为空");
                    }
                }
                else
                {
                    Debug.LogWarning($"[NetworkInventory] 未找到 PlayerNetworkProxy 或 PlayerActor");
                }
            }

            return true;
        }
        else
        {
            Debug.LogWarning($"容器已满：{item.itemType}");
            NotifyClientTipsClientRpc("栏位已满");
            return false;
        }
    }

    /// <summary>
    /// 服务器端根据物品 ID 扣减或移除物品。
    /// </summary>
    /// <param name="itemId">物品 ID。</param>
    /// <param name="amount">扣减数量。</param>
    /// <param name="rpcParams">RPC 参数。</param>
    [ServerRpc(RequireOwnership = true)]
    public void RemoveItemServerRpc(string itemId, long amount = 1, ServerRpcParams rpcParams = default)
    {
        if (!IsServer)
        {
            return;
        }

        var (slot, container, index) = FindItemInContainers(itemId);
        if (slot.isNull)
        {
            return;
        }

        if (container == null)
        {
            ActiveItemSlot.Value = InventorySlot.Empty;
            return;
        }

        bool isQualityItem = slot.item.itemType == EnumItemType.QualityPassive;
        
        if (slot.item.isStackable)
        {
            slot.amount = Math.Max(0, slot.amount - amount);
            if (slot.amount <= 0)
            {
                container[index] = InventorySlot.Empty;
            }
            else
            {
                container[index] = slot;
            }
        }
        else
        {
            container[index] = InventorySlot.Empty;
        }

        RaiseItemRemoved(itemId, slot.item.itemType);
        
        // 如果是品质道具被移除，手动触发效果重建
        if (isQualityItem)
        {
            Debug.Log($"[NetworkInventory] 品质道具已移除，手动触发效果重建");
            RaiseContainerChanged(EnumItemType.QualityPassive);
            
            // 额外：直接查找并调用 PlayerQualityEffectModule 的效果重建
            var playerProxy = GetComponent<PlayerNetworkProxy>();
            if (playerProxy != null && playerProxy.PlayerActor != null)
            {
                var qualityModule = playerProxy.PlayerActor.QualityEffectModule;
                if (qualityModule != null)
                {
                    Debug.Log($"[NetworkInventory] 直接调用 PlayerQualityEffectModule.RebuildFromInventory (移除道具)");
                    qualityModule.RebuildFromInventory();
                }
                else
                {
                    Debug.LogWarning($"[NetworkInventory] PlayerActor.QualityEffectModule 为空");
                }
            }
            else
            {
                Debug.LogWarning($"[NetworkInventory] 未找到 PlayerNetworkProxy 或 PlayerActor");
            }
        }
    }

    /// <summary>
    /// 拓展指定类型道具槽的容量（服务器权限）。
    /// </summary>
    /// <param name="targetType">需扩展的槽位类型。</param>
    /// <param name="addCount">扩展槽位数量。</param>
    /// <param name="rpcParams">RPC 参数。</param>
    [ServerRpc(RequireOwnership = true)]
    public void ExpandSlotCapacityServerRpc(EnumItemType targetType, int addCount = 1, ServerRpcParams rpcParams = default)
    {
        if (!IsServer)
        {
            return;
        }

        switch (targetType)
        {
            case EnumItemType.Consumable:
                ConsumableSlotCapacity.Value = Mathf.Min(8, ConsumableSlotCapacity.Value + addCount);
                break;
            case EnumItemType.QualityPassive:
                QualityItemSlotCapacity.Value = Mathf.Min(12, QualityItemSlotCapacity.Value + addCount);
                break;
            default:
                NotifyClientTipsClientRpc("该栏位无法扩展");
                break;
        }
    }

    /// <summary>
    /// 在服务器端执行合成逻辑，消耗材料并生成产物。
    /// </summary>
    /// <param name="itemId1">材料一的物品 ID。</param>
    /// <param name="itemId2">材料二的物品 ID。</param>
    /// <param name="rpcParams">RPC 参数。</param>
    [ServerRpc(RequireOwnership = true)]
    public void SynthesizeItemServerRpc(string itemId1, string itemId2, ServerRpcParams rpcParams = default)
    {
        if (!IsServer)
        {
            return;
        }

        var (slot1, container1, index1) = FindItemInContainers(itemId1);
        var (slot2, container2, index2) = FindItemInContainers(itemId2);

        if (slot1.isNull || slot2.isNull)
        {
            NotifyClientTipsClientRpc("合成材料不足");
            return;
        }

        var recipe = SynthesisRecipeManager.Instance?.GetRecipe(slot1.item.itemId.ToString(), slot2.item.itemId.ToString());
        if (recipe == null)
        {
            NotifyClientTipsClientRpc("无法合成该组合");
            return;
        }

        if (recipe.ProductQuality == EnumQualityLevel.Legendary)
        {
            NotifyClientTipsClientRpc("传奇道具不可合成");
            return;
        }

        ConsumeSlotItem(container1, index1, slot1);
        ConsumeSlotItem(container2, index2, slot2);

        var productSO = SOManager.Instance?.GetSOById<BaseInventoryItemSO>(recipe.ProductId);
        if (productSO == null)
        {
            Debug.LogError($"未找到合成产物：{recipe.ProductId}");
            return;
        }

        AddItemServerRpc(new InventoryItem(productSO), 1);
        NotifyClientTipsClientRpc($"合成成功：{productSO.name}");
    }

    /// <summary>
    /// 消耗指定槽位的物品（堆叠则减 1，其他直接清空）。
    /// </summary>
    private void ConsumeSlotItem(NetworkList<InventorySlot> container, int index, InventorySlot slot)
    {
        if (container == null)
        {
            ActiveItemSlot.Value = InventorySlot.Empty;
            return;
        }

        if (slot.item.isStackable && slot.amount > 1)
        {
            slot.amount--;
            container[index] = slot;
        }
        else
        {
            container[index] = InventorySlot.Empty;
        }
    }

    /// <summary>
    /// 广播服务器确认的拾取事件，供 UI 与音效等表现层刷新。
    /// </summary>
    private void RaiseItemPickedUp(InventoryItem item, long amount)
    {
        var clientRpcParams = BuildOwnerClientRpcParams();
        NotifyItemPickedUpClientRpc(item.itemId.ToString(), Mathf.Max(1, (int)amount), item.itemType, clientRpcParams);
    }

    /// <summary>
    /// 广播服务器确认的移除事件，供 UI 与音效等表现层刷新。
    /// </summary>
    private void RaiseItemRemoved(string itemId, EnumItemType itemType)
    {
        var clientRpcParams = BuildOwnerClientRpcParams();
        NotifyItemRemovedClientRpc(itemId, itemType, clientRpcParams);
    }

    private ClientRpcParams BuildOwnerClientRpcParams()
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { OwnerClientId }
            }
        };
    }

    [ClientRpc]
    private void NotifyItemPickedUpClientRpc(string itemId, int amount, EnumItemType itemType, ClientRpcParams clientRpcParams = default)
    {
        EventCenter.Instance.Trigger(EventName.ItemPickedUp, OwnerClientId, itemId, amount);
    }

    [ClientRpc]
    private void NotifyItemRemovedClientRpc(string itemId, EnumItemType itemType, ClientRpcParams clientRpcParams = default)
    {
        EventCenter.Instance.Trigger(EventName.ItemRemoved, new ItemRemovedEvt
        {
            itemId = itemId,
            ownerId = OwnerClientId,
            itemType = itemType
        });
    }

    /// <summary>
    /// 将主动道具槽设置为指定物品。
    /// </summary>
    private void SetActiveSlot(InventoryItem item)
    {
        ActiveItemSlot.Value = new InventorySlot(item, 1);
    }

    /// <summary>
    /// 尝试在已有槽位中堆叠同类物品。
    /// </summary>
    private bool TryStackItem(NetworkList<InventorySlot> container, InventoryItem item, long amount)
    {
        for (int i = 0; i < container.Count; i++)
        {
            var slot = container[i];
            if (slot.IsStackableWith(item))
            {
                slot.AddAmount(amount);
                slot.amount = System.Math.Min(slot.amount, long.MaxValue);
                container[i] = slot;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 尝试向容器写入新物品（寻找空槽或追加）。
    /// </summary>
    private bool TryFillEmptySlot(NetworkList<InventorySlot> container, InventoryItem item, long amount, int capacity)
    {
        for (int i = 0; i < container.Count; i++)
        {
            var slot = container[i];
            if (slot.IsEmpty())
            {
                container[i] = new InventorySlot(item, amount);
                return true;
            }
        }

        if (container.Count < capacity)
        {
            container.Add(new InventorySlot(item, amount));
            return true;
        }

        return false;
    }

    /// <summary>
    /// 根据类型获取对应容器引用。
    /// </summary>
    private NetworkList<InventorySlot> GetContainerByType(EnumItemType type)
    {
        return type switch
        {
            EnumItemType.Weapon => WeaponSlots,
            EnumItemType.Consumable => ConsumableSlots,
            EnumItemType.QualityPassive => QualityItemSlots,
            _ => null
        };
    }

    /// <summary>
    /// 获取指定类型容器的容量上限。
    /// </summary>
    private int GetContainerCapacity(EnumItemType type)
    {
        return type switch
        {
            EnumItemType.Weapon => WeaponSlotCapacity,
            EnumItemType.Consumable => ConsumableSlotCapacity.Value,
            EnumItemType.QualityPassive => QualityItemSlotCapacity.Value,
            EnumItemType.Active => 1,
            _ => 0
        };
    }

    /// <summary>
    /// 在所有容器中查找物品，并返回所在槽位及容器信息。
    /// </summary>
    private (InventorySlot slot, NetworkList<InventorySlot> container, int index) FindItemInContainers(string itemId)
    {
        FixedString128Bytes targetId = itemId;

        for (int i = 0; i < WeaponSlots.Count; i++)
        {
            var slot = WeaponSlots[i];
            if (!slot.isNull && slot.item.itemId.Equals(targetId))
            {
                return (slot, WeaponSlots, i);
            }
        }

        for (int i = 0; i < ConsumableSlots.Count; i++)
        {
            var slot = ConsumableSlots[i];
            if (!slot.isNull && slot.item.itemId.Equals(targetId))
            {
                return (slot, ConsumableSlots, i);
            }
        }

        for (int i = 0; i < QualityItemSlots.Count; i++)
        {
            var slot = QualityItemSlots[i];
            if (!slot.isNull && slot.item.itemId.Equals(targetId))
            {
                return (slot, QualityItemSlots, i);
            }
        }

        if (!ActiveItemSlot.Value.isNull && ActiveItemSlot.Value.item.itemId.Equals(targetId))
        {
            return (ActiveItemSlot.Value, null, -1);
        }

        return (InventorySlot.Empty, null, -1);
    }

    [ClientRpc]
    /// <summary>
    /// 简单的客户端提示 RPC，可替换为 UI 系统。
    /// </summary>
    private void NotifyClientTipsClientRpc(string message)
    {
        Debug.Log(message);
    }
}

