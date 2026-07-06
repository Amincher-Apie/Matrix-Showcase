using System.Collections.Generic;
using System.Linq; // 新增Linq引用，用于Contains方法
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace Framework.UI.Editor
{
    public class AnalysisControlTool
    {
        /// <summary>
        /// 解析所有窗口控件 为窗口控件自动绑定提供数据支持 
        /// </summary>
        public static void AnalysisWindowControl(ref List<ControlData> controlDataList, Transform transform,
            string windowName)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                GameObject go = transform.GetChild(i).gameObject;
                string name = go.name;
                
                if(name.Contains("#"))
                    continue;

                //[控件类型]控件名 格式匹配
                if (name.Contains("[") && name.Contains("]"))
                {
                    int index = name.IndexOf("]") + 1;
                    string controlName = name.Substring(index, name.Length - index);
                    string controlType = name.Substring(1, index - 2);
                    
                    // 支持的控件类型（使用Linq的Contains方法）
                    var supportedTypes = new[] { "Button", "Text", "Image", "Toggle", "Slider", "InputField" };
                    if (supportedTypes.Contains(controlType)) // 修复：使用Linq的Contains
                    {
                        var controlData = new ControlData { 
                            ControlName = controlName, 
                            ControlType = controlType, 
                            InstanceID = go.GetInstanceID() 
                        };
                        controlDataList.Add(controlData);

                        // 处理列表元素绑定
                        if (controlType.Contains(","))
                        {
                            controlData.DataList = new List<ControlData>();
                            controlData.ControlType = controlData.ControlType.Replace(",", "");
                            for (int j = 0; j < go.transform.childCount; j++) 
                            {
                                GameObject listObjItem = go.transform.GetChild(j).gameObject;
                                controlData.DataList.Add(new ControlData { 
                                    ControlName = listObjItem.name.Replace("#",""),  
                                    InstanceID = listObjItem.GetInstanceID()
                                });
                            }
                        }
                    }
                }
                
                AnalysisWindowControl(ref controlDataList, transform.GetChild(i), windowName);
            }
        }

        public static bool IsPrefabInstance(GameObject gameObject)
        {
            var type = PrefabUtility.GetPrefabAssetType(gameObject);
            var status = PrefabUtility.GetPrefabInstanceStatus(gameObject);
            return status != PrefabInstanceStatus.NotAPrefab && type != PrefabAssetType.NotAPrefab;
        }
    }
}
