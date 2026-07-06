using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Excel文件的配置类
/// 定义单个Excel文件的解析规则：文件路径、数据起始行、工作表配置列表
/// 支持多工作表管理，一个Excel文件可对应多个工作表配置
/// </summary>
[Serializable]
public class ExcelFileConfig 
{
    /// <summary>
    /// Excel文件路径
    /// 存储需要解析的Excel文件的绝对路径（仅支持.xlsx格式）
    /// 路径必须存在，否则无法读取数据
    /// </summary>
    [BoxGroup("文件基础信息"), LabelText("Excel文件路径"), 
     FilePath(Extensions = "xlsx", RequireExistingPath = true),
     Tooltip("选择要解析的Excel文件（仅支持.xlsx格式，不支持.xls）")]
    public string excelFilePath;

    /// <summary>
    /// 数据起始行（从1开始）
    /// 存储Excel文件中实际数据的起始行号（默认2，即跳过第1行标题）
    /// 根据Excel格式调整，确保读取到正确数据
    /// </summary>
    [BoxGroup("文件基础信息"), LabelText("数据起始行"), 
     MinValue(1), 
     Tooltip("Excel中实际数据的起始行（标题行下方第一行，默认2）")]
    public int dataStartRow = 2;

    /// <summary>
    /// 工作表配置列表
    /// 存储当前Excel文件中需要处理的工作表配置（无需处理的工作表可不添加）
    /// 一个Excel文件可对应多个工作表配置
    /// </summary>
    [LabelText("工作表列表"), 
     ListDrawerSettings(Expanded = true, AddCopiesLastElement = true),
     Tooltip("添加当前Excel中需要处理的工作表（无需处理的工作表可不加）")]
    public List<WorksheetConfig> worksheetConfigs = new List<WorksheetConfig>();
}