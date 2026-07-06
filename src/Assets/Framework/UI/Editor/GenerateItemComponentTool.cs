using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Framework.UI.Editor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class GenerateItemComponentTool : Editor
{
    /// <summary>
    /// Item所拥有的UI控件列表
    /// </summary>
    private static List<ControlData> _controlDataList;
    
    /// <summary>
    /// 方法字典映射
    /// </summary>
    static Dictionary<string, string> _methodDic = new Dictionary<string, string>();

    [MenuItem("GameObject/生成列表项Item绑定脚本", false, 0)]
    static void CreateItemComponent()
    {
        GameObject go = Selection.objects.First() as GameObject; //获得的当前选中的GameObject
        if (go == null)
        {
            Debug.LogError("请选择一个GameObject");
            return;
        }
        
        _controlDataList = new List<ControlData>();
        
        //设置Item脚本生成的路径
        //检查路径UISetting.Instance.ItemScriptsGeneratorPath对应的目录是否已经存在
        if (!Directory.Exists(UISetting.Instance.ItemScriptsGeneratorPath))
        {
            //如果不存在 则创建对应路径
            Directory.CreateDirectory(UISetting.Instance.ItemScriptsGeneratorPath);
        }
        
        string itemName = go.name.Replace("#", "");
        
        AnalysisControlTool.AnalysisWindowControl(ref _controlDataList, go.transform, itemName);
        
        //存储字段名称
        string controlListJson = JsonConvert.SerializeObject(_controlDataList);
        PlayerPrefs.SetString("ControlDataList", controlListJson);
        
        //生成CS脚本
        string scriptContent = GenerateItemComponentContent(itemName);
        string scriptFilePath = $"{UISetting.Instance.ItemScriptsGeneratorPath}/{itemName}.cs"; 
        
        ScriptDisplayWindow.ShowWindow(scriptContent, scriptFilePath);
        EditorPrefs.SetString("itemGeneratorClassPath", scriptFilePath);
    }

    private static string GenerateItemComponentContent(string name)
    {
        _methodDic.Clear();
        
        StringBuilder sb = new StringBuilder();
        
        sb.AppendLine("/*---------------------------------");
        sb.AppendLine(" 该脚本由UI框架自动生成 负责挂在在对应的列表项上");
        sb.AppendLine(" 对列表项的控件进行声明和相关事件的绑定 一般情况下不允许修改");
        sb.AppendLine("---------------------------------*/");

        sb.AppendLine("using Framework.UI.Base;");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using UnityEngine.UI;");
        
        // 引入配置的命名空间
        foreach (string nameSpace in UISetting.Instance.UsingNameSpaceArr)
        {
            sb.AppendLine($"using {nameSpace};");
        }
        sb.AppendLine();
        
        sb.AppendLine($"\tpublic class {name} : MonoBehaviour");
        sb.AppendLine("\t{");
        
        // 控件字段声明区域
        sb.AppendLine("\t\t#region 控件字段");
        foreach (var controlData in _controlDataList)
        {
            if (controlData.DataList != null)
            {
                sb.AppendLine($"\t\tpublic {controlData.ControlType}[] {controlData.ControlName}{controlData.ControlType}Array;");
            }
            else
            {
                sb.AppendLine($"\t\tpublic {controlData.ControlType} {controlData.ControlName}{controlData.ControlType};");
            }
        }
        sb.AppendLine("\t\t#endregion");
        sb.AppendLine();
        
        // Unity生命周期方法区域
        sb.AppendLine("\t\t#region Unity生命周期方法");
        sb.AppendLine("\t\t/// <summary>");
        sb.AppendLine("\t\t/// 初始化组件与事件绑定（自动调用）");
        sb.AppendLine("\t\t/// </summary>");
        sb.AppendLine("\t\tprivate void Awake()");
        sb.AppendLine("\t\t{");
        sb.AppendLine("\t\t\t// 自动绑定UI事件");
        foreach (var controlData in _controlDataList)
        {
            string fieldName = $"{controlData.ControlName}{controlData.ControlType}";
            string controlType = controlData.ControlType;
            string methodPrefix = $"On{controlData.ControlName}";
            
            if (controlType.Contains("Button"))
            {
                sb.AppendLine($"\t\t\t{fieldName}.onClick.AddListener({methodPrefix}ButtonClick);");
            }
            else if (controlType.Contains("InputField"))
            {
                sb.AppendLine($"\t\t\t{fieldName}.onValueChanged.AddListener({methodPrefix}InputChange);");
                sb.AppendLine($"\t\t\t{fieldName}.onEndEdit.AddListener({methodPrefix}InputEnd);");
            }
            else if (controlType.Contains("Toggle"))
            {
                sb.AppendLine($"\t\t\t{fieldName}.onValueChanged.AddListener({methodPrefix}ToggleChange);");
            }
            else if (controlType.Contains("Slider"))
            {
                sb.AppendLine($"\t\t\t{fieldName}.onValueChanged.AddListener({methodPrefix}SliderValueChange);");
            }
        }
        sb.AppendLine("\t\t\t");
        sb.AppendLine($"\t\t\tDebug.Log($\"{{gameObject.name}} UI事件绑定完成\");");
        sb.AppendLine("\t\t}");
        sb.AppendLine();
        
        sb.AppendLine("\t\t/// <summary>");
        sb.AppendLine("\t\t/// 设置列表项数据");
        sb.AppendLine("\t\t/// </summary>");
        sb.AppendLine("\t\tpublic void SetItemData()");
        sb.AppendLine("\t\t{");
        sb.AppendLine("\t\t\t// 请在此处实现数据设置逻辑");
        sb.AppendLine("\t\t}");
        sb.AppendLine();
        
        sb.AppendLine("\t\t/// <summary>");
        sb.AppendLine("\t\t/// 销毁时自动清理资源（自动调用）");
        sb.AppendLine("\t\t/// </summary>");
        sb.AppendLine("\t\tprivate void OnDestroy()");
        sb.AppendLine("\t\t{");
        sb.AppendLine("\t\t\t// 清理事件监听，避免内存泄漏");
        foreach (var controlData in _controlDataList)
        {
            string fieldName = $"{controlData.ControlName}{controlData.ControlType}";
            string controlType = controlData.ControlType;
            
            if (controlType.Contains("Button"))
            {
                sb.AppendLine($"\t\t\t{fieldName}?.onClick.RemoveAllListeners();");
            }
            else if (controlType.Contains("InputField"))
            {
                sb.AppendLine($"\t\t\t{fieldName}?.onValueChanged.RemoveAllListeners();");
                sb.AppendLine($"\t\t\t{fieldName}?.onEndEdit.RemoveAllListeners();");
            }
            else if (controlType.Contains("Toggle"))
            {
                sb.AppendLine($"\t\t\t{fieldName}?.onValueChanged.RemoveAllListeners();");
            }
            else if (controlType.Contains("Slider"))
            {
                sb.AppendLine($"\t\t\t{fieldName}?.onValueChanged.RemoveAllListeners();");
            }
        }
        sb.AppendLine("\t\t\t");
        sb.AppendLine($"\t\t\tDebug.Log($\"{{gameObject.name}} UI事件清理完成\");");
        sb.AppendLine("\t\t}");
        sb.AppendLine("\t\t#endregion");
        sb.AppendLine();
        
        // UI事件处理方法区域
        sb.AppendLine("\t\t#region UI事件处理");
        foreach (var controlData in _controlDataList)
        {
            string controlType = controlData.ControlType;
            string methodPrefix = $"On{controlData.ControlName}";
            
            if (controlType.Contains("Button"))
            {
                CreateItemMethod(sb, ref _methodDic, $"{methodPrefix}ButtonClick");
            }
            else if (controlType.Contains("InputField"))
            {
                CreateItemMethod(sb, ref _methodDic, $"{methodPrefix}InputChange", "string text");
                CreateItemMethod(sb, ref _methodDic, $"{methodPrefix}InputEnd", "string text");
            }
            else if (controlType.Contains("Toggle"))
            {
                CreateItemMethod(sb, ref _methodDic, $"{methodPrefix}ToggleChange", "bool isOn");
            }
            else if (controlType.Contains("Slider"))
            {
                CreateItemMethod(sb, ref _methodDic, $"{methodPrefix}SliderValueChange", "float value");
            }
        }
        sb.AppendLine("\t\t#endregion");
        
        sb.AppendLine("\t}");
        
        return sb.ToString();
    }

    /// <summary>
    /// 生成列表项的事件处理方法
    /// </summary>
    private static void CreateItemMethod(StringBuilder sb, ref Dictionary<string, string> methodDic, string methodName, string param = "")
    {
        // 构建方法内容
        StringBuilder methodContent = new StringBuilder();
        methodContent.AppendLine($"\t\tprivate void {methodName}({param})");
        methodContent.AppendLine("\t\t{");
        methodContent.AppendLine($"\t\t\t// {methodName} 事件处理逻辑");
        methodContent.AppendLine("\t\t}");
        methodContent.AppendLine();
        
        // 添加到字符串构建器
        sb.AppendLine(methodContent.ToString());
        
        // 存储到方法字典用于后续更新
        methodDic.Add(methodName, methodContent.ToString());
    }

    [UnityEditor.Callbacks.DidReloadScripts]
    private static void AddComponentToItem()
    {
        //如果当前不是生成数据脚本的回调，就不处理
        string scriptPath = EditorPrefs.GetString("itemGeneratorClassPath").Replace(Application.dataPath, "Assets/");
        if (string.IsNullOrEmpty(scriptPath))
        {
            return;
        }
        
        //1.通过反射的方式，从程序集中找到这个脚本，把它挂在到当前的物体上
        //获取所有的程序集
        System.Type targetScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath)?.GetClass();
        if (targetScript == null)
        {
            Debug.Log($"Failed to load script! Path:{scriptPath}");
            return;
        }

        //获取要挂载的那个物体
        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject == null)
            return;

        //先获取现窗口上有没有挂载该数据组件，如果没挂载在进行挂载
        Component compt = selectedObject.GetComponent(targetScript);
        if (compt == null)
        {
            compt = selectedObject.AddComponent(targetScript);
        }

        //2.通过反射的方式，遍历数据列表 找到对应的字段，赋值
        //获取对象数据列表
        string datalistJson = PlayerPrefs.GetString("ControlDataList");
        List<ControlData> objDataList = JsonConvert.DeserializeObject<List<ControlData>>(datalistJson);
        //获取脚本所有字段
        FieldInfo[] fieldInfoList = targetScript.GetFields();

        foreach (var item in fieldInfoList)
        {
            foreach (var objData in objDataList)
            {
                if (item.Name == $"{objData.ControlName}{objData.ControlType}"||item.Name == $"{objData.ControlName}{objData.ControlType}Array")
                {
                    //根据Insid找到对应的对象
                    GameObject uiObject = EditorUtility.InstanceIDToObject(objData.InstanceID) as GameObject;
                    //设置该字段所对应的对象
                    if (objData.DataList == null)
                    {
                        //设置该字段所对应的对象
                        if (string.Equals(objData.ControlType, "GameObject"))
                        {
                            item.SetValue(compt, uiObject);
                        }
                        else
                        {
                            Debug.Log("targetComponent:"+uiObject.GetComponent(objData.ControlType) +" type:"+objData.ControlType);
                            item.SetValue(compt, uiObject.GetComponent(objData.ControlType));
                        }
                    }
                    else
                    {
                        if (objData.ControlType.Contains("GameObject"))
                        {
                            GameObject[] newArray = new GameObject[objData.DataList.Count];
                            for (int i = 0; i < objData.DataList.Count; i++)
                            {
                                newArray[i] = EditorUtility.InstanceIDToObject(objData.DataList[i].InstanceID) as GameObject;
                            }

                            item.SetValue(compt, newArray);
                        }
                        else
                        {
                            // 获取数组类型
                            Type arrayType = item.FieldType;
                            // 获取数组元素类型
                            Type elementType = arrayType.GetElementType();
                            //获取该节点下的所有的物体
                            Component[] components = uiObject.GetComponentsInChildren(elementType);
                            // 创建目标数组
                            Array targetArray = Array.CreateInstance(elementType, components.Length);

                            // 将组件赋值给目标数组
                            for (int i = 0; i < components.Length; i++)
                            {
                                if (components[i] != null && elementType.IsAssignableFrom(components[i].GetType()))
                                {
                                    targetArray.SetValue(components[i], i);
                                }
                                else
                                {
                                    Debug.LogError($"Element at index {i} is not of type {elementType.Name}!");
                                }
                            }

                            // 设置字段的值
                            item.SetValue(compt, targetArray);
                        }
                    }

                    break;
                }
            }
        }
        EditorPrefs.DeleteKey("itemGeneratorClassPath");
        PlayerPrefs.DeleteKey("ControlDataList");
        //自动保存预制体
        if (AnalysisControlTool.IsPrefabInstance(selectedObject))
        {
            PrefabUtility.ApplyPrefabInstance(selectedObject, InteractionMode.AutomatedAction);
        }
        UnityEditor.EditorUtility.SetDirty(compt); // 标记对象为"脏"以刷新
        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(compt);
    }
}