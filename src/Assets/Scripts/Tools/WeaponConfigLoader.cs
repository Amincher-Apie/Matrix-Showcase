using System;
using UnityEngine;
using System.Collections.Generic;
using Framework.Resource;

public static class WeaponConfigLoader
{
    // path: "Configs/Weapons/{id}.json"
    public static WeaponConfig Load(string id)
    {
        string path = $"Configs/Weapons/{id}";
        // 用ResourcesManager抽象，拿到TextAsset
        var ta = ResourcesManager.Instance.Load<TextAsset>(path);
        if (ta == null) throw new Exception($"Weapon config not found: {path}");
        return JsonUtility.FromJson<WeaponConfig>(ta.text);
    }
}