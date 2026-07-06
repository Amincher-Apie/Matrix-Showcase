using Unity.Netcode;
using UnityEngine;
using BehaviorDesigner.Runtime;

public class BossActor : LogicActor
{
    [Header("Boss 基础")]
    [SerializeField] private string _bossConfigId = "Reaper";

    [Header("网络")]
    [SerializeField] private NetworkObject _networkObject;

    [Header("BehaviorDesigner")]
    [SerializeField] private BehaviorTree _behaviorTree;

    [Header("动画与移动")]
    [SerializeField] private Animator _animator;
    [SerializeField] private Rigidbody _rigidbody;
    [SerializeField] private UnityEngine.AI.NavMeshAgent _navMeshAgent;

    [Header("技能挂点")]
    [SerializeField] private Transform _buildPoint;
    [SerializeField] private Transform _footPoint;

    [Header("技能预制体")]
    [SerializeField] private GameObject _laserPrefab;
    [SerializeField] private GameObject _shockWavePrefab;

    [Header("近战伤害判定")]
    [SerializeField] private float _meleeDamage = 15f;
    [SerializeField] private float _meleeHitInvincibilityDuration = 1.5f;

    public override ulong ObjectId => _networkObject != null ? _networkObject.NetworkObjectId : (ulong)GetInstanceID();

    public BossModule BossModule { get; private set; }
    public BossBTBridge Bridge { get; private set; }

    public string BossConfigId => _bossConfigId;
    public BehaviorTree BehaviorTree => _behaviorTree;
    public Animator Animator => _animator;
    public Rigidbody Rigidbody => _rigidbody;
    public UnityEngine.AI.NavMeshAgent NavMeshAgent => _navMeshAgent;
    public Transform BuildPoint => _buildPoint;
    public Transform FootPoint => _footPoint;
    public GameObject LaserPrefab => _laserPrefab;
    public GameObject ShockWavePrefab => _shockWavePrefab;
    public float MeleeDamage => _meleeDamage;
    public float MeleeHitInvincibilityDuration => _meleeHitInvincibilityDuration;

    protected override void RegisterModules()
    {
        BossModule = new BossModule(this);
        AddModule(BossModule);
    }

    private void Reset()
    {
        TryAutoWire();
    }

    protected override void Awake()
    {
        TryAutoWire();
        base.Awake();
        Bridge = GetComponent<BossBTBridge>();
        if (Bridge == null)
            Bridge = gameObject.AddComponent<BossBTBridge>();
    }

    private void TryAutoWire()
    {
        if (_networkObject == null)
            _networkObject = GetComponent<NetworkObject>();
        if (_behaviorTree == null)
            _behaviorTree = GetComponent<BehaviorTree>();
        if (_animator == null)
            _animator = GetComponent<Animator>();
        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody>();
        if (_navMeshAgent == null)
            _navMeshAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (_buildPoint == null)
        {
            var build = transform.Find("build");
            if (build != null) _buildPoint = build;
        }
        if (_footPoint == null)
        {
            var foot = transform.Find("foot");
            if (foot != null) _footPoint = foot;
        }
    }

    public void ActivateAfterSpawn()
    {
        OnActivate();
    }

    // ─── Animation Event 入口（由 Animator 直接调用）────────────────

    public void ShootComplete(int value)   => BossModule?.OnShootComplete(value);
    public void FaceToPlayer(int value)    => BossModule?.OnFaceToPlayer(value);
    public void CreateShockWave()          => BossModule?.CreateShockWave();
    public void OnAttackHit(int model)     => BossModule?.SetAttackHitBox(model, true);
    public void OffAttackHit(int model)    => BossModule?.SetAttackHitBox(model, false);
    public void GetHit(float value)        => BossModule?.GetHit(value);
}
