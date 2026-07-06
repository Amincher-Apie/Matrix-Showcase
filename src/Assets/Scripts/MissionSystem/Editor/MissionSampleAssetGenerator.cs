using Matrix.Missions;
using UnityEditor;
using UnityEngine;

namespace Matrix.Missions.Editor
{
    public static class MissionSampleAssetGenerator
    {
        private const string MissionRootFolder = "Assets/Resources/Configs/Missions";
        private const string LibraryFolder = "Assets/Resources/Configs/Missions/Libraries";
        private const string PrimaryBossFolder = "Assets/Resources/Configs/Missions/Primary/Boss";
        private const string SecondaryEliminateFolder = "Assets/Resources/Configs/Missions/Secondary/Eliminate";
        private const string SecondaryDefenseFolder = "Assets/Resources/Configs/Missions/Secondary/Defense";
        private const string SecondaryCaptureFolder = "Assets/Resources/Configs/Missions/Secondary/Capture";
        private const string SecondaryDestroyFolder = "Assets/Resources/Configs/Missions/Secondary/Destroy";

        [MenuItem("Tools/Missions/Generate Sample Mission Assets")]
        public static void GenerateSampleMissionAssets()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Configs");
            EnsureFolder(MissionRootFolder);
            EnsureFolder(LibraryFolder);
            EnsureFolder("Assets/Resources/Configs/Missions/Primary");
            EnsureFolder(PrimaryBossFolder);
            EnsureFolder("Assets/Resources/Configs/Missions/Secondary");
            EnsureFolder(SecondaryEliminateFolder);
            EnsureFolder(SecondaryDefenseFolder);
            EnsureFolder(SecondaryCaptureFolder);
            EnsureFolder(SecondaryDestroyFolder);

            MissionConfig bossMission = CreateBossMission();
            MissionConfig eliminateMission = CreateEliminateMission();
            MissionConfig defenseMission = CreateDefenseMission();
            MissionConfig captureMission = CreateCaptureMission();
            MissionConfig destroyMission = CreateDestroyMission();

            MissionLibrary missionLibrary = CreateOrLoadAsset<MissionLibrary>($"{LibraryFolder}/MissionLibrary_Default.asset");
            BindLibraryAssets(missionLibrary, bossMission, eliminateMission, defenseMission, captureMission, destroyMission);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.SetDirty(missionLibrary);
            Selection.activeObject = missionLibrary;

            Debug.Log("[MissionSampleAssetGenerator] 已生成示例任务资产结构。");
        }

