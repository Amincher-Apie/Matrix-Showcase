using UnityEngine;

public struct AIMoveIntent
{
    public bool hasIntent;
    public Vector3 direction;   // 水平、已归一化
    public float speed;         // 单位/秒
}