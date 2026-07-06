using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Excel转SO工具的总配置类（ScriptableObject）
/// 管理所有Excel文件的配置列表，可保存为SO资产（如ExcelToSO_Config.asset）
/// 工具的核心配置入口，所有解析规则都通过该类统一管理
/// </summary>
[CreateAssetMenu(fileName = "ExcelToSO_Config", menuName = "工具/Excel转SO配置", order = 100)]
public class ExcelToSOConfig : ScriptableObject
{
    /// <summary>
    /// Excel文件配置列表
    /// 存储所有需要处理的Excel文件配置（每个文件对应一个ExcelFileConfig）
    /// 工具运行时会遍历该列表，逐一解析Excel文件
    /// </summary>
    [LabelText("Excel文件列表"), ListDrawerSettings(Expanded = true, DefaultExpandedState = true),
     Tooltip("添加所有需要处理的Excel文件（每个文件对应一个配置项）")]
    public List<ExcelFileConfig> excelFileConfigs = new List<ExcelFileConfig>();
}