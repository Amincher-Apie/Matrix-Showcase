using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Framework.Singleton;

/// <summary>
/// 基于单例基类的玩家输入系统封装
/// 职责：统一管理输入事件、状态控制和底层输入转发
/// </summary>
public class PlayerInputSystem : MonoSingletonBase<PlayerInputSystem>, InputController.IPlayerActions
{
    #region 核心字段
    private InputController _inputController; // 底层输入控制器
    private bool _isInputLocked = false;      // 输入锁定状态
    #endregion

    #region 输入事件定义（外部模块通过事件响应输入）
    public event Action<Vector2> OnMovementInput;   // 移动输入事件（方向）
    public event Action<Vector2> OnLookInput;       // 视角输入事件（偏移量）
    public event Action OnJumpInput;                // 跳跃输入事件
    public event Action OnFireInput;                // 开火输入事件
    public event Action OnReloadInput;              // 换弹输入事件
    public event Action OnCastSkillInput;           // 技能输入事件
    public event Action OnCastUltimateInput;        // 终极技能输入事件
    public event Action OnUseActiveItemInput;       // 使用物品输入事件
    public event Action OnOpenMapInput;             // 打开地图输入事件
    public event Action OnOpenInventoryInput;       // 打开背包输入事件
    public event Action OnFireStarted;              // 开火键按下（started 阶段）
    public event Action OnFireCanceled;             // 开火键松开（canceled 阶段）
    public event Action OnSprintInput;
    #endregion

    public bool Sprint => _inputController.Player.Sprint.ReadValue<float>() > 0;
    public bool Jump => _inputController.Player.Jump.ReadValue<float>() > 0;

    /// <summary>
    /// 开火键是否处于按住状态（Auto / Charge 模式需要）
    /// </summary>
    public bool IsFireHeld => _inputController != null && _inputController.Player.Fire.IsPressed();

    #region 单例初始化（重写基类方法）
    /// <summary>
    /// 初始化输入系统（在基类Awake中调用）
    /// </summary>
    protected override void Initialize()
    {
        base.Initialize();
        CreateInputController();
        BindInputCallbacks();
    }

    /// <summary>
    /// 创建并初始化底层输入控制器
    /// </summary>
    private void CreateInputController()
    {
        if (_inputController == null)
        {
            _inputController = new InputController();
        }
    }

    /// <summary>
    /// 绑定输入回调（将底层输入转发到当前类）
    /// </summary>
    private void BindInputCallbacks()
    {
        _inputController.Player.AddCallbacks(this);
    }
    #endregion

    #region 生命周期管理（自动处理输入启用/禁用）
    private void OnEnable()
    {
        if (_inputController != null)
            _inputController.Enable();
    }

    private void OnDisable()
    {
        if (_inputController != null)
            _inputController.Disable();
    }

    /// <summary>
    /// 释放资源（重写基类方法，确保底层资源正确释放）
    /// </summary>
    public override void Release()
    {
        _inputController?.Dispose();
        _inputController = null;
        base.Release();
    }
    #endregion

    #region 输入状态控制（锁定/解锁输入）
    /// <summary>
    /// 锁定或解锁所有输入
    /// </summary>
    /// <param name="isLocked">是否锁定</param>
    public void SetInputLock(bool isLocked)
    {
        _isInputLocked = isLocked;
        // 双重保障：锁定时禁用底层输入，解锁时重新启用
        if (isLocked)
            _inputController?.Disable();
        else
            _inputController?.Enable();
    }

    /// <summary>
    /// 单独启用/禁用某个输入动作
    /// </summary>
    /// <param name="actionName">动作名称（如"Fire"、"Jump"）</param>
    /// <param name="isEnable">是否启用</param>
    public void SetActionEnable(string actionName, bool isEnable)
    {
        var action = _inputController?.FindAction(actionName);
        if (action == null)
        {
            Debug.LogWarning($"输入动作 [{actionName}] 不存在，无法设置状态");
            return;
        }

        if (isEnable)
            action.Enable();
        else
            action.Disable();
    }

    /// <summary>
    /// 通过名称查找 InputAction。
    /// 外部模块（如 InteractionDetector）用此方法获取 Action 引用以检测按键状态。
    /// </summary>
    /// <param name="actionName">动作名称（如 "Interact"）。</param>
    /// <returns>找到的 InputAction，不存在时返回 null。</returns>
    public InputAction FindAction(string actionName)
    {
        return _inputController?.FindAction(actionName);
    }
    #endregion

    #region 直接输入读取
    /// <summary>
    /// 直接获取移动输入值
    /// </summary>
    public Vector2 GetMovementInput()
    {
        return _isInputLocked ? Vector2.zero : _inputController.Player.Movement.ReadValue<Vector2>();
    }

    /// <summary>
    /// 直接获取视角输入值
    /// </summary>
    public Vector2 GetLookInput()
    {
        return _isInputLocked ? Vector2.zero : _inputController.Player.Look.ReadValue<Vector2>();
    }

    #endregion

    #region InputController.IPlayerActions 接口实现（底层输入转发）
    public void OnMovement(InputAction.CallbackContext context)
    {
        if (_isInputLocked) return;
        if (context.performed)
        {
            OnMovementInput?.Invoke(context.ReadValue<Vector2>());
        }
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        if (_isInputLocked) return;
        if (context.performed)
        {
            OnLookInput?.Invoke(context.ReadValue<Vector2>());
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (_isInputLocked) return;
        if (context.performed)
        {
            OnJumpInput?.Invoke();
        }
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        if (_isInputLocked) return;
        if (context.performed)
        {
            OnSprintInput?.Invoke();
        }
    }

    public void OnFire(InputAction.CallbackContext context)
    {
        if (_isInputLocked) return;
        if (context.started)
        {
            OnFireStarted?.Invoke();
        }
        if (context.performed)
        {
            OnFireInput?.Invoke();
        }
        if (context.canceled)
        {
            OnFireCanceled?.Invoke();
        }
    }

    public void OnReload(InputAction.CallbackContext context)
    {
        if (_isInputLocked) return;
        if (context.performed)
        {
            OnReloadInput?.Invoke();
        }
    }

    public void OnCastSkill(InputAction.CallbackContext context)
    {
        if (_isInputLocked) return;
        if (context.performed)
        {
            OnCastSkillInput?.Invoke();
        }
    }

    public void OnCastUltimate(InputAction.CallbackContext context)
    {
        if (_isInputLocked) return;
        if (context.performed)
        {
            OnCastUltimateInput?.Invoke();
        }
    }

    public void OnUseActiveItem(InputAction.CallbackContext context)
    {
        if (_isInputLocked) return;
        if (context.performed)
        {
            OnUseActiveItemInput?.Invoke();
        }
    }

    public void OnOpenMap(InputAction.CallbackContext context)
    {
        if (_isInputLocked) return;
        if (context.performed)
        {
            OnOpenMapInput?.Invoke();
        }
    }

    public void OnOpenInventory(InputAction.CallbackContext context)
    {
        if (_isInputLocked) return;
        if (context.performed)
        {
#if UNITY_EDITOR
            Debug.Log("[PlayerInputSystem] OpenInventory input performed");
#endif
            OnOpenInventoryInput?.Invoke();
        }
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        // InteractionDetector 直接通过 InputAction.WasPressedThisFrame 检测，
        // 此处不需要额外事件转发。只需确保 InputAction 未被锁定。
        // 如果 _isInputLocked 为 true，InputAction 已通过 SetInputLock → Disable 禁用，
        // 不会到达此回调。
    }
    #endregion
}
