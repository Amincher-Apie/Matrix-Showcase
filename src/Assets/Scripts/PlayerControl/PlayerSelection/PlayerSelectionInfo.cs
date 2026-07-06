using UnityEngine;

public struct PlayerSelectionInfo
{
    public string PlayerId;
    public string DisplayName;
    public string Description;
    public Sprite Icon;
    public PlayerAttributeConfig AttributeConfig;
    public HeroSO HeroSO;           // 英雄完整数据模板
    public bool IsUnlocked;
    public int RequiredLevel;
}
