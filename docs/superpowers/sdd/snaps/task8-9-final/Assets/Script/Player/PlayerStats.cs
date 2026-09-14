using System;
using UnityEngine;
using Demo.Combat;
using Demo.Data;

namespace Demo.Player
{
    /// <summary>
    /// 玩家攻击力。Phase 09 接装备系统时通过 SetWeapon 写入武器。
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        [SerializeField] private int baseDamage = 10;

        private WeaponData _equippedWeapon;

        public int BaseDamage { get { return baseDamage; } }

        public WeaponData EquippedWeapon { get { return _equippedWeapon; } }

        public int FinalDamage
        {
            get { return DamageCalculator.Calculate(baseDamage, _equippedWeapon); }
        }

        public event Action StatsChanged;

        public void SetWeapon(WeaponData weapon)
        {
            if (_equippedWeapon == weapon)
            {
                return;
            }

            _equippedWeapon = weapon;

            if (StatsChanged != null)
            {
                StatsChanged();
            }
        }
    }
}
