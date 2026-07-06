// 文件位置: NetworkLayer/CombatSystem/ServerCombatModule.cs

using System.Collections.Generic;
using Framework.LogicLayer.DamageCenter;
using Matrix.Missions;
using Unity.Netcode;
using UnityEngine;

public class ServerCombatModule : NetworkBehaviour
{
    private const bool VerboseHitLogs = false;

    private Dictionary<BulletKind, IFireMethod> _fireMethods;
    private ServerWeaponRuntime _currentWeapon;

    public override void OnNetworkSpawn()
    {
        _fireMethods = new Dictionary<BulletKind, IFireMethod>
        {
            [BulletKind.HitScan] = new HitScanFireMethod(new ServerHitScanService()),
            [BulletKind.Projectile] = new ProjectileFireMethod()
        };
        
        _currentWeapon = GetComponent<ServerWeaponRuntime>();
    }

    /// <summary>
    /// 这里作伤害验证与计算，验证成功之后就会自动同步
    /// </summary>
    /// <param name="qst"></param>
    public void RequestFire(ClientFireRequest qst)
    {
        if (!IsServer) return;
        var ctx = qst.context;
        var clientHits = qst.predictedHits;
        
        // 验证客户端权限
        if (ctx.instigator != OwnerClientId) return;

        // 获取对应的开火方法
        if (_fireMethods.TryGetValue(ctx.bulletKind, out var fireMethod))
        {
            // 服务器验证开火结果
            var validationResult = fireMethod.ServerValidate(ctx, clientHits, _currentWeapon);
            
            if (validationResult.isValid)
            {
                
                // 处理已验证的命中
                ProcessValidatedHits(ctx, validationResult.validatedHits);
                
                // 同步开火事件给所有客户端
                SyncFireEventClientRpc(ctx);
                
                // 处理投射物
                if (validationResult.projectileInfo.HasValue)
                {
                    SyncProjectileSpawnClientRpc(validationResult.projectileInfo.Value);
                }
            }
            else
            {
                // 客户端作弊检测
                Debug.LogWarning($"客户端 {ctx.instigator} 发送了无效的开火请求");
            }
        }
    }

    public bool RequestProfileDamage(
        FireContext ctx,
        ulong targetId,
        Vector3 hitPoint,
        DamageProfile profile,
        PhysicalBulletType bulletType)
    {
        if (!IsServer) return false;
        if (ctx.instigator != OwnerClientId) return false;

        var hit = new ValidatedHit
        {
            targetId = targetId,
            point = hitPoint,
            normal = Vector3.zero,
            distance = Vector3.Distance(ctx.origin, hitPoint)
        };

        if (!NetworkObjectManager.Instance.TryGetNetworkProxy<NetworkProxyBase>(targetId, out var target))
        {
            var directDamageInfo = BuildDirectDamageInfo(ctx, hit, profile, bulletType);
            return TryProcessMissionObjectiveHit(hit, directDamageInfo);
        }

        var damageInfo = DamageCalculator.CalculateDamageFromProfile(
            ctx.shooterObjectId,
            targetId,
            profile,
            bulletType);
        damageInfo.isSkill = false;
        damageInfo.hitWorldPos = hit.point;
        damageInfo.hasHitWorldPos = true;
        damageInfo.instigator = ctx.instigator;

        var targetAttr = target.GetServerAttributeModule<ServerAttributeModule>();
        if (targetAttr == null)
        {
            return false;
        }

        targetAttr.TakeDamage(damageInfo);
        return true;
    }

    private void ProcessValidatedHits(FireContext ctx, List<ValidatedHit> validatedHits)
    {
        if (VerboseHitLogs)
        {
            Debug.Log($"[ProcessValidatedHits] validatedHits count = {validatedHits.Count}");
        }
        foreach (var hit in validatedHits)
        {
            if (!NetworkObjectManager.Instance.TryGetNetworkProxy<NetworkProxyBase>(hit.targetId, out var target))
            {
                TryProcessMissionObjectiveHit(ctx, hit);
                continue;
            }

            // 1. 攻击方 Buff：使用普通攻击 / 技能
            if (NetworkObjectManager.Instance.TryGetNetworkProxy<NetworkProxyBase>(ctx.shooterObjectId, out var shooter))
            {
                var shooterBuffModule = shooter.GetComponent<ServerBuffModule>();
                var shooterHandler    = shooterBuffModule ? shooterBuffModule.Handler : null;

                // 普攻
                //（如果是技能开火，这里改成 ApplyOnUseSkill / ApplyAfterUseSkill）
                shooterHandler?.ApplyOnUseNormalAtk(default); // 这里可以传一个“将要造成的伤害包”或者先 default
            }
            // 计算伤害
            // 2. 计算伤害（武器版或者技能版）
            var damageInfo = DamageCalculator.CalculateDamage(
                ctx.shooterObjectId,
                hit.targetId
            );
#if UNITY_EDITOR
                // 为每个字段添加 Debug 信息
#endif
            // 3. 攻击方 Buff：真正造成伤害
            if (NetworkObjectManager.Instance.TryGetNetworkProxy<NetworkProxyBase>(ctx.shooterObjectId, out var shooter2))
            {
                var shooterBuffModule = shooter2.GetComponent<ServerBuffModule>();
                var shooterHandler    = shooterBuffModule ? shooterBuffModule.Handler : null;

                shooterHandler?.ApplyOnCauseDamage(damageInfo);
                shooterHandler?.ApplyOnHit(damageInfo);
                
                // ★ 触发品质效果：造成伤害时
                var shooterPlayerProxy = shooter2 as PlayerNetworkProxy;
                if (shooterPlayerProxy?.PlayerActor != null)
                {
                    shooterPlayerProxy.PlayerActor.QualityEffectModule?.RaiseOnHitDealt(
                        target, 
                        damageInfo.amount
                    );
                    
                    // 如果是暴击，也触发暴击触发器
                    if (damageInfo.isCritical)
                    {
                        shooterPlayerProxy.PlayerActor.QualityEffectModule?.RaiseOnCrit(
                            target, 
                            damageInfo.amount
                        );
                    }
                }
            }
            
            // 4. 把伤害交给受击者，这里会通过NetworkVariable自动同步
            var targetAttr = target.GetServerAttributeModule<ServerAttributeModule>();
            
            damageInfo.hitWorldPos = hit.point;
            damageInfo.hasHitWorldPos = true;
            damageInfo.instigator = ctx.instigator;
            
            targetAttr?.TakeDamage(damageInfo);
            
            // // ============================================================
            // // ★ 新增：构造 DamageVfxEvent（命中点在这里最权威）
            // // ============================================================
            // var vfxEvt = new DamageVfxEvent
            // {
            //     targetId = hit.targetId,
            //     sourceId = ctx.shooterObjectId,
            //     hitWorldPos = hit.point,
            //     damageResult = new DamageResult(damageInfo)
            // };
            //
            // // ★ 同步给所有客户端（仅表现）
            // var rpcParams = new ClientRpcParams
            // {
            //     Send = new ClientRpcSendParams
            //     {
            //         TargetClientIds = new[] { ctx.instigator }
            //     }
            // };
            //
            // SyncDamageVfxClientRpc(vfxEvt, rpcParams);
        }
    }

    private bool TryProcessMissionObjectiveHit(FireContext ctx, ValidatedHit hit)
    {
        var damageInfo = BuildDirectDamageInfo(ctx, hit);
        return TryProcessMissionObjectiveHit(hit, damageInfo);
    }

    private bool TryProcessMissionObjectiveHit(ValidatedHit hit, DamageInfo damageInfo)
    {
        var networkManager = NetworkManager.Singleton;
        if (networkManager == null ||
            networkManager.SpawnManager == null ||
            !networkManager.SpawnManager.SpawnedObjects.TryGetValue(hit.targetId, out var networkObject))
        {
            return false;
        }

        var defenseObjective = networkObject.GetComponent<DefenseObjective>();
        if (defenseObjective != null)
        {
            damageInfo.targetActorId = hit.targetId;
            defenseObjective.TakeDamage(damageInfo);
            return true;
        }

        var damageableTarget = networkObject.GetComponent<MissionDamageableTarget>();
        if (damageableTarget == null)
        {
            return false;
        }

        damageInfo.targetActorId = hit.targetId;
        damageableTarget.TakeDamage(damageInfo);
        return true;
    }

