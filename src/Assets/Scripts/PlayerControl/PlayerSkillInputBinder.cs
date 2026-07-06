using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 玩家技能输入绑定器（运行时 AddComponent，方案 B）。
///
/// 职责：
/// - 仅在 IsOwner 时启用
/// - 监听数字键 1-0，映射到技能槽 0-9
/// - 构建 SkillCastContext（相机中心射线）并调用 PlayerSkillModule.TryCastSkill
///
/// 挂载方式：由 PlayerInitializer 在 Owner 客户端运行时 AddComponent。
/// 无需手动挂载到 Prefab。
///
/// 配置手册：docs/phase1-configuration-guide.md
/// </summary>
[DefaultExecutionOrder(100)] // 在 PlayerInitializer(-100) 之后初始化
public class PlayerSkillInputBinder : MonoBehaviour
{
    private PlayerSkillModule _skillModule;
    private PlayerActor _playerActor;
    private Camera _mainCamera;

    /// <summary>支持的最大技能槽数量（数字键 1–0 = 10 个）</summary>
    private const int MaxSlots = 10;

    private void Start()
    {
        _playerActor = GetComponent<PlayerActor>();
        if (_playerActor == null)
        {
            Debug.LogError("[PlayerSkillInputBinder] 未找到 PlayerActor，组件将禁用");
            enabled = false;
            return;
        }

        _skillModule = _playerActor.SkillModule;
        if (_skillModule == null)
        {
            Debug.LogError("[PlayerSkillInputBinder] 未找到 PlayerSkillModule，组件将禁用");
            enabled = false;
            return;
        }

        // 仅 Owner 客户端启用输入监听
        if (!_playerActor.IsOwner)
        {
            Debug.Log($"[PlayerSkillInputBinder] 非 Owner（ObjectId={_playerActor.ObjectId}），禁用输入绑定");
            enabled = false;
            return;
        }

        _mainCamera = Camera.main;
        Debug.Log($"[PlayerSkillInputBinder] 输入绑定就绪 — 数字键 1-0 映射到技能槽 0-{MaxSlots - 1}，" +
                  $"相机: {(_mainCamera != null ? _mainCamera.name : "未找到")}");
    }

    private void Update()
    {
        if (_skillModule == null) return;

        for (int i = 0; i < MaxSlots; i++)
        {
            if (IsSlotKeyPressed(i))
            {
                TryCastSkillInSlot(i);
            }
        }
    }

    // ────────────────────────────
    //  核心方法
    // ────────────────────────────

    /// <summary>尝试释放指定槽位的技能</summary>
    private void TryCastSkillInSlot(int slotIndex)
    {
        var def = _skillModule.GetSkillInSlot(slotIndex);
        if (def == null) return; // 槽位为空，静默忽略

        var ctx = BuildCastContext(def, slotIndex);
        _skillModule.TryCastSkill(slotIndex, ctx);
    }

    /// <summary>根据技能定义和目标类型构建 SkillCastContext</summary>
    private SkillCastContext BuildCastContext(SkillDefinitionSO def, int slotIndex)
    {
        var ctx = new SkillCastContext
        {
            slotIndex = slotIndex,
            skillId = def.id,
            targetType = def.targetType,
        };

        if (_mainCamera != null)
        {
            // origin → 相机位置
            ctx.origin = _mainCamera.transform.position;

            // direction → 相机屏幕中心射线方向
            Ray centerRay = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            ctx.direction = centerRay.direction;

            // point → 射线命中点 或 origin + direction * baseRange 兜底
            if (Physics.Raycast(centerRay, out RaycastHit hit, def.baseRange))
            {
                ctx.point = hit.point;
            }
            else
            {
                ctx.point = centerRay.GetPoint(def.baseRange);
            }
        }
        else
        {
            // 兜底：玩家 Transform 近似
            var t = transform;
            ctx.origin = t.position + Vector3.up * 1.5f;
            ctx.direction = t.forward;
            ctx.point = ctx.origin + ctx.direction * def.baseRange;
        }

        return ctx;
    }

    // ────────────────────────────
    //  输入检测（兼容新旧 Input System）
    // ────────────────────────────

    private static bool IsSlotKeyPressed(int slotIndex)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return slotIndex switch
            {
                0 => Keyboard.current.digit1Key.wasPressedThisFrame,
                1 => Keyboard.current.digit2Key.wasPressedThisFrame,
                2 => Keyboard.current.digit3Key.wasPressedThisFrame,
                3 => Keyboard.current.digit4Key.wasPressedThisFrame,
                4 => Keyboard.current.digit5Key.wasPressedThisFrame,
                5 => Keyboard.current.digit6Key.wasPressedThisFrame,
                6 => Keyboard.current.digit7Key.wasPressedThisFrame,
                7 => Keyboard.current.digit8Key.wasPressedThisFrame,
                8 => Keyboard.current.digit9Key.wasPressedThisFrame,
                9 => Keyboard.current.digit0Key.wasPressedThisFrame,
                _ => false,
            };
        }
#endif
        // 回退：旧 Input Manager
        return Input.GetKeyDown(KeyCodeForSlot(slotIndex));
    }

    private static KeyCode KeyCodeForSlot(int slotIndex)
    {
        return slotIndex switch
        {
            0 => KeyCode.Alpha1,
            1 => KeyCode.Alpha2,
            2 => KeyCode.Alpha3,
            3 => KeyCode.Alpha4,
            4 => KeyCode.Alpha5,
            5 => KeyCode.Alpha6,
            6 => KeyCode.Alpha7,
            7 => KeyCode.Alpha8,
            8 => KeyCode.Alpha9,
            9 => KeyCode.Alpha0,
            _ => KeyCode.None,
        };
    }
}
