// LogicObject.cs
using UnityEngine;

/// <summary>
/// 逻辑对象基类，实现ILogicObject接口并封装通用生命周期流程
/// 纯业务逻辑，无网络依赖
/// </summary>
public abstract class LogicObject : MonoBehaviour, ILogicObject
{
    [SerializeField]
    protected string objectName;

    /// <summary>
    /// 标记本地初始化状态，防止重复调用
    /// </summary>
    protected bool _isLocalInited = false;

    /// <summary>
    /// 标记对象是否已激活
    /// </summary>
    protected bool _isActivated = false;

    /// <summary>
    /// 对象唯一标识（本地生成或从网络代理获取）
    /// </summary>
    public abstract ulong ObjectId { get; }

    /// <summary>
    /// 该逻辑对象对应的渲染对象
    /// </summary>
    public RenderObject RenderObject { get; protected set; }

    /// <summary>
    /// Unity Awake回调，用于自动触发本地初始化
    /// </summary>
    protected virtual void Awake()
    {
        if (!_isLocalInited)
        {
            LocalInit();
            _isLocalInited = true;
        }
    }

    /// <summary>
    /// 实现ILogicObject的本地初始化方法
    /// </summary>
    public virtual void LocalInit()
    {
        RenderObject = GetComponent<RenderObject>();
    }

    /// <summary>
    /// 实现ILogicObject的激活逻辑
    /// </summary>
    public virtual void OnActivate()
    {
        if (!_isActivated)
        {
            _isActivated = true;
        }
        
        if (RenderObject != null)
        {
            RenderObject.OnActivate();
        }
    }

    /// <summary>
    /// Unity销毁回调，用于自动触发本地清理
    /// </summary>
    protected virtual void OnDestroy()
    {
        if (_isLocalInited)
        {
            LocalDestroy();
            _isLocalInited = false;
        }
    }

    /// <summary>
    /// 实现ILogicObject的本地销毁清理
    /// </summary>
    public virtual void LocalDestroy()
    {
        StopAllCoroutines();
        _isActivated = false;
        
        if (RenderObject != null)
        {
            RenderObject.Destroy();
        }
    }
}
