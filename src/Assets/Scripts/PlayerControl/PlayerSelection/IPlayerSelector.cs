using System.Collections.Generic;

/// <summary>
/// 角色选择器接口 - 用于角色选择界面
/// </summary>
public interface IPlayerSelector
{
    /// <summary>
    /// 获取所有可选择的角色
    /// </summary>
    List<PlayerSelectionInfo> GetSelectablePlayers();
    
    /// <summary>
    /// 选择角色
    /// </summary>
    bool SelectPlayer(string playerId);
    
    /// <summary>
    /// 获取当前选择的角色
    /// </summary>
    PlayerSelectionInfo GetSelectedPlayer();
    
    /// <summary>
    /// 角色选择完成事件
    /// </summary>
    event System.Action<PlayerSelectionInfo> OnPlayerSelected;
}
