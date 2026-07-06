using System;

public enum BuffOwnerCategory
{
    Player,
    NormalEnemy,
    EliteEnemy,
    BossEnemy
}

/// <summary>
/// Buff 所在“逻辑实体”的抽象，不依赖具体 PlayerActor / EnemyActor。
/// </summary>
public interface IBuffOwnerContext
{
    /// <summary>对应 NetworkObject 的 Id，用于 Server/Client 映射。</summary>
    ulong NetworkObjectId { get; }

    /// <summary>用于区分玩家 / 普通怪 / 精英 / Boss，控制叠层规则。</summary>
    BuffOwnerCategory OwnerCategory { get; }

    /// <summary>抽象的属性访问接口，内部再去调 PlayerAttribute / EnemyAttribute。</summary>
    IAttributeProxy AttributeProxy { get; }
}

/// <summary>
/// 这是一个你现有属性系统上的薄封装接口，
/// PlayerAttribute / EnemyAttribute 都可以包装成这个接口供 Buff 使用。
/// </summary>
public interface IAttributeProxy
{
    float GetAttribute(AttributeType type);

    /// <summary>
    /// 施加一个来源可追踪的修改器
    /// </summary>
    /// <param name="type">修正类型</param>
    /// <param name="modifyType">修正方式</param>
    /// <param name="value">值，可以指定但是以第一次的进入为主</param>
    /// <param name="sourceId">建议用「BuffID」或「道具Id」或「施加者的ObjectId」</param>
    /// <param name="stacks">默认为1指示添加1层，若原来无修改器，那么就直接新建</param>
    void AddModifier(AttributeType type, AttributeModifyType modifyType, float value, ulong sourceId, int stacks = 1);

    /// <summary>
    /// 按来源移除修改器层
    /// </summary>
    /// <param name="type">修正类型</param>
    /// <param name="sourceId">来源ID，这里是指定的ID或者是某个游戏对象的ObjectID</param>
    /// <param name="stacks">默认为1指示删除几层，若为0则删除所有</param>
    void RemoveModifiers(AttributeType type, ulong sourceId, int stacks = 1);
}