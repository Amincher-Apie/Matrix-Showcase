using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using Framework.Singleton;

/// <summary>
/// Json数据管理类 专注于通过Newtonsoft.Json进行数据的序列化和反序列化
/// </summary>
public class JsonManager : SingletonBase<JsonManager>, IDisposable
{
    /// <summary>
    /// Newtonsoft.Json序列化设置
    /// 统一配置以保证框架内序列化行为一致性
    /// </summary>
    private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
    {
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.Indented
    };

    /// <summary>
    /// 初始化（单例创建时调用）
    /// 符合框架SingletonBase的生命周期规范
    /// </summary>
    protected override void Initialize()
    {
        base.Initialize();
        Debug.Log("[JsonManager] 初始化完成，使用Newtonsoft.Json序列化器");
    }

    /// <summary>
    /// 根据文件名生成完整读取路径：
    /// 1. 优先在 PersistentDataPath 中找玩家的可写文件
    /// 2. 若未找到，则退回到 StreamingAssets 中寻找首包默认文件（只读）
    /// 与框架资源管理路径策略保持一致
    /// </summary>
    /// <param name="fileName">不带扩展名的文件名</param>
    /// <returns>最终用于读取的完整文件路径</returns>
    private static string GetFullPath(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError("[JsonManager] 文件名不能为空");
            return string.Empty;
        }

        // 优先检查玩家可读写路径（符合框架数据持久化策略）
        string persistentPath = Path.Combine(Application.persistentDataPath, $"{fileName}.json");
        Debug.Log($"[JsonManager] 查找文件 - 文件名: {fileName}");
        Debug.Log($"[JsonManager] PersistentDataPath: {Application.persistentDataPath}");
        Debug.Log($"[JsonManager] 尝试路径1 (Persistent): {persistentPath}");
        Debug.Log($"[JsonManager] 路径1是否存在: {File.Exists(persistentPath)}");
        
        if (File.Exists(persistentPath))
        {
            Debug.Log($"[JsonManager] ✓ 找到文件: {persistentPath}");
            return persistentPath;
        }

        // 其次检查首包默认数据路径
        string streamingPath = Path.Combine(Application.streamingAssetsPath, $"{fileName}.json");
        Debug.Log($"[JsonManager] StreamingAssetsPath: {Application.streamingAssetsPath}");
        Debug.Log($"[JsonManager] 尝试路径2 (StreamingAssets): {streamingPath}");
        Debug.Log($"[JsonManager] 路径2是否存在: {File.Exists(streamingPath)}");
        
        if (File.Exists(streamingPath))
        {
            Debug.Log($"[JsonManager] ✓ 找到文件: {streamingPath}");
        }
        else
        {
            Debug.LogWarning($"[JsonManager] ✗ 两个路径都未找到文件，将返回StreamingAssets路径");
        }
        
