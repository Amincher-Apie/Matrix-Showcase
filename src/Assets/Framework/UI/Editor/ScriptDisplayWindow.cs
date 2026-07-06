using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Framework.UI.Editor;

public class ScriptDisplayWindow : EditorWindow
{
    private string scriptContent;
    private string filePath;
    private string mFileName;
    private Vector2 scroll = new Vector2();
    
    public static void ShowWindow(string content, string filePath, Dictionary<string, string> _insertDic = null,List<ControlData> fieldList=null)
    {
        ScriptDisplayWindow window = (ScriptDisplayWindow)GetWindowWithRect(typeof(ScriptDisplayWindow), new Rect(100, 50, 800, 700), true, "Window生成界面");
        window.scriptContent = content;
        window.filePath = filePath;
        window.mFileName = Path.GetFileName(filePath);
        
        string originScript = string.Empty;
        bool isInsterSuccess = false;
        
        if (File.Exists(window.filePath) && (_insertDic!=null || fieldList!=null))
        {
            originScript = File.ReadAllText(window.filePath);
            
            if (string.IsNullOrEmpty(originScript) == false)
            {
                if (fieldList!=null)
                {
                    //插入字段
                    foreach (var item in fieldList)
                    {
                        if (!originScript.Contains($"{item.ControlName}{item.ControlType}"))
                        {
                            string insterArrayType = item.DataList!=null?"[]":"";
                            string insterArray = item.DataList!=null?"Array":"";
                            originScript = window.scriptContent = originScript.Insert(window.GetInsertFieldIndex(originScript)
                                , $"public { item.ControlType }{insterArrayType} {item.ControlName}{item.ControlType}{insterArray};\n\n\t\t");
                            isInsterSuccess = true;
                        }
                    }
                }
                if (_insertDic != null)
                {
                    //插入方法
                    foreach (var item in _insertDic)
                    {
                        if (!originScript.Contains(item.Key))
                        {
                            int insterIndex = window.GetInsertMethodIndex(originScript);
                            originScript = window.scriptContent = originScript.Insert(insterIndex,"\n"+ item.Value+"\n\t\t");
                            isInsterSuccess = true;
                        }
                    }
                }

                if (fieldList!=null)
                {
                    //插入事件（移除ScrollView）
                    foreach (var item in fieldList)
                    {  
                        string field = $"{item.ControlName}{item.ControlType}";
                        string type = item.ControlType;
                        string methodName = "On" + item.ControlName;
                        string suffix = "";
                        StringBuilder sb=new StringBuilder();
                        
                        if (type.Contains("Button"))
                        {
                            suffix = "ButtonClick";
                            sb.AppendLine($"\t\t\t{field}.onClick.AddListener({methodName}{suffix});");
                        }
                        else if (type.Contains("InputField"))
                        {
                            suffix = "InputChange";
                            sb.AppendLine($"\t\t\t{field}.onValueChanged.AddListener({methodName}{suffix});");
                            suffix = "InputEnd";
                            sb.AppendLine($"\t\t\t{field}.onEndEdit.AddListener({methodName}{suffix});");
                        }
                        else if (type.Contains("Toggle"))
                        {
                            suffix = "ToggleChange";
                            sb.AppendLine($"\t\t\t{field}.onValueChanged.AddListener((state) => {methodName}{suffix}(state, {field}));");
                        }
                        else if (type.Contains("Slider"))
                        {
                            suffix = "SliderValueChange";
                            sb.AppendLine($"\t\t\t{field}.onValueChanged.AddListener({methodName}{suffix});");
                        }
                        else
                        {
                            continue;
                        }
                        
                        if (!originScript.Contains($"AddListener({methodName}{suffix})"))
                        {
                            sb.Insert(0,"//按钮事件自动注册绑定\n");
                            originScript = window.scriptContent = originScript.Replace("//按钮事件自动注册绑定", $"{sb.ToString()}");
                            isInsterSuccess = true;
                        }
                    }
                }
            }
            
            if (isInsterSuccess == false)
            {
                window.scriptContent = originScript;
            }
        }

        originScript = null;
        _insertDic = null;
        window.Show();
    }
    
    public void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll,GUILayout.Height(600),GUILayout.Width(800));
        EditorGUILayout.TextArea(scriptContent);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.TextArea("脚本生成路径："+filePath);
        if (GUILayout.Button("选择路径",GUILayout.Width(80)))
        {
            filePath= EditorUtility.OpenFolderPanel("脚本生成路径", filePath, "ZMUI")+"/"+mFileName;
            EditorPrefs.SetString("GeneratorClassPath", filePath);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("生成脚本",GUILayout.Height(30)))
        {
            ButtonClick();
        }
        EditorGUILayout.EndHorizontal();
    }
    
    public void ButtonClick()
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
        
        StreamWriter writer = File.CreateText(filePath);
        writer.Write(scriptContent);
        writer.Close();
        writer.Dispose();
        scriptContent = string.Empty;
        Debug.Log("Create Code finish! Cs path:" + filePath);
        AssetDatabase.Refresh();
        if (EditorUtility.DisplayDialog("自动化工具", "生成脚本成功！", "确定"))
        {
            Close();
        } 
    }
    
    public int GetInsertMethodIndex(string content)
    {
        Regex regex = new Regex("UI组件事件");
        Match match = regex.Match(content);
        return match.Index+6;
    }
    
    public int GetInsertFieldIndex(string content)
    {
        Regex regex = new Regex("自定义字段");
        Match match = regex.Match(content);
        Regex regex1 = new Regex("public");
        MatchCollection matchColltion = regex1.Matches(content);

        for (int i = 0; i < matchColltion.Count; i++)
        {
            if (matchColltion[i].Index > match.Index)
            {
                return matchColltion[i].Index;
            }
        }
        return -1;
    }
}
