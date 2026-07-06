using Framework.UI.Base;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 对局结算窗口。
/// 由 RunManager 在 RunSummary 状态下通过 ClientRpc 弹出。
/// </summary>
public class RunResultWindow : WindowBase
{
    [Header("UI References")]
    [SerializeField] private Text resultTitleText;
    [SerializeField] private Text resultDetailText;
    [SerializeField] private Button returnButton;

    private bool _isVictory;

    public override void OnAwake()
    {
        base.OnAwake();
        if (returnButton != null)
            returnButton.onClick.AddListener(OnReturnClicked);
    }

    public override void OnShow()
    {
        base.OnShow();
        RefreshUI();
    }

    /// <summary>
    /// 由外部在弹出前设置结果。
    /// </summary>
    public void SetResult(bool isVictory, string details)
    {
        _isVictory = isVictory;
        if (resultTitleText != null)
            resultTitleText.text = isVictory ? "胜利" : "失败";
        if (resultDetailText != null)
            resultDetailText.text = details;
    }

    private void RefreshUI()
    {
        if (resultTitleText != null)
            resultTitleText.text = _isVictory ? "胜利" : "失败";
    }

    private void OnReturnClicked()
    {
        DestroySelf();
        // TODO: 返回大厅或任务选择，当前先销毁窗口
    }
}
