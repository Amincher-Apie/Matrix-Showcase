
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyDropTableSO_Template", menuName = "游戏配置/怪物系统/创建配置/怪物掉落表配置")]
public class EnemyDropTableSO : ScriptableObject
{
    public enum DropCategory
    {
        /// <summary>必定掉落（如货币），跳过概率判定，仅数量随机。</summary>
        Guaranteed,
        /// <summary>按概率判定掉落（如道具/武器）。</summary>
        Random,
    }

    [Serializable]
    public class DropEntry
    {
        [Header("掉落类型")]
        public DropCategory category = DropCategory.Random;

        [Header("要掉落的物品（SO Id）")]
        public string itemSoId;

        [Header("掉落概率（0~1，仅 Random 类型生效）")]
        [Range(0f, 1f)] public float chance = 0.1f;

        [Header("数量范围")]
        public int minCount = 1;
        public int maxCount = 1;

        [Header("是否全员都能拾取一份")]
        public bool isSharedForAllPlayers = false;

        [Header("掉落物网络 Prefab 路径")]
        public string dropPrefabPath = "NetworkPrefabs/Drop/DropItem";
    }

    public List<DropEntry> entries = new();
}