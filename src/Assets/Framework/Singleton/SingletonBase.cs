using UnityEngine;
namespace Framework.Singleton
{
    /// <summary>
    /// 单例模式管理器基类
    /// 不继承自Mono 无线程安全锁 仅适用于不存在多线程访问单例实例的管理器继承
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SingletonBase<T> where T : SingletonBase<T>, new()
    {
        // 单例实例
        private static T _instance;

        /// <summary>
        /// 单例实例访问入口
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.Log($"[SingletonBase] 首次访问 {typeof(T).Name}，创建实例");
                    _instance = new T();
                    _instance.Initialize();
                }
                return _instance;
            }
        }

        #region 生命周期函数

        /// <summary>
        /// 初始化方法（实例创建时自动调用）
        /// 子类重写实现初始化逻辑
        /// </summary>
        protected virtual void Initialize() { }

        /// <summary>
        /// 启动方法（需手动调用）
        /// 适合依赖其他管理器初始化完成后的逻辑
        /// </summary>
        public virtual void Start() { }

        /// <summary>
        /// 帧更新方法（需手动在Update中调用）
        /// </summary>
        public virtual void OnUpdate() { }

        /// <summary>
        /// 资源释放方法（需手动调用）
        /// </summary>
        public virtual void Release()
        {
            _instance = null;
        }

        #endregion

    }
}
