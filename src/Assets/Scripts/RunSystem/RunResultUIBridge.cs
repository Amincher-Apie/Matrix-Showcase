using Framework.UI.Core;
using Matrix.RunSystem;
using UnityEngine;

/// <summary>
/// Bridges run result events to the in-game result windows.
/// 监听 RunSummaryReady 事件，根据 IsVictory 弹出 GameVictoryWindow 或 GameFailedWindow。
/// 弹出前通过 SetResultCache() 将结算数据推入对应窗口的静态缓存。
/// 通过 RuntimeInitializeOnLoadMethod 自动初始化，无需在场景中手动挂载。
/// </summary>
public class RunResultUIBridge : MonoBehaviour
{
    private static RunResultUIBridge _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (_instance != null) return;
        var go = new GameObject("[Auto]RunResultUIBridge");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<RunResultUIBridge>();
    }

    private void OnEnable()
    {
        EventCenter.Instance.AddListener<RunResultEvt>(EventName.RunSummaryReady, OnRunSummaryReady);
    }

    private void OnDisable()
    {
        EventCenter.Instance.RemoveListener<RunResultEvt>(EventName.RunSummaryReady, OnRunSummaryReady);
    }

    private void OnRunSummaryReady(RunResultEvt evt)
    {
        if (evt.IsVictory)
        {
            GameVictoryWindow.SetResultCache(evt);
            UIManager.Instance.PopUpWindow<GameVictoryWindow>();
        }
        else
        {
            GameFailedWindow.SetResultCache(evt);
            UIManager.Instance.PopUpWindow<GameFailedWindow>();
        }
    }
}
