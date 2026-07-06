// LogicLayer/DamageCenter/DamageCalculator.cs

using System.Collections.Generic;
using UnityEngine;

namespace Framework.LogicLayer.DamageCenter
{
    /// <summary>
    /// 伤害计算器
    /// </summary>
    public static class DamageCalculator
    {
        #region 常量配置
        private const float ARMOR_CONSTANT = 100f;           // 护甲常数（你原来的值）
        private const float BASE_CRITICAL_MULTIPLIER = 1.5f; // 没有配置时的默认暴击倍数
        private const float SOLID_ARMOR_PENETRATION_BONUS = 0.2f;
        #endregion

        #region 公开方法
        
        /// <summary>
        /// 【武器直接攻击专用】根据攻击方 / 受击方，生成一次完整的 DamageInfo
        /// 【入口】：ServerCombatModule.ProcessValidatedHits
        /// </summary>
        public static DamageInfo CalculateDamage(ulong sourceActorId, ulong targetActorId)
        {
            var damageInfo = new DamageInfo
            {
                sourceActorId = sourceActorId,
                targetActorId = targetActorId,
                isCritical = false,
                isSkill = false,
                iceDamage = 0,
                fireDamage = 0,
                poisonDamage = 0,
                electricDamage = 0,
                iceTriggerLayer = 0,
                fireTriggerLayer = 0,
                poisonTriggerLayer = 0,
                electricTriggerLayer = 0
            };

            // 1. 通过 NetworkObjectId 拿到双方的 NetworkProxy 和 Attribute / Weapon 模块
            if (!NetworkObjectManager.Instance.TryGetNetworkProxy<NetworkProxyBase>(sourceActorId, out var sourceProxy))
            {
                Debug.LogError($"[DamageCalculator] 找不到攻击方 NetworkProxy, sourceActorId={sourceActorId}");
                return damageInfo;
            }

            if (!NetworkObjectManager.Instance.TryGetNetworkProxy<NetworkProxyBase>(targetActorId, out var targetProxy))
            {
                Debug.LogError($"[DamageCalculator] 找不到受击方 NetworkProxy, targetActorId={targetActorId}");
                return damageInfo;
            }

            var sourceAttr = sourceProxy.GetServerAttributeModule<ServerAttributeModule>();
            var targetAttr = targetProxy.GetServerAttributeModule<ServerAttributeModule>();
            var weaponRuntime = sourceProxy.GetServerWeaponRuntime<ServerWeaponRuntime>();

            if (!sourceAttr || !targetAttr || !weaponRuntime)
            {
                Debug.LogError("[DamageCalculator] 模块缺失: " +
                               $"sourceAttr={sourceAttr}, targetAttr={targetAttr}, weaponRuntime={weaponRuntime}");
                return damageInfo;
            }

            // 2. 读取武器基础伤害（包含物理三种 + 四元素）
            var profile = weaponRuntime.GetModifiedDamageProfile();
            float solid = Mathf.Max(0, profile.solid);
            float liquid = Mathf.Max(0, profile.liquid);
            float gas = Mathf.Max(0, profile.gas);

            float ice = Mathf.Max(0, profile.ice);
            float fire = Mathf.Max(0, profile.fire);
            float toxic = Mathf.Max(0, profile.toxic);
            float electric = Mathf.Max(0, profile.electric);

            // 3. 推断物理子弹类型（如果你有 WeaponSO 上的枚举，可替换为直接读取）
            damageInfo.physicalBulletType = weaponRuntime.GetPhysicalBulletType();

            // 4. 目标的抗性（-1 ~ 1）
            float rSolid = targetAttr.GetAttribute(AttributeType.Resistance_Solid);
            float rLiquid = targetAttr.GetAttribute(AttributeType.Resistance_Liquid);
            float rGas = targetAttr.GetAttribute(AttributeType.Resistance_Gas);

            float rIce = targetAttr.GetAttribute(AttributeType.Resistance_Ice);
            float rFire = targetAttr.GetAttribute(AttributeType.Resistance_Fire);
            float rToxic = targetAttr.GetAttribute(AttributeType.Resistance_Toxic);
            float rElectric = targetAttr.GetAttribute(AttributeType.Resistance_Electric);

            // 5. 元素抗性区：按照你给的公式对四元素分别做 (1 - 抗性)
            float solidAfterRes = solid * (1f - rSolid);
            float liquidAfterRes = liquid * (1f - rLiquid);
            float gasAfterRes = gas * (1f - rGas);
            float iceAfterRes = ice * (1f - rIce);
            float fireAfterRes = fire * (1f - rFire);
            float toxicAfterRes = toxic * (1f - rToxic);
            float elecAfterRes = electric * (1f - rElectric);

            float elementalAfterRes = iceAfterRes + fireAfterRes + toxicAfterRes + elecAfterRes;

            // 物理基础伤害（这里把 solid/liquid/gas 都归为「物理基础伤害」的一部分）
            float physicalBaseDamage = solidAfterRes + liquidAfterRes + gasAfterRes;

            // 6. 攻击者增伤区（DamageOutPutRate），以 1 + Rate 的形式处理
            float damageOutputRate = sourceAttr.GetAttribute(AttributeType.DamageOutPutRate);
            float dmgOutputFactor = 1f + Mathf.Max(0f, damageOutputRate);

            // 7. 暴击区：由武器提供暴击率/暴击倍数
            float critChanceRaw = weaponRuntime.GetAttribute(WeaponAttributeType.CritChance);
            float critMulti = Mathf.Max(1f, weaponRuntime.GetAttribute(WeaponAttributeType.CritMultiplier));
            float critChance = Mathf.Clamp01(critChanceRaw); // 假定是 0~1，如果你是 0~100 请记得 /100

            bool isCrit = UnityEngine.Random.value < critChance;
            float critFactor = isCrit ? critMulti : 1f;

            damageInfo.isCritical = isCrit;

            // 8. 护甲区
            float armor = Mathf.Max(0f, targetAttr.GetAttribute(AttributeType.Armor));
            float armorPenRate = Mathf.Clamp01(sourceAttr.GetAttribute(AttributeType.ArmorPenetrationRate));

            // 固体子弹额外+0.2 穿透
            if (damageInfo.physicalBulletType == PhysicalBulletType.Solid)
            {
                armorPenRate = Mathf.Clamp01(armorPenRate + SOLID_ARMOR_PENETRATION_BONUS);
            }

            float effectiveArmor = armor * (1f - armorPenRate);
            float armorFactor = ARMOR_CONSTANT / (ARMOR_CONSTANT + Mathf.Max(0f, effectiveArmor));

            // 9. 特殊乘区（暂时为 1，将来可以引入 buff、派系克制等）
            float specialFactor = 1f;

            // 10. 先把物理基础和元素基础（已经过元素抗性区）堆在一起
            float baseBeforeGlobal =
                physicalBaseDamage    // 物理基础
              + elementalAfterRes;    // 元素基础 * 抗性区

            float globalFactor = dmgOutputFactor * critFactor * armorFactor * specialFactor;

            // 11. 分别对物理与四元素乘上 globalFactor，方便后续做「分立伤害」
            float finalPhysicalDamage = physicalBaseDamage * globalFactor;

            float finalIceDamage = iceAfterRes * globalFactor;
            float finalFireDamage = fireAfterRes * globalFactor;
            float finalToxicDamage = toxicAfterRes * globalFactor;
            float finalElecDamage = elecAfterRes * globalFactor;

            float totalDamage =
                finalPhysicalDamage +
                finalIceDamage + finalFireDamage + finalToxicDamage + finalElecDamage;

            // 12. 元素异常触发层数（ProcChance > 1 时，多段触发）
            float procChanceRaw = weaponRuntime.GetAttribute(WeaponAttributeType.ProcChance);
            // 假定 ProcChance 是类似 0.5=50%，1.5=150%（也可以支持直接填 150，届时请 /100）
            float procChance = Mathf.Max(0f, procChanceRaw);

            ComputeElementProcLayers(
                procChance,
                finalIceDamage,
                finalFireDamage,
                finalToxicDamage,
                finalElecDamage,
                out int iceLayers,
                out int fireLayers,
                out int toxicLayers,
                out int electLayers
            );

            // 13. 回填到 DamageInfo
            damageInfo.amount = totalDamage;

            damageInfo.iceDamage = finalIceDamage;
            damageInfo.fireDamage = finalFireDamage;
            damageInfo.poisonDamage = finalToxicDamage;
            damageInfo.electricDamage = finalElecDamage;

            damageInfo.iceTriggerLayer = iceLayers;
            damageInfo.fireTriggerLayer = fireLayers;
            damageInfo.poisonTriggerLayer = toxicLayers;
            damageInfo.electricTriggerLayer = electLayers;

            return damageInfo;
        }
        
