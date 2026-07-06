using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "_DamageSpriteInit", menuName = "DamageTextSystem/DamageSpriteInit", order = 1)]
public class DamageSprite : ScriptableObject
{
    public ElementType elementType;
    public Color color;
    public Sprite sprite;
}
