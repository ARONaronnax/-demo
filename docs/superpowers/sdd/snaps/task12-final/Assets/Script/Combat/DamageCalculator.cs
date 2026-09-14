using UnityEngine;
using Demo.Data;

namespace Demo.Combat
{
    /// <summary>
    /// 最终攻击力 = 基础攻击力 + 装备武器伤害。
    /// 战斗系统一律使用本方法的返回值，UI 不得自行计算。
    /// </summary>
    public static class DamageCalculator
    {
        public static int Calculate(int baseDamage, WeaponData weapon)
        {
            int basePart = Mathf.Max(0, baseDamage);
            int weaponPart = weapon != null ? Mathf.Max(0, weapon.damage) : 0;

            return basePart + weaponPart;
        }
    }
}
