
using System;
using UnityEngine;

public static class WeaponSOLoader
{
    private const string LEGACY_WEAPON_SO_PATH = "Configs/Weapons";
    private const string WEAPON_SO_PATH = "Data/SO/WeaponSO";
    private const string DEFAULT_WEAPON_ID = "10002";
    
    /// <summary>
    /// 加载武器SO配置
    /// </summary>
    public static WeaponSO Load(string weaponId)
    {
        var weaponSO = TryLoad(weaponId, false);
        if (weaponSO == null)
        {
            Debug.LogError($"加载武器SO失败: {weaponId}");
            return null;
        }
        return weaponSO;
    }

    public static WeaponSO LoadOrDefault(string weaponId)
    {
        var weaponSO = TryLoad(weaponId, false);
        return weaponSO != null ? weaponSO : LoadDefault();
    }

    public static WeaponSO LoadDefault()
    {
        var preferred = TryLoad(DEFAULT_WEAPON_ID, false);
        if (preferred != null)
            return preferred;

        var weaponSOs = Resources.LoadAll<WeaponSO>(WEAPON_SO_PATH);
        if (weaponSOs != null)
        {
            for (int i = 0; i < weaponSOs.Length; i++)
            {
                if (weaponSOs[i] != null)
                    return weaponSOs[i];
            }
        }

        Debug.LogError($"默认武器SO不存在，路径: Resources/{WEAPON_SO_PATH}");
        return null;
    }

    public static string GetStableId(WeaponSO weaponSO)
    {
        if (weaponSO == null)
            return string.Empty;

        return !string.IsNullOrWhiteSpace(weaponSO.id) ? weaponSO.id : weaponSO.name;
    }
    
    /// <summary>
    /// 异步加载武器SO配置
    /// </summary>
    public static void LoadAsync(string weaponId, System.Action<WeaponSO> onLoaded)
    {
        var weaponSO = TryLoad(weaponId, false);
        if (weaponSO != null)
        {
            onLoaded?.Invoke(weaponSO);
            return;
        }

        Debug.LogError($"异步加载武器SO失败: {weaponId}");
        onLoaded?.Invoke(null);
    }

    private static WeaponSO TryLoad(string weaponId, bool logOnFailure)
    {
        if (string.IsNullOrWhiteSpace(weaponId))
            return null;

        var weaponSO = Resources.Load<WeaponSO>($"{LEGACY_WEAPON_SO_PATH}/{weaponId}");
        if (weaponSO != null)
            return weaponSO;

        weaponSO = Resources.Load<WeaponSO>($"{WEAPON_SO_PATH}/{weaponId}");
        if (weaponSO != null)
            return weaponSO;

        var weaponSOs = Resources.LoadAll<WeaponSO>(WEAPON_SO_PATH);
        if (weaponSOs != null)
        {
            for (int i = 0; i < weaponSOs.Length; i++)
            {
                var candidate = weaponSOs[i];
                if (candidate == null)
                    continue;

                if (string.Equals(candidate.id, weaponId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.name, weaponId, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
        }

        if (logOnFailure)
            Debug.LogWarning($"未找到武器SO: {weaponId}");

        return null;
    }
}
