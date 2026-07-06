using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "Weapon_Template", menuName = "游戏配置/武器系统/创建物品/武器")]
public class WeaponSO : BaseInventoryItemSO
{

    [Space(10)]
    [FoldoutGroup("数值属性")]
    [LabelText("弹头的物理伤害类型")]
    [Tooltip("物理弹头类型，决定子弹对护盾的穿透行为和额外机制。\n- Solid（固体）：额外 +20% 护甲穿透率。对护盾无特殊效果。\n- Liquid（液体）：对护盾造成 2 倍伤害。无视护盾的 part of 伤害溢散规则。\n- Gas（气体）：与 Solid 行为相同（先打盾再溢血），无额外穿透。")]
    public PhysicalBulletType activeDamageType;

    [Space(10)]
    [FoldoutGroup("数值属性")]
    [LabelText("固体伤害")]
    [ShowIf("activeDamageType", PhysicalBulletType.Solid)]
    [Tooltip("固体物理伤害值（仅 activeDamageType 为 Solid 时显示）。\n参与物理基础伤害 → 受目标 Resistance_Solid 抗性减免 → 受攻击方增伤区 / 暴击区 / 护甲区缩放。\nSolid 弹头额外 +20% 护甲穿透。")]
    public float solidDamage;

    [Space(10)]
    [FoldoutGroup("数值属性")]
    [LabelText("液体伤害")]
    [ShowIf("activeDamageType", PhysicalBulletType.Liquid)]
    [Tooltip("液体物理伤害值（仅 activeDamageType 为 Liquid 时显示）。\n参与物理基础伤害 → 受目标 Resistance_Liquid 抗性减免 → 攻击方增伤区 / 暴击区 / 护甲区缩放。\nLiquid 弹头对护盾造成 2 倍伤害。")]
    public float liquidDamage;

    [Space(10)]
    [FoldoutGroup("数值属性")]
    [LabelText("气体伤害")]
    [ShowIf("activeDamageType", PhysicalBulletType.Gas)]
    [Tooltip("气体物理伤害值（仅 activeDamageType 为 Gas 时显示）。\n参与物理基础伤害 → 受目标 Resistance_Gas 抗性减免 → 攻击方增伤区 / 暴击区 / 护甲区缩放。\nGas 弹头无特殊护盾行为，与 Solid 走相同先盾后血路径。")]
    public float gasDamage;

    [Space(10)]
    [FoldoutGroup("数值属性")]
    [LabelText("火元素伤害")]
    [Tooltip("火元素伤害值。受目标 Resistance_Fire 抗性减免后进入元素伤害池。\n参与四元素加权随机——决定触发异常的层数分配和触发何种元素异常。")]
    public float fireDamage;

    [Space(10)]
    [FoldoutGroup("数值属性")]
    [LabelText("冰元素伤害")]
    [Tooltip("冰元素伤害值。受目标 Resistance_Ice 抗性减免后进入元素伤害池。\n参与四元素加权随机——决定触发异常的层数分配和触发何种元素异常。")]
    public float iceDamage;

    [Space(10)]
    [FoldoutGroup("数值属性")]
    [LabelText("电元素伤害")]
    [Tooltip("电元素伤害值。受目标 Resistance_Electric 抗性减免后进入元素伤害池。\n参与四元素加权随机——决定触发异常的层数分配和触发何种元素异常。")]
    public float lightningDamage;

    [Space(10)]
    [FoldoutGroup("数值属性")]
    [LabelText("毒元素伤害")]
    [Tooltip("毒元素伤害值。受目标 Resistance_Toxic 抗性减免后进入元素伤害池。\n参与四元素加权随机——决定触发异常的层数分配和触发何种元素异常。\n特殊：毒伤害在护盾/生命结算中直接穿透护盾打生命值。")]
    public float poisonDamage;

    [Space(10)]
    [FoldoutGroup("数值属性")]
    [LabelText("暴击倍率")]
    [Tooltip("暴击伤害倍率（>1 的部分为额外暴击增伤）。例如 1.5 表示暴击时造成 1.5 倍伤害。\n在 DamageCalculator 中与武器 CritChance 配合使用。\n运行时映射为 WeaponAttributeType.CritMultiplier，可被 Modifier 修改。")]
    public float criticalDamage;

    [Space(10)]
    [FoldoutGroup("数值属性")]
    [LabelText("暴击率")]
    [Tooltip("暴击几率，取值范围 0~1。0.15 = 15% 暴击率，1.0 = 100% 必暴。\n注意：DamageCalculator 以 Clamp01 截断；ServerWeaponRuntime.HandleCritSpecialRules 中\"暴击率超 100% 溢出转暴伤\"的代码阈值是 100，当前两处语义不一致，若填值 >1 可能出现非预期行为。")]
    public float criticalRate;

