using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Framework.UI.Base
{
    // 单个窗口的配置数据
    [Serializable]
    public class WindowData
    {
        [Tooltip("窗口类名（必须与脚本类名一致）")]
        public string windowName;
    
        [Tooltip("Resources文件夹下的预制体路径（如UI/LoginWindow）")]
        public string path;
    
        [Tooltip("是否是全屏窗口")]
        public bool isFullScreen;
    
        [Tooltip("窗口层级（越大越靠上）")]
        public int sortingOrder;
    }

// 窗口配置表（ScriptableObject资源）
    [CreateAssetMenu(fileName = "WindowConfig", menuName = "UIFramework/WindowConfig", order = 1)]
    public class WindowConfig : ScriptableObject
    {
        [Header("窗口配置列表")]
        public List<WindowData> windowDatas = new List<WindowData>();

        // 缓存窗口数据（提高查询效率）
        private Dictionary<string, WindowData> _windowDataCache;

        private void OnEnable()
        {
            // 初始化缓存
            _windowDataCache = new Dictionary<string, WindowData>();
            foreach (var data in windowDatas)
            {
                if (!string.IsNullOrEmpty(data.windowName) && !_windowDataCache.ContainsKey(data.windowName))
                {
                    _windowDataCache.Add(data.windowName, data);
                }
            }
        }

        /// <summary>
        /// 根据窗口名称获取配置
        /// </summary>
        public WindowData GetWindowData(string windowName)
        {
            if (_windowDataCache.TryGetValue(windowName, out var data))
            {
                return data;
            }
        
            // 缓存未命中时，遍历列表查找（容错处理）
            foreach (var item in windowDatas)
            {
                if (item.windowName == windowName)
                {
                    _windowDataCache.Add(windowName, item);
                    return item;
                }
            }
        
            return null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器下自动生成窗口配置（扫描所有WindowBase子类）
        /// </summary>
        public void GeneratorWindowConfig()
        {
            // 清空现有配置（保留手动修改的路径等信息）
            var tempDatas = new Dictionary<string, WindowData>(windowDatas.ToDictionary(d => d.windowName));
            windowDatas.Clear();
            _windowDataCache.Clear();

            // 反射查找所有继承自WindowBase的类
            var windowTypes = Assembly.GetAssembly(typeof(WindowBase))
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(WindowBase)));

            foreach (var type in windowTypes)
            {
                string windowName = type.Name;
            
                // 如果已有配置，复用路径和层级信息
                if (tempDatas.TryGetValue(windowName, out var existingData))
                {
                    windowDatas.Add(existingData);
                    _windowDataCache.Add(windowName, existingData);
                }
                // 新窗口自动生成默认配置
                else
                {
                    var newData = new WindowData
                    {
                        windowName = windowName,
                        path = $"Prefab/UI/{windowName}", // 默认路径：Resources/Prefab/UI/窗口名.prefab
                        isFullScreen = false,
                        sortingOrder = 10 // 默认层级
                    };
                
                    windowDatas.Add(newData);
                    _windowDataCache.Add(windowName, newData);
                    Debug.Log($"自动生成窗口配置：{windowName}，默认路径：{newData.path}");
                }
            }

            // 标记资源为已修改，需要保存
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log("窗口配置自动生成完成，共" + windowDatas.Count + "个窗口");
        }
#endif
    }
}