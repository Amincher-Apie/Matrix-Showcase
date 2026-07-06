using System.Collections.Generic;
using System.Text;
using Framework.Singleton;
using UnityEngine;

public class DamageWorldTextManager : MonoSingletonBase<DamageWorldTextManager>
{
    [Header("Prefab（UI：TextMeshProUGUI）")]
    public GameObject damageWorldTextPrefab;

    [Header("UI Canvas（建议：Screen Space - Overlay）")]
    public Canvas damageCanvas;

    [Header("对象池")]
    public int initialPoolSize = 20;
    public int poolExpansionSize = 5;

    [Header("缩放")]
    public float minDamageForScaling = 100f;
    public float maxDamageForScaling = 10000f;

    [Header("颜色安排")]
    [Tooltip("普通伤害颜色")]
    public Color normalDamageColor = Color.white;
    [Tooltip("暴击伤害颜色")]
    public Color criticalDamageColor = new(1f, 0.85f, 0.2f);
    [Tooltip("护盾伤害颜色")]
    public Color shieldDamageColor = new(0.3f, 0.6f, 1f);
    [Tooltip("技能伤害颜色")]
    public Color skillDamageColor = new(0.6f, 0.4f, 1f);

    private readonly Queue<GameObject> _pool = new();
    private Camera _camera;

    protected override void Awake()
    {
        base.Awake();

        _camera = Camera.main;

        if (!damageCanvas)
        {
            Debug.LogError("没有对应的");
        }

        InitPool();
    }

    private void InitPool()
    {
        for (int i = 0; i < initialPoolSize; i++)
            CreateOne();
    }

    private void CreateOne()
    {
        Transform parent = damageCanvas ? damageCanvas.transform : transform;
        var go = Instantiate(damageWorldTextPrefab, parent);
        go.SetActive(false);
        _pool.Enqueue(go);
    }

    private void ExpandPool()
    {
        for (int i = 0; i < poolExpansionSize; i++)
            CreateOne();
    }

    // ===============================
    // 外部唯一入口（不改你的调用方式）
    // ===============================
    public void ShowDamageWorld(Vector3 worldPos, DamageResult r)
    {
        _camera ??= Camera.main;
        if (!_camera) return;

        if (_pool.Count == 0)
            ExpandPool();

        var go = _pool.Dequeue();
        go.SetActive(true);

        var text = go.GetComponentInChildren<DamageWorldText>(true);
        if (!text)
        {
            go.SetActive(false);
            _pool.Enqueue(go);
            return;
        }

        // 1) 元素图标串
        string iconTags = BuildTriggeredIconTags(r);

        // 2) 颜色
        Color damageColor = ResolveDamageColor(r);

        // 3) 播放（UI：会在 Update 中持续跟随 worldPos -> screen）
        text.Play(
            hitWorldPos: worldPos,
            damageValue: r.totalDamage,
            isCritical: r.isCritical,
            damageTypeColor: damageColor,
            elementSpriteName: iconTags,
            cam: _camera,
            minDamageForScaling: minDamageForScaling,
            maxDamageForScaling: maxDamageForScaling
        );
    }

    public void ReturnToPool(GameObject go)
    {
        go.SetActive(false);
        _pool.Enqueue(go);
    }

    // ===============================
    // 元素图标构建（只看 TriggerLayer）
    // ===============================
    private string BuildTriggeredIconTags(DamageResult r)
    {
        bool ice = r.iceTriggerLayer > 0;
        bool fire = r.fireTriggerLayer > 0;
        bool poison = r.poisonTriggerLayer > 0;
        bool electric = r.electricTriggerLayer > 0;

        if (!ice && !fire && !poison && !electric)
            return "";

        var sb = new StringBuilder(64);
        AppendIcon(sb, ice, "ice");
        AppendIcon(sb, fire, "fire");
        AppendIcon(sb, poison, "poison");
        AppendIcon(sb, electric, "electric");
        return sb.ToString();
    }

    private void AppendIcon(StringBuilder sb, bool triggered, string spriteName)
    {
        if (!triggered) return;
        if (sb.Length > 0) sb.Append(" ");
        sb.Append($"<sprite name=\"{spriteName}\">");
    }

    // ===============================
    // 颜色规则（DamageType 决定）
    // ===============================
    private Color ResolveDamageColor(DamageResult r)
    {
        if (r.isCritical) return criticalDamageColor;
        if (r.shieldDamage > 0) return shieldDamageColor;
        if (r.isSkill) return skillDamageColor;
        return normalDamageColor;
    }
}
