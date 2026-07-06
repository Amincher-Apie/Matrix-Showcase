using System.Collections.Generic;
using Framework.LogicLayer.DamageCenter;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 敌人战斗模块 - 敌人的战斗行为
/// 简化版本，敌人可能不需要所有玩家功能（如蓄力等）
/// </summary>
public class EnemyCombatModule : ICombat
{
    private readonly EnemyActor _owner;
    private ServerWeaponRuntime _currentWeapon;
    private ServerCombatModule _serverCombatModule;
    private Dictionary<BulletKind, IFireMethod> _fireMethods;
    private EnemySO _enemySO;
    
    public ulong ObjectId => _owner.ObjectId;
    public ServerWeaponRuntime CurrentConfig => _currentWeapon;
    public int CurrentAmmo => _currentWeapon?.AmmoInMag ?? 0;
    public float NextFireTime = 0;
    
    private ulong _shotId = 0;
    
    public EnemyCombatModule(EnemyActor owner)
    {
        _owner = owner;
        _fireMethods = new Dictionary<BulletKind, IFireMethod>();
    }
    
    public void LocalInit()
    {
        // 初始化开火方法
        var hitScanService = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer
            ? (IHitScanService)new ServerHitScanService()
            : new ClientHitScanService();

        _fireMethods[BulletKind.HitScan] = new HitScanFireMethod(hitScanService);
        _fireMethods[BulletKind.Projectile] = new ProjectileFireMethod();

        var proxy = _owner.GetComponent<EnemyNetworkProxy>();
        _currentWeapon = proxy != null && proxy.ServerWeaponRuntime != null
            ? proxy.ServerWeaponRuntime
            : _owner.GetComponent<ServerWeaponRuntime>();
        _serverCombatModule = proxy != null && proxy.ServerCombatModule != null
            ? proxy.ServerCombatModule
            : _owner.GetComponent<ServerCombatModule>();
        _enemySO = ResolveEnemySO();
        
        DebugLog.Info("Combat.EnemyModule", $"[EnemyCombatModule] 初始化完成 - ObjectId: {ObjectId}");
    }
    
    public void OnActivate() { }
    
    public void LocalDestroy()
    {
        _fireMethods?.Clear();
        _currentWeapon = null;
        _serverCombatModule = null;
        _enemySO = null;
    }
    
    public bool TryFire(FireContext ctx)
    {
        if (!HasWeaponRuntime() || !CanFire()) return false;
        var weaponSO = _currentWeapon.WeaponSO;
        
        // 更新开火冷却
        UpdateFireCooldown();
        
        ctx.shotId = _shotId;
        _shotId++;
        ctx.shooterObjectId = _owner.ObjectId;
        ctx.instigator = ResolveAuthorityClientId();
        ctx.bulletKind = weaponSO.bulletKind;
        
        // 执行开火
        if (_fireMethods.TryGetValue(ctx.bulletKind, out var fireMethod))
        {
            var predictedHits = fireMethod.ClientExecute(ctx, _currentWeapon);
            
            // 触发客户端特效
            TriggerClientFireEffects(ctx, predictedHits);
            
            _serverCombatModule?.RequestFire(new ClientFireRequest
            {
                context = ctx,
                predictedHits = predictedHits
            });
            
            // 消耗弹药
            if (_currentWeapon != null)
            {
                // _currentWeapon.ConsumeAmmoServerRpc(1);
            }
            
            return true;
        }
        
        return false;
    }

    public bool TryFireAtTarget(FireContext ctx, IAttackableObject target)
    {
        if (HasWeaponRuntime())
        {
            return TryFire(ctx);
        }

        if (target == null || !CanFire())
        {
            return false;
        }

        ctx.shotId = _shotId;
        _shotId++;
        ctx.shooterObjectId = _owner.ObjectId;
        ctx.instigator = ResolveAuthorityClientId();
        ctx.bulletKind = BulletKind.HitScan;

        UpdateEnemyProfileCooldown();

        if (!TryBuildConfiguredDamageProfile(out var profile, out var bulletType))
        {
            return false;
        }

        var hitPoint = target.GetTargetPoint();
        if (_serverCombatModule == null ||
            !_serverCombatModule.RequestProfileDamage(
                ctx,
                target.ObjectId,
                hitPoint,
                profile,
                bulletType))
        {
            return false;
        }

        TriggerClientFireEffects(ctx, new List<HitResult>
        {
            new HitResult
            {
                targetId = target.ObjectId,
                point = hitPoint,
                normal = Vector3.zero,
                distance = Vector3.Distance(ctx.origin, hitPoint)
            }
        });

        DebugLog.Info("Combat.EnemyModule", $"[EnemyCombatModule] 使用 EnemySO 伤害结算 - Enemy={_owner.ObjectId}, Target={target.ObjectId}");
        return true;
    }
    
    private void UpdateFireCooldown()
    {
        if (_currentWeapon != null && _currentWeapon.WeaponSO != null)
        {
            NextFireTime = Time.time + 1f / _currentWeapon.WeaponSO.fireRate;
        }
    }

    private void UpdateEnemyProfileCooldown()
    {
        float attacksPerSecond = _enemySO != null ? Mathf.Max(0.01f, _enemySO.attackSpeed) : 1f;
        NextFireTime = Time.time + 1f / attacksPerSecond;
    }
    
    private void TriggerClientFireEffects(FireContext ctx, List<HitResult> hits)
    {
        string weaponId = ResolveWeaponEventId();

        // 触发开火事件
        EventCenter.Instance.Trigger(EventName.LocalWeaponFired, new WeaponFiredEvt
        {
            actorId = _owner.ObjectId,
            weaponId = weaponId,
            shotId = ctx.shotId,
            origin = ctx.origin,
            dir = ctx.dir,
            instigatorClientId = 0, // 敌人不需要客户端ID
            isLocalPlayer = false
        });
        
        // 触发命中特效
        foreach (var hit in hits)
        {
            EventCenter.Instance.Trigger(EventName.HitResolved, new HitResolvedEvt
            {
                actorId = _owner.ObjectId,
                weaponId = weaponId,
                targetId = hit.targetId,
            });
        }
    }
    
    public void StartCharge()
    {
        // 敌人可能不需要蓄力功能
        Debug.LogWarning("[EnemyCombatModule] 敌人不支持蓄力功能");
    }
    
    public bool ReleaseCharge(FireContext ctx)
    {
        // 敌人可能不需要蓄力功能
        return false;
    }
    
    public bool CanFire()
    {
        if (!HasWeaponRuntime())
        {
            return CanUseEnemyProfileAttack();
        }
        
        if (_currentWeapon.AmmoInMag <= 0)
        {
            Reload();
            return false;
        }
        
        if (Time.time < NextFireTime)
        {
            return false;
        }
        
        return true;
    }
    
    public bool Reload()
    {
        if (_currentWeapon?.CanReload() == true)
        {
            // _currentWeapon.ReloadServerRpc();
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// 设置当前武器（由外部调用）
    /// </summary>
    public void SetWeapon(ServerWeaponRuntime weapon)
    {
        _currentWeapon = weapon;
    }

    private string ResolveWeaponEventId()
    {
        if (_currentWeapon != null && _currentWeapon.WeaponSO != null)
        {
            return _currentWeapon.WeaponSO.id;
        }

        return _enemySO != null ? $"EnemySO:{_enemySO.id}" : "0";
    }

    private bool HasWeaponRuntime()
    {
        return _currentWeapon != null && _currentWeapon.WeaponSO != null;
    }

    public bool TryBuildConfiguredDamageProfile(out DamageProfile profile, out PhysicalBulletType bulletType)
    {
        profile = null;
        bulletType = PhysicalBulletType.Solid;

        if (_enemySO == null || _enemySO.attackType == EnemyAttackType.None)
        {
            return false;
        }

        profile = BuildDamageProfileFromEnemySO(_enemySO);
        if (!HasAnyDamage(profile))
        {
            return false;
        }

        bulletType = ResolvePhysicalBulletType(profile);
        return true;
    }

    private bool CanUseEnemyProfileAttack()
    {
        if (Time.time < NextFireTime)
        {
            return false;
        }

        return TryBuildConfiguredDamageProfile(out _, out _);
    }

    private static bool HasAnyDamage(DamageProfile profile)
    {
        return profile != null &&
               (profile.solid > 0 ||
                profile.liquid > 0 ||
                profile.gas > 0 ||
                profile.ice > 0 ||
                profile.fire > 0 ||
                profile.toxic > 0 ||
                profile.electric > 0);
    }

    private static DamageProfile BuildDamageProfileFromEnemySO(EnemySO enemySO)
    {
        return new DamageProfile
        {
            solid = Mathf.RoundToInt(Mathf.Max(0f, enemySO?.physicalDamage ?? 0f)),
            liquid = Mathf.RoundToInt(Mathf.Max(0f, enemySO?.liquidDamage ?? 0f)),
            gas = Mathf.RoundToInt(Mathf.Max(0f, enemySO?.gasDamage ?? 0f)),
            ice = Mathf.RoundToInt(Mathf.Max(0f, enemySO?.iceDamage ?? 0f)),
            fire = Mathf.RoundToInt(Mathf.Max(0f, enemySO?.fireDamage ?? 0f)),
            toxic = Mathf.RoundToInt(Mathf.Max(0f, enemySO?.poisonDamage ?? 0f)),
            electric = Mathf.RoundToInt(Mathf.Max(0f, enemySO?.electricDamage ?? 0f))
        };
    }

    private static PhysicalBulletType ResolvePhysicalBulletType(DamageProfile profile)
    {
        if (profile == null)
        {
            return PhysicalBulletType.Solid;
        }

        if (profile.liquid > profile.solid && profile.liquid >= profile.gas)
        {
            return PhysicalBulletType.Liquid;
        }

        if (profile.gas > profile.solid && profile.gas > profile.liquid)
        {
            return PhysicalBulletType.Gas;
        }

        return PhysicalBulletType.Solid;
    }

    private EnemySO ResolveEnemySO()
    {
        string enemyConfigId = _owner.GetEnemyConfigId();
        if (string.IsNullOrEmpty(enemyConfigId))
        {
            return null;
        }

        // 归一化：MonsterSpawnManager 传入 "Normal/002"，但 EnemySO.id 是 "002"
        string normalizedId = NormalizeEnemyConfigId(enemyConfigId);

        if (SOManager.Instance != null)
        {
            var so = SOManager.Instance.GetSOById<EnemySO>(normalizedId);
            if (so != null)
            {
                return so;
            }
        }

        var allEnemySOs = Resources.LoadAll<EnemySO>("Data/SO/EnemySO");
        for (int i = 0; i < allEnemySOs.Length; i++)
        {
            if (allEnemySOs[i] != null && allEnemySOs[i].id == normalizedId)
            {
                return allEnemySOs[i];
            }
        }

        DebugLog.Warning("Combat.EnemyModule", $"[EnemyCombatModule] 未找到 EnemySO, enemyConfigId={enemyConfigId} (normalized={normalizedId})");
        return null;
    }

    /// <summary>
    /// 归一化敌人配置 ID — 去除 rank/ 前缀（与 AttributeManager.ExtractEnemyAttributeId 一致）
    /// </summary>
    private static string NormalizeEnemyConfigId(string enemyConfigId)
    {
        if (string.IsNullOrWhiteSpace(enemyConfigId))
            return string.Empty;

        var trimmedId = enemyConfigId.Trim();
        var separatorIndex = trimmedId.LastIndexOfAny(new[] { '/', '\\' });
        return separatorIndex >= 0 && separatorIndex < trimmedId.Length - 1
            ? trimmedId.Substring(separatorIndex + 1)
            : trimmedId;
    }

    private ulong ResolveAuthorityClientId()
    {
        var networkObject = _owner.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            return networkObject.OwnerClientId;
        }

        return NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
    }
}
