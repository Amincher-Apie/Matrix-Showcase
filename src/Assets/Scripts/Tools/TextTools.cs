using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextTools 
{
    /// <summary>
    /// 添加富文本颜色
    /// </summary>
    /// <param name="str"></param>
    /// <param name="color"></param>
    /// <returns></returns>
    public static string TextAddColor(string str, Color color) {
        //color 转十六进制
        string hex = ColorUtility.ToHtmlStringRGBA(color);
        return $"<color=#{hex}>{str}</color>";
    }

    public static string TextAddColor(string str, string color) {
        return $"<color={color}>{str}</color>";
    }
}
