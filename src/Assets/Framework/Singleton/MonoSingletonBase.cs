using UnityEngine;

namespace Framework.Singleton
{
    /// <summary>
    /// 继承自MonoBehaviour的单例基类
    /// </summary>
    /// <typeparam name="T">单例类型</typeparam>
    public abstract class MonoSingletonBase<T> : MonoBehaviour where T : MonoSingletonBase<T>
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
                    // 查找场景中是否已有实例
                    _instance = FindObjectOfType<T>();

                    // 场景中没有则创建新实例
                    if (_instance == null)
                    {
                        GameObject singletonObj = new GameObject($"[Singleton]{typeof(T).Name}");
                        _instance = singletonObj.AddComponent<T>();
                        DontDestroyOnLoad(singletonObj); // 跨场景不销毁
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 初始化（在Awake中调用，子类重写）
        /// </summary>
        protected virtual void Initialize() { }

        /// <summary>
        /// 单例创建时调用（确保只初始化一次）
        /// </summary>
        protected virtual void Awake()
        {
            // 防止重复创建实例
            if (_instance == null)
            {
                _instance = (T)this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                Debug.LogWarning($"[MonoSingletonBase] 已存在{typeof(T).Name}实例，销毁重复对象");
            }
        }

        /// <summary>
        /// 释放资源（需手动调用）
        /// </summary>
        public virtual void Release()
        {
            if (_instance == this)
            {
                _instance = null;
                Destroy(gameObject);
            }
        }
    }
}