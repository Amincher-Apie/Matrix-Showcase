using UnityEngine;

namespace Matrix.Interaction
{
    public interface IInteractable
    {
        Transform InteractionAnchor { get; }
        string InteractionPrompt { get; }
        float InteractionRadius { get; }
        bool CanInteract(ulong requesterId);
        void OnInteractServer(ulong requesterId);
        void OnInteractClient();

        /// <summary>
        /// InteractionDetector 首次检测到此交互对象时调用。
        /// 实现者在此处创建/显示自己的 Billboard。
        /// </summary>
        void OnHoverEnter();

        /// <summary>
        /// InteractionDetector 不再检测到此交互对象时调用。
        /// 实现者在此处隐藏/销毁自己的 Billboard。
        /// </summary>
        void OnHoverExit();
    }
}
