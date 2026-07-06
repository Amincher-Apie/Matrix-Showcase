// ILogicObject.cs
/// <summary>
/// 所有逻辑对象的根接口
/// 统一规范生命周期，完全移除网络依赖
/// </summary>
public interface ILogicObject
{
    /// <summary>
    /// 对象唯一标识ID
    /// </summary>
    ulong ObjectId { get; }

    /// <summary>
    /// 本地初始化生命周期函数 
    /// 放在Awake中自动调用
    /// </summary>
    void LocalInit();

    /// <summary>
    /// 对象激活逻辑
    /// </summary>
    void OnActivate();

    /// <summary>
    /// 本地销毁清理
    /// 放在OnDestroy中自动调用
    /// </summary>
    void LocalDestroy();
}