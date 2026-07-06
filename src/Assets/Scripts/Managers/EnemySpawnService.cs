using Framework.NetworkLayer.NetworkObjectPool;
using UnityEngine;
using Unity.Netcode;

public class EnemySpawnService : MonoBehaviour
{
    public static EnemySpawnService Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// 服务器生成敌人：Prefab/Enemy/{rank}/{id}
    /// 例如 rank=MonsterRank.Normal, id=002 → Prefab/Enemy/Normal/002
    /// </summary>
    public NetworkObject SpawnEnemy(string enemyId, Vector3 pos, Quaternion rot, string aiConfigPath = null)
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[EnemySpawnService] SpawnEnemy called on non-server.");
            return null;
        }

        string prefabPath = $"Prefab/Enemy/{enemyId}";

        var no = NetworkObjectPoolManager.Instance.GetAndSpawn(
            prefabPath,
            pos,
            rot,
            beforeSpawn: pooledObject => PrepareEnemyBeforeNetworkSpawn(pooledObject, enemyId, prefabPath, aiConfigPath));
        if (no == null)
        {
            Debug.LogError($"[EnemySpawnService] GetAndSpawn failed, path={prefabPath}");
            return null;
        }

        return no;
    }

    private static void PrepareEnemyBeforeNetworkSpawn(NetworkObject networkObject, string enemyId, string prefabPath, string aiConfigPath)
    {
        var actor = networkObject.GetComponent<EnemyActor>();
        if (actor != null)
        {
            actor.ConfigureForSpawn(enemyId, prefabPath, aiConfigPath);
            actor.ActivateAfterSpawn();
        }
        else
        {
            Debug.LogWarning($"[EnemySpawnService] EnemyActor not found on prefabPath={prefabPath}");
        }

        var serverAttr = networkObject.GetComponent<ServerEnemyAttributeModule>();
        if (serverAttr != null)
        {
            serverAttr.SetPrefabPath(prefabPath);
        }
    }
}
