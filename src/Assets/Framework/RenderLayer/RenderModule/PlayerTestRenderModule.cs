using Unity.Netcode;
using UnityEngine;

public class PlayerTestRenderModule : IRenderModule
{
    private readonly PlayerRender _playerRender;
    private Transform _firePoint; // 缓存FirePoint，避免重复查找
    public Transform FirePoint => _firePoint;
    
    [Header("特效配置")]
    private string _fireEffectPath = "Prefab/Bullet/Bullet"; // 特效路径（请替换为实际路径）
    
    // 添加武器引用
    private WeaponAimController _weaponAimController;
    
    // 方便拿到 ActorId
    private ulong ActorId => _playerRender != null ? _playerRender.ActorId : 0;

    public PlayerTestRenderModule(PlayerRender playerRender)
    {
        _playerRender = playerRender;
    }
    
    /// <summary>
    /// 设置武器控制器引用
    /// </summary>
    public void SetWeaponAimController(WeaponAimController weaponAimController)
    {
        _weaponAimController = weaponAimController;
        if (_weaponAimController && _weaponAimController.firePoint)
        {
            _firePoint = _weaponAimController.firePoint;
            // Debug.Log($"PlayerTestRenderModule: 已绑定武器 FirePoint");
        }
        else
        {
            Debug.LogWarning("PlayerTestRenderModule: 武器控制器或FirePoint为空，使用备用FirePoint");
            InitBackupFirePoint();
        }
    }

    /// <summary>
    /// 备用FirePoint初始化（武器未找到时使用）
    /// </summary>
    private void InitBackupFirePoint()
    {
        if (_firePoint) return;
        _firePoint = _playerRender.transform.Find("FirePoint");
        if (!_firePoint)
        {
            Debug.LogWarning($"{_playerRender.name} 未找到FirePoint，使用根节点作为开火位置");
            _firePoint = _playerRender.transform;
        }
    }

    /// <summary>
    /// 执行开火特效渲染
    /// </summary>
    public void FireRender()
    {
        if (!_firePoint)
        {
            InitBackupFirePoint();
            if (!_firePoint) return;
        }

        // 播放特效（通过特效管理器实例化）
        var effect = EffectRenderManager.Instance.PlayEffect(
            _fireEffectPath,
            _firePoint,       // 父节点设为FirePoint，跟随移动
            Vector3.zero,     // 相对FirePoint的本地位置
            Quaternion.identity, // 相对FirePoint的本地旋转
            false             // 不自动回收（手动控制生命周期）
        );
        
    }

    #region 渲染模块生命周期
    
    public void Initialize()
    {
        // 本地预测用：只负责自己枪的即时火光
        EventCenter.Instance.AddListener<WeaponFiredEvt>(EventName.LocalWeaponFired, PlayLocalFireEffect);
     
        // 远端同步用：负责其他玩家（+自己）的“最终”效果
        EventCenter.Instance.AddListener<WeaponFiredEvt>(EventName.RemoteWeaponFired, PlayRemoteFireEffect);
    }

    public void OnActivate() { }

    public void Destroy()
    {
        EventCenter.Instance.RemoveListener<WeaponFiredEvt>(EventName.LocalWeaponFired, PlayLocalFireEffect);
        
        EventCenter.Instance.RemoveListener<WeaponFiredEvt>(EventName.RemoteWeaponFired, PlayRemoteFireEffect);
    }
    #endregion
    
    #region 事件回调

    /// <summary>
    /// 本地预测开火特效（LocalWeaponFiredEvt）
    /// </summary>
    private void PlayLocalFireEffect(WeaponFiredEvt evt)
    {
        var localClientId = NetworkManager.Singleton.LocalClientId;

#if UNITY_EDITOR
        Debug.Log($"[OnLocalWeaponFired] evt.actorId={evt.actorId}, ActorId={ActorId}, " +
                  $"instigator={evt.instigatorClientId}, local={localClientId}");
#endif
        // ① 必须是本地玩家触发 && 是这个渲染对象对应的 Actor
        if (!evt.isLocalPlayer || evt.instigatorClientId != localClientId)
            return;

        if (evt.actorId != ActorId)
            return;

#if UNITY_EDITOR
        Debug.Log($"[OnLocalWeaponFired] 本地玩家 {evt.actorId} 播放预测开火特效");
#endif
        FireRender();
    }

    /// <summary>
    /// 服务器广播的 WeaponFiredEvt，在本地用于远端玩家 / 回滚校正
    /// </summary>
    private void PlayRemoteFireEffect(WeaponFiredEvt evt)
    {
        var localClientId = NetworkManager.Singleton.LocalClientId;

#if UNITY_EDITOR
        Debug.Log($"[OnRemoteWeaponFired] evt.actorId={evt.actorId}, ActorId={ActorId}, " +
                  $"instigator={evt.instigatorClientId}, local={localClientId}");
#endif
        // ① 只处理“我这个 ActorId ”的事件
        if (evt.actorId != ActorId)
            return;

        // ② 如果是当前客户端自己开的枪，本地已经播过 LocalWeaponFired，
        //    这里先跳过，避免重复（以后想做回滚 / 校正可以在这里动手）。
        if (evt.instigatorClientId == localClientId && evt.isLocalPlayer)
        {
#if UNITY_EDITOR
            Debug.Log("[OnRemoteWeaponFired] 本地玩家自己触发的远端事件，已在本地预测中播过，跳过。");
#endif
            return;
        }

#if UNITY_EDITOR
        Debug.Log($"[OnRemoteWeaponFired] 在本客户端为 Actor {evt.actorId} 播放同步开火特效");
#endif

        FireRender();
    }

    #endregion
}