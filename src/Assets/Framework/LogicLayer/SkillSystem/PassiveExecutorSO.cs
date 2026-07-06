using UnityEngine;

/// <summary>
/// HeroSO 被动执行器的 ScriptableObject 基类。
/// </summary>
public abstract class PassiveExecutorSO : ScriptableObject, IPassiveExecutor
{
    public abstract string Id { get; }

    public abstract void OnHeroSpawned(PlayerActor player);

    public abstract void OnHeroDestroyed(PlayerActor player);
}
