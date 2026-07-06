using System;
using System.Collections.Generic;

namespace Matrix.SpawnSystem
{
    /// <summary>
    /// Lightweight interface for cross-assembly communication between Scripts and Framework.
    /// Lives in Scripts so MissionManager (Scripts assembly) can reference it without depending on Framework.
    /// </summary>
    public interface IMonsterSpawnSystem
    {
        void InitializeWithMapResult(object result, object profile);

        void UpdatePlayerCount(int count);
        void UpdateActiveTaskCount(int count);
        void UpdateOccupiedRegions(List<int> occupiedNodeIds);

        void Shutdown();

        int ActiveMonsterCount { get; }
        int DynamicCap { get; }
        float CurrentSpawnRate { get; }
    }
}
