using NUnit.Framework;
using UnityEngine;
using Demo.Data;
using Demo.Equipment;

namespace Demo.Tests
{
    /// <summary>
    /// 装备数据本身。纯 C#，不碰场景、不碰 MonoBehaviour —— 和 InventorySystem 一样，
    /// 数据只有这一份，UI 和 MonoBehaviour 都只是它的投影。
    /// </summary>
    public class EquipmentSystemTests
    {
        private WeaponData _sword;
        private WeaponData _axe;

        private EquipmentSystem _equipment;
        private int _changedCount;

        [SetUp]
        public void SetUp()
        {
            _sword = ScriptableObject.CreateInstance<WeaponData>();
            _sword.displayName = "铁剑";
            _sword.damage = 5;

            _axe = ScriptableObject.CreateInstance<WeaponData>();
            _axe.displayName = "战斧";
            _axe.damage = 20;

            _equipment = new EquipmentSystem();
            _changedCount = 0;

            _equipment.Changed += OnChanged;
        }

        private void OnChanged()
        {
            _changedCount++;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sword);
            Object.DestroyImmediate(_axe);
        }

        // ---------------------------------------------------------
        // 初始状态
        // ---------------------------------------------------------

        [Test]
        public void StartsEmpty()
        {
            Assert.IsNull(_equipment.MainHand);
            Assert.IsFalse(_equipment.HasWeapon);
        }

        // ---------------------------------------------------------
        // 装备
        // ---------------------------------------------------------

        [Test]
        public void Equip_SetsMainHand()
        {
            _equipment.Equip(_sword);

            Assert.AreSame(_sword, _equipment.MainHand);
            Assert.IsTrue(_equipment.HasWeapon);
        }

        [Test]
        public void Equip_RaisesChanged()
        {
            _equipment.Equip(_sword);

            Assert.AreEqual(1, _changedCount);
        }

        [Test]
        public void Equip_DifferentWeapon_ReplacesIt()
        {
            _equipment.Equip(_sword);
            _equipment.Equip(_axe);

            Assert.AreSame(_axe, _equipment.MainHand, "主手只有一个位置，新武器应该顶掉旧的");
            Assert.AreEqual(2, _changedCount);
        }

        [Test]
        public void Equip_SameWeaponTwice_RaisesChangedOnce()
        {
            // 重复装同一把不算变化。Phase 07 的挂点收到事件就会重建模型，
            // 多发一次事件就是白白重建一次。
            _equipment.Equip(_sword);
            _equipment.Equip(_sword);

            Assert.AreEqual(1, _changedCount);
            Assert.AreSame(_sword, _equipment.MainHand);
        }

        [Test]
        public void Equip_Null_IsIgnored()
        {
            // "装一把不存在的武器"是调用方的 bug，不是"卸下"的意思。
            // 卸下有专门的 Unequip，所以这里什么都不做，也不发事件。
            _equipment.Equip(null);

            Assert.IsFalse(_equipment.HasWeapon);
            Assert.AreEqual(0, _changedCount);

            // 而且不能把系统弄成半死状态：之后正经装备照样得生效
            _equipment.Equip(_sword);

            Assert.AreSame(_sword, _equipment.MainHand);
        }

        [Test]
        public void Equip_NullWhileArmed_KeepsTheWeapon()
        {
            _equipment.Equip(_sword);

            _equipment.Equip(null);

            Assert.AreSame(_sword, _equipment.MainHand);
            Assert.AreEqual(1, _changedCount, "null 不该把手上的武器顶掉");
        }

        // ---------------------------------------------------------
        // 卸下
        // ---------------------------------------------------------

        [Test]
        public void Unequip_ClearsMainHand()
        {
            _equipment.Equip(_sword);

            _equipment.Unequip();

            Assert.IsNull(_equipment.MainHand);
            Assert.IsFalse(_equipment.HasWeapon);
        }

        [Test]
        public void Unequip_RaisesChanged()
        {
            _equipment.Equip(_sword);

            _equipment.Unequip();

            Assert.AreEqual(2, _changedCount);
        }

        [Test]
        public void Unequip_WhenEmpty_DoesNothing()
        {
            _equipment.Unequip();

            Assert.IsFalse(_equipment.HasWeapon);
            Assert.AreEqual(0, _changedCount, "本来就空手，卸下不算变化");
        }

        [Test]
        public void Equip_AfterUnequip_Works()
        {
            _equipment.Equip(_sword);
            _equipment.Unequip();

            _equipment.Equip(_axe);

            Assert.AreSame(_axe, _equipment.MainHand);
        }

        [Test]
        public void Equip_AfterUnequipSameWeapon_RaisesChangedAgain()
        {
            // 卸下再装回同一把是**真的变化**：中间空过手，
            // 挂点那边的模型确实被摘掉过，得收到通知才能装回去。
            _equipment.Equip(_sword);
            _equipment.Unequip();
            _equipment.Equip(_sword);

            Assert.AreSame(_sword, _equipment.MainHand);
            Assert.AreEqual(3, _changedCount);
        }

        // ---------------------------------------------------------
        // 订阅
        // ---------------------------------------------------------

        [Test]
        public void Unsubscribed_StopsGettingNotified()
        {
            _equipment.Changed -= OnChanged;

            _equipment.Equip(_sword);

            Assert.AreEqual(0, _changedCount);
        }
    }
}
