using System;
using UnityEngine;

namespace Matrix.PCG
{
    /// <summary>
    /// Runtime one-shot generation input.
    /// It only describes this run (style + tasks + seed), and never changes profile content.
    /// </summary>
    [Serializable]
    public sealed class PcgGeneratePackage
    {
        [Header("Style Selection")]
        [Tooltip("Style key used to resolve profile from registry.")]
        public string StyleKey = string.Empty;

        [Header("Run Input")]
        [Tooltip("Deterministic seed for this run.")]
        public int Seed = 1;

        [Tooltip("本局生成时要使用的任务组。当前阶段主任务固定为 Boss 战。")]
        public MapTaskInput TaskInput = new MapTaskInput();

        [Header("Metadata")]
        [Tooltip("Optional caller tag for logging/tracing.")]
        public string RequestSource = string.Empty;
    }
}
