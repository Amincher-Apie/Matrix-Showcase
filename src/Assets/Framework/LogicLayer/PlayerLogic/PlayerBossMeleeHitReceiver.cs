using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 玩家侧 Boss 近战受击检测：Trigger 重叠 + Tag "Hit"（对齐 Project A PlayerController.OnTriggerStay）。
/// 仅服务端扣血，经 ServerPlayerAttributeModule → DamageCenter 管线。
/// </summary>
[DisallowMultipleComponent]
public class PlayerBossMeleeHitReceiver : MonoBehaviour
{
    [SerializeField] private float _hitInvincibilityDuration = 1.5f;

    private PlayerNetworkProxy _proxy;
    private float _invincibleUntil;

    public void Initialize(PlayerNetworkProxy proxy, float invincibilityDuration = -1f)
    {
        _proxy = proxy;
        if (invincibilityDuration > 0f)
            _hitInvincibilityDuration = invincibilityDuration;
    }

    private void Awake()
    {
        if (_proxy == null)
            _proxy = GetComponent<PlayerNetworkProxy>();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!ShouldProcessOnServer())
            return;

        if (Time.time < _invincibleUntil)
            return;

        if (!other.CompareTag("Hit"))
            return;

        if (other is BoxCollider box && !box.enabled)
            return;

        var bossActor = other.GetComponentInParent<BossActor>();
        if (bossActor?.BossModule == null)
            return;

        if (!bossActor.BossModule.TryApplyMeleeHit(_proxy, other))
            return;

        _invincibleUntil = Time.time + _hitInvincibilityDuration;
    }

    private bool ShouldProcessOnServer()
    {
        if (_proxy == null)
            _proxy = GetComponent<PlayerNetworkProxy>();
        if (_proxy == null)
            return false;

        var netObj = _proxy.NetworkObject;
        if (netObj == null || !netObj.IsSpawned)
            return true;

        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    }
}