        /// <summary>
        /// 【技能】 计算一次完整的 DamageInfo
        /// 基于“任意伤害面板（技能/道具/环境）”计算一次完整的 DamageInfo。
        /// 这里不要求攻击方一定有武器模块。
        /// </summary>
        public static DamageInfo CalculateDamageFromProfile(
            ulong sourceActorId,
            ulong targetActorId,
            DamageProfile baseProfile,               // 可以来自武器，也可以是技能自己算出来的
            PhysicalBulletType bulletType,          // 固体 / 液体 / 气体：影响护甲 + 护盾规则
            bool enableCrit        = false,         // 技能默认不暴击；需要的话可以手动开
            float extraCritChance  = 0f,            // 技能自己的额外暴击率（0~1）
            float extraCritMulti   = 0f,            // 技能自己的额外暴击倍数（+0.5 就是多 50%）
            float procChance       = 0f             // 技能自己的元素异常触发率
        )
        {
            var damageInfo = new DamageInfo(bulletType, sourceActorId, targetActorId)
            {
                isSkill = true
            };

            // 1. 拿到双方 Attribute（不再强制拿 WeaponRuntime）
            if (!NetworkObjectManager.Instance.TryGetNetworkProxy<NetworkProxyBase>(sourceActorId, out var sourceProxy))
            {
                Debug.LogError($"[DamageCalculator] 找不到攻击方 NetworkProxy, sourceActorId={sourceActorId}");
                return damageInfo;
            }

            if (!NetworkObjectManager.Instance.TryGetNetworkProxy<NetworkProxyBase>(targetActorId, out var targetProxy))
            {
                Debug.LogError($"[DamageCalculator] 找不到受击方 NetworkProxy, targetActorId={targetActorId}");
                return damageInfo;
            }

            var sourceAttr = sourceProxy.GetServerAttributeModule<ServerAttributeModule>();
            var targetAttr = targetProxy.GetServerAttributeModule<ServerAttributeModule>();

            if (!sourceAttr || !targetAttr)
            {
                Debug.LogError("[DamageCalculator] 模块缺失: " +
                               $"sourceAttr={sourceAttr}, targetAttr={targetAttr}");
                return damageInfo;
            }

            // 2. 使用传进来的 baseProfile 作为“原始面板”
            float solid    = Mathf.Max(0, baseProfile.solid);
            float liquid   = Mathf.Max(0, baseProfile.liquid);
            float gas      = Mathf.Max(0, baseProfile.gas);
            float ice      = Mathf.Max(0, baseProfile.ice);
            float fire     = Mathf.Max(0, baseProfile.fire);
            float toxic    = Mathf.Max(0, baseProfile.toxic);
            float electric = Mathf.Max(0, baseProfile.electric);

            // 3. 抗性区（完全照抄你原来的逻辑）
            float rSolid   = targetAttr.GetAttribute(AttributeType.Resistance_Solid);
            float rLiquid  = targetAttr.GetAttribute(AttributeType.Resistance_Liquid);
            float rGas     = targetAttr.GetAttribute(AttributeType.Resistance_Gas);
            float rIce     = targetAttr.GetAttribute(AttributeType.Resistance_Ice);
            float rFire    = targetAttr.GetAttribute(AttributeType.Resistance_Fire);
            float rToxic   = targetAttr.GetAttribute(AttributeType.Resistance_Toxic);
            float rElectric= targetAttr.GetAttribute(AttributeType.Resistance_Electric);

            float solidAfterRes   = solid   * (1f - rSolid);
            float liquidAfterRes  = liquid  * (1f - rLiquid);
            float gasAfterRes     = gas     * (1f - rGas);
            float iceAfterRes     = ice     * (1f - rIce);
            float fireAfterRes    = fire    * (1f - rFire);
            float toxicAfterRes   = toxic   * (1f - rToxic);
            float elecAfterRes    = electric* (1f - rElectric);

            float elementalAfterRes =
                iceAfterRes + fireAfterRes + toxicAfterRes + elecAfterRes;

            // 4. 物理基础（固/液/气 抗性后 + 你愿不愿意把 gas 算进来）
            float physicalBaseDamage = solidAfterRes + liquidAfterRes + gasAfterRes;

            // 5. 输出率区（攻击方属性）
            float dmgOutputFactor = Mathf.Max(0f,
                1f + sourceAttr.GetAttribute(AttributeType.DamageOutPutRate));

            // 6. 暴击区（这里不依赖武器，可以全部由技能 + 属性决定）
            bool isCrit = false;
            float critFactor = 1f;

            if (enableCrit)
            {
                float finalCritChance = Mathf.Clamp01(extraCritChance);
                float finalCritMulti  = BASE_CRITICAL_MULTIPLIER + extraCritMulti;
                
                if (Random.value < finalCritChance)
                {
                    isCrit = true;
                    critFactor = finalCritMulti;
                }
            }

            damageInfo.isCritical = isCrit;

            // 7. 护甲区（完全沿用你原来的）
            float armor        = Mathf.Max(0f, targetAttr.GetAttribute(AttributeType.Armor));
            float armorPenRate = Mathf.Clamp01(sourceAttr.GetAttribute(AttributeType.ArmorPenetrationRate));

            if (bulletType == PhysicalBulletType.Solid)
            {
                armorPenRate = Mathf.Clamp01(armorPenRate + SOLID_ARMOR_PENETRATION_BONUS);
            }

            float effectiveArmor = armor * (1f - armorPenRate);
            float armorFactor = ARMOR_CONSTANT / (ARMOR_CONSTANT + Mathf.Max(0f, effectiveArmor));

            // 8. 特殊乘区（先留 1，将来给 Buff / 派系克制）
            float specialFactor = 1f;

            float globalFactor = dmgOutputFactor * critFactor * armorFactor * specialFactor;

            // 9. 把 globalFactor 乘回各分量
            float finalPhysicalDamage = physicalBaseDamage * globalFactor;
            float finalIceDamage      = iceAfterRes * globalFactor;
            float finalFireDamage     = fireAfterRes * globalFactor;
            float finalToxicDamage    = toxicAfterRes * globalFactor;
            float finalElecDamage     = elecAfterRes * globalFactor;

            float totalDamage =
                finalPhysicalDamage +
                finalIceDamage + finalFireDamage + finalToxicDamage + finalElecDamage;

            // 10. 技能元素异常层数计算。默认 procChance = 0，保持旧技能不触发异常。
            ComputeElementProcLayers(
                Mathf.Max(0f, procChance),
                finalIceDamage,
                finalFireDamage,
                finalToxicDamage,
                finalElecDamage,
                out int iceLayers,
                out int fireLayers,
                out int toxicLayers,
                out int electLayers
            );

            damageInfo.amount          = totalDamage;
            damageInfo.iceDamage       = finalIceDamage;
            damageInfo.fireDamage      = finalFireDamage;
            damageInfo.poisonDamage    = finalToxicDamage;
            damageInfo.electricDamage  = finalElecDamage;
            damageInfo.iceTriggerLayer = iceLayers;
            damageInfo.fireTriggerLayer= fireLayers;
            damageInfo.poisonTriggerLayer = toxicLayers;
            damageInfo.electricTriggerLayer= electLayers;

            return damageInfo;
        }
        
        /// <summary>
        /// 【配合CalculateDamage】
        /// 由 AttributeModule 调用，结合当前血量/护盾，计算最终 DamageResult
        /// 这里实现「毒元素穿盾」&「液体子弹对护盾造成双倍伤害」的逻辑。
        /// </summary>
        public static DamageResult ApplyDamage(DamageInfo info, float currentHealth, float currentShield)
        {
            var result = new DamageResult
            {
                isCritical = info.isCritical,
                iceDamage = info.iceDamage,
                fireDamage = info.fireDamage,
                poisonDamage = info.poisonDamage,
                electricDamage = info.electricDamage,
                iceTriggerLayer = info.iceTriggerLayer,
                fireTriggerLayer = info.fireTriggerLayer,
                poisonTriggerLayer = info.poisonTriggerLayer,
                electricTriggerLayer = info.electricTriggerLayer,
                shieldDamage = 0f,
                healthDamage = 0f,
                totalDamage = 0f,
                targetDied = false
            };

            // 1. 毒元素先直接对生命造成伤害（穿盾）
            float health = currentHealth;
            float shield = currentShield;

            float poisonHpDamage = Mathf.Max(0f, info.poisonDamage);
            float hpBefore = health;
            health = Mathf.Max(0f, health - poisonHpDamage);
            float actualPoisonHpDamage = hpBefore - health;

            float baseNonPoisonDamage = Mathf.Max(0f, info.amount - info.poisonDamage);

            float shieldDamage = 0f;
            float hpDamageFromNonPoison = 0f;

            // 2. 根据物理子弹类型，处理剩余的非毒部分
            switch (info.physicalBulletType)
            {
                case PhysicalBulletType.Liquid:
                    {
                        // 液体子弹：剩余伤害对护盾造成双倍伤害
                        // 设 remaining = baseNonPoisonDamage
                        float remaining = baseNonPoisonDamage;

                        // 护盾意向伤害（理想情况下会对护盾造成 2 * remaining 的损失）
                        float intendedShieldDamage = remaining * 2f;

                        float shieldBefore = shield;
                        float actualShieldLoss = Mathf.Min(intendedShieldDamage, shield);
                        shield -= actualShieldLoss;

                        shieldDamage += actualShieldLoss;

                        // 如果护盾不够吃完 intendedShieldDamage，则有一部分「浪费」
                        // 将浪费的那部分折算回基础伤害再打到生命上：
                        // intended = 2 * remaining
                        // 实际盾损 = 2 * usedBase -> usedBase = actualShieldLoss / 2
                        // 剩余基础伤害 = remaining - usedBase
                        float usedBaseDamage = actualShieldLoss * 0.5f;
                        float baseOverflow = Mathf.Max(0f, remaining - usedBaseDamage);

                        if (baseOverflow > 0f && health > 0f)
                        {
                            float hpBefore2 = health;
                            health = Mathf.Max(0f, health - baseOverflow);
                            float hpLoss2 = hpBefore2 - health;
                            hpDamageFromNonPoison += hpLoss2;
                        }
                    }
                    break;

                case PhysicalBulletType.Solid:
                case PhysicalBulletType.Gas:
                default:
                    {
                        // 普通情况：非毒伤害先打护盾，再溢出到生命
                        float remaining = baseNonPoisonDamage;

                        float shieldLoss = Mathf.Min(remaining, shield);
                        shield -= shieldLoss;
                        shieldDamage += shieldLoss;

                        float overflow = remaining - shieldLoss;
                        if (overflow > 0f && health > 0f)
                        {
                            float hpBefore2 = health;
                            health = Mathf.Max(0f, health - overflow);
                            float hpLoss2 = hpBefore2 - health;
                            hpDamageFromNonPoison += hpLoss2;
                        }
                    }
                    break;
            }

            float totalHpDamage = actualPoisonHpDamage + hpDamageFromNonPoison;

            result.healthDamage = totalHpDamage;
            result.shieldDamage = shieldDamage;
            result.totalDamage = totalHpDamage + shieldDamage;
            result.targetDied = health <= 0f;

            return result;
        }
        
