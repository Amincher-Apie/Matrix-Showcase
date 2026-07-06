using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 父级对象查找工具类（静态类）
/// 提供配置层级间的父级查找能力，支持MultiFieldItem→ColumnMapping→WorksheetConfig的关联识别
/// 核心解决配置项无法识别所属父级的问题
/// </summary>
public static class ParentFinder
{
    /// <summary>
    /// 根据子对象查找指定类型的父级对象
    /// </summary>
    /// <typeparam name="T">目标父级类型（如ColumnMapping、WorksheetConfig）</typeparam>
    /// <param name="child">需要查找父级的子对象（如MultiFieldItem、ColumnMapping）</param>
    /// <returns>找到的父级对象，未找到则返回null</returns>
    public static T FindParent<T>(object child) where T : class
    {
        // 子对象为空时直接返回，避免空引用异常
        if (child == null) 
        {
            Debug.LogError("查找父级失败：子对象为null");
            return null;
        }

        // 1. 优先处理：MultiFieldItem → ColumnMapping 的父级查找
        if (typeof(T) == typeof(ColumnMapping) && child is MultiFieldItem multiFieldChild)
        {
            return FindColumnMappingForMultiFieldItem(multiFieldChild) as T;
        }

        // 2. 次优先处理：ColumnMapping → WorksheetConfig 的父级查找
        if (typeof(T) == typeof(WorksheetConfig) && child is ColumnMapping columnMappingChild)
        {
            return FindWorksheetConfigForColumnMapping(columnMappingChild) as T;
        }

        // 3. 兜底逻辑：兼容其他类型的父级查找（如WorksheetConfig→ExcelFileConfig）
        try
        {
            // 先从当前活跃的配置文件中查找（提升效率）
            var activeConfig = GetActiveExcelToSOConfig();
            if (activeConfig != null)
            {
                foreach (var excelFile in activeConfig.excelFileConfigs)
                {
                    foreach (var worksheet in excelFile.worksheetConfigs)
                    {
                        // 检查当前工作表的列映射是否包含子对象
                        if (worksheet.columnMappings == null) continue;
                        foreach (var column in worksheet.columnMappings)
                        {
                            if (IsParentContainChild(column, child))
                            {
                                return column as T;
                            }
                        }
                        // 检查当前工作表是否直接包含子对象
                        if (IsParentContainChild(worksheet, child))
                        {
                            return worksheet as T;
                        }
                    }
                }
            }

            // 递归查找（处理深层嵌套场景，避免遗漏）
            var visited = new HashSet<object>(); // 记录已访问对象，防止循环引用
            return RecursiveSearch<T>(child, visited);
        }
        catch (Exception ex)
        {
            Debug.LogError($"查找父级失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 查找包含指定MultiFieldItem的ColumnMapping（针对性查找）
    /// </summary>
    /// <param name="multiFieldItem">需要查找父级的MultiFieldItem实例</param>
    /// <returns>找到的ColumnMapping，未找到则返回null</returns>
    private static ColumnMapping FindColumnMappingForMultiFieldItem(MultiFieldItem multiFieldItem)
    {
        // 1. 获取项目中所有ExcelToSOConfig配置文件（确保不遗漏）
        var configGuids = AssetDatabase.FindAssets("t:ExcelToSOConfig");
        foreach (var guid in configGuids)
        {
            var configPath = AssetDatabase.GUIDToAssetPath(guid);
            var config = AssetDatabase.LoadAssetAtPath<ExcelToSOConfig>(configPath);
            if (config == null) continue;

            // 2. 遍历所有配置层级，检查ColumnMapping的multiFieldItems列表
            foreach (var excelFile in config.excelFileConfigs)
            {
                foreach (var worksheet in excelFile.worksheetConfigs)
                {
                    if (worksheet.columnMappings == null) continue;
                    foreach (var column in worksheet.columnMappings)
                    {
                        if (column.multiFieldItems == null) continue;

                        // 关键判断：列表是否包含当前MultiFieldItem（直接关联识别）
                        if (column.multiFieldItems.Contains(multiFieldItem))
                        {
                            return column;
                        }
                    }
                }
            }
        }

        Debug.LogError("未找到MultiFieldItem的父级ColumnMapping，请检查配置是否保存");
        return null;
    }

    /// <summary>
    /// 查找包含指定ColumnMapping的WorksheetConfig（针对性查找）
    /// </summary>
    /// <param name="columnMapping">需要查找父级的ColumnMapping实例</param>
    /// <returns>找到的WorksheetConfig，未找到则返回null</returns>
    private static WorksheetConfig FindWorksheetConfigForColumnMapping(ColumnMapping columnMapping)
    {
        // 1. 获取项目中所有ExcelToSOConfig配置文件
        var configGuids = AssetDatabase.FindAssets("t:ExcelToSOConfig");
        foreach (var guid in configGuids)
        {
            var configPath = AssetDatabase.GUIDToAssetPath(guid);
            var config = AssetDatabase.LoadAssetAtPath<ExcelToSOConfig>(configPath);
            if (config == null) continue;

            // 2. 遍历所有配置层级，检查WorksheetConfig的columnMappings列表
            foreach (var excelFile in config.excelFileConfigs)
            {
                foreach (var worksheet in excelFile.worksheetConfigs)
                {
                    if (worksheet.columnMappings == null) continue;

                    // 关键判断：列表是否包含当前ColumnMapping（直接关联识别）
                    if (worksheet.columnMappings.Contains(columnMapping))
                    {
                        return worksheet;
                    }
                }
            }
        }

        Debug.LogError("未找到ColumnMapping的父级WorksheetConfig，请检查配置是否保存");
        return null;
    }

    /// <summary>
    /// 获取当前在Inspector中活跃的ExcelToSOConfig配置文件
    /// 优先处理活跃配置，提升查找效率（避免遍历所有配置）
    /// </summary>
    /// <returns>活跃的ExcelToSOConfig，未找到则返回null</returns>
    private static ExcelToSOConfig GetActiveExcelToSOConfig()
    {
        // 1. 检查当前选中的对象是否为配置文件
        var selectedObj = Selection.activeObject;
        if (selectedObj is ExcelToSOConfig activeConfig)
        {
            return activeConfig;
        }

        // 2. 检查当前Inspector窗口打开的对象是否为配置文件
        var editorWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
        foreach (var window in editorWindows)
        {
            if (window.title.Contains("Inspector"))
            {
                // 反射获取Inspector当前检查的对象（m_CurrentInspectedObject为Inspector私有字段）
                var field = window.GetType().GetField("m_CurrentInspectedObject", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    var inspectedObj = field.GetValue(window) as ExcelToSOConfig;
                    if (inspectedObj != null)
                    {
                        return inspectedObj;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 判断父级对象是否包含指定子对象（通过列表字段判断）
    /// 仅检查父级的List类型字段，避免无效反射
    /// </summary>
    /// <param name="parent">父级对象</param>
    /// <param name="child">子对象</param>
    /// <returns>包含则返回true，否则返回false</returns>
    private static bool IsParentContainChild(object parent, object child)
    {
        if (parent == null || child == null) return false;

        // 反射获取父级的所有List类型字段
        var listFields = parent.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(List<>));

        foreach (var field in listFields)
        {
            // 将字段值转为IEnumerable，遍历检查是否包含子对象
            var list = field.GetValue(parent) as IEnumerable;
            if (list == null) continue;

            foreach (var item in list)
            {
                if (item == child)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 递归查找父级对象（兜底逻辑）
    /// 处理深层嵌套或非常规关联场景
    /// </summary>
    /// <typeparam name="T">目标父级类型</typeparam>
    /// <param name="current">当前递归检查的对象</param>
    /// <param name="visited">已访问对象集合（防止循环引用）</param>
    /// <returns>找到的父级对象，未找到则返回null</returns>
    private static T RecursiveSearch<T>(object current, HashSet<object> visited) where T : class
    {
        // 终止条件1：当前对象为空或已访问（避免循环引用）
        if (current == null || visited.Contains(current)) return null;
        visited.Add(current);

        // 终止条件2：当前对象就是目标父级类型
        if (current is T target) return target;

        // 递归检查当前对象的所有字段值
        var fields = current.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var field in fields)
        {
            var fieldValue = field.GetValue(current);
            var result = RecursiveSearch<T>(fieldValue, visited);
            if (result != null) return result;
        }

        return null;
    }

    /// <summary>
    /// 判断指定类型是否为Unity内置类型（用于过滤SO的无效字段）
    /// 排除Unity内置类（如Object、MonoBehaviour）的字段，仅保留自定义业务字段
    /// </summary>
    /// <param name="type">需要判断的类型</param>
    /// <returns>是Unity内置类型则返回true，否则返回false</returns>
    public static bool IsUnityBuiltInType(Type type)
    {
        if (type == null) return false;

        // 内置类型特征：命名空间以UnityEngine或UnityEditor开头，且不是自定义BaseSO子类
        return type.Namespace != null && 
               (type.Namespace.StartsWith("UnityEngine") || type.Namespace.StartsWith("UnityEditor")) &&
               !type.Name.StartsWith("BaseSO");
    }
}