    private DamageInfo BuildDirectDamageInfo(FireContext ctx, ValidatedHit hit)
    {
        var bulletType = _currentWeapon != null
            ? _currentWeapon.GetPhysicalBulletType()
            : PhysicalBulletType.Solid;

        var damageInfo = new DamageInfo(bulletType, ctx.shooterObjectId, hit.targetId)
        {
            instigator = ctx.instigator,
            hitWorldPos = hit.point,
            hasHitWorldPos = true
        };

        if (_currentWeapon == null)
        {
            return damageInfo;
        }

        var profile = _currentWeapon.GetModifiedDamageProfile();
        damageInfo.amount =
            Mathf.Max(0f, profile.solid) +
            Mathf.Max(0f, profile.liquid) +
            Mathf.Max(0f, profile.gas) +
            Mathf.Max(0f, profile.ice) +
            Mathf.Max(0f, profile.fire) +
            Mathf.Max(0f, profile.toxic) +
            Mathf.Max(0f, profile.electric);
        damageInfo.iceDamage = Mathf.Max(0f, profile.ice);
        damageInfo.fireDamage = Mathf.Max(0f, profile.fire);
        damageInfo.poisonDamage = Mathf.Max(0f, profile.toxic);
        damageInfo.electricDamage = Mathf.Max(0f, profile.electric);

        return damageInfo;
    }

    private DamageInfo BuildDirectDamageInfo(
        FireContext ctx,
        ValidatedHit hit,
        DamageProfile profile,
        PhysicalBulletType bulletType)
    {
        var damageInfo = new DamageInfo(bulletType, ctx.shooterObjectId, hit.targetId)
        {
            instigator = ctx.instigator,
            hitWorldPos = hit.point,
            hasHitWorldPos = true
        };

        damageInfo.amount =
            Mathf.Max(0f, profile.solid) +
            Mathf.Max(0f, profile.liquid) +
            Mathf.Max(0f, profile.gas) +
            Mathf.Max(0f, profile.ice) +
            Mathf.Max(0f, profile.fire) +
            Mathf.Max(0f, profile.toxic) +
            Mathf.Max(0f, profile.electric);
        damageInfo.iceDamage = Mathf.Max(0f, profile.ice);
        damageInfo.fireDamage = Mathf.Max(0f, profile.fire);
        damageInfo.poisonDamage = Mathf.Max(0f, profile.toxic);
        damageInfo.electricDamage = Mathf.Max(0f, profile.electric);

        return damageInfo;
    }

    [ClientRpc]
    private void SyncFireEventClientRpc(FireContext ctx)
    {
        EventCenter.Instance.Trigger(EventName.RemoteWeaponFired, new WeaponFiredEvt
        {
            actorId = ctx.shooterObjectId,
            weaponId = _currentWeapon.WeaponSO.id,
            shotId = ctx.shotId,
            origin = ctx.origin,
            dir = ctx.dir,
            instigatorClientId = ctx.instigator,
            isLocalPlayer = (ctx.instigator == NetworkManager.LocalClientId)  // 判断是否是本地玩家
        });
    }


    [ClientRpc]
    private void SyncProjectileSpawnClientRpc(ProjectileInfo projectileInfo)
    {
        // 所有客户端接收投射物生成事件
        EventCenter.Instance.Trigger(EventName.ProjectileSpawned, new ProjectileSpawnedEvt
        {
            actorId = projectileInfo.instigatorId,
            weaponId = projectileInfo.weaponId,
            shotId = projectileInfo.shotId,
            origin = projectileInfo.origin,
            dir = projectileInfo.direction,
            speed = projectileInfo.speed,
            range = projectileInfo.range,
            pelletCount = 1
        });
    }
    


}
