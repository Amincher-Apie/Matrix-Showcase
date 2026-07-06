using System;
using UnityEngine;

/// <summary>
/// HeroSO 加载工具。仿 WeaponSOLoader 模式，从 Resources 加载英雄配置。
/// </summary>
public static class HeroSOLoader
{
    private const string HERO_SO_PATH = "Configs/HeroSO";

    /// <summary>
    /// 同步加载 HeroSO。
    /// </summary>
    public static HeroSO Load(string heroId)
    {
        var heroSO = TryLoad(heroId, false);
        if (heroSO == null)
        {
            Debug.LogError($"[HeroSOLoader] 加载 HeroSO 失败: {heroId}");
            return null;
        }
        return heroSO;
    }

    public static HeroSO LoadOrDefault(string heroId)
    {
        var heroSO = TryLoad(heroId, false);
        return heroSO != null ? heroSO : LoadDefault();
    }

    public static HeroSO LoadDefault()
    {
        var heroSOs = Resources.LoadAll<HeroSO>(HERO_SO_PATH);
        if (heroSOs != null)
        {
            for (int i = 0; i < heroSOs.Length; i++)
            {
                if (heroSOs[i] != null)
                    return heroSOs[i];
            }
        }

        Debug.LogError($"[HeroSOLoader] 默认 HeroSO 不存在，路径: Resources/{HERO_SO_PATH}");
        return null;
    }

    public static string GetStableId(HeroSO heroSO)
    {
        if (heroSO == null)
            return string.Empty;

        return !string.IsNullOrWhiteSpace(heroSO.id) ? heroSO.id : heroSO.name;
    }

    /// <summary>
    /// 异步加载 HeroSO。
    /// </summary>
    public static void LoadAsync(string heroId, System.Action<HeroSO> onLoaded)
    {
        var heroSO = TryLoad(heroId, false);
        if (heroSO != null)
        {
            onLoaded?.Invoke(heroSO);
            return;
        }

        Debug.LogError($"[HeroSOLoader] 异步加载 HeroSO 失败: {heroId}");
        onLoaded?.Invoke(null);
    }

    private static HeroSO TryLoad(string heroId, bool logOnFailure)
    {
        if (string.IsNullOrWhiteSpace(heroId))
            return null;

        var heroSO = Resources.Load<HeroSO>($"{HERO_SO_PATH}/{heroId}");
        if (heroSO != null)
            return heroSO;

        var heroSOs = Resources.LoadAll<HeroSO>(HERO_SO_PATH);
        if (heroSOs != null)
        {
            for (int i = 0; i < heroSOs.Length; i++)
            {
                var candidate = heroSOs[i];
                if (candidate == null)
                    continue;

                if (string.Equals(candidate.id, heroId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.name, heroId, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
        }

        if (logOnFailure)
            Debug.LogWarning($"[HeroSOLoader] 未找到 HeroSO: {heroId}");

        return null;
    }
}
