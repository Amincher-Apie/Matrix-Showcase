using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 动能爆发：周期性给英雄提供短暂移速增益。
/// </summary>
[CreateAssetMenu(
    fileName = "KineticBurstPassiveExecutor",
    menuName = "游戏配置/角色系统/被动执行器/动能爆发")]
public class KineticBurstPassiveExecutor : PassiveExecutorSO
{
    private const ulong SourcePrefix = 0x4B42555253540000UL;
    private readonly Dictionary<ulong, Coroutine> _routines = new();
    private readonly Dictionary<ulong, MonoBehaviour> _runners = new();

    public override string Id => "KineticBurst";

    [Min(0f)]
    public float duration = 2f;

    [Min(0f)]
    public float cooldown = 10f;

    public float moveSpeedPercent = 50f;

    private void OnEnable()
    {
        _routines.Clear();
        _runners.Clear();
    }

    public override void OnHeroSpawned(PlayerActor player)
    {
        if (player == null || !player.IsServer || player.networkProxy == null)
        {
            return;
        }

        var runner = player.networkProxy as MonoBehaviour;
        if (runner == null)
        {
            Debug.LogWarning("[KineticBurst] 玩家网络代理无法作为协程 Runner。");
            return;
        }

        ulong playerId = player.ObjectId;

        // 仅当已存在旧协程时才执行清理，防止首次生成时在未 Spawn 状态下走 RPC 路径
        if (_routines.ContainsKey(playerId) || _runners.ContainsKey(playerId))
        {
            OnHeroDestroyed(player);
        }

        _runners[playerId] = runner;
        _routines[playerId] = runner.StartCoroutine(BurstLoop(player));
    }

    public override void OnHeroDestroyed(PlayerActor player)
    {
        if (player == null || !player.IsServer) return;

        ulong playerId = player.ObjectId;
        if (_routines.TryGetValue(playerId, out var routine) && routine != null)
        {
            if (_runners.TryGetValue(playerId, out var runner) && runner != null)
            {
                runner.StopCoroutine(routine);
            }
        }

        RemoveBoost(player);
        _routines.Remove(playerId);
        _runners.Remove(playerId);
    }

    private IEnumerator BurstLoop(PlayerActor player)
    {
        while (player != null && player.networkProxy != null && player.IsServer)
        {
            if (IsAlive(player))
            {
                ApplyBoost(player);
                yield return new WaitForSeconds(duration);
                RemoveBoost(player);
            }

            float rest = Mathf.Max(0f, cooldown - duration);
            if (rest > 0f)
            {
                yield return new WaitForSeconds(rest);
            }
            else
            {
                yield return null;
            }
        }
    }

    private void ApplyBoost(PlayerActor player)
    {
        var attr = player.networkProxy.GetServerAttributeModule<ServerPlayerAttributeModule>();
        if (attr == null) return;

        ulong sourceId = BuildSourceId(player.ObjectId);
        attr.RemoveModifiers(AttributeType.MoveSpeed, sourceId, 0);
        attr.AddModifier(
            AttributeType.MoveSpeed,
            AttributeModifyType.Percentage,
            moveSpeedPercent,
            sourceId,
            1);
    }

    private void RemoveBoost(PlayerActor player)
    {
        var attr = player?.networkProxy != null
            ? player.networkProxy.GetServerAttributeModule<ServerPlayerAttributeModule>()
            : null;
        if (attr == null) return;

        attr.RemoveModifiers(AttributeType.MoveSpeed, BuildSourceId(player.ObjectId), 0);
    }

    private static bool IsAlive(PlayerActor player)
    {
        var attr = player.networkProxy.GetServerAttributeModule<ServerPlayerAttributeModule>();
        return attr == null || attr.CurrentLifeState == PlayerLifeState.Alive;
    }

    private static ulong BuildSourceId(ulong playerId)
    {
        return SourcePrefix ^ playerId;
    }
}
