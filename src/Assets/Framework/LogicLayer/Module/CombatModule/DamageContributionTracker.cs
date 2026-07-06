using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public sealed class DamageContributionTracker : MonoBehaviour
{
    [SerializeField] private int configuredGoldReward;

    private readonly Dictionary<ulong, float> _contributions = new Dictionary<ulong, float>();
    private bool _distributed;

    public IReadOnlyDictionary<ulong, float> Snapshot => _contributions;

    public void ConfigureGoldReward(int totalGoldReward)
    {
        configuredGoldReward = Mathf.Max(0, totalGoldReward);
        ResetTracker();
    }

    public void RecordDamage(ulong clientId, float damageAmount)
    {
        if (_distributed || damageAmount <= 0f)
        {
            return;
        }

        if (_contributions.ContainsKey(clientId))
        {
            _contributions[clientId] += damageAmount;
        }
        else
        {
            _contributions.Add(clientId, damageAmount);
        }
    }

    public void DistributeConfiguredRewardAndReset()
    {
        var reward = configuredGoldReward > 0 ? configuredGoldReward : ResolveGoldRewardFromAttribute();
        DistributeAndReset(reward);
    }

    public void DistributeAndReset(int totalGoldReward)
    {
        if (_distributed)
        {
            return;
        }

        _distributed = true;
        totalGoldReward = Mathf.Max(0, totalGoldReward);

        if (totalGoldReward <= 0 || _contributions.Count == 0)
        {
            _contributions.Clear();
            return;
        }

        var totalContribution = 0f;
        foreach (var pair in _contributions)
        {
            totalContribution += Mathf.Max(0f, pair.Value);
        }

        if (totalContribution <= 0f)
        {
            _contributions.Clear();
            return;
        }

        var networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.ConnectedClients == null)
        {
            _contributions.Clear();
            return;
        }

        var distributedGold = 0;
        var topClientId = 0UL;
        var topContribution = float.NegativeInfinity;

        foreach (var pair in _contributions)
        {
            var contribution = Mathf.Max(0f, pair.Value);
            if (contribution > topContribution)
            {
                topContribution = contribution;
                topClientId = pair.Key;
            }

            var share = Mathf.FloorToInt(totalGoldReward * (contribution / totalContribution));
            if (share <= 0)
            {
                continue;
            }

            if (TryGetInventory(networkManager, pair.Key, out var inventory))
            {
                inventory.InGameCurrency.Value += share;
                distributedGold += share;
            }
        }

        var remainder = totalGoldReward - distributedGold;
        if (remainder > 0 && TryGetInventory(networkManager, topClientId, out var topInventory))
        {
            topInventory.InGameCurrency.Value += remainder;
        }

        _contributions.Clear();
    }

    private void ResetTracker()
    {
        _distributed = false;
        _contributions.Clear();
    }

    private int ResolveGoldRewardFromAttribute()
    {
        var enemyAttribute = GetComponent<ServerEnemyAttributeModule>();
        if (enemyAttribute == null)
        {
            return 0;
        }

        return Mathf.Max(0, Mathf.RoundToInt(enemyAttribute.GetAttribute(AttributeType.InGameGoldReward)));
    }

    private static bool TryGetInventory(NetworkManager networkManager, ulong clientId, out NetworkInventory inventory)
    {
        inventory = null;

        if (!networkManager.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject == null)
        {
            return false;
        }

        inventory = client.PlayerObject.GetComponent<NetworkInventory>();
        return inventory != null;
    }
}