        /// <summary>
        /// 创建或覆盖示例 Boss 主任务配置。
        /// </summary>
        private static MissionConfig CreateBossMission()
        {
            MissionConfig config = CreateOrLoadAsset<MissionConfig>($"{PrimaryBossFolder}/Mission_Boss_001_Guardian.asset");
            SerializedObject serializedObject = new SerializedObject(config);

            serializedObject.FindProperty("missionId").stringValue = "mission_boss_001_guardian";
            serializedObject.FindProperty("displayName").stringValue = "歼灭守门者";
            serializedObject.FindProperty("description").stringValue = "进入 Boss 房间后生成守门者，击杀后推进本局主线。";
            serializedObject.FindProperty("missionType").enumValueIndex = (int)MissionType.Boss;
            serializedObject.FindProperty("missionCategory").enumValueIndex = (int)MissionCategory.Primary;
            serializedObject.FindProperty("externalTaskId").stringValue = "main_boss_guardian";
            serializedObject.FindProperty("triggerOnRoomEnter").boolValue = true;
            serializedObject.FindProperty("triggerHeight").floatValue = 8f;
            serializedObject.FindProperty("pointerLabel").stringValue = "前往 Boss 房";

            SerializedProperty spawnEntries = serializedObject.FindProperty("spawnEntries");
            spawnEntries.arraySize = 1;
            SerializedProperty bossSpawn = spawnEntries.GetArrayElementAtIndex(0);
            bossSpawn.FindPropertyRelative("EnemyPrefabAddress").stringValue = "Boss/Boss";
            bossSpawn.FindPropertyRelative("Count").intValue = 1;
            bossSpawn.FindPropertyRelative("AiConfigPath").stringValue = string.Empty; // Boss AI 由行为树驱动，不需要 AiConfigPath

            serializedObject.FindProperty("killTargetCount").intValue = 1;
            serializedObject.FindProperty("defenseDurationSeconds").floatValue = 90f;
            serializedObject.FindProperty("captureRequiredProgress").floatValue = 100f;

            ClearDestroyRounds(serializedObject.FindProperty("destroyRounds"));
            serializedObject.FindProperty("currencyReward").intValue = 1000;
            ClearRewards(serializedObject.FindProperty("rewards"));

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        /// <summary>
        /// 创建或覆盖示例歼灭次任务配置。
        /// </summary>
        private static MissionConfig CreateEliminateMission()
        {
            MissionConfig config = CreateOrLoadAsset<MissionConfig>($"{SecondaryEliminateFolder}/Mission_Eliminate_001_Sweep.asset");
            SerializedObject serializedObject = new SerializedObject(config);

            serializedObject.FindProperty("missionId").stringValue = "mission_eliminate_001_sweep";
            serializedObject.FindProperty("displayName").stringValue = "净化敌群";
            serializedObject.FindProperty("description").stringValue = "进入歼灭房后追踪全图敌人死亡数量，完成指定击杀数即可完成。";
            serializedObject.FindProperty("missionType").enumValueIndex = (int)MissionType.Eliminate;
            serializedObject.FindProperty("missionCategory").enumValueIndex = (int)MissionCategory.Secondary;
            serializedObject.FindProperty("externalTaskId").stringValue = "side_eliminate_sweep";
            serializedObject.FindProperty("triggerOnRoomEnter").boolValue = true;
            serializedObject.FindProperty("triggerHeight").floatValue = 6f;
            serializedObject.FindProperty("pointerLabel").stringValue = "前往歼灭房";

            serializedObject.FindProperty("killTargetCount").intValue = 10;
            ClearSpawnEntries(serializedObject.FindProperty("spawnEntries"));

            ClearDestroyRounds(serializedObject.FindProperty("destroyRounds"));
            serializedObject.FindProperty("currencyReward").intValue = 1000;
            ClearRewards(serializedObject.FindProperty("rewards"));

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        /// <summary>
        /// 创建或覆盖示例防御次任务配置。
        /// </summary>
        private static MissionConfig CreateDefenseMission()
        {
            MissionConfig config = CreateOrLoadAsset<MissionConfig>($"{SecondaryDefenseFolder}/Mission_Defense_001_Core.asset");
            SerializedObject serializedObject = new SerializedObject(config);

            serializedObject.FindProperty("missionId").stringValue = "mission_defense_001_core";
            serializedObject.FindProperty("displayName").stringValue = "守住能源核心";
            serializedObject.FindProperty("description").stringValue = "进入防御房后刷新核心，守住指定时长则成功。";
            serializedObject.FindProperty("missionType").enumValueIndex = (int)MissionType.Defense;
            serializedObject.FindProperty("missionCategory").enumValueIndex = (int)MissionCategory.Secondary;
            serializedObject.FindProperty("externalTaskId").stringValue = "side_defense_core";
            serializedObject.FindProperty("triggerOnRoomEnter").boolValue = true;
            serializedObject.FindProperty("triggerHeight").floatValue = 6f;
            serializedObject.FindProperty("pointerLabel").stringValue = "前往防御房";
            serializedObject.FindProperty("defenseDurationSeconds").floatValue = 75f;
            serializedObject.FindProperty("defenseObjectiveMaxHealth").floatValue = 500f;
            serializedObject.FindProperty("defenseObjectiveShield").floatValue = 100f;
            serializedObject.FindProperty("defenseObjectiveThreatPriority").intValue = 100;

            ClearSpawnEntries(serializedObject.FindProperty("spawnEntries"));
            ClearDestroyRounds(serializedObject.FindProperty("destroyRounds"));
            serializedObject.FindProperty("currencyReward").intValue = 1000;
            ClearRewards(serializedObject.FindProperty("rewards"));

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        /// <summary>
        /// 创建或覆盖示例捕获次任务配置。
        /// </summary>
        private static MissionConfig CreateCaptureMission()
        {
            MissionConfig config = CreateOrLoadAsset<MissionConfig>($"{SecondaryCaptureFolder}/Mission_Capture_001_Relay.asset");
            SerializedObject serializedObject = new SerializedObject(config);

            serializedObject.FindProperty("missionId").stringValue = "mission_capture_001_relay";
            serializedObject.FindProperty("displayName").stringValue = "控制中继站";
            serializedObject.FindProperty("description").stringValue = "进入捕获房后刷新占点装置，累计占点进度到目标值后完成。";
            serializedObject.FindProperty("missionType").enumValueIndex = (int)MissionType.Capture;
            serializedObject.FindProperty("missionCategory").enumValueIndex = (int)MissionCategory.Secondary;
            serializedObject.FindProperty("externalTaskId").stringValue = "side_capture_relay";
            serializedObject.FindProperty("triggerOnRoomEnter").boolValue = true;
            serializedObject.FindProperty("triggerHeight").floatValue = 6f;
            serializedObject.FindProperty("pointerLabel").stringValue = "前往捕获房";
            serializedObject.FindProperty("captureRequiredProgress").floatValue = 1f;
            serializedObject.FindProperty("captureItemId").stringValue = "Capture/RelayCore";
            serializedObject.FindProperty("captureItemAmount").intValue = 1;
            serializedObject.FindProperty("capturePickupPrompt").stringValue = "按 F 拾取";

            SerializedProperty captureSpawnEntries = serializedObject.FindProperty("spawnEntries");
            captureSpawnEntries.arraySize = 1;
            SerializedProperty captureTargetSpawn = captureSpawnEntries.GetArrayElementAtIndex(0);
            captureTargetSpawn.FindPropertyRelative("EnemyPrefabAddress").stringValue = "Normal/001";
            captureTargetSpawn.FindPropertyRelative("Count").intValue = 1;
            captureTargetSpawn.FindPropertyRelative("AiConfigPath").stringValue = "Configs/AI/EnemyAI_Default";
            ClearDestroyRounds(serializedObject.FindProperty("destroyRounds"));
            serializedObject.FindProperty("currencyReward").intValue = 1000;
            ClearRewards(serializedObject.FindProperty("rewards"));

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        /// <summary>
        /// 创建或覆盖示例破坏次任务配置。
        /// </summary>
        private static MissionConfig CreateDestroyMission()
        {
            MissionConfig config = CreateOrLoadAsset<MissionConfig>($"{SecondaryDestroyFolder}/Mission_Destroy_001_Reactor.asset");
            SerializedObject serializedObject = new SerializedObject(config);

            serializedObject.FindProperty("missionId").stringValue = "mission_destroy_001_reactor";
            serializedObject.FindProperty("displayName").stringValue = "摧毁反应堆";
            serializedObject.FindProperty("description").stringValue = "进入破坏房后按轮次刷新目标，全部摧毁后完成。";
            serializedObject.FindProperty("missionType").enumValueIndex = (int)MissionType.Destroy;
            serializedObject.FindProperty("missionCategory").enumValueIndex = (int)MissionCategory.Secondary;
            serializedObject.FindProperty("externalTaskId").stringValue = "side_destroy_reactor";
            serializedObject.FindProperty("triggerOnRoomEnter").boolValue = true;
            serializedObject.FindProperty("triggerHeight").floatValue = 6f;
            serializedObject.FindProperty("pointerLabel").stringValue = "前往破坏房";

            ClearSpawnEntries(serializedObject.FindProperty("spawnEntries"));
            SetDestroyRounds(serializedObject.FindProperty("destroyRounds"));
            serializedObject.FindProperty("currencyReward").intValue = 1000;
            ClearRewards(serializedObject.FindProperty("rewards"));

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        /// <summary>
        /// 把示例任务配置绑定到默认任务库中。
        /// </summary>
        private static void BindLibraryAssets(MissionLibrary missionLibrary, params MissionConfig[] missionConfigs)
        {
            SerializedObject serializedObject = new SerializedObject(missionLibrary);
            SerializedProperty missionsProperty = serializedObject.FindProperty("missions");
            missionsProperty.arraySize = missionConfigs != null ? missionConfigs.Length : 0;

            for (int i = 0; i < missionsProperty.arraySize; i++)
            {
                missionsProperty.GetArrayElementAtIndex(i).objectReferenceValue = missionConfigs[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 创建资产目录；如果目录已存在则直接复用。
        /// </summary>
        private static void EnsureFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath))
            {
                return;
            }

            int splitIndex = assetFolderPath.LastIndexOf('/');
            string parent = assetFolderPath.Substring(0, splitIndex);
            string folderName = assetFolderPath.Substring(splitIndex + 1);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        /// <summary>
        /// 创建或读取指定路径上的 ScriptableObject 资产。
        /// </summary>
        private static T CreateOrLoadAsset<T>(string assetPath) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        /// <summary>
        /// 清空刷怪条目列表，适用于非歼灭类任务。
        /// </summary>
        private static void ClearSpawnEntries(SerializedProperty spawnEntries)
        {
            spawnEntries.arraySize = 0;
        }

        /// <summary>
        /// 清空破坏轮次列表。
        /// </summary>
        private static void ClearDestroyRounds(SerializedProperty destroyRounds)
        {
            destroyRounds.arraySize = 0;
        }

        /// <summary>
        /// 写入一套两轮的破坏任务示例轮次。
        /// </summary>
        private static void SetDestroyRounds(SerializedProperty destroyRounds)
        {
            destroyRounds.arraySize = 2;
            destroyRounds.GetArrayElementAtIndex(0).FindPropertyRelative("TargetCount").intValue = 2;
            destroyRounds.GetArrayElementAtIndex(0).FindPropertyRelative("GoldReward").intValue = 60;
            destroyRounds.GetArrayElementAtIndex(1).FindPropertyRelative("TargetCount").intValue = 3;
            destroyRounds.GetArrayElementAtIndex(1).FindPropertyRelative("GoldReward").intValue = 80;
        }

        /// <summary>
        /// 清空奖励列表。
        /// </summary>
        private static void ClearRewards(SerializedProperty rewards)
        {
            rewards.arraySize = 0;
        }
    }
}