        return streamingPath;
    }

    #region 同步方法
    /// <summary>
    /// 存储数据到硬盘（同步）
    /// </summary>
    /// <param name="data">需要保存的数据</param>
    /// <param name="fileName">文件名（不带扩展名）</param>
    public void SaveData(object data, string fileName)
    {
        if (data == null)
        {
            Debug.LogError("[JsonManager] 保存的数据不能为null");
            throw new ArgumentNullException(nameof(data));
        }

        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError("[JsonManager] 文件名不能为空");
            throw new ArgumentException("文件名不能为空", nameof(fileName));
        }

        try
        {
            string json = JsonConvert.SerializeObject(data, _jsonSettings);
            string path = Path.Combine(Application.persistentDataPath, $"{fileName}.json");
            
            // 确保目录存在（框架中文件操作的标准处理）
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, json);
            Debug.Log($"[JsonManager] 数据已保存至: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[JsonManager] 同步保存数据失败: {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// 从硬盘读取数据（同步）
    /// 当数据结构变化时自动容错，返回默认值填充
    /// </summary>
    /// <param name="fileName">文件名（不带扩展名）</param>
    /// <typeparam name="T">目标类型；必须有无参构造函数</typeparam>
    public T LoadData<T>(string fileName) where T : new()
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError("[JsonManager] 文件名不能为空");
            return new T();
        }

        try
        {
            string path = GetFullPath(fileName);
            Debug.Log($"[JsonManager] LoadData - 最终使用的路径: {path}");
            Debug.Log($"[JsonManager] LoadData - 文件是否存在: {File.Exists(path)}");
            
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[JsonManager] 未找到数据文件，返回默认值: {fileName}.json");
                Debug.LogWarning($"[JsonManager] 查找的完整路径: {path}");
                return new T();
            }

            Debug.Log($"[JsonManager] ✓ 成功找到文件，开始读取: {path}");
            string json = File.ReadAllText(path);
            Debug.Log($"[JsonManager] ✓ 文件读取成功，JSON长度: {json.Length} 字符");
            return JsonConvert.DeserializeObject<T>(json, _jsonSettings) ?? new T();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[JsonManager] 反序列化失败（结构变化或文件损坏）：{e.Message}，返回默认值");
            return new T();
        }
    }

    /// <summary>
    /// 删除指定名称的数据文件（同步）
    /// </summary>
    /// <param name="fileName">要删除的文件名（不带扩展名）</param>
    /// <returns>是否成功删除文件</returns>
    public bool DeleteData(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError("[JsonManager] 文件名不能为空");
            return false;
        }

        try
        {
            string path = Path.Combine(Application.persistentDataPath, $"{fileName}.json");
            
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log($"[JsonManager] 成功删除数据文件: {path}");
                return true;
            }
            
            string streamingPath = Path.Combine(Application.streamingAssetsPath, $"{fileName}.json");
            if (File.Exists(streamingPath))
            {
                Debug.LogWarning($"[JsonManager] 尝试删除StreamingAssets中的只读文件: {streamingPath}，操作被拒绝");
                return false;
            }
            
            Debug.Log($"[JsonManager] 未找到要删除的数据文件: {fileName}.json");
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[JsonManager] 删除数据文件失败: {e.Message}");
            return false;
        }
    }
    #endregion

    #region 异步方法
    /// <summary>
    /// 存储数据到硬盘（异步）
    /// 基于Task的异步实现，与框架异步操作模式兼容
    /// </summary>
    public async Task SaveDataAsync(object data, string fileName)
    {
        if (data == null)
        {
            Debug.LogError("[JsonManager] 保存的数据不能为null");
            throw new ArgumentNullException(nameof(data));
        }

        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError("[JsonManager] 文件名不能为空");
            throw new ArgumentException("文件名不能为空", nameof(fileName));
        }

        try
        {
            string json = JsonConvert.SerializeObject(data, _jsonSettings);
            string path = Path.Combine(Application.persistentDataPath, $"{fileName}.json");
            
            // 确保目录存在
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(path, json);
            Debug.Log($"[JsonManager] 数据已异步保存至: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[JsonManager] 异步保存数据失败: {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// 从硬盘读取数据（异步）
    /// </summary>
    public async Task<T> LoadDataAsync<T>(string fileName) where T : new()
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError("[JsonManager] 文件名不能为空");
            return new T();
        }

        try
        {
            string path = GetFullPath(fileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[JsonManager] 未找到数据文件，返回默认值: {fileName}.json");
                return new T();
            }

            string json = await File.ReadAllTextAsync(path);
            return JsonConvert.DeserializeObject<T>(json, _jsonSettings) ?? new T();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[JsonManager] 异步反序列化失败：{e.Message}，返回默认值");
            return new T();
        }
    }

    /// <summary>
    /// 删除指定名称的数据文件（异步）
    /// </summary>
    /// <param name="fileName">要删除的文件名（不带扩展名）</param>
    /// <returns>是否成功删除文件</returns>
    public async Task<bool> DeleteDataAsync(string fileName)
    {
        // 利用Task.Run包装同步方法，保持异步一致性
        return await Task.Run(() => DeleteData(fileName));
    }
    #endregion

    /// <summary>
    /// 序列化对象为Json字符串
    /// 提供给外部直接使用的序列化接口
    /// </summary>
    public string Serialize(object obj)
    {
        if (obj == null)
        {
            Debug.LogWarning("[JsonManager] 序列化对象为null");
            return string.Empty;
        }

        try
        {
            return JsonConvert.SerializeObject(obj, _jsonSettings);
        }
        catch (Exception e)
        {
            Debug.LogError($"[JsonManager] 序列化失败: {e.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// 反序列化Json字符串为对象
    /// 提供给外部直接使用的反序列化接口
    /// </summary>
    public T Deserialize<T>(string json) where T : new()
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("[JsonManager] 反序列化字符串为空");
            return new T();
        }

        try
        {
            return JsonConvert.DeserializeObject<T>(json, _jsonSettings) ?? new T();
        }
        catch (Exception e)
        {
            Debug.LogError($"[JsonManager] 反序列化失败: {e.Message}");
            return new T();
        }
    }

    /// <summary>
    /// 实现IDisposable接口，释放资源
    /// 符合框架资源释放规范
    /// </summary>
    public void Dispose()
    {
        // 清理可能的缓存资源
        Debug.Log("[JsonManager] 已释放资源");
    }

    /// <summary>
    /// 释放单例实例
    /// 重写框架的Release方法，确保正确清理
    /// </summary>
    public override void Release()
    {
        Dispose();
        base.Release();
    }
}