        #endregion

        
        #region 私有工具方法

        /// <summary>
        /// 根据总 Proc 概率和四元素伤害占比，计算每个元素的触发层数
        /// procChance 例如：0.5=50%，1.5=150%，2.8=280%……
        /// </summary>
        private static void ComputeElementProcLayers(
            float procChance,
            float iceDamage,
            float fireDamage,
            float toxicDamage,
            float electricDamage,
            out int iceLayers,
            out int fireLayers,
            out int toxicLayers,
            out int electricLayers)
        {
            iceLayers = fireLayers = toxicLayers = electricLayers = 0;

            float sum = iceDamage + fireDamage + toxicDamage + electricDamage;
            if (sum <= 0f || procChance <= 0f)
                return;

            float pIce = iceDamage / sum;
            float pFire = fireDamage / sum;
            float pToxic = toxicDamage / sum;
            float pElec = electricDamage / sum;

            int fullProcs = Mathf.FloorToInt(procChance);
            float extraProc = procChance - fullProcs;

            int totalProcs = fullProcs;
            if (UnityEngine.Random.value < extraProc)
                totalProcs++;

            for (int i = 0; i < totalProcs; i++)
            {
                var r = UnityEngine.Random.value;
                if (r < pIce)
                    iceLayers++;
                else if (r < pIce + pFire)
                    fireLayers++;
                else if (r < pIce + pFire + pToxic)
                    toxicLayers++;
                else
                    electricLayers++;
            }
        }

        #endregion
        
    }
}