    [Space(10)]
    [FoldoutGroup("数值属性")]
    [LabelText("触发异常的几率")]
    [Tooltip("每次命中平均触发的元素异常总层数。触发哪种元素由各元素伤害占比加权随机。\n例：0 不触发，0.5 = 50% 触发 1 层，1.0 = 必定 1 层，1.5 = 必定 1 层 + 50% 再触发 1 层，2.8 = 必定 2 层 + 80% 再触发 1 层。")]
    public float triggerRate;

    [Space(10)]
    [FoldoutGroup("数值属性")]
    [LabelText("射击速度(发/秒)")]
    [Tooltip("每秒发射次数（Rounds Per Second）。\nPlayerCombatModule.UpdateFireCooldown() 据此计算 NextFireTime = Time.time + 1/fireRate。\n运行时映射为 WeaponAttributeType.FireRate，可被 Modifier 修改。")]
    public float fireRate;

    [Space(10)]
    [FoldoutGroup("数值属性")]
    [LabelText("最大射程")]
    [Tooltip("武器有效射程（米）。HitScan 模式的 Raycast 最大距离，也是 Projectile 模式子弹飞行距离上限。\n运行时映射为 WeaponAttributeType.RangeMax。\n范围外目标不会被命中。")]
    public float effectiveRange;

    [Space(10)]
    [FoldoutGroup("数值属性")]
    [LabelText("弹头数量")]
    [Tooltip("每次开火发射的弹头数量（取值 ≥1）。\nHitScan 模式：每发弹头独立做一次 Raycast（模拟霰弹散布）。\nProjectile 模式：每发弹头生成一个独立投射物。\n例：1 = 步枪/手枪，8 = 霰弹枪。")]
    public float bulletCount;

    [Space(10)]
    [FoldoutGroup("数值属性")]
    [LabelText("子弹速度")]
    [Tooltip("子弹飞行速度（m/s），仅 Projectile 模式生效。HitScan 模式忽略此值。\n运行时映射为 WeaponAttributeType.BulletSpeed，可被 Modifier 修改。")]
    public float bulletSpeed;

    [Space(10)]
    [FoldoutGroup("数值属性")]
    [LabelText("弹匣容量")]
    [Tooltip("弹匣最大弹药数。≥1 时支持手动换弹，弹药耗尽时自动触发 Reload。\n换弹后将此值回填至当前弹药。\n运行时映射为 WeaponAttributeType.MagazineSize，可被 Modifier 修改。")]
    public float magazineSize;

    [Space(10)]
    [FoldoutGroup("数值属性")]
    [LabelText("换弹时间")]
    [Tooltip("换弹所需时间（秒）。PlayerCombatModule.Reload() 用此值计算 reloadEndTime。\n应匹配武器换弹动画 Clip 的实际时长（MegaBlast 等）。\n运行时映射为 WeaponAttributeType.ReloadTime，可被 Modifier 修改（加速/减速换弹）。\n注意：动画 Exit Time 与逻辑换弹时长独立——Modifier 修改后可能造成逻辑/表现不同步（已知问题）。")]
    public float reloadTime;

    [Space(10)]
    [FoldoutGroup("数值属性")]
    [LabelText("蓄力时间")]
    [Tooltip("蓄力至满级所需时间（秒）。0 或不填表示不支持蓄力。\n按住开火键时从 0 开始累加 chargeLevel=(按住时长/chargeTime)，达到 0.99 自动释放。\n运行时映射为 WeaponAttributeType.ChargeTime，可被 Modifier 修改。\n仅对 fireMode=Charge 的武器生效。")]
    public float chargeTime;

    [Space(10)]
    [FoldoutGroup("基础属性")]
    [LabelText("子弹类型")]
    [Tooltip("开火命中判定方式。\n- HitScan：瞬时命中，客户端预测 Raycast + 服务端验证（位置容差 1m）。\n- Projectile：飞行弹道，客户端仅预测发射，命中由服务端弹道计算。\n决定 IFireMethod 的选择（HitScanFireMethod / ProjectileFireMethod）。")]
    public BulletKind bulletKind;

    [Space(10)]
    [FoldoutGroup("基础属性")]
    [LabelText("开火方式")]
    [Tooltip("开火模式。\n- Semi：单击一发（扣扳机一次 = 一次 TryFire）。\n- Auto：按住连发，射速由 fireRate 控制。\n- Charge：按住蓄力（chargeTime 秒满级），松手释放。\n输入链路：ThirdPersonPlayerController 根据此值分派 OnFireInput / Update / OnFireCanceled。")]
    public FireMode fireMode;

    [Space(10)]
    [FoldoutGroup("渲染属性")]
    [LabelText("武器子弹")]
    [Tooltip("弹道投射物的 Prefab（仅 Projectile 模式使用）。HitScan 模式不需要此字段。\n实例化后由 Projectile 飞行逻辑控制移动和命中检测。")]
    public GameObject bullet;

    public override EnumItemType itemType => EnumItemType.Weapon;
}
