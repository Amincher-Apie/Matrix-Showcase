using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Plastic.Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Framework.UI.Editor
{
    public class GenerateWindowComponentTool : UnityEditor.Editor
    {
        static Dictionary<string, string> methodDic = new Dictionary<string, string>();

        [MenuItem("GameObject/生成Window脚本", false, 0)]
        static void GenerateWindowComponent()
        {
            GameObject go = Selection.activeGameObject;
            if (go == null)
            {
                Debug.LogError("请选中需要生成代码文件的窗口");
                return;
            }
            
            // 获取配置的生成路径
            string targetPath = UISetting.Instance.WindowGeneratorPath;
            if (string.IsNullOrEmpty(targetPath))
            {
                Debug.LogError("错误：Window脚本生成路径未配置，请检查UISetting中的WindowGeneratorPath");
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
                    Debug.Log($"成功创建Window脚本文件夹：{targetPath}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"创建文件夹失败：{ex.Message}，路径：{targetPath}");
                    return;
                }
            }
            else
            {
                Debug.Log($"Window脚本文件夹已存在，路径：{targetPath}");
            }

            string csContent = GenerateWindowComponentContent(go.name);
            string scriptPath = Path.Combine(targetPath, $"{go.name}.cs").Replace("\\", "/");
            ScriptDisplayWindow.ShowWindow(csContent, scriptPath, methodDic);
        }

        public static string GenerateWindowComponentContent(string windowName)
        {
            string dataListJson = PlayerPrefs.GetString("ControlDataList");
            
            if (string.IsNullOrEmpty(dataListJson))
            {
                Debug.LogError("控件数据为空，请先执行「生成窗口绑定组件数据脚本」");
                return string.Empty;
            }

            List<ControlData> controlDataList = JsonConvert.DeserializeObject<List<ControlData>>(dataListJson);
            if (controlDataList == null)
            {
                Debug.LogError("控件数据反序列化失败，格式错误或数据损坏");
                return string.Empty;
            }
            
            methodDic.Clear();
            StringBuilder sb = new StringBuilder();
            
            sb.AppendLine("/*---------------------------------");
            sb.AppendLine("该脚本为UI框架自动生成 初始化的时候以获得到对应窗口数据脚本的引用");
            sb.AppendLine("本脚本主要负责书写控件交互逻辑 以及UI更新相关方法");
            sb.AppendLine("---------------------------------*/");
            sb.AppendLine();
            sb.AppendLine("using Framework.UI.Base;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.UI;");
            sb.AppendLine();
            
            sb.AppendLine($"\tpublic class {windowName} : WindowBase");
            sb.AppendLine("\t{");
            sb.AppendLine("\t");
            
            sb.AppendLine($"\t\t public {windowName}DataComponent dataComponent;");
            
            // 生命周期函数
            sb.AppendLine("\t");
            sb.AppendLine($"\t\t #region 生命周期函数");
            sb.AppendLine("\t");
            sb.AppendLine($"\t\t //调用机制与Mono Awake一致");
            sb.AppendLine("\t\t public override void OnAwake()");
            sb.AppendLine("\t\t {");
            sb.AppendLine("\t\t\t base.OnAwake();");
            sb.AppendLine($"\t\t\t dataComponent = GameObject.GetComponent<{windowName}DataComponent>();");
            sb.AppendLine($"\t\t\t dataComponent.InitComponent(this);");
            sb.AppendLine("\t\t }");
            sb.AppendLine("\t");
            
            sb.AppendLine($"\t\t //物体显示时执行");
            sb.AppendLine("\t\t public override void OnShow()");
            sb.AppendLine("\t\t {");
            sb.AppendLine("\t\t\t base.OnShow();");
            sb.AppendLine("\t\t }");
            sb.AppendLine("\t");
            
            sb.AppendLine($"\t\t //物体隐藏时执行");
            sb.AppendLine("\t\t public override void OnHide()");
            sb.AppendLine("\t\t {");
            sb.AppendLine("\t\t\t base.OnHide();");
            sb.AppendLine("\t\t }");
            sb.AppendLine("\t");
            
            sb.AppendLine($"\t\t //物体销毁时执行");
            sb.AppendLine("\t\t public override void OnDestroy()");
            sb.AppendLine("\t\t {");
            sb.AppendLine("\t\t\t base.OnDestroy();");
            sb.AppendLine("\t\t }");
            sb.AppendLine("\t");
            sb.AppendLine($"\t\t #endregion");
            
            sb.AppendLine("\t");
            
            // API Function 
            sb.AppendLine($"\t\t #region API Function");
            sb.AppendLine("\t");
            sb.AppendLine($"\t\t    ");
            sb.AppendLine("\t");
            sb.AppendLine($"\t\t #endregion");
            sb.AppendLine("\t");
            
            // UI控件事件
            sb.AppendLine($"\t\t #region UI组件事件");
            sb.AppendLine("\t");
            foreach (var control in controlDataList)
            {
                string methodName = "On" + control.ControlName;
                string controlType = control.ControlType;
                string suffix = "";
                
                if (controlType.Contains("Button"))
                {
                    suffix = "ButtonClick";
                    CreateMethod(sb, ref methodDic, methodName + suffix);
                }
                else if (controlType.Contains("InputField"))
                {
                    suffix = "InputChange";
                    CreateMethod(sb, ref methodDic, methodName + suffix, "string text");
                    suffix = "InputEnd";
                    CreateMethod(sb, ref methodDic, methodName + suffix, "string text");
                }
                else if (controlType.Contains("Toggle"))
                {
                    suffix = "ToggleChange";
                    CreateMethod(sb, ref methodDic, methodName + suffix, "bool state,Toggle toggle");
                }
                else if (controlType.Contains("Slider"))
                {
                    suffix = "SliderValueChange";
                    CreateMethod(sb, ref methodDic, methodName + suffix, "float value");
                }
            }
            sb.AppendLine("\t");
            sb.AppendLine($"\t\t #endregion");
            sb.AppendLine("\t}");
            
            return sb.ToString();
        }

        public static void CreateMethod(StringBuilder sb, ref Dictionary<string, string> methodDic, string methodName,
            string param = "")
        {
            sb.AppendLine($"\t\t public void {methodName}({param})");
            sb.AppendLine("\t\t {");
            sb.AppendLine("\t\t");
            if (methodName == "OnCloseButtonClick")
            {
                sb.AppendLine("\t\t\tHideWindow();");
            }
            sb.AppendLine("\t\t }");
            sb.AppendLine("\t");

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"\t\t public void {methodName}({param})");
            builder.AppendLine("\t\t {");
            builder.AppendLine("\t\t");
            builder.AppendLine("\t\t }");
            methodDic.Add(methodName, builder.ToString());
        }
    }
}
