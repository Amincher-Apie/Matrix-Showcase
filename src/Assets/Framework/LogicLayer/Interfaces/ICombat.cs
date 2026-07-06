using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICombat : IModule
{
    bool TryFire(FireContext ctx); // 单发触发（自动武器由外部循环调用）
    void StartCharge();                                   // 蓄力开始
    bool ReleaseCharge(FireContext ctx);
    bool CanFire();
    bool Reload();                                        // 开始装填
    int  CurrentAmmo { get; }
    ServerWeaponRuntime CurrentConfig { get; }
}

