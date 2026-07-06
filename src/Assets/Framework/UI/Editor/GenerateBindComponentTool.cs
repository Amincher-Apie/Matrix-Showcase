using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Plastic.Newtonsoft.Json;
using UnityEngine;
using UnityEditor;

namespace Framework.UI.Editor
{
    public class GenerateBindComponentTool : UnityEditor.Editor
    {
        private static List<ControlData> _controlDataList;

        [MenuItem("GameObject/生成窗口绑定组件数据脚本", false, 0)]
        static void GenerateWindowDataComponent()
        {
            GameObject obj = Selection.objects.First() as GameObject;
            if (obj == null)
            {
                Debug.LogError("需要选择 GameObject");
                return;
            }
            _controlDataList = new List<ControlData>();

            // 获取配置的生成路径
            string targetPath = UISetting.Instance.BindComponentGeneratorPath;
            if (string.IsNullOrEmpty(targetPath))
            {
                Debug.LogError("错误：绑定组件生成路径未配置，请检查UISetting中的BindComponentGeneratorPath");
                return;
            }

            // 处理路径（统一分隔符，确保正确性）
            targetPath = targetPath.Replace("\\", "/");

            // 检查并创建文件夹（仅在不存在时创建）
            if (!Directory.Exists(targetPath))
            {
                try
                {
                    Directory.CreateDirectory(targetPath);
                    Debug.Log($"成功创建绑定组件文件夹：{targetPath}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"创建文件夹失败：{ex.Message}，路径：{targetPath}");
                    return;
                }
            }
            else
            {
                Debug.Log($"绑定组件文件夹已存在，路径：{targetPath}");
            }

            // 解析控件数据
            AnalysisControlTool.AnalysisWindowControl(ref _controlDataList, obj.transform, obj.name);
            
            // 序列化控件数据
            string datalistJson = JsonConvert.SerializeObject(_controlDataList);
            PlayerPrefs.SetString("ControlDataList", datalistJson);
            
            // 生成脚本内容
            string csContent = GenerateWindowDataComponentContent(obj.name);
            string scriptPath = Path.Combine(targetPath, $"{obj.name}DataComponent.cs").Replace("\\", "/");
            ScriptDisplayWindow.ShowWindow(csContent, scriptPath);
            EditorPrefs.SetString("BindDataGeneratorClassPath", scriptPath);
        }

        public static string GenerateWindowDataComponentContent(string windowName)
        {
            StringBuilder sb = new StringBuilder();
            
            sb.AppendLine("/*---------------------------------");
            sb.AppendLine(" 该脚本由UI框架自动生成 负责挂在在对应的窗口上");
            sb.AppendLine(" 对窗口上的控件进行声明和相关事件的绑定 一般情况下不允许修改");
            sb.AppendLine("---------------------------------*/");

            sb.AppendLine("using Framework.UI.Base;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.UI;");
            
            foreach (string nameSpace in UISetting.Instance.UsingNameSpaceArr)
            {
                sb.AppendLine($"using {nameSpace};");
            }
            sb.AppendLine();
            
            // 生成类名
            sb.AppendLine($"\tpublic class {windowName + "Data" + "Component : MonoBehaviour"}");
            sb.AppendLine("\t{");

            // 声明控件
            foreach (var controlData in _controlDataList)
            {
                if (controlData.DataList != null)
                {
                    sb.AppendLine($"\t\tpublic {controlData.ControlType}[] {controlData.ControlName}{controlData.ControlType}Array;\n");
                }
                else
                {
                    sb.AppendLine($"\t\tpublic {controlData.ControlType} {controlData.ControlName}{controlData.ControlType};\n");
                }
            }

            // 初始化组件接口
            sb.AppendLine("\t\tpublic void InitComponent(WindowBase target)");
            sb.AppendLine("\t\t{");

            sb.AppendLine("\t\t     //组件事件绑定");
            sb.AppendLine($"\t\t     {windowName} mWindow = ({windowName})target;");

            // 生成UI事件绑定代码
            foreach (var item in _controlDataList)
            {
                string type = item.ControlType;
                string methodName = item.ControlName;
                string suffix = "";
                
                if (type.Contains("Button"))
                {
                    suffix = "Click";
                    sb.AppendLine($"\t\t     target.AddButtonClickListener({methodName}{type}, mWindow.On{methodName}Button{suffix});");
                }
                else if (type.Contains("InputField"))
                {
                    sb.AppendLine($"\t\t     target.AddInputFieldListener({methodName}{type}, mWindow.On{methodName}InputChange, mWindow.On{methodName}InputEnd);");
                }
                else if (type.Contains("Toggle"))
                {
                    suffix = "Change";
                    sb.AppendLine($"\t\t     target.AddToggleClickListener({methodName}{type}, mWindow.On{methodName}Toggle{suffix});");
                }
                else if (type.Contains("Slider"))
                {
                    suffix = "ValueChange";
                    sb.AppendLine($"\t\t     target.AddSliderListener({methodName}{type}, mWindow.On{methodName}Slider{suffix});");
                }
            }
            sb.AppendLine("\t\t}");
            sb.AppendLine("\t}");

            return sb.ToString();
        }
        
        [UnityEditor.Callbacks.DidReloadScripts]
        public static void AddComponentToWindow()
        {
            string scriptPath = EditorPrefs.GetString("BindDataGeneratorClassPath");
            if (string.IsNullOrEmpty(scriptPath))
            {
                return;
            }

            // 执行后立即清理，防止退出 PlayMode 等非预期时机重复触发
            EditorPrefs.DeleteKey("BindDataGeneratorClassPath");
            PlayerPrefs.DeleteKey("ControlDataList");

            System.Type targetScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath)?.GetClass();
            if (targetScript == null)
            {
                Debug.LogError($"Failed to load script at path: {scriptPath}");
                return;
            }

            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
                return;

            // 校验：仅当选中对象预期包含该 DataComponent 时才挂载
            string expectedClassName = Path.GetFileNameWithoutExtension(scriptPath);
            if (!selectedObject.name.EndsWith("Window") ||
                !expectedClassName.StartsWith(selectedObject.name))
            {
                Debug.LogWarning($"[GenerateBindComponent] 跳过挂载：选中对象 '{selectedObject.name}' 与脚本 '{expectedClassName}' 不匹配");
                return;
            }

            Component compt = selectedObject.GetComponent(targetScript);
            if (compt == null)
            {
                compt = selectedObject.AddComponent(targetScript);
            }

            string datalistJson = PlayerPrefs.GetString("ControlDataList");
            if (string.IsNullOrEmpty(datalistJson))
            {
                Debug.LogWarning("[GenerateBindComponent] ControlDataList 已过期或不存在，跳过字段绑定");
                return;
            }
            List<ControlData> objDataList = JsonConvert.DeserializeObject<List<ControlData>>(datalistJson);
            FieldInfo[] fieldInfoList = targetScript.GetFields();

            foreach (var item in fieldInfoList)
            {
                foreach (var objData in objDataList)
                {
                    if (item.Name == $"{objData.ControlName}{objData.ControlType}" || item.Name == $"{objData.ControlName}{objData.ControlType}Array")
                    {
                        GameObject uiObject = EditorUtility.InstanceIDToObject(objData.InstanceID) as GameObject;
                        if (objData.DataList == null)
                        {
                            if (string.Equals(objData.ControlType, "GameObject"))
                            {
                                item.SetValue(compt, uiObject);
                            }
                            else
                            {
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
                                Type arrayType = item.FieldType;
                                Type elementType = arrayType.GetElementType();
                                Component[] components = uiObject.GetComponentsInChildren(elementType);
                                Array targetArray = Array.CreateInstance(elementType, components.Length);

                                for (int i = 0; i < components.Length; i++)
                                {
                                    if (components[i] != null && elementType.IsAssignableFrom(components[i].GetType()))
                                        targetArray.SetValue(components[i], i);
                                }
                                item.SetValue(compt, targetArray);
                            }
                        }
                    }
                }
            }
        }
    }
}
