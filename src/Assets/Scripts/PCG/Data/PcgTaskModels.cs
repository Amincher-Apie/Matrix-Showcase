using System;
using System.Collections.Generic;
using UnityEngine;

namespace Matrix.PCG
{
    public enum PrimaryTaskType
    {
        BossBattle = 0
    }

    public enum SideTaskType
    {
        Elimination = 0,
        Defense = 1,
        Capture = 2,
        Destroy = 3
    }

    public enum RoomRole
    {
        Start = 0,
        Connector = 1,
        Shop = 2,
        SideElimination = 3,
        SideDefense = 4,
        Boss = 5,
        SideCapture = 6,
        SideDestroy = 7
    }

    public enum ConnectorKind
    {
        Entrance = 0,
        Exit = 1,
        Bidirectional = 2
    }

    [Serializable]
    public sealed class PrimaryTaskInput
    {
        [Tooltip("当前阶段主任务为 Boss 战，但仍保留统一任务输入接口。")]
        public PrimaryTaskType TaskType = PrimaryTaskType.BossBattle;

        [Tooltip("Optional id from external task system.")]
        public string ExternalTaskId = string.Empty;
    }

    [Serializable]
    public sealed class SideTaskInput
    {
        [Tooltip("运行时次任务类型，会映射到对应的功能房。")]
        public SideTaskType TaskType = SideTaskType.Elimination;

        [Tooltip("Optional id from external task system.")]
        public string ExternalTaskId = string.Empty;
    }

    /// <summary>
    /// Task-system-facing input object. Can be manually passed now and replaced by real task system output later.
    /// </summary>
    [Serializable]
    public sealed class MapTaskInput
    {
        public PrimaryTaskInput PrimaryTask = new PrimaryTaskInput();
        public List<SideTaskInput> SideTasks = new List<SideTaskInput>();

        [Tooltip("Optional provider tag, e.g. TaskSystemV1.")]
        public string TaskProvider = string.Empty;

        [TextArea]
        [Tooltip("Optional raw payload (json/text) from external task provider.")]
        public string ProviderPayloadJson = string.Empty;
    }
}
