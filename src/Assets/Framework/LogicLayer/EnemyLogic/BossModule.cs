using System.Collections.Generic;
using BehaviorDesigner.Runtime;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Boss 逻辑模块。
/// 持有运行态数据（HP/Phase/动画事件标志），提供 Command API 给 BT Task 通过 BossBTBridge 调用。
/// 伤害权威由 ServerBossAttributeModule 负责，这里的 _health 仅作本地镜像（单机/测试用）。
/// </summary>
public class BossModule : IModule
{
    private readonly BossActor _owner;

    #region RuntimeState
    private float _health;
    public float MaxHealth = 100f;
    public float Health => _health;
    public bool ShootCompleted { get; private set; }
    public bool IsTurnEnabled { get; private set; }

    /// <summary>单次挥砍窗口内已命中的玩家（key = hit 盒编号 1~3）。</summary>
    private readonly Dictionary<int, HashSet<ulong>> _swingHitTargets = new Dictionary<int, HashSet<ulong>>();
    #endregion

    public ulong ObjectId => _owner.ObjectId;

    public BossModule(BossActor owner)
    {
        _owner = owner;
    }

    public void LocalInit()
    {
        _health = MaxHealth;
        ShootCompleted = false;
        IsTurnEnabled = false;
        _swingHitTargets.Clear();

        InitializeMeleeHitBoxes();

        SetBTBool("isIdle", true);
        SetBTBool("death", false);
        SetBTObject("self", _owner.gameObject);
    }

    public void OnActivate() { }

    public void LocalDestroy()
    {
        ShootCompleted = false;
        IsTurnEnabled = false;
    }

    public void OnShootComplete(int value)
    {
        if (value == 1)
            SpawnLaser();
        ShootCompleted = value == 1;
    }

    public void OnFaceToPlayer(int value)
    {
        IsTurnEnabled = value == 1;
    }

    public void CreateShockWave()
    {
        if (_owner.ShockWavePrefab == null || _owner.FootPoint == null)
            return;

        var wave = Object.Instantiate(_owner.ShockWavePrefab, _owner.FootPoint);
        wave.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        wave.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        wave.transform.parent = null;
    }

    public void SetAttackHitBox(int model, bool enabled)
    {
        if (!IsServerAuthority())
            return;

        var hit = _owner.transform.Find($"hit{model}");
        if (hit == null) return;

        if (enabled)
        {
            if (!_swingHitTargets.TryGetValue(model, out var hitSet))
            {
                hitSet = new HashSet<ulong>();
                _swingHitTargets[model] = hitSet;
            }
            else
            {
                hitSet.Clear();
            }
        }

        var box = hit.GetComponent<BoxCollider>();
        if (box != null)
            box.enabled = enabled;
    }

    /// <summary>
    /// 玩家 Trigger 重叠 Boss hit 盒时调用；同一挥砍窗口对同一玩家只结算一次。
    /// </summary>
    public bool TryApplyMeleeHit(PlayerNetworkProxy target, Collider hitCollider)
    {
        if (!IsServerAuthority() || target == null || hitCollider == null)
            return false;

        if (!TryParseHitIndex(hitCollider, out var hitIndex))
            return false;

        var playerActor = target.PlayerActor;
        if (playerActor == null || !playerActor.IsActiveForAI || !playerActor.IsAliveForAI)
            return false;

        var targetAttr = target.ServerPlayerAttributeModule;
        if (targetAttr == null)
            return false;

        ulong targetId = target.NetworkObjectId;
        if (!_swingHitTargets.TryGetValue(hitIndex, out var hitSet))
        {
            hitSet = new HashSet<ulong>();
            _swingHitTargets[hitIndex] = hitSet;
        }

        if (hitSet.Contains(targetId))
            return false;

        var bossProxy = _owner.GetComponent<BossNetworkProxy>();
        if (bossProxy == null)
            return false;

        ulong sourceId = bossProxy.NetworkObjectId;
        var hitPos = hitCollider.ClosestPoint(target.transform.position);
        var damageInfo = new DamageInfo(PhysicalBulletType.Solid, sourceId, targetId)
        {
            amount = _owner.MeleeDamage,
            hasHitWorldPos = true,
            hitWorldPos = hitPos
        };

        targetAttr.TakeDamage(damageInfo);
        hitSet.Add(targetId);
        return true;
    }

    private void InitializeMeleeHitBoxes()
    {
        for (var i = 1; i <= 3; i++)
        {
            var hit = _owner.transform.Find($"hit{i}");
            if (hit == null)
                continue;

            var box = hit.GetComponent<BoxCollider>();
            if (box == null)
                continue;

            box.isTrigger = true;
            box.enabled = false;
        }
    }

    private static bool TryParseHitIndex(Collider hitCollider, out int hitIndex)
    {
        hitIndex = 0;
        var hitName = hitCollider.gameObject.name;
        if (hitName.Length < 4 || !hitName.StartsWith("hit"))
            return false;

        if (!int.TryParse(hitName.Substring(3), out hitIndex))
            return false;

        return hitIndex >= 1;
    }

    private bool IsServerAuthority()
    {
        var netObj = _owner.GetComponent<NetworkObject>();
        if (netObj == null || !netObj.IsSpawned)
            return true;

        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    }

    public void GetHit(float value)
    {
        if (value <= 0f)
            return;

        var serverAttr = _owner.GetComponent<ServerAttributeModule>();
        if (serverAttr != null && serverAttr.IsServer)
        {
            serverAttr.ModifyAttributeServerRpc(
                AttributeType.Health,
                -value,
                AttributeModifyType.Add,
                ObjectId,
                1);
            return;
        }

        if (_health <= 0f) return;
        _health = Mathf.Max(0f, _health - value);
        if (_health <= 0f)
            SetBTBool("death", true);
    }

    private void SpawnLaser()
    {
        if (_owner.LaserPrefab == null || _owner.BuildPoint == null)
            return;

        var laser = Object.Instantiate(_owner.LaserPrefab, _owner.BuildPoint);
        laser.transform.localPosition = Vector3.zero;
        laser.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        laser.transform.parent = null;
    }

    private void SetBTBool(string key, bool value)
    {
        _owner.BehaviorTree?.SetVariableValue(key, value);
    }

    private void SetBTObject(string key, Object value)
    {
        _owner.BehaviorTree?.SetVariableValue(key, value);
    }
}
