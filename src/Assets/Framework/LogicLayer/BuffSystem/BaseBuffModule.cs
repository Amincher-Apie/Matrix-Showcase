using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseBuffModule : ScriptableObject
{
    public abstract void Apply(BuffInfo buffInfo, DamageInfo damageInfo = default);
}