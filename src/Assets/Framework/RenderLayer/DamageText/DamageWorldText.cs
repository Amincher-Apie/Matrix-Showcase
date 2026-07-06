using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class DamageWorldText : MonoBehaviour
{
    [Header("Life")]
    public float lifeTime = 3f;
    public float fadeDuration = 0.5f;

    [Header("Float (Screen Space)")]
    public AnimationCurve floatCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float floatHeightPixels = 20f;
    public float randomRadiusPixels = 25f;

    [Header("Pop Scale")]
    public float scaleAnimationDuration = 0.2f;
    public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public Vector3 maxScale = new Vector3(1.5f, 1.5f, 1.5f);

    [Header("Font Size (UI TMP)")]
    public float minFontSize = 24f;
    public float maxFontSize = 40f;

    [Header("Offscreen Handling")]
    [Tooltip("目标点在屏幕外/相机背后时隐藏（避免镜头转走数字还留在镜头里）")]
    public bool hideWhenOffscreen = true;

    [Tooltip("如果 offscreen 就直接回收（更干净），否则只是隐藏，时间到再回收")]
    public bool returnToPoolWhenOffscreen = false; // 性能不足时可以开

    private TextMeshProUGUI _tmp;
    private RectTransform _rt;
    private Canvas _canvas;
    private RectTransform _canvasRt;

    private Camera _cam;
    private Vector3 _worldPos;

    private float _timer;
    private Vector2 _randOffsetPx;
    private Vector3 _originalScale;

    private void Awake()
    {
        _tmp = GetComponent<TextMeshProUGUI>();
        _rt = transform as RectTransform;
        _canvas = GetComponentInParent<Canvas>();
        _canvasRt = _canvas ? _canvas.transform as RectTransform : null;

        _originalScale = _rt.localScale;

        // 关键：让 pivot 固定为中心，UI 位置就是“视觉中心”
        _rt.pivot = new Vector2(0.5f, 0.5f);
        _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);

        // 实例化材质，避免共享材质导致描边/颜色互相影响
        _tmp.fontMaterial = new Material(_tmp.fontSharedMaterial);
        ApplyOutline(true, Color.black, 0.25f, 0.0f);
    }

    public void Play(
        Vector3 hitWorldPos,
        float damageValue,
        bool isCritical,
        Color damageTypeColor,
        string elementSpriteName,
        Camera cam,
        float minDamageForScaling,
        float maxDamageForScaling
    )
    {
        _cam = cam ? cam : Camera.main;
        _worldPos = hitWorldPos;

        // 随机屏幕偏移
        float a = Random.Range(0f, 2f * Mathf.PI);
        float r = Random.Range(0f, randomRadiusPixels);
        _randOffsetPx = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);

        // 排版（UI 版建议居中，避免你之前的 alignment 引起位置反常）
        _tmp.alignment = TextAlignmentOptions.Center;
        _tmp.enableWordWrapping = false;
        _tmp.overflowMode = TextOverflowModes.Overflow;

        // 字号随伤害变化
        float ratio = Mathf.Clamp01(Mathf.InverseLerp(minDamageForScaling, maxDamageForScaling, damageValue));
        _tmp.fontSize = Mathf.Lerp(minFontSize, maxFontSize, ratio);

        // 暴击样式 + pop
        if (isCritical)
        {
            _tmp.fontStyle = FontStyles.Bold;
            StartScaleAnimation(maxScale * 1.2f);
        }
        else
        {
            _tmp.fontStyle = FontStyles.Normal;
            StartScaleAnimation(maxScale);
        }

        // 文本：数字 + 图标
        string num = FormatDamage(damageValue);
        string iconTag = elementSpriteName ?? string.Empty;
        _tmp.text = string.IsNullOrEmpty(iconTag) ? num : $"{num} {iconTag}";

        ApplyFaceColor(damageTypeColor);
        SetAlpha(1f);

        _timer = 0f;
        _rt.localScale = Vector3.zero;

        // 立即对齐一次位置（避免首帧闪跳）
        UpdateScreenPosition(0f, immediate: true);
    }

    private void LateUpdate()
    {
        _timer += Time.deltaTime;

        float t = Mathf.Clamp01(_timer / lifeTime);
        float cv = floatCurve.Evaluate(t);

        // 每帧把 worldPos -> screenPos（镜头转走就会自然离开屏幕）
        bool visible = UpdateScreenPosition(cv, immediate: false);

        if (!visible && hideWhenOffscreen && returnToPoolWhenOffscreen)
        {
            DamageWorldTextManager.Instance.ReturnToPool(gameObject);
            return;
        }

        // 淡出
        if (_timer > lifeTime - fadeDuration)
        {
            float a = 1f - ((_timer - (lifeTime - fadeDuration)) / fadeDuration);
            SetAlpha(a);
        }

        if (_timer >= lifeTime)
        {
            DamageWorldTextManager.Instance.ReturnToPool(gameObject);
        }
    }

    /// <summary>
    /// 返回是否“在屏幕内且在相机前方”
    /// </summary>
    private bool UpdateScreenPosition(float curveValue, bool immediate)
    {
        if (!_cam) return false;

        // 1) world -> screen
        Vector3 sp = _cam.WorldToScreenPoint(_worldPos);

        // 背后：z < 0
        bool inFront = sp.z > 0.01f;

        // 2) 屏幕内判定（不做 clamp！避免“镜头转走数字还在屏幕里”）
        bool inScreen =
            sp.x >= 0f && sp.x <= Screen.width &&
            sp.y >= 0f && sp.y <= Screen.height;

        bool visible = inFront && inScreen;

        if (hideWhenOffscreen)
        {
            // 隐藏但不改变生命周期（除非 returnToPoolWhenOffscreen）
            _tmp.enabled = visible;
        }
        else
        {
            _tmp.enabled = true;
        }

        if (!visible && hideWhenOffscreen)
            return false;

        // 3) screen -> canvas local
        Vector2 localPoint;
        if (_canvas && _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Overlay：camera 传 null
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, sp, null, out localPoint);
        }
        else
        {
            // ScreenSpace-Camera / WorldSpace：传 canvas.worldCamera 更稳
            Camera uiCam = _canvas && _canvas.worldCamera ? _canvas.worldCamera : _cam;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, sp, uiCam, out localPoint);
        }

        // 4) 浮动 + 随机偏移（像素空间）
        float up = floatHeightPixels * curveValue;
        Vector2 offset = new Vector2(_randOffsetPx.x * curveValue, _randOffsetPx.y * curveValue + up);

        _rt.anchoredPosition = localPoint + offset;
        return true;
    }

    private void StartScaleAnimation(Vector3 targetScale)
    {
        StopAllCoroutines();
        StartCoroutine(ScaleRoutine(targetScale));
    }

    private IEnumerator ScaleRoutine(Vector3 targetScale)
    {
        float tt = 0f;

        while (tt < scaleAnimationDuration)
        {
            tt += Time.deltaTime;
            float n = Mathf.Clamp01(tt / scaleAnimationDuration);
            float c = scaleCurve.Evaluate(n);

            if (tt < scaleAnimationDuration * 0.5f)
            {
                _rt.localScale = Vector3.Lerp(Vector3.zero, targetScale, c * 2f);
            }
            else
            {
                float rec = (tt - scaleAnimationDuration * 0.5f) / (scaleAnimationDuration * 0.5f);
                _rt.localScale = Vector3.Lerp(targetScale, _originalScale, rec);
            }

            yield return null;
        }

        _rt.localScale = _originalScale;
    }

    private void SetAlpha(float a)
    {
        var c = _tmp.color;
        c.a = a;
        _tmp.color = c;

        var mat = _tmp.fontMaterial;
        var face = mat.GetColor(ShaderUtilities.ID_FaceColor);
        face.a = a;
        mat.SetColor(ShaderUtilities.ID_FaceColor, face);

        var oc = mat.GetColor(ShaderUtilities.ID_OutlineColor);
        oc.a = a;
        mat.SetColor(ShaderUtilities.ID_OutlineColor, oc);
    }

    private static string FormatDamage(float damage)
    {
        if (damage < 1000) return Mathf.RoundToInt(damage).ToString();

        if (damage < 1_000_000)
        {
            float k = damage / 1000f;
            if (k >= 100) return Mathf.RoundToInt(k) + "K";
            return (k.ToString("0.#") + "K").Replace(".0", "");
        }

        float m = damage / 1_000_000f;
        if (m >= 10) return Mathf.RoundToInt(m) + "M";
        return (m.ToString("0.#") + "M").Replace(".0", "");
    }

    private void ApplyOutline(bool enabled, Color outlineColor, float outlineWidth, float outlineSoftness)
    {
        var mat = _tmp.fontMaterial;
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, enabled ? outlineWidth : 0f);
        mat.SetFloat(ShaderUtilities.ID_OutlineSoftness, outlineSoftness);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
    }

    private void ApplyFaceColor(Color c)
    {
        _tmp.color = c;
        var mat = _tmp.fontMaterial;
        mat.SetColor(ShaderUtilities.ID_FaceColor, c);
    }
}
