/// <summary>
/// 被动能力执行器接口。
/// HeroSO 中引用的被动执行器资源需实现此接口。
/// </summary>
public interface IPassiveExecutor
{
    /// <summary>被动能力在 HeroSO 中的唯一标识。</summary>
    string Id { get; }

    /// <summary>英雄生成时调用，用于注册被动效果。</summary>
    void OnHeroSpawned(PlayerActor player);

    /// <summary>英雄销毁时调用，用于清理被动效果。</summary>
    void OnHeroDestroyed(PlayerActor player);
}
