using NUnit.Framework;
using UnityEngine;
using Demo.Core;
using Demo.Combat;
using Demo.Data;

namespace Demo.Tests
{
    public class DamageCalculatorTests
    {
        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            InputLock.Clear();
        }

        [Test]
        public void NoWeapon_ReturnsBaseDamage()
        {
            Assert.AreEqual(10, DamageCalculator.Calculate(10, null));
        }

        [Test]
        public void WithWeapon_AddsWeaponDamage()
        {
            WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.damage = 7;

            Assert.AreEqual(17, DamageCalculator.Calculate(10, weapon));

            Object.DestroyImmediate(weapon);
        }

        [Test]
        public void NegativeBaseDamage_ClampsToZero()
        {
            Assert.AreEqual(0, DamageCalculator.Calculate(-5, null));
        }

        [Test]
        public void NegativeWeaponDamage_ClampsToBaseDamage()
        {
            WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.damage = -3;

            Assert.AreEqual(10, DamageCalculator.Calculate(10, weapon));

            Object.DestroyImmediate(weapon);
        }
    }
}
