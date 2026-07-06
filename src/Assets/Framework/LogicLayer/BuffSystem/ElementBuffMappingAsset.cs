using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ElementBuffMapping", menuName = "游戏配置/BuffSystem/ElementBuffMapping")]
public class ElementBuffMappingAsset : ScriptableObject
{
    private static ElementBuffMappingAsset _instance;
    private static BuffData[] _cachedBuffData;

    public BuffData fireBuffData;
    public BuffData iceBuffData;
    public BuffData poisonBuffData;
    public BuffData electricBuffData;

    public static ElementBuffMappingAsset Instance
    {
        get
        {
            if (_instance == null)
            {
                var mappings = Resources.LoadAll<ElementBuffMappingAsset>("");
                if (mappings != null && mappings.Length > 0)
                {
                    _instance = mappings[0];
                }
            }

            return _instance;
        }
    }

    public BuffData GetBuffData(ElementType element)
    {
        return element switch
        {
            ElementType.Fire => fireBuffData,
            ElementType.Ice => iceBuffData,
            ElementType.Poison => poisonBuffData,
            ElementType.Electric => electricBuffData,
            _ => null
        };
    }

    public static BuffData Resolve(ElementType element)
    {
        var mapped = Instance != null ? Instance.GetBuffData(element) : null;
        if (mapped != null)
        {
            return mapped;
        }

        return FindBuffDataByConvention(element);
    }

    public static ElementType InferElement(BuffData buffData, ElementType fallback = ElementType.Fire)
    {
        if (buffData == null || buffData.tags == null)
        {
            return fallback;
        }

        foreach (string rawTag in buffData.tags)
        {
            string tag = rawTag?.Trim().ToLowerInvariant();
            switch (tag)
            {
                case "fire":
                    return ElementType.Fire;
                case "ice":
                    return ElementType.Ice;
                case "poison":
                case "toxic":
                    return ElementType.Poison;
                case "electric":
                case "lightning":
                    return ElementType.Electric;
            }
        }

        return fallback;
    }

    public static float ResolveElementDamage(DamageInfo info, ElementType element)
    {
        return element switch
        {
            ElementType.Fire => info.fireDamage,
            ElementType.Ice => info.iceDamage,
            ElementType.Poison => info.poisonDamage,
            ElementType.Electric => info.electricDamage,
            _ => 0f
        };
    }

    private static BuffData FindBuffDataByConvention(ElementType element)
    {
        _cachedBuffData ??= Resources.LoadAll<BuffData>("");
        if (_cachedBuffData == null || _cachedBuffData.Length == 0)
        {
            return null;
        }

        int expectedId = element switch
        {
            ElementType.Fire => 2001,
            ElementType.Poison => 2002,
            ElementType.Electric => 2003,
            ElementType.Ice => 2004,
            _ => 0
        };

        if (expectedId != 0)
        {
            var byId = Array.Find(_cachedBuffData, b => b != null && b.buffID == expectedId);
            if (byId != null)
            {
                return byId;
            }
        }

        string expectedTag = element switch
        {
            ElementType.Fire => "fire",
            ElementType.Poison => "poison",
            ElementType.Electric => "electric",
            ElementType.Ice => "ice",
            _ => string.Empty
        };

        return Array.Find(_cachedBuffData, b => HasTag(b, expectedTag));
    }

    private static bool HasTag(BuffData buffData, string expectedTag)
    {
        if (buffData == null || buffData.tags == null || string.IsNullOrEmpty(expectedTag))
        {
            return false;
        }

        foreach (string rawTag in buffData.tags)
        {
            if (string.Equals(rawTag?.Trim(), expectedTag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
