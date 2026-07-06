using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 资源路径配置类
/// 集中管理所有资源的基础路径，避免硬编码
/// </summary>
public static class ResPathConfig
{
    // 预制体相关路径
    public static class Prefab
    {
        // 子弹预制体基础路径（对应Resources/Prefab/Bullet/）
        public const string BulletBasePath = "Prefab/Bullet/";
        
        // 可以在这里扩展其他预制体路径，例如：
        // public const string EnemyBasePath = "Prefab/Enemy/";
        // public const string UIBasePath = "Prefab/UI/";
        
        ///Prefab/UI/UIItem/DatabaseItem.prefab
        public const string UIItemPrefabPath = "Prefab/UI/UIItem/";

        ///Prefab/UI/Inventory/ItemBox.prefab
        public const string InventoryUIPrefabPath = "Prefab/UI/Inventory/";
    }

    // 其他类型资源路径（按需添加）
    public static class Texture
    {
        // 示例：纹理资源基础路径
        // public const string WeaponIconPath = "Texture/WeaponIcon/";
    }

    public static class Audio
    {
        // 示例：音频资源基础路径
        // public const string BulletSoundPath = "Audio/Bullet/";
    }

    /// <summary>
    /// 拼接子弹完整路径的快捷方法
    /// </summary>
    /// <param name="bulletName">具体子弹名称（不含路径和扩展名）</param>
    /// <returns>完整的资源加载路径</returns>
    public static string GetBulletFullPath(string bulletName)
    {
        return $"{Prefab.BulletBasePath}{bulletName}";
    }

    public static string GetUIItemFullPath(string uiItemName)
    {
        return $"{Prefab.UIItemPrefabPath}{uiItemName}";
    }

    public static string GetInventoryUIFullPath(string uiItemName)
    {
        return $"{Prefab.InventoryUIPrefabPath}{uiItemName}";
    }

}