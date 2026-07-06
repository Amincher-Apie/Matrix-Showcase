// 文件位置: LogicLayer/CombatModule/PlayerCombatModule.cs

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerCombatModule : ICombat
{
    private readonly PlayerActor _owner;
    private ServerWeaponRuntime _currentWeapon;
    private Dictionary<BulletKind, IFireMethod> _fireMethods;
    
    public ulong ObjectId => _owner.ObjectId;
    public ServerWeaponRuntime CurrentConfig => _currentWeapon;
    public int CurrentAmmo => _currentWeapon?.AmmoInMag ?? 0;
    public float NextFireTime = 0;

    // 装弹锁
    private bool _isReloading;
    private float _reloadEndTime;
    
    // 蓄力相关字段
    private bool _isCharging = false;
    private float _chargeStartTime = 0f;
    private float _currentChargeLevel = 0f;
    
    private ulong _shotId = 0;
    
    public PlayerCombatModule(PlayerActor owner)
    {
        _owner = owner;
        _fireMethods = new Dictionary<BulletKind, IFireMethod>();
    }

    public void LocalInit()
    {
        // 初始化开火方法
        _fireMethods[BulletKind.HitScan] = new HitScanFireMethod(new ClientHitScanService());
        _fireMethods[BulletKind.Projectile] = new ProjectileFireMethod();
#if UNITY_EDITOR
        Debug.Log("[PlayerCombatModule] LocalInit");
        if (_owner == null)
        {
            Debug.LogError("[PlayerCombatModule] OnActivate: _owner 为空");
            return;
        }
    
        if (_owner.networkProxy == null)
        {
            Debug.LogError("[PlayerCombatModule] OnActivate: _owner.networkProxy 为空");
            return;
        }
    
        var serverWeaponRuntime = _owner.networkProxy.ServerWeaponRuntime;
        if (serverWeaponRuntime == null)
        {
            Debug.LogWarning("[PlayerCombatModule] OnActivate: ServerWeaponRuntime 为空");
        }
    
        _currentWeapon = serverWeaponRuntime;
#else
        _currentWeapon = _owner.networkProxy.ServerWeaponRuntime;
#endif
    }

    public void OnActivate() { }
    
    public void LocalDestroy() { }

    public bool TryFire(FireContext ctx)
    {
        if (!CanFire()) return false;
        
        // 本地射速检定
        UpdateFireCooldown();
        
        ctx.shotId = _shotId;
        _shotId++;
        // 设置开火方式类型
        ctx.bulletKind = _currentWeapon.WeaponSO.bulletKind;
        
        // 如果有蓄力，设置蓄力等级
        if (_isCharging)
        {
            ResetChargeState();
        }
        
#if UNITY_EDITOR
        Debug.Log($"[PlayerCombatModule] Fire origin: {ctx.origin}");
        Debug.Log($"[PlayerCombatModule] Fire direction: {ctx.dir}");
#endif

        // 客户端预测执行开火
        if (_fireMethods.TryGetValue(ctx.bulletKind, out var fireMethod))
        {
            var predictedHits = fireMethod.ClientExecute(ctx, _currentWeapon);
            
            // 立即触发客户端特效
            TriggerClientFireEffects(ctx, predictedHits);
            
#if UNITY_EDITOR
            Debug.Log($"[PlayerCombatModule] ClientFire: At {Time.time} From {_owner.ObjectId}");
#endif
            // 发送开火请求到服务器
            SendFireRequestToServer(ctx, predictedHits);
            
            // 客户端消耗弹药
            _currentWeapon.ConsumeAmmoServerRpc(1);
            
            return true;
        }
        
        return false;
    }

    private void UpdateFireCooldown()
    {
        // 本地计算下次开火时间
        if (_currentWeapon)
        {
            NextFireTime = Time.time + 1f/_currentWeapon.WeaponSO.fireRate;
        }
    }

    private void TriggerClientFireEffects(FireContext ctx, List<HitResult> hits)
    {
        var localClientId = NetworkManager.Singleton.LocalClientId;

        EventCenter.Instance.Trigger(EventName.LocalWeaponFired, new WeaponFiredEvt
        {
            actorId = _owner.ObjectId,
            weaponId = _currentWeapon.WeaponSO.id,
            shotId = ctx.shotId,
            origin = ctx.origin,
            dir = ctx.dir,
            instigatorClientId = localClientId,   // ★ 直接用本机的 localClientId
            isLocalPlayer = true                 // ★ 明确标记这是本地预测事件
        });

        // 触发命中特效
        foreach (var hit in hits)
        {
            EventCenter.Instance.Trigger(EventName.HitResolved, new HitResolvedEvt
            {
                actorId = _owner.ObjectId,
                weaponId = _currentWeapon.WeaponSO.id,
                targetId = hit.targetId,
            });
        }
    }

    private void SendFireRequestToServer(FireContext ctx, List<HitResult> predictedHits)
    {
        _owner.networkProxy.FireServerRpc(new ClientFireRequest
        {
            context = ctx,
            predictedHits = predictedHits
        });
    }

    public void StartCharge()
    {
        if (!CanStartCharge()) return;
        
        _isCharging = true;
        _chargeStartTime = Time.time;
        _currentChargeLevel = 0f;
        
        // // 触发开始蓄力事件
        // EventCenter.Instance.Trigger(EventName.ChargeStarted, new ChargeStartedEvt
        // {
        //     actorId = _owner.ObjectId,
        //     weaponId = _currentWeapon.WeaponSO.id
        // });
    }

    public bool ReleaseCharge(FireContext ctx)
    {
        if (!_isCharging) return false;
        
        // 计算蓄力等级
        float chargeTime = Time.time - _chargeStartTime;
        _currentChargeLevel = CalculateChargeLevel(chargeTime);
        
        // 释放蓄力开火
        bool success = TryFire(ctx);
        
        if (success)
        {
            // // 触发蓄力释放事件
            // EventCenter.Instance.Trigger(EventName.ChargeReleased, new ChargeReleasedEvt
            // {
            //     actorId = _owner.ObjectId,
            //     weaponId = _currentWeapon.WeaponSO.id,
            //     chargeLevel = _currentChargeLevel
            // });
        }
        
        return success;
    }

    private void ResetChargeState()
    {
        _isCharging = false;
        _chargeStartTime = 0f;
        _currentChargeLevel = 0f;
    }

    private bool CanStartCharge()
    {
        if (!_currentWeapon)
        {
            Debug.Log($"[PlayerCombatModule] 无法开始蓄力: 当前武器为空");
            return false;
        }
        
        // 检查武器是否支持蓄力
        if (_currentWeapon.WeaponSO.chargeTime <= 0)
        {
            Debug.Log($"[PlayerCombatModule] 无法开始蓄力: 武器不支持蓄力");
            return false;
        }
        
        // 检查弹药
        if (_currentWeapon.AmmoInMag <= 0)
        {
            Debug.Log($"[PlayerCombatModule] 无法开始蓄力: 弹药不足");
            return false;
        }
        
        // 检查是否在冷却中
        if (Time.time < NextFireTime)
        {
            Debug.Log($"[PlayerCombatModule] 无法开始蓄力: 武器冷却中");
            return false;
        }
        
        return true;
    }

    private float CalculateChargeLevel(float chargeTime)
    {
        if (_currentWeapon == null) return 0f;
        
        var weaponSO = _currentWeapon.WeaponSO;
        float maxChargeTime = weaponSO.chargeTime;
        float minChargeTime = 0;
        
        // 计算蓄力等级 (0-1)
        float chargeLevel = Mathf.Clamp01((chargeTime - minChargeTime) / (maxChargeTime - minChargeTime));
        
        return chargeLevel;
    }

    public void UpdateCharge()
    {
        if (!_isCharging) return;
        
        // 更新当前蓄力等级
        float chargeTime = Time.time - _chargeStartTime;
        float newChargeLevel = CalculateChargeLevel(chargeTime);
        
        // 如果蓄力等级变化，触发更新事件
        if (Mathf.Abs(newChargeLevel - _currentChargeLevel) > 0.01f)
        {
            _currentChargeLevel = newChargeLevel;
            
            // 触发蓄力更新事件
            // EventCenter.Instance.Trigger(EventName.ChargeUpdated, new ChargeUpdatedEvt
            // {
            //     actorId = _owner.ObjectId,
            //     weaponId = _currentWeapon.WeaponSO.id,
            //     chargeLevel = _currentChargeLevel
            // });
        }
        
        // 检查是否达到最大蓄力自动释放 _currentWeapon.WeaponSO.autoReleaseAtMaxCharge
        if (_currentChargeLevel >= 0.99f)
        {
            // 自动释放最大蓄力
            var autoCtx = new FireContext
            {
                origin = _owner.transform.position,
                dir = _owner.transform.forward
            };
            ReleaseCharge(autoCtx);
        }
    }

    public bool CanFire()
    {
        // 检查是否有当前武器
        if (!_currentWeapon)
        {
            Debug.Log($"[PlayerCombatModule] 无法开火: 当前武器为空");
            return false;
        }

        // 装弹锁完成检测
        TickReloadState(Time.time);

        // 装弹锁
        if (_isReloading)
        {
            Debug.Log($"[PlayerCombatModule] 无法开火: 装弹中，还需等待: {_reloadEndTime - Time.time:F2}秒");
            return false;
        }

        // 检查弹药是否充足
        if (_currentWeapon.AmmoInMag <= 0)
        {
            Debug.Log($"[PlayerCombatModule] 无法开火: 弹药不足，当前弹药: {_currentWeapon.AmmoInMag}");
            Reload();
            return false;
        }

        // 本地射速检定
        if (Time.time < NextFireTime)
        {
            Debug.Log($"[PlayerCombatModule] 无法开火: 尚未到达下次开火时间，还需等待: {NextFireTime - Time.time:F2}秒");
            return false;
        }

        return true;
    }

    public bool Reload()
    {
        if (_isReloading) return false;
        if (_currentWeapon?.CanReload() != true) return false;

        float reloadTime = _currentWeapon.GetAttribute(WeaponAttributeType.ReloadTime);
        _isReloading = true;
        _reloadEndTime = Time.time + reloadTime;

        EventCenter.Instance.Trigger(EventName.ReloadStarted, new ReloadEvt
        {
            actorId = _owner.ObjectId,
            weaponId = _currentWeapon.WeaponSO.id,
            duration = reloadTime
        });

        _currentWeapon.ReloadServerRpc();
        return true;
    }

    public void TickReloadState(float now)
    {
        if (!_isReloading)
        {
            return;
        }

        if (!_currentWeapon)
        {
            _isReloading = false;
            return;
        }

        if (now < _reloadEndTime)
        {
            return;
        }

        _isReloading = false;
        EventCenter.Instance.Trigger(EventName.ReloadFinished, new ReloadEvt
        {
            actorId = _owner.ObjectId,
            weaponId = _currentWeapon.WeaponSO.id,
            duration = 0f
        });
    }
}
