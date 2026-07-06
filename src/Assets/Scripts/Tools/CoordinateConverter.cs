using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 坐标转换工具类
/// 提供3D世界坐标(WorldPos)、屏幕坐标(ScreenPos)和Canvas坐标(CanvasPos)之间的相互转换
/// </summary>
public static class CoordinateConverter
{
    #region 屏幕坐标 ↔ 3D世界坐标

    /// <summary>
    /// 将屏幕坐标转换为3D世界坐标
    /// </summary>
    /// <param name="screenPos">屏幕坐标(ScreenPos)，像素单位</param>
    /// <returns>转换后的3D世界坐标(WorldPos)</returns>
    public static Vector3 ConvertScreenPosToWorldPos(Vector2 screenPos)
    {
        return Camera.main.ScreenToWorldPoint(screenPos);
    }

    /// <summary>
    /// 将3D世界坐标转换为屏幕坐标
    /// </summary>
    /// <param name="worldPos">3D世界坐标(WorldPos)</param>
    /// <returns>转换后的屏幕坐标(ScreenPos)，像素单位</returns>
    public static Vector3 ConvertWorldPosToScreenPos(Vector3 worldPos)
    {
        return Camera.main.WorldToScreenPoint(worldPos);
    }

    #endregion

    #region 3D世界坐标 ↔ Canvas坐标

    /// <summary>
    /// 将3D世界坐标转换为Canvas坐标
    /// </summary>
    /// <param name="worldPos">3D世界坐标(WorldPos)</param>
    /// <param name="canvasRect">Canvas的RectTransform组件</param>
    /// <param name="uiCamera">UI渲染摄像机</param>
    /// <returns>转换后的Canvas坐标(CanvasPos)</returns>
    public static Vector3 ConvertWorldPosToCanvasPos(Vector3 worldPos, RectTransform canvasRect, Camera uiCamera)
    {
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenPos, uiCamera, out Vector3 canvasPos))
        {
            return canvasPos;
        }
        
        return Vector3.zero;
    }

    /// <summary>
    /// 将Canvas坐标转换为3D世界坐标
    /// </summary>
    /// <param name="canvasPos">Canvas坐标(CanvasPos)</param>
    /// <param name="uiCamera">UI渲染摄像机</param>
    /// <returns>转换后的3D世界坐标(WorldPos)</returns>
    public static Vector3 ConvertCanvasPosToWorldPos(Vector3 canvasPos, Camera uiCamera)
    {
        Vector3 screenPos = uiCamera.WorldToScreenPoint(canvasPos);
        screenPos.z = 0f;
        return Camera.main.ScreenToWorldPoint(screenPos);
    }

    #endregion

    #region 屏幕坐标 ↔ Canvas坐标

    /// <summary>
    /// 将屏幕坐标转换为Canvas坐标
    /// </summary>
    /// <param name="screenPos">屏幕坐标(ScreenPos)，像素单位</param>
    /// <param name="canvasRect">Canvas的RectTransform组件</param>
    /// <param name="uiCamera">UI渲染摄像机</param>
    /// <returns>转换后的Canvas坐标(CanvasPos)</returns>
    public static Vector3 ConvertScreenPosToCanvasPos(Vector2 screenPos, RectTransform canvasRect, Camera uiCamera)
    {
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenPos, uiCamera, out Vector3 canvasPos))
        {
            return canvasPos;
        }
        
        return Vector3.zero;
    }

    /// <summary>
    /// 将Canvas坐标转换为屏幕坐标
    /// </summary>
    /// <param name="canvasPos">Canvas坐标(CanvasPos)</param>
    /// <param name="canvas">UI所在的Canvas</param>
    /// <returns>转换后的屏幕坐标(ScreenPos)，像素单位</returns>
    public static Vector3 ConvertCanvasPosToScreenPos(Vector3 canvasPos, Canvas canvas)
    {
        Vector3 screenPos = canvas.worldCamera.WorldToScreenPoint(canvasPos);
        screenPos.z = 0;
        return screenPos;
    }

    #endregion

    #region Canvas坐标 ↔ 主摄像机3D世界坐标

    /// <summary>
    /// 将Canvas坐标转换为主摄像机的3D世界坐标
    /// </summary>
    /// <param name="canvasPos">Canvas坐标(CanvasPos)</param>
    /// <param name="canvas">UI所在的Canvas</param>
    /// <returns>转换后的3D世界坐标(WorldPos)</returns>
    public static Vector3 ConvertCanvasPosToMainCameraWorldPos(Vector3 canvasPos, Canvas canvas)
    {
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
            canvas.transform as RectTransform, 
            canvas.worldCamera.WorldToScreenPoint(canvasPos), 
            Camera.main, 
            out Vector3 worldPos))
        {
            return worldPos;
        }
        
        return Vector3.zero;
    }

    #endregion
}
