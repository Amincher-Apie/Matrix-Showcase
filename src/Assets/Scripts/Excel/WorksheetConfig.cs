using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Excel工作表的配置类
/// 定义单个工作表的解析规则：关联SO模板、生成路径、列映射列表
/// 支持原生拖拽SO模板，替代Type选择，提升易用性和稳定性
/// </summary>
[Serializable]
public class WorksheetConfig
{
    /// <summary>
    /// 工作表名称
    /// 对应Excel文件中的工作表名称（区分大小写，如"WeaponConfig"）
    /// 必须与Excel中的名称完全一致，否则无法读取数据
    /// </summary>
    [BoxGroup("基础配置"), LabelText("工作表名称"), Required, Tooltip("Excel中工作表的名称（区分大小写，如WeaponConfig）")]
    public string workSheetName;

    /// <summary>
    /// SO模板实例（原生拖拽）
    /// 存储当前工作表需要生成的SO类型实例（如Weapon_Template）
    /// 通过拖拽赋值，替代Type选择，避免序列化问题
    /// </summary>
    [BoxGroup("基础配置"), LabelText("SO模板（直接拖拽到这里）"), 
     Tooltip("从Project窗口拖拽【BaseSO子类的SO实例】（如Weapon_Template）"),
     SerializeField] // Unity原生序列化标记，确保拖拽后引用不丢失
    public BaseSO soTemplate;

    /// <summary>
    /// SO生成根目录
    /// 存储当前工作表生成的SO资产的保存路径（如"Assets/Data/SO/Weapon"）
    /// 路径不存在时会自动创建
    /// </summary>
    [BoxGroup("路径配置"), LabelText("SO生成的根目录"), 
     FolderPath(RequireExistingPath = false),
     Tooltip("生成的SO资产保存路径（自动创建不存在的目录，默认Assets/Data/SO）")]
    public string savePath = "Assets/Data/SO";

    /// <summary>
    /// 列映射列表
    /// 存储当前工作表所有Excel列的映射规则（每个列对应一个ColumnMapping）
    /// 决定Excel数据如何解析到SO字段
    /// </summary>
    [BoxGroup("列映射配置"), LabelText("Excel列与SO字段映射关系"), 
     ListDrawerSettings(Expanded = true, DefaultExpandedState = true),
     Tooltip("添加每一列的映射规则，字段支持下拉选择")]
    public List<ColumnMapping> columnMappings = new List<ColumnMapping>();

    #region 原生校验：SO模板合法性检查
    /// <summary>
    /// Unity编辑器原生方法：字段值变化或进入编辑模式时自动调用
    /// 用于校验SO模板的合法性，避免无效配置导致生成失败
    /// </summary>
    private void OnValidate()
    {
        // 模板为空时不校验（避免初始状态报错）
        if (soTemplate == null) return;

        // 校验1：模板必须是Project窗口中的资产（排除场景中的临时实例）
        bool isProjectAsset = AssetDatabase.Contains(soTemplate);
        if (!isProjectAsset)
        {
            Debug.LogError($"工作表「{workSheetName}」SO模板错误：请拖拽Project窗口中的SO资产（不要拖场景实例）");
            soTemplate = null; // 置空无效引用，避免后续错误
            return;
        }

        // 校验2：模板必须是BaseSO的非抽象子类（确保符合配置规范）
        Type templateType = soTemplate.GetType();
        bool isBaseSoSubclass = templateType.IsSubclassOf(typeof(BaseSO)) && !templateType.IsAbstract;
        if (!isBaseSoSubclass)
        {
            Debug.LogError($"工作表「{workSheetName}」SO模板错误：仅支持拖拽BaseSO子类的SO实例（如WeaponSO）");
            soTemplate = null; // 置空无效引用，避免后续错误
            return;
        }
    }
    #endregion

    #region 辅助方法：获取SO类型
    /// <summary>
    /// 从SO模板中获取SO类型（对外提供，统一类型获取逻辑）
    /// 替代直接访问soTemplate.GetType()，便于后续维护
    /// </summary>
    /// <returns>SO模板对应的Type，模板为空则返回null</returns>
    public Type GetSoType()
    {
        if (soTemplate == null)
        {
            Debug.LogError($"工作表「{workSheetName}」未设置SO模板，请从Project窗口拖拽合法SO实例");
            return null;
        }
        return soTemplate.GetType();
    }
    #endregion
}
