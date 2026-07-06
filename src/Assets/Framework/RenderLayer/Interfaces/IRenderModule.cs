/// <summary>
/// 渲染模块接口
/// </summary>
public interface IRenderModule
{
    /// <summary>
    /// 初始化渲染模块
    /// </summary>
    void Initialize();
    
    void OnActivate();

    /// <summary>
    /// 销毁渲染模块
    /// </summary>
    void Destroy();
}