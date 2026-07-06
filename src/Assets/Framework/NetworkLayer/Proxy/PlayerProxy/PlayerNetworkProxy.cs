using System;
using System.Collections;
using System.Collections.Generic;
using Framework.UI.Core;
using Matrix.Interaction;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 玩家网络代理，处理玩家相关的网络通信。
/// 当前阶段在保留原有武器与技能同步逻辑的同时，补充服务端可攻击目标注册。
/// </summary>
public class PlayerNetworkProxy : NetworkProxyBase
{
    private static readonly string[] WeaponFollowPointCandidateNames =
    {
        "WeaponFollowPoint",
        "WeaponSocket",
        "WeaponMount",
        "WeaponPoint",
        "RightHandWeaponPoint",
        "RightHandSocket",
        "RightHand",
        "mixamorig:RightHand",
        "Bip001 R Hand"
    };

    /// <summary>
    /// 当前网络代理关联的玩家逻辑对象。
    /// </summary>
    public PlayerActor PlayerActor { get; private set; }

    /// <summary>
    /// 服务端玩家属性模块引用。
    /// </summary>
    [field: SerializeField]
    public ServerPlayerAttributeModule ServerPlayerAttributeModule { get; private set; }
    
    /// <summary>
    /// 服务端战斗模块引用。
    /// </summary>
    [field: SerializeField]
    public ServerCombatModule ServerCombatModule { get; private set; }

    /// <summary>
    /// 服务端武器运行时引用。
    /// </summary>
    [field: SerializeField]
    public ServerWeaponRuntime ServerWeaponRuntime { get; private set; }
    
    /// <summary>
    /// 服务端技能模块引用。
    /// </summary>
    [field: SerializeField]
    public ServerSkillModule ServerSkillModule { get; private set; }
    
    /// <summary>
    /// 服务端 Buff 模块引用。
    /// </summary>
    [field: SerializeField]
    public ServerBuffModule ServerBuffModule { get; private set; }
    
    /// <summary>
    /// 网络背包引用。
    /// </summary>
    [field: SerializeField]
    public NetworkInventory NetworkInventory { get; private set; }
    
    /// <summary>
    /// 武器挂点。
    /// </summary>
    [field: SerializeField]
    public Transform WeaponFollowPoint { get; set; }
    
    /// <summary>
    /// 服务端权威记录的当前武器 ID。
    /// </summary>
    private readonly NetworkVariable<FixedString32Bytes> _weaponId =
        new NetworkVariable<FixedString32Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
    
    /// <summary>
    /// 在网络生成前暂存待应用的武器 ID。
    /// </summary>
    private string _pendingWeaponId;

    /// <summary>
    /// 当前客户端侧实例化的武器表现对象。
    /// </summary>
    private GameObject _currentWeaponView;

    /// <summary>
    /// 设置与该网络代理关联的玩家逻辑对象。
    /// </summary>
    /// <param name="playerActor">要绑定的玩家逻辑对象。</param>
    public void SetPlayerActor(PlayerActor playerActor)
    {
        PlayerActor = playerActor;
        base.SetLogicObject(playerActor); 
    }
    
    /// <summary>
    /// 玩家发起开火请求的服务端 RPC。
    /// 服务端会强制覆盖 instigator，避免客户端伪造来源。
    /// </summary>
    /// <param name="qstClientFireRequest">客户端提交的开火请求。</param>
    [ServerRpc]
    public void FireServerRpc(ClientFireRequest qstClientFireRequest)
    {
#if UNITY_EDITOR
        Debug.Log($"[PlayerNetworkProxy] 服务端收到开火请求 At: {Time.time}, From: {qstClientFireRequest.context.instigator}, shooterObjectId: {qstClientFireRequest.context.shooterObjectId}");
#endif
        qstClientFireRequest.context.instigator = OwnerClientId;
        
        Debug.Log($"Server: 玩家 {NetworkObjectId}（客户端 {qstClientFireRequest.context.instigator}）触发开火 ServerRpc");
        
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.ContainsKey(NetworkObjectId))
        {
            Debug.LogError($"Server: 网络ID {NetworkObjectId} 的对象不存在于 SpawnedObjects 中");
            return;
        }
        
        ServerCombatModule.RequestFire(qstClientFireRequest);
    }
    
    /// <summary>
    /// 真正执行设置武器的服务端逻辑。
    /// 该方法只在网络对象已生成时调用。
    /// </summary>
    /// <param name="weaponId">要设置的武器 ID。</param>
    private void ApplyWeaponOnServer(string weaponId)
    {
        var weaponSO = SOManager.Instance.GetSOById<WeaponSO>(weaponId);
        if (!weaponSO)
        {
            Debug.LogError($"[PlayerNetworkProxy] 在 SOManager 中找不到 WeaponSO, id = {weaponId}");
            return;
        }

        if (ServerWeaponRuntime)
        {
            ServerWeaponRuntime.SetWeaponSO(weaponSO);
        }

        Debug.Log($"[PlayerNetworkProxy] 服务端设置 id[{NetworkObjectId}] 武器SO: {weaponId}");
        _weaponId.Value = new FixedString32Bytes(weaponId);
    }

    /// <summary>
    /// 在服务端设置当前武器。
    /// 若网络对象尚未生成，则先缓存到待处理字段中。
    /// </summary>
    /// <param name="weaponId">要设置的武器 ID。</param>
    public void SetWeaponOnServer(string weaponId)
    {
        if (!IsServer)
        {
            Debug.LogError("[PlayerNetworkProxy] SetWeaponOnServer 只能在服务端调用");
            return;
        }

        if (!IsSpawned)
        {
            _pendingWeaponId = weaponId;
            return;
        }

        ApplyWeaponOnServer(weaponId);
    }

    /// <summary>
    /// 在本地客户端实例化武器模型并绑定相关控制器。
    /// </summary>
    /// <param name="weaponId">要实例化的武器 ID。</param>
    private void SpawnAndBindWeaponById(string weaponId)
    {
        if (IsServer && !IsClient)
        {
            return;
        }

        var weaponSO = SOManager.Instance.GetSOById<WeaponSO>(weaponId);
        if (!weaponSO)
        {
            Debug.LogError($"[PlayerNetworkProxy] 在 SOManager 中找不到 WeaponSO, id = {weaponId}");
            return;
        }

        if (!weaponSO.prefab)
        {
            Debug.LogError($"[PlayerNetworkProxy] WeaponSO[{weaponId}] 没有配置 prefab");
            return;
        }

        ClearCurrentWeaponView();

        var weaponParent = ResolveWeaponFollowPoint();

        var weaponInstance = Instantiate(weaponSO.prefab, weaponParent);
        _currentWeaponView = weaponInstance;
        weaponInstance.transform.localPosition = Vector3.zero;
        weaponInstance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        weaponInstance.transform.localScale = Vector3.one;
        weaponInstance.name = $"Weapon_{weaponId}";

        var weaponAimController = weaponInstance.GetComponent<WeaponAimController>();
        var weaponAnimationController = weaponInstance.GetComponentInChildren<WeaponAnimationController>(true);
        Debug.Log($"[PlayerNetworkProxy] 客户端实例化武器模型: {weaponId}");

        if (PlayerActor != null && PlayerActor.PlayerRender != null)
        {
            PlayerActor.PlayerRender.BindWeaponController(weaponAimController, weaponAnimationController, weaponId);
        }
        else
        {
            Debug.LogWarning("[PlayerNetworkProxy] PlayerRender 未找到，武器表现绑定跳过。");
        }

        PlayerActor?.BindWeaponAimerToViewController(weaponAimController);

        if (ServerWeaponRuntime)
        {
            ServerWeaponRuntime.ClientSetWeaponSO(weaponSO);
        }
    }

    /// <summary>
    /// 客户端请求服务端切换武器的 RPC。
    /// </summary>
    /// <param name="weaponId">请求切换的武器 ID。</param>
    /// <param name="rpcParams">服务端 RPC 参数。</param>
    [ServerRpc]
    public void RequestSetWeaponServerRpc(string weaponId, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
        {
            Debug.LogWarning("[PlayerNetworkProxy] 非拥有者客户端尝试设置武器，已拒绝");
            return;
        }

        SetWeaponOnServer(weaponId);
    }
    
    /// <summary>
    /// 客户端请求服务端释放技能的 RPC。
    /// </summary>
    /// <param name="request">技能释放请求。</param>
    /// <param name="rpcParams">服务端 RPC 参数。</param>
    [ServerRpc(RequireOwnership = true)]
    public void CastSkillServerRpc(ClientSkillCastRequest request, ServerRpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        if (ServerSkillModule == null)
        {
            Debug.LogError("[PlayerNetworkProxy] ServerSkillModule 为空");
            return;
        }

        ServerSkillModule.ServerTryCastSkill(request, rpcParams.Receive.SenderClientId);
    }

    /// <summary>
    /// NGO 网络生成回调。
    /// 这里会完成逻辑对象绑定、服务端可攻击目标注册以及本地武器同步初始化。
    /// </summary>
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        ResolvePlayerActor();

        if (IsServer && PlayerActor != null)
        {
            AttackableObjectManager.Instance.Register(PlayerActor);
            EnsureBossMeleeHitReceiver();
        }

        if (IsServer && !string.IsNullOrEmpty(_pendingWeaponId))
        {
            ApplyWeaponOnServer(_pendingWeaponId);
            _pendingWeaponId = null;
        }

        if (IsClient)
        {
            _weaponId.OnValueChanged += OnWeaponIdChanged;

            if (!_weaponId.Value.IsEmpty)
            {
                OnWeaponIdChanged(default, _weaponId.Value);
            }
        }

        if (IsOwner)
        {
            var controller = GetComponent<ThirdPersonPlayerController>();
            controller?.InitAsLocalPlayer();
            EnsureInteractionDetector();
        }
        else
        {
            var controller = GetComponent<ThirdPersonPlayerController>();
            controller?.SetInputEnabled(false);
        }
        PlayerActor.enabled = true;

        if (IsOwner)
        {
            StartCoroutine(OpenGameHudAfterLocalPlayerInitialized());
        }
    }

    /// <summary>
    /// NGO 网络反生成回调。
    /// 这里会取消玩家的可攻击目标注册，并清理客户端武器监听。
    /// </summary>
    public override void OnNetworkDespawn()
    {
        if (IsServer && PlayerActor != null)
        {
            AttackableObjectManager.Instance.Unregister(PlayerActor);
        }

        if (IsClient)
        {
            _weaponId.OnValueChanged -= OnWeaponIdChanged;
        }

        ClearCurrentWeaponView();

        base.OnNetworkDespawn();
    }
    
    private void EnsureBossMeleeHitReceiver()
    {
        var receiver = GetComponent<PlayerBossMeleeHitReceiver>();
        if (receiver == null)
            receiver = gameObject.AddComponent<PlayerBossMeleeHitReceiver>();

        receiver.Initialize(this);
    }

    /// <summary>
    /// 销毁时清理资源引用。
    /// </summary>
    public override void OnDestroy()
    {
        ClearCurrentWeaponView();
        PlayerActor = null;
        base.OnDestroy();
    }

    private void ClearCurrentWeaponView()
    {
        if (_currentWeaponView == null)
        {
            return;
        }

        Destroy(_currentWeaponView);
        _currentWeaponView = null;
    }

    private Transform ResolveWeaponFollowPoint()
    {
        if (WeaponFollowPoint != null)
        {
            return WeaponFollowPoint;
        }

        WeaponFollowPoint = FindChildByCandidateNames(transform, WeaponFollowPointCandidateNames);
        if (WeaponFollowPoint != null)
        {
            return WeaponFollowPoint;
        }

        var animator = GetComponentInChildren<Animator>(true);
        if (animator != null && animator.isHuman)
        {
            var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rightHand != null)
            {
                WeaponFollowPoint = rightHand;
                Debug.LogWarning(
                    "[PlayerNetworkProxy] WeaponFollowPoint 未配置，临时使用 Humanoid RightHand 作为武器挂点。需要人工确认武器位置/旋转偏移。",
                    this
                );
                return WeaponFollowPoint;
            }
        }

        Debug.LogWarning("[PlayerNetworkProxy] WeaponFollowPoint 未配置且未能自动解析，武器将临时挂到玩家根节点。", this);
        return transform;
    }

    private static Transform FindChildByCandidateNames(Transform root, string[] candidateNames)
    {
        if (root == null || candidateNames == null)
        {
            return null;
        }

        var children = root.GetComponentsInChildren<Transform>(true);
        foreach (var child in children)
        {
            foreach (string candidateName in candidateNames)
            {
                if (IsCandidateTransformName(child.name, candidateName))
                {
                    return child;
                }
            }
        }

        return null;
    }

    private static bool IsCandidateTransformName(string transformName, string candidateName)
    {
        if (string.IsNullOrEmpty(transformName) || string.IsNullOrEmpty(candidateName))
        {
            return false;
        }

        return string.Equals(transformName, candidateName, StringComparison.OrdinalIgnoreCase)
               || transformName.EndsWith($":{candidateName}", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 武器 ID 网络变量变化回调。
    /// </summary>
    /// <param name="oldValue">旧武器 ID。</param>
    /// <param name="newValue">新武器 ID。</param>
    private void OnWeaponIdChanged(FixedString32Bytes oldValue, FixedString32Bytes newValue)
    {
        var idStr = newValue.ToString();
        if (string.IsNullOrEmpty(idStr))
            return;

        SpawnAndBindWeaponById(idStr);
    }

    private IEnumerator OpenGameHudAfterLocalPlayerInitialized()
    {
        yield return null;

        if (!IsOwner || !IsSpawned || Application.isBatchMode)
        {
            yield break;
        }

        // 等待武器配置完成后再打开 HUD，避免 AmorArea 初始化时 ServerWeaponRuntime 为空
        float weaponTimeout = 5f;
        float weaponElapsed = 0f;
        while (_weaponId.Value.IsEmpty && weaponElapsed < weaponTimeout)
        {
            yield return new WaitForSeconds(0.1f);
            weaponElapsed += 0.1f;
        }

        if (_weaponId.Value.IsEmpty)
        {
            Debug.LogWarning("[PlayerNetworkProxy] 等待武器配置超时，仍然打开 HUD");
        }

        var existingHud = UIManager.Instance.GetWindow(nameof(GameHUDWindow));
        if (existingHud != null && existingHud.Visible)
        {
            yield break;
        }

        UIManager.Instance.DestroyAllWindow(new List<string> { nameof(GameHUDWindow) });
        UIManager.Instance.PopUpWindow<GameHUDWindow>();
    }

    private void EnsureInteractionDetector()
    {
        if (GetComponent<InteractionDetector>() != null)
        {
            return;
        }

        gameObject.AddComponent<InteractionDetector>();
    }

    /// <summary>
    /// 解析并缓存当前网络代理关联的玩家逻辑对象。
    /// 该方法用于保证网络生命周期与逻辑对象生命周期对齐。
    /// </summary>
    private void ResolvePlayerActor()
    {
        if (PlayerActor != null)
        {
            if (PlayerActor.networkProxy == null)
            {
                PlayerActor.SetNetworkProxy(this);
            }
            return;
        }

        var playerActor = GetComponent<PlayerActor>();
        if (playerActor == null)
        {
            Debug.LogError("[PlayerNetworkProxy] PlayerActor not found");
            return;
        }

        SetPlayerActor(playerActor);
        playerActor.SetNetworkProxy(this);
    }
    
    /// <summary>
    /// 获取玩家对应的服务端属性模块。
    /// </summary>
    /// <typeparam name="T">服务端属性模块类型。</typeparam>
    /// <returns>返回匹配类型的服务端属性模块。</returns>
    public override T GetServerAttributeModule<T>()
    {
        return ServerPlayerAttributeModule as T;
    }

    /// <summary>
    /// 获取玩家对应的服务端武器运行时。
    /// </summary>
    /// <typeparam name="T">服务端武器运行时类型。</typeparam>
    /// <returns>返回匹配类型的服务端武器运行时。</returns>
    public override T GetServerWeaponRuntime<T>()
    {
        return ServerWeaponRuntime as T;
    }

    /// <summary>
    /// 获取玩家对应的服务端战斗模块。
    /// </summary>
    /// <typeparam name="T">服务端战斗模块类型。</typeparam>
    /// <returns>返回匹配类型的服务端战斗模块。</returns>
    public override T GetServerCombatModule<T>()
    {
        return ServerCombatModule as T;
    }
}
