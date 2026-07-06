using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Framework.Singleton;
using Framework.Resource;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math; // 引入资源管理器命名空间
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// SO管理器 负责分类储存管理所有继承自BaseSO的具体SO类
/// </summary>
public class SOManager : MonoSingletonBase<SOManager>
{
    /// <summary>
    /// SO类型和SO列表的映射字典
    /// </summary>
    private Dictionary<Type, IList> _soListsDic = new Dictionary<Type, IList>();

    public List<WeaponSO> WeaponSoList => GetSOList<WeaponSO>();

    public List<QualityItemSO> QualityItemSoList => GetSOList<QualityItemSO>();

    public List<EnemySO> EnemySoList => GetSOList<EnemySO>();

    /// <summary>
    /// 根据SO类得到存储对应SO类对象的列表
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public List<T> GetSOList<T>() where T : BaseSO
    {
        Type type = typeof(T);
        if (_soListsDic.TryGetValue(type, out var list))
        {
            return list as List<T>;
        }
        
        return new List<T>();
    }

    /// <summary>
    /// 将具体类型的SO加入到对应的List中（同时添加到所有父类的列表中，支持多态查询）
    /// </summary>
    /// <param name="so"></param>
    public void AddSO(BaseSO so)
    {
        if (so == null) return;

        Type soType = so.GetType();
        
        // 获取该类型的所有父类（包括自身），直到 BaseSO
        var typesToAdd = new List<Type>();
        Type currentType = soType;
        while (currentType != null && (currentType == typeof(BaseSO) || currentType.IsSubclassOf(typeof(BaseSO))))
        {
            typesToAdd.Add(currentType);
            currentType = currentType.BaseType;
        }

        // 将 SO 添加到所有相关类型的列表中
        bool addedToAny = false;
        foreach (var type in typesToAdd)
        {
            if (_soListsDic.TryGetValue(type, out var list))
            {
                // 避免重复添加（根据id判断，确保SO的id是唯一标识）
                if (!list.Cast<BaseSO>().Any(item => item.id == so.id))
                {
                    list.Add(so);
                    addedToAny = true;
                }
            }
        }

        if (addedToAny)
        {
            Debug.Log($"Adding SO: name={so.name}, id={so.id}, Type={soType.Name} (添加到 {typesToAdd.Count} 个类型列表)");
        }
        else
        {
            Debug.LogError($"未找到{soType.Name}或其父类对应的列表，请检查InitSOLists是否正确初始化");
        }
    }
    
    /// <summary>
    /// 根据ID查找SO实例
    /// </summary>
    /// <typeparam name="T">SO类型</typeparam>
    /// <param name="id">SO的ID</param>
    /// <returns>找到的SO实例，未找到返回null</returns>
    public T GetSOById<T>(string id) where T : BaseSO
    {
        return GetSOList<T>().FirstOrDefault(so => so.id == id);
    }

    /// <summary>
    /// 加载项目中所有的SO实例（编辑器模式 和 运行模式）
    /// </summary>
    public void LoadAllSO()
    {
        _soListsDic.Clear();
        InitSOLists();

        if (Application.isEditor && !Application.isPlaying)
        {
            // 编辑器模式（非Play状态）
            LoadAllSOInEditor();
        }
        else if (Application.isPlaying)
        {
            // 运行模式（Play状态/打包后）：同步加载（适合快速验证，实际推荐异步）
            LoadAllSOInRuntime();
            
            // 优先使用异步加载，避免阻塞主线程
            //StartCoroutine(LoadAllSOInRuntimeAsync());
        }
    }


    #region 运行模式加载逻辑（新增）

    /// <summary>
    /// 运行模式：同步加载所有SO（Resources路径）
    /// </summary>
    private void LoadAllSOInRuntime()
    {
        if (ResourcesManager.Instance == null)
        {
            Debug.LogError("运行时加载SO失败：ResourcesManager未初始化");
            return;
        }

        // 资源路径：Assets/Resources/Data/SO → Resources加载路径为"Data/SO"（忽略Assets/Resources前缀）
        string soResourcesPath = "Data/SO";

        // 通过资源管理器加载该路径下所有BaseSO类型资源（包括子类）
        var allSO = ResourcesManager.Instance.LoadAll<BaseSO>(soResourcesPath);

        if (allSO == null || allSO.Length == 0)
        {
            Debug.LogWarning($"运行时未加载到任何SO，路径：{soResourcesPath}");
            return;
        }

        // 逐个添加到管理器列表
        foreach (var so in allSO)
        {
            if (so != null)
            {
                AddSO(so);
            }
        }

        Debug.Log($"运行模式（同步）加载完成，共{allSO.Length}个BaseSO资源");
    }
    
    /*
    /// <summary>
    /// 运行模式：异步加载所有SO（推荐，避免卡顿）
    /// </summary>
    private IEnumerator LoadAllSOInRuntimeAsync()
    {
        if (ResourcesManager.Instance == null)
        {
            Debug.LogError("运行时异步加载SO失败：ResourcesManager未初始化");
            yield break;
        }

        // 资源路径：Assets/Resources/Data/SO → 加载路径为"Data/SO"
        string soResourcesPath = "Data/SO";

        // 异步加载该路径下所有BaseSO（通过ResourcesManager的异步接口）
        Coroutine loadCoroutine = ResourcesManager.Instance.LoadAsync<BaseSO[]>(
            soResourcesPath, 
            (isSuccess, soArray) => 
            {
                if (!isSuccess || soArray == null)
                {
                    Debug.LogError($"异步加载SO失败，路径：{soResourcesPath}");
                    return;
                }

                // 加载成功，添加到列表
                foreach (var so in soArray)
                {
                    if (so != null)
                    {
                        AddSO(so);
                    }
                }

                Debug.Log($"运行模式（异步）加载完成，共{soArray.Length}个BaseSO资源");
            }
        );

        // 等待加载协程完成
        if (loadCoroutine != null)
        {
            yield return loadCoroutine;
        }
    }*/

    #endregion


#if UNITY_EDITOR
    /// <summary>
    /// 编辑器模式下加载所有BaseSO资源
    /// </summary>
    private void LoadAllSOInEditor()
    {
        // 1. 查找项目中所有类型为BaseSO的资源（包括子类）
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:BaseSO");
        foreach (string guid in guids)
        {
            // 2. 从GUID获取资源路径
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            // 3. 加载资源并添加到管理器
            BaseSO so = UnityEditor.AssetDatabase.LoadAssetAtPath<BaseSO>(assetPath);
            if (so != null)
            {
                AddSO(so);
            }
        }
        Debug.Log($"编辑器模式下加载完成，共{guids.Length}个BaseSO资源");
    }
#endif


    #region 生命周期函数

    /// <summary>
    /// 初始化SO类型和SO列表的映射字典
    /// </summary>
    private void InitSOLists()
    {
        _soListsDic.Clear();
        
        // 获取所有继承自BaseSO的类型（包括抽象类，用于支持基类查询）
        var soTypes = Assembly.GetAssembly(typeof(BaseSO))
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(BaseSO)) || t == typeof(BaseSO));

        foreach (var type in soTypes)
        {
            // 创建对应类型的列表并添加到字典
            var listType = typeof(List<>).MakeGenericType(type);
            IList list = (IList)Activator.CreateInstance(listType);
            _soListsDic[type] = list;
        }
    }

    protected override void Initialize()
    {
        base.Initialize();
        InitSOLists();

        // 运行模式下，在初始化后自动触发加载（确保ResourcesManager已就绪）
        if (Application.isPlaying)
        {
            // 延迟1帧，确保ResourcesManager初始化完成
            StartCoroutine(DelayLoadInRuntime());
        }
    }

    /// <summary>
    /// 延迟一帧加载，避免ResourcesManager未初始化导致的问题
    /// </summary>
    private IEnumerator DelayLoadInRuntime()
    {
        yield return null; // 等待一帧
        LoadAllSO(); // 触发运行时加载
    }

    public override void Release()
    {
        base.Release();
        _soListsDic.Clear();
    }

    #endregion
}
