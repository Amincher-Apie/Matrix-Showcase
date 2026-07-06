using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public abstract class BaseSO : ScriptableObject
{
    [FoldoutGroup("基础属性"), ] 
    [LabelText("ID")]
    public string id;
    
    [Space(10)]
    [FoldoutGroup("基础属性")]
    [LabelText("名称")]
    public string name;
    
    [FoldoutGroup("渲染属性")] 
    [LabelText("图标")] 
    public Sprite icon;
    
    [Space(10)]
    [FoldoutGroup("基础属性")]
    [LabelText("描述")]
    [TextArea(5,5)]
    public string description;
}
