using UnityEngine;

public class WeaponAimController : MonoBehaviour
{
    private const float MinDirectionSqrMagnitude = 0.0001f;

    public Transform firePoint;
    private Camera _mainCamera;
    
    // 用来控制“能被瞄准射线命中”的层，排除自己
    [SerializeField] private LayerMask aimLayerMask = ~0;   // 默认全部，建议在 Inspector 里配置

    // 自己角色（用来判断“是不是打到自己”）
    [SerializeField] private Transform ownerRoot; // 角色的根节点，比如 PlayerActor.transform
    
    [SerializeField] private ThirdPersonPlayerController thirdPersonController;

    [Header("=== 瞄准根节点自动旋转 ===")]
    [Tooltip("用于对齐武器模型的根节点。未指定时使用当前 Transform。")]
    [SerializeField] private Transform aimRoot;

    [Tooltip("是否自动旋转 aimRoot，让 aimRoot -> firePoint 向量指向准心目标点。")]
    [SerializeField] private bool autoRotateRootToCrosshair = true;

    [Tooltip("瞄准旋转插值速度。设为 0 时瞬间对齐。")]
    [SerializeField, Min(0f)] private float aimRotationLerpSpeed = 30f;

    [Tooltip("准心射线未命中时使用的最大瞄准距离。")]
    [SerializeField, Min(1f)] private float maxAimDistance = 1000f;

    private void Awake()
    {
        EnsureReferences();
    }

    private void Start()
    {
        EnsureReferences();
    }

    private void LateUpdate()
    {
        UpdateAimRootRotation();
    }

    public void BindCamera(Camera mainCamera)
    {
        _mainCamera = mainCamera;
    }

    public void SetOwnerRoot(Transform owner)
    {
        ownerRoot = owner;
    }

    public void SetAimRoot(Transform root)
    {
        aimRoot = root ? root : transform;
    }

    public void SetPlayerController(ThirdPersonPlayerController controller)
    {
        thirdPersonController = controller;
    }

    // 供外部使用的“逻辑瞄准射线”
    public Ray GetAimRay()
    {
        // 兜底：没有相机的时候，从枪口直射
        if (!_mainCamera)
        {
#if UNITY_EDITOR
            Debug.LogWarning("[WeaponAimController] _mainCamera is null, fallback to firePoint.forward");
#endif
            if (firePoint)
                return new Ray(firePoint.position, firePoint.forward);
            else
                return new Ray(transform.position, transform.forward);
        }

        Ray cameraRay = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 targetPoint = ResolveAimTargetPoint(cameraRay);

        // ④ 计算真正的“逻辑射线”：从枪口指向 targetPoint
        Vector3 origin = firePoint ? firePoint.position : cameraRay.origin;
        Vector3 dir = (targetPoint - origin);

        if (dir.sqrMagnitude < MinDirectionSqrMagnitude)
        {
            // 防止 targetPoint 跟 origin 几乎重合导致 NaN
            dir = cameraRay.direction;
        }
        else
        {
            dir.Normalize();
        }

#if UNITY_EDITOR
        // 绿色：相机射线
        Debug.DrawRay(cameraRay.origin, cameraRay.direction * 50f, Color.green, 1.5f);
        // 红色：最终用于开火的逻辑射线
        Debug.DrawRay(origin, dir * 50f, Color.red, 2f);
#endif

        return new Ray(origin, dir);
    }

    private void EnsureReferences()
    {
        if (!firePoint)
        {
            firePoint = transform.Find("FirePoint");
            if (!firePoint) firePoint = transform;
        }

        if (!aimRoot)
        {
            aimRoot = transform;
        }
    }

    private void UpdateAimRootRotation()
    {
        if (!autoRotateRootToCrosshair || !_mainCamera)
        {
            return;
        }

        EnsureReferences();

        Transform root = aimRoot ? aimRoot : transform;
        if (!root || !firePoint || root == firePoint)
        {
            return;
        }

        Ray cameraRay = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 targetPoint = ResolveAimTargetPoint(cameraRay);
        Vector3 currentAimVector = firePoint.position - root.position;
        Vector3 desiredAimVector = targetPoint - root.position;

        if (currentAimVector.sqrMagnitude < MinDirectionSqrMagnitude ||
            desiredAimVector.sqrMagnitude < MinDirectionSqrMagnitude)
        {
            return;
        }

        Quaternion deltaRotation = Quaternion.FromToRotation(
            currentAimVector.normalized,
            desiredAimVector.normalized
        );
        Quaternion targetRotation = deltaRotation * root.rotation;

        root.rotation = aimRotationLerpSpeed <= 0f
            ? targetRotation
            : Quaternion.Slerp(root.rotation, targetRotation, Time.deltaTime * aimRotationLerpSpeed);
    }

    private Vector3 ResolveAimTargetPoint(Ray cameraRay)
    {
        float distance = Mathf.Max(1f, maxAimDistance);
        if (Physics.Raycast(cameraRay, out var hit, distance, aimLayerMask, QueryTriggerInteraction.Ignore) &&
            !IsHitSelf(hit.collider))
        {
            return hit.point;
        }

        return cameraRay.origin + cameraRay.direction * distance;
    }

    // 判断命中的碰撞体是不是“自己角色”
    private bool IsHitSelf(Collider col)
    {
        if (!ownerRoot) return false;

        // 看命中的物体是不是在 ownerRoot 的层级下面
        return col.transform == ownerRoot || col.transform.IsChildOf(ownerRoot);
    }

    public Vector3 GetFirePointPosition()
    {
        EnsureReferences();
        return firePoint ? firePoint.position : transform.position;
    }
}
