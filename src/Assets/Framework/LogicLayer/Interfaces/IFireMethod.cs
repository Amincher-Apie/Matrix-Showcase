// 文件位置: LogicLayer/Interfaces/IFireMethod.cs

using System.Collections.Generic;

public interface IFireMethod
{
    /// <summary>
    /// 客户端预测执行开火
    /// </summary>
    List<HitResult> ClientExecute(FireContext context, ServerWeaponRuntime weapon);
    
    /// <summary>
    /// 服务器验证开火结果
    /// </summary>
    FireValidationResult ServerValidate(FireContext context, List<HitResult> clientHits, ServerWeaponRuntime weapon);
    
    /// <summary>
    /// 获取开火类型
    /// </summary>
    BulletKind GetBulletKind();
}