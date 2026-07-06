using System.Collections.Generic;
using Unity.Netcode;

namespace Framework.LogicLayer.Module.SpawnSystem
{
    /// <summary>
    /// Global monster index and counting system.
    /// Server-only. Driven by EventCenter.UnitDied events - no modifications to existing code needed.
    /// </summary>
    public sealed class MonsterRegistry
    {
        public static MonsterRegistry Instance { get; private set; }

        private readonly Dictionary<int, NetworkObject> _monsters = new Dictionary<int, NetworkObject>();
        private int _nextId = 1;
        private int _totalSpawnedCount;
        private int _totalKilledCount;

        public int ActiveMonsterCount => _monsters.Count;
        public int TotalSpawnedCount => _totalSpawnedCount;
        public int TotalKilledCount => _totalKilledCount;

        private MonsterRegistry() { }

        public static void Create()
        {
            if (Instance == null)
                Instance = new MonsterRegistry();
        }

        /// <summary>
        /// Registers a spawned monster. Returns the assigned registry ID.
        /// </summary>
        public int Register(NetworkObject monsterNetObj)
        {
            if (monsterNetObj == null) return -1;
            int id = _nextId++;
            _monsters[id] = monsterNetObj;
            _totalSpawnedCount++;
            return id;
        }

        /// <summary>
        /// Unregisters a monster by its registry ID.
        /// </summary>
        public void Unregister(int id)
        {
            _monsters.Remove(id);
        }

        /// <summary>
        /// Called when a monster dies. Looks up by NetworkObjectId and removes it from the registry.
        /// </summary>
        public void ReportKillByNetworkId(ulong networkObjectId)
        {
            foreach (var kvp in _monsters)
            {
                if (kvp.Value != null && kvp.Value.NetworkObjectId == networkObjectId)
                {
                    _monsters.Remove(kvp.Key);
                    _totalKilledCount++;
                    return;
                }
            }
        }

        /// <summary>
        /// Clears all state. Called when a new map is generated.
        /// </summary>
        public void Clear()
        {
            _monsters.Clear();
            _totalSpawnedCount = 0;
            _totalKilledCount = 0;
            _nextId = 1;
        }

        public IReadOnlyDictionary<int, NetworkObject> GetAllActive() => _monsters;
    }
}
