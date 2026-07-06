// Framework/RenderLayer/Interfaces/IRenderObject.cs
using UnityEngine;

/// <summary>
/// 所有渲染对象的根接口
/// 与ILogicObject生命周期对应
/// </summary>
public interface IRenderObject
{
    /// <summary>
    /// 初始化渲染对象
    /// 逻辑对象创建后调用
    /// </summary>
    void Initialize();

    /// <summary>
    /// 激活渲染对象
    /// 对应逻辑对象OnActivate
    /// </summary>
    void OnActivate();

    /// <summary>
    /// 销毁渲染对象
    /// 在逻辑对象销毁前调用
    /// </summary>
    void Destroy();
}