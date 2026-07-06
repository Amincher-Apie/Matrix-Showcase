using Framework.LogicLayer.DamageCenter;
using Unity.Netcode;
using UnityEngine;

namespace Matrix.Missions
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DefenseObjective : NetworkBehaviour, IAttackableObject
    {
        public readonly NetworkVariable<float> CurrentHealth = new NetworkVariable<float>(500f);
        public readonly NetworkVariable<float> MaxHealth = new NetworkVariable<float>(500f);
        public readonly NetworkVariable<float> CurrentShield = new NetworkVariable<float>(100f);

        [SerializeField] private float initialMaxHealth = 500f;
        [SerializeField] private float initialShield = 100f;
        [SerializeField] private int threatPriority = 100;
        [SerializeField] private Vector3 targetPointOffset = new Vector3(0f, 1f, 0f);

        private bool _deathNotified;

        public ulong ObjectId => NetworkObjectId;
        public Transform TargetTransform => transform;
        public AttackableObjectType TargetType => AttackableObjectType.MissionTarget;
        public bool IsActiveForAI => isActiveAndEnabled && !_deathNotified;
        public bool IsAliveForAI => CurrentHealth.Value > 0f || CurrentShield.Value > 0f;
        public int ThreatPriority => threatPriority;

        public Vector3 GetTargetPoint()
        {
            return transform.position + targetPointOffset;
        }

        public void Configure(float maxHealth, float shield, int priority)
        {
            initialMaxHealth = Mathf.Max(1f, maxHealth);
            initialShield = Mathf.Max(0f, shield);
            threatPriority = Mathf.Max(1, priority);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _deathNotified = false;

            if (IsServer)
            {
                MaxHealth.Value = Mathf.Max(1f, initialMaxHealth);
                CurrentHealth.Value = MaxHealth.Value;
                CurrentShield.Value = Mathf.Max(0f, initialShield);
                AttackableObjectManager.Instance.Register(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                AttackableObjectManager.Instance.Unregister(this);
            }

            base.OnNetworkDespawn();
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            if (!IsServer || _deathNotified)
            {
                return;
            }

            damageInfo.targetActorId = NetworkObjectId;
            var damageResult = DamageCalculator.ApplyDamage(
                damageInfo,
                CurrentHealth.Value,
                CurrentShield.Value);

            CurrentShield.Value = Mathf.Max(0f, CurrentShield.Value - damageResult.shieldDamage);
            CurrentHealth.Value = Mathf.Max(0f, CurrentHealth.Value - damageResult.healthDamage);
            damageResult.targetDied = CurrentHealth.Value <= 0f && CurrentShield.Value <= 0f;

            EventCenter.Instance.Trigger(EventName.UnitDamaged, new UnitDamagedEvt
            {
                targetId = NetworkObjectId,
                instigatorId = damageInfo.sourceActorId,
                damageResult = damageResult
            });

            var tracker = GetComponent<DamageContributionTracker>();
            if (tracker != null && TryResolveContributorClientId(damageInfo, out var clientId))
            {
                tracker.RecordDamage(clientId, damageResult.totalDamage);
            }

            if (damageResult.targetDied)
            {
                NotifyDeath();
            }
        }

        private void NotifyDeath()
        {
            if (_deathNotified)
            {
                return;
            }

            _deathNotified = true;
            AttackableObjectManager.Instance.Unregister(this);
            EventCenter.Instance.Trigger(EventName.UnitDied, new UnitDiedEvt { unitId = NetworkObjectId });
        }

        private static bool TryResolveContributorClientId(DamageInfo damageInfo, out ulong clientId)
        {
            clientId = 0;

            if (NetworkObjectManager.Instance == null ||
                !NetworkObjectManager.Instance.TryGetNetworkProxy<PlayerNetworkProxy>(
                    damageInfo.sourceActorId,
                    out var playerProxy) ||
                playerProxy == null)
            {
                return false;
            }

            clientId = playerProxy.OwnerClientId;
            return true;
        }
    }
}
