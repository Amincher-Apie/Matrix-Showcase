using System;
using Cinemachine;
using Framework.UI.Core;
using UnityEngine;

/// <summary>
/// 第三人称玩家控制器
/// 负责处理角色移动、相机旋转、跳跃重力、射击转向、静止转向等核心逻辑
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class ThirdPersonPlayerController : MonoBehaviour
{
    private const int DefaultWorldAndNavMeshMask = (1 << 0) | (1 << 8);

    #region 字段分类：相机系统配置
    [Header("=== 相机系统配置 ===")]
    [Space(5)]
    [Tooltip("场景中的主相机，不指定时会自动使用 Camera.main")]
    [SerializeField] private Camera _mainCamera;   // 不再 public，避免被乱改
    // 但加 SerializeField，可以在 Inspector 手动拖
                                             
    [Tooltip("虚拟相机（可选，使用 Cinemachine 时）")]
    [SerializeField] private CinemachineVirtualCamera _virtualCamera;

    [Tooltip("虚拟相机的跟随目标点（Cinemachine相机以此为跟踪对象）")]
    public Transform CinemachineCameraTarget;

    [Tooltip("相机碰撞检测层。默认检测 Default 和 NavMeshGeometry，避免穿过 PCG 房间墙体")]
    [SerializeField] private LayerMask _cameraCollisionLayers = (1 << 0) | (1 << 8);

    [Tooltip("第三人称相机碰撞球半径，用于避免相机贴入或穿过墙体")]
    [SerializeField, Range(0.01f, 1f)] private float _cameraCollisionRadius = 0.3f;

    [Tooltip("Runtime camera follow target offset in local yaw space. Positive X shifts aim over the right shoulder.")]
    [SerializeField] private Vector3 _cameraTargetLocalOffset = new Vector3(0.45f, 0.15f, 0f);

    [Tooltip("相机仰视的最大角度（向上看的极限，单位：度）")]
    public float CameraTopClamp = 70f;
    
    [Tooltip("相机俯视的最大角度（向下看的极限，单位：度）")]
    public float CameraBottomClamp = -30f;
    
    [Tooltip("是否锁定相机旋转（锁定后鼠标无法拖动调整相机角度）")]
    public bool IsCameraLocked = false;
    
    [Tooltip("鼠标控制相机的灵敏度（值越大，相机旋转越快）")]
    public float MouseSensitivity = 1.0f;

    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;
    private Transform _runtimeCameraTarget;
    #endregion

    #region 字段分类：输入系统配置
    [Header("=== 输入系统配置 ===")]
    [Space(5)]
    
    private PlayerInputSystem _playerInputSystem;
    #endregion

    #region 字段分类：移动系统配置
    [Header("=== 移动系统配置 ===")]
    [Space(5)]
    
    [Tooltip("角色正常步行速度（单位：米/秒）")]
    public float WalkSpeed = 1.0f;
    
    [Tooltip("角色冲刺奔跑速度（单位：米/秒）")]
    public float SprintSpeed = 5.0f;
    
    [Tooltip("普通移动时的转向平滑时间（值越小，转向越灵敏）")]
    public float NormalRotationSmoothTime = 0.12f;
    
    [Tooltip("速度变化率（值越大，加速/减速越快）")]
    public float SpeedChangeRate = 10.0f;
    
    [Tooltip("重力加速度（建议设为负数，模拟向下的重力）")]
    public float Gravity = -15.0f;
    
    [Tooltip("角色下落的最大速度（防止下落过快）")]
    public float TerminalFallSpeed = -53.0f; // 改为负值，表示向下的最大速度
    #endregion

    #region 字段分类：射击转向配置
    [Header("=== 射击转向配置 ===")]
    [Space(5)]
    
    [Tooltip("射击时允许的最大角度偏差（超过此值则自动转向相机方向）")]
    public float MaxShootAngleDifference = 15f;
    
    [Tooltip("是否在射击时强制转向相机方向（开启后射击必转向）")]
    public bool ForceRotateOnShoot = true;
    
    [Tooltip("射击转向的平滑时间（比普通转向快，确保响应灵敏）")]
    public float ShootRotationSmoothTime = 0.05f;
    #endregion

    #region 字段分类：静止转向配置
    [Header("=== 静止转向配置 ===")]
    [Space(5)]
    
    [Tooltip("静止时是否随相机自动转向（无移动输入时生效）")]
    public bool RotateWhenIdle = true;
    
    [Tooltip("静止时允许的最大角度偏差（超过此值则自动转向相机方向）")]
    public float IdleMaxAngleDifference = 5f;
    
    [Tooltip("静止转向的平滑时间（控制转向的平滑程度）")]
    public float IdleRotationSmoothTime = 0.1f;
    #endregion

    #region 字段分类：地面检测配置
    [Header("=== 地面检测配置 ===")]
    [Space(5)]
    
    [Tooltip("角色当前是否在地面上（由代码自动更新，无需手动修改）")]
    public bool IsGrounded;
    
    [Tooltip("地面检测球体的Y轴偏移（确保检测点在角色底部）")]
    public float GroundCheckYOffset = 0.14f; // 调整为更合理的默认值
    
    [Tooltip("地面检测球体的半径（建议与CharacterController半径一致）")]
    public float GroundCheckSphereRadius = 0.3f;
    
    [Tooltip("哪些图层会被判定为地面（只检测该图层的物体）")]
    public LayerMask GroundLayers = DefaultWorldAndNavMeshMask;
    #endregion

    #region 字段分类：跳跃系统配置
    [Header("=== 跳跃系统配置 ===")]
    [Space(5)]
    
    [Tooltip("角色跳跃高度（单位：米）")]
    public float JumpHeight = 1.2f;
    
    [Tooltip("两次跳跃的最小间隔（设为0可无限跳）")]
    public float JumpCooldown = 0.50f;
    
    [Tooltip("离开地面后延迟多久判定为下落（避免走台阶误判）")]
    public float FallJudgeDelay = 0.15f;
    
    private float _jumpCooldownTimer;
    private float _fallJudgeTimer;
    #endregion

    #region 字段分类：组件与状态变量
    private CharacterController _characterController;
    private Animator _animator;
    private float _currentMoveSpeed;
    private float _targetRotationAngle;
    private float _rotationVelocity;
    private float _verticalVelocity;
    private PlayerActor _playerActor;
    private PlayerNetworkProxy _networkProxy;
    private WeaponAimController _weaponAimController;
    private ulong ActorId => _playerActor ? _playerActor.ObjectId : 0;
    private bool _isLocalPlayerInitialized = false;
    private bool _inputEnabled = false;
    private bool _inputEventsRegistered = false;
    private bool _isFireHeld = false;
    public bool IsLocalPlayer { get; private set; }
    #endregion

    #region 初始化
    
    private void Awake()
    {
        _playerInputSystem = PlayerInputSystem.Instance;
        if (!_playerInputSystem)
        {
            Debug.LogError("PlayerInputSystem 单例未找到，请确保已创建 PlayerInputSystem 实例！");
        }
        _playerActor = GetComponent<PlayerActor>();
        _networkProxy = GetComponent<PlayerNetworkProxy>();
        _characterController = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();

        if (_mainCamera == null) _mainCamera = Camera.main;
        EnsureCinemachineBrain();

        // 优先查找自身子物体的 VCam，避免 ParrelSync 多客户端时抢夺其他玩家的摄像机
        if (_virtualCamera == null)
            _virtualCamera = GetComponentInChildren<CinemachineVirtualCamera>(includeInactive: true);
    }
    
    private void OnEnable()
    {
        RegisterInputEvents();
    }
    
    private void Start()
    {

        _playerActor = GetComponent<PlayerActor>();
        _networkProxy = GetComponent<PlayerNetworkProxy>();
        
        if (!_playerActor)
        {
            Debug.LogWarning("PlayerActor 未找到 on this GameObject.", this);
        }
    
        if (!_networkProxy)
        {
            Debug.LogWarning("PlayerNetworkProxy 未找到 on this GameObject.", this);
        }

        _characterController = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();
        
        // 1. 不是本地玩家就禁用相机和输入
        if (_networkProxy && !_networkProxy.IsOwner)
        {
            enabled = false;
            return;
        }

        // 2. 相机引用：优先用 Inspector 里拖的，否则用 Camera.main
        if (!_mainCamera)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                Debug.LogError("未找到 Tag 为 \"MainCamera\" 的相机，请检查相机配置！");
            }
        }

        // 3. VCam 已在 Awake 中通过 GetComponentInChildren 获取，这里仅做兜底

        // if (_virtualCamera != null && CinemachineCameraTarget != null)
        // {
        //     _virtualCamera.Follow = CinemachineCameraTarget;
        //     
        // }
        // else if (_virtualCamera == null)
        // {
        //     Debug.LogWarning("未找到 CinemachineVirtualCamera，若使用 Cinemachine，请在 Inspector 指定或场景中放置一个虚拟相机。");
        // }

        // if (CinemachineCameraTarget)
        // {
        //     _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
        //     _cinemachineTargetPitch = CinemachineCameraTarget.transform.rotation.eulerAngles.x;
        // }
        // else
        // {
        //     Debug.LogError("未设置 CinemachineCameraTarget，请在Inspector中指定相机跟随目标！");
        // }

        _jumpCooldownTimer = 0; // 初始化为0，允许立即跳跃
        EnsureCinemachineBrain();
        _fallJudgeTimer = FallJudgeDelay;

        // HideAndLockMouse();
    }
    
    public void InitAsLocalPlayer()
    {
        if (_isLocalPlayerInitialized) return;

        _isLocalPlayerInitialized = true;
        _inputEnabled = true;
        IsLocalPlayer = true;

        EnsureCinemachineBrain();

        // 绑定相机，并提升本机 VCam 优先级防止多客户端抢夺
        if (_virtualCamera && CinemachineCameraTarget)
        {
            EnsureRuntimeCameraTarget();
            _virtualCamera.Follow = _runtimeCameraTarget ? _runtimeCameraTarget : CinemachineCameraTarget;
            _virtualCamera.Priority = 100;
            ConfigureCameraCollision();
        }

        if (CinemachineCameraTarget)
        {
            _cinemachineTargetYaw = CinemachineCameraTarget.eulerAngles.y;
            _cinemachineTargetPitch = CinemachineCameraTarget.eulerAngles.x;
            SyncRuntimeCameraTarget();
        }

        // 武器瞄准绑定（防御式）
        if (_weaponAimController != null && _mainCamera && _playerActor)
        {
            _weaponAimController.BindCamera(_mainCamera);
            _weaponAimController.SetOwnerRoot(_playerActor.transform);
        }

        HideAndLockMouse();
        RegisterInputEvents();
    }

    private void OnDisable()
    {
        UnregisterInputEvents();
    }

    private void RegisterInputEvents()
    {
        if (_inputEventsRegistered)
        {
            return;
        }

        _playerInputSystem ??= PlayerInputSystem.Instance;
        if (_playerInputSystem == null)
        {
            return;
        }

        _playerInputSystem.OnFireStarted += OnFireStarted;
        _playerInputSystem.OnFireInput += OnFireInput;
        _playerInputSystem.OnFireCanceled += OnFireCanceled;
        _playerInputSystem.OnReloadInput += OnReloadInput;
        _playerInputSystem.OnOpenInventoryInput += OnOpenInventoryInput;
        _inputEventsRegistered = true;
    }

    private void UnregisterInputEvents()
    {
        if (!_inputEventsRegistered || _playerInputSystem == null)
        {
            return;
        }

        _playerInputSystem.OnFireStarted -= OnFireStarted;
        _playerInputSystem.OnFireInput -= OnFireInput;
        _playerInputSystem.OnFireCanceled -= OnFireCanceled;
        _playerInputSystem.OnReloadInput -= OnReloadInput;
        _playerInputSystem.OnOpenInventoryInput -= OnOpenInventoryInput;
        _inputEventsRegistered = false;
    }

    private void EnsureCinemachineBrain()
    {
        if (!_mainCamera)
        {
            return;
        }

        if (!_mainCamera.GetComponent<CinemachineBrain>())
        {
            _mainCamera.gameObject.AddComponent<CinemachineBrain>();
        }
    }

    private void ConfigureCameraCollision()
    {
        if (!_virtualCamera)
        {
            return;
        }

        var thirdPersonFollow =
            _virtualCamera.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        if (thirdPersonFollow == null)
        {
            Debug.LogWarning(
                "虚拟相机未使用 Cinemachine3rdPersonFollow，无法配置第三人称相机碰撞。",
                _virtualCamera
            );
            return;
        }

        thirdPersonFollow.CameraCollisionFilter = _cameraCollisionLayers;
        thirdPersonFollow.CameraRadius = Mathf.Max(0.01f, _cameraCollisionRadius);
    }

    private void EnsureRuntimeCameraTarget()
    {
        if (_runtimeCameraTarget || !CinemachineCameraTarget)
        {
            return;
        }

        var runtimeTarget = new GameObject($"{name}_CameraRuntimeTarget");
        runtimeTarget.transform.SetParent(transform, worldPositionStays: true);
        _runtimeCameraTarget = runtimeTarget.transform;
        SyncRuntimeCameraTarget();
    }

    private void SyncRuntimeCameraTarget()
    {
        if (!_runtimeCameraTarget || !CinemachineCameraTarget)
        {
            return;
        }

        Quaternion yawRotation = Quaternion.Euler(0f, _cinemachineTargetYaw, 0f);
        _runtimeCameraTarget.position = CinemachineCameraTarget.position + yawRotation * _cameraTargetLocalOffset;
        _runtimeCameraTarget.rotation = Quaternion.Euler(
            _cinemachineTargetPitch,
            _cinemachineTargetYaw,
            0f
        );
    }

    public void BindWeaponController(WeaponAimController weaponAimer)
    {
        _weaponAimController = weaponAimer;
        
        if (_playerActor == null) _playerActor = GetComponent<PlayerActor>();
        if (_mainCamera == null) _mainCamera = Camera.main;
        
        // ✅ 如果已经是本地玩家并已完成初始化，补一次绑定
        if (IsLocalPlayer && _mainCamera && _playerActor && _weaponAimController)
        {
            _weaponAimController.BindCamera(_mainCamera);
            _weaponAimController.SetOwnerRoot(_playerActor.transform);
        }
    }

    // private void AwokeWeaponAimer()
    // {
    //     _weaponAimController.BindCamera(_mainCamera);
    //     _weaponAimController.SetOwnerRoot(_playerActor.transform);
    // }

    #endregion

    #region APIFunctions

    public void SetInputEnabled(bool enabling)
    {
        _inputEnabled = enabling;
    }

    #endregion
    
    #region 输入回调

    /// <summary>
    /// 开火键 performed 回调（Semi 模式在此处理；Auto/Charge 由 Update 驱动）
    /// </summary>
    private void OnFireInput()
    {
        if (_networkProxy == null || _playerActor == null)
            return;
        if (!_networkProxy.IsOwner)
            return;

        var combat = _playerActor.CombatModule;
        if (combat == null)
            return;

        var fireMode = combat.CurrentConfig?.WeaponSO?.fireMode ?? FireMode.Semi;

        // Semi 模式：每次 performed 打一发
        if (fireMode == FireMode.Semi)
        {
            HandleShootRotation();
            if (!combat.CanFire())
            {
                Debug.Log("服务器/逻辑体判定当前无法开火");
                return;
            }
#if UNITY_EDITOR
            Debug.Log($"[ThirdPersonPlayerController] Semi fire at {Time.time} from {_playerActor.ObjectId}");
#endif
            combat.TryFire(CreateFireContext());
        }
        // Auto / Charge 由 Update 驱动，不在此处理
    }

    /// <summary>
    /// 开火键按下（started 阶段）— Auto 标记按住，Charge 开始蓄力
    /// </summary>
    private void OnFireStarted()
    {
        if (_networkProxy == null || _playerActor == null || !_networkProxy.IsOwner)
            return;

        var combat = _playerActor.CombatModule;
        if (combat == null)
            return;

        _isFireHeld = true;
        var fireMode = combat.CurrentConfig?.WeaponSO?.fireMode ?? FireMode.Semi;

        if (fireMode == FireMode.Charge)
        {
            combat.StartCharge();
        }
    }

    /// <summary>
    /// 开火键松开（canceled 阶段）— Auto 停止连发，Charge 释放蓄力
    /// </summary>
    private void OnFireCanceled()
    {
        _isFireHeld = false;

        if (_networkProxy == null || _playerActor == null || !_networkProxy.IsOwner)
            return;

        var combat = _playerActor.CombatModule;
        if (combat == null)
            return;

        var fireMode = combat.CurrentConfig?.WeaponSO?.fireMode ?? FireMode.Semi;

        if (fireMode == FireMode.Charge)
        {
            combat.ReleaseCharge(CreateFireContext());
        }
    }

    private void OnReloadInput()
    {
        if (_networkProxy == null || _playerActor == null)
        {
            return;
        }

        if (!_networkProxy.IsOwner)
        {
            return;
        }

        _playerActor.CombatModule?.Reload();
    }

    private void OnOpenInventoryInput()
    {
        if (_networkProxy == null || !_networkProxy.IsOwner)
        {
            return;
        }

        InventoryWindow inventoryWindow = UIManager.Instance.GetWindow(nameof(InventoryWindow)) as InventoryWindow;
        if (inventoryWindow != null && inventoryWindow.Visible)
        {
            inventoryWindow.HideWindow();
            return;
        }

        UIManager.Instance.PopUpWindow<InventoryWindow>();
    }

    #region FireContext 构造（非 NetworkBehaviour）

    /// <summary>
    /// 构造开火上下文。注意：不在这里写入 instigator。
    /// instigator 将在 PlayerNetworkProxy.FireServerRpc 内被覆盖。
    /// </summary>
    private FireContext CreateFireContext()
    {
        var ctx = new FireContext
        {
            shooterObjectId = _playerActor ? _playerActor.ObjectId : 0
        };

        if (_weaponAimController)
        {
            Ray aimRay = _weaponAimController.GetAimRay();
            ctx.origin = aimRay.origin;
            ctx.dir    = aimRay.direction;
#if UNITY_EDITOR
            Debug.Log($"[CreateFireContext] origin={ctx.origin}, dir={ctx.dir}");
            Debug.DrawRay(ctx.origin, ctx.dir * 50f, Color.yellow, 1f);
#endif
        }
        else
        {
            // 兜底：从角色正前方射出
            ctx.origin = transform.position + transform.forward * 0.5f;
            ctx.dir    = transform.forward;
        }

#if UNITY_EDITOR
        Debug.Log($"[CreateFireContext] shooterObjectId = {ctx.shooterObjectId}");
#endif

        return ctx;
    }

    #endregion

    #endregion
    private void Update()
    {
        _playerActor?.CombatModule?.TickReloadState(Time.time);

        if (!_inputEnabled) return;

        if (!_characterController || !_playerInputSystem || !_mainCamera) return;

        // === FireMode: Auto / Charge 持续逻辑 ===
        if (_isFireHeld && _playerActor?.CombatModule != null)
        {
            var combat = _playerActor.CombatModule;
            var fireMode = combat.CurrentConfig?.WeaponSO?.fireMode ?? FireMode.Semi;

            switch (fireMode)
            {
                case FireMode.Auto:
                    HandleShootRotation();
                    combat.TryFire(CreateFireContext());
                    break;
                case FireMode.Charge:
                    combat.UpdateCharge();
                    break;
            }
        }

        CheckGroundedState();
        HandleJumpAndGravity();
        HandleCharacterMovement();
        WriteAnimatorParameters();

        if (RotateWhenIdle && _playerInputSystem.GetMovementInput() == Vector2.zero)
        {
            HandleIdleRotation();
        }
    }


    private void LateUpdate()
    {
        if (!_inputEnabled) return;
        if (!_mainCamera)
        {
            return;
        }

        HandleCameraRotation();
    }


    private void HandleCameraRotation()
    {
        Vector2 mouseLookInput = _playerInputSystem.GetLookInput();

        float yawIncrement = mouseLookInput.x * MouseSensitivity;
        float pitchIncrement = -mouseLookInput.y * MouseSensitivity;

        _cinemachineTargetYaw += yawIncrement;
        _cinemachineTargetPitch += pitchIncrement;

        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, CameraBottomClamp, CameraTopClamp);
        _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);

        if (_runtimeCameraTarget)
        {
            SyncRuntimeCameraTarget();
        }
        else if (CinemachineCameraTarget != null && !IsCameraLocked)
        {
            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(
                _cinemachineTargetPitch,
                _cinemachineTargetYaw,
                0f
            );
        }
    }


    private void HandleIdleRotation()
    {
        Quaternion cameraHorizontalRot = Quaternion.Euler(0f, _mainCamera.transform.eulerAngles.y, 0f);
        Quaternion playerHorizontalRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        float angleDifference = Quaternion.Angle(playerHorizontalRot, cameraHorizontalRot);

        if (angleDifference > IdleMaxAngleDifference)
        {
            _targetRotationAngle = _mainCamera.transform.eulerAngles.y;

            float smoothRotation = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                _targetRotationAngle,
                ref _rotationVelocity,
                smoothTime: IdleRotationSmoothTime
            );

            transform.rotation = Quaternion.Euler(0f, smoothRotation, 0f);
        }
    }


    private void HandleCharacterMovement()
    {
        float targetSpeed = _playerInputSystem.Sprint ? SprintSpeed : WalkSpeed;
        if (_playerInputSystem.GetMovementInput() == Vector2.zero)
        {
            targetSpeed = 0f;
        }

        float currentHorizontalSpeed = new Vector3(
            _characterController.velocity.x,
            0f,
            _characterController.velocity.z
        ).magnitude;

        float speedOffset = 0.1f;
        if (Mathf.Abs(currentHorizontalSpeed - targetSpeed) > speedOffset)
        {
            _currentMoveSpeed = Mathf.Lerp(
                a: currentHorizontalSpeed,
                b: targetSpeed,
                t: Time.deltaTime * SpeedChangeRate
            );
            _currentMoveSpeed = Mathf.Round(_currentMoveSpeed * 1000f) / 1000f;
        }
        else
        {
            _currentMoveSpeed = targetSpeed;
        }

        Vector3 inputDirection = new Vector3(
            x: _playerInputSystem.GetMovementInput().x,
            y: 0f,
            z: _playerInputSystem.GetMovementInput().y
        ).normalized;

        if (inputDirection != Vector3.zero)
        {
            _targetRotationAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + _mainCamera.transform.eulerAngles.y;

            float smoothRotation = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                _targetRotationAngle,
                ref _rotationVelocity,
                smoothTime: NormalRotationSmoothTime
            );

            transform.rotation = Quaternion.Euler(0f, smoothRotation, 0f);
        }

        Vector3 finalMoveDirection = Quaternion.Euler(0f, _targetRotationAngle, 0f) * Vector3.forward;

        _characterController.Move(
            finalMoveDirection.normalized * (_currentMoveSpeed * Time.deltaTime) + 
            new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime
        );
    }


    private void WriteAnimatorParameters()
    {
        if (_animator == null) return;

        Vector2 input = _playerInputSystem.GetMovementInput();
        float speedNorm = _currentMoveSpeed / SprintSpeed;

        _animator.SetFloat("MoveX", input.x * speedNorm);
        _animator.SetFloat("MoveY", input.y * speedNorm);
        _animator.SetFloat("Speed", speedNorm);
        _animator.SetBool("IsGround", IsGrounded);
    }

    public void HandleShootRotation()
    {
        if (_mainCamera == null)
        {
            return;
        }

        Quaternion cameraHorizontalRot = Quaternion.Euler(0f, _mainCamera.transform.eulerAngles.y, 0f);
        Quaternion playerHorizontalRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        float angleDifference = Quaternion.Angle(playerHorizontalRot, cameraHorizontalRot);

        if (ForceRotateOnShoot || angleDifference > MaxShootAngleDifference)
        {
            _targetRotationAngle = _mainCamera.transform.eulerAngles.y;

            float smoothRotation = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                _targetRotationAngle,
                ref _rotationVelocity,
                smoothTime: ShootRotationSmoothTime
            );

            transform.rotation = Quaternion.Euler(0f, smoothRotation, 0f);
        }
    }


    private void CheckGroundedState()
    {
        Vector3 sphereCheckPosition = new Vector3(
            x: transform.position.x,
            y: transform.position.y - GroundCheckYOffset,
            z: transform.position.z
        );

        int groundMask = GroundLayers.value != 0 ? GroundLayers.value : DefaultWorldAndNavMeshMask;
        bool sphereGrounded = Physics.CheckSphere(
            sphereCheckPosition,
            GroundCheckSphereRadius,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        bool controllerGrounded = _characterController != null && _characterController.isGrounded;
        IsGrounded = sphereGrounded || controllerGrounded;
    }


    private void HandleJumpAndGravity()
    {
        if (IsGrounded)
        {
            _fallJudgeTimer = FallJudgeDelay;

            if (_verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }

            if (_playerInputSystem.Jump && _jumpCooldownTimer <= 0f)
            {
                _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                _jumpCooldownTimer = JumpCooldown;
            }

            if (_jumpCooldownTimer > 0f)
            {
                _jumpCooldownTimer -= Time.deltaTime;
            }

        }
        else
        {
            _jumpCooldownTimer = JumpCooldown;

            if (_fallJudgeTimer > 0f)
            {
                _fallJudgeTimer -= Time.deltaTime;
            }


            // 修复重力应用逻辑：当垂直速度大于终端速度时（即下落速度还没达到最大），继续应用重力
            if (_verticalVelocity > TerminalFallSpeed)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
                // 确保速度不会超过终端速度
                _verticalVelocity = Mathf.Max(_verticalVelocity, TerminalFallSpeed);
            }
        }
    }


    private void HideAndLockMouse()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }


    public void ShowAndUnlockMouse()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    /// <summary>
    /// 静默此控制器的输入和鼠标。常用于测试场景中防止玩家角色捕获 Editor 窗口的输入。
    /// </summary>
    public void Silence()
    {
        _inputEnabled = false;
        ShowAndUnlockMouse();
    }


    private static float ClampAngle(float angle, float minAngle, float maxAngle)
    {
        if (angle < -360f)
        {
            angle += 360f;
        }
        if (angle > 360f)
        {
            angle -= 360f;
        }

        return Mathf.Clamp(angle, minAngle, maxAngle);
    }


    private void OnDrawGizmosSelected()
    {
        Vector3 sphereCheckPosition = new Vector3(
            x: transform.position.x,
            y: transform.position.y - GroundCheckYOffset,
            z: transform.position.z
        );

        Gizmos.color = IsGrounded ? new Color(0f, 1f, 0f, 0.35f) : new Color(1f, 0f, 0f, 0.35f);

        Gizmos.DrawSphere(sphereCheckPosition, GroundCheckSphereRadius);
    }
}
