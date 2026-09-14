using NUnit.Framework;
using UnityEngine;
using Demo.Core;
using Demo.Data;
using Demo.Equipment;
using Demo.Player;

namespace Demo.Tests
{
    /// <summary>
    /// 装备桥接层：把"想装这把"的请求落到 PlayerStats 上，再广播出去。
    ///
    /// 注意 Bind() 的存在理由：EditMode 下 AddComponent **不会触发 OnEnable**，
    /// 光靠生命周期回调订阅的话，这个类在这里一行都测不到。
    /// Bind 是给编辑器脚本和测试的注入点，订阅动作和 OnEnable 是同一个方法。
    /// </summary>
    public class EquipmentComponentTests
    {
        private WeaponData _sword;
        private WeaponData _axe;

        private GameObject _root;
        private PlayerStats _stats;
        private EquipmentComponent _equipment;

        private int _equippedEventCount;
        private WeaponData _lastEquipped;
        private bool _lastEquippedWasNull;

        [SetUp]
        public void SetUp()
        {
            _sword = ScriptableObject.CreateInstance<WeaponData>();
            _sword.displayName = "铁剑";
            _sword.damage = 5;

            _axe = ScriptableObject.CreateInstance<WeaponData>();
            _axe.displayName = "战斧";
            _axe.damage = 20;

            _root = new GameObject("EquipmentComponentTest");

            _stats = _root.AddComponent<PlayerStats>();
            _equipment = _root.AddComponent<EquipmentComponent>();

            _equippedEventCount = 0;
            _lastEquipped = null;
            _lastEquippedWasNull = false;

            EventBus.Subscribe<WeaponEquippedEvent>(OnWeaponEquipped);

            _equipment.Bind(_stats);
        }

        private void OnWeaponEquipped(WeaponEquippedEvent e)
        {
            _equippedEventCount++;
            _lastEquipped = e.Weapon;
            _lastEquippedWasNull = e.Weapon == null;
        }

        [TearDown]
        public void TearDown()
        {
            // 静态总线是全局的，漏退订会污染后面所有测试
            EventBus.Unsubscribe<WeaponEquippedEvent>(OnWeaponEquipped);

            if (_equipment != null)
            {
                _equipment.StopListening();
            }

            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }

            Object.DestroyImmediate(_sword);
            Object.DestroyImmediate(_axe);
        }

        /// <summary>PlayerStats 的 baseDamage 默认 10，别在测试里跟着改。</summary>
        private const int BaseDamage = 10;

        // ---------------------------------------------------------
        // 装备
        // ---------------------------------------------------------

        [Test]
        public void StartsEmpty()
        {
            Assert.IsNull(_equipment.MainHand);
            Assert.AreEqual(BaseDamage, _stats.FinalDamage, "没装武器时攻击力就是基础值");
        }

        [Test]
        public void Equip_SetsMainHand()
        {
            _equipment.System.Equip(_sword);

            Assert.AreSame(_sword, _equipment.MainHand);
        }

        [Test]
        public void Equip_UpdatesPlayerStats()
        {
            _equipment.System.Equip(_sword);

            Assert.AreSame(_sword, _stats.EquippedWeapon);
            Assert.AreEqual(BaseDamage + _sword.damage, _stats.FinalDamage, "攻击力该加上武器伤害");
        }

        [Test]
        public void Equip_DifferentWeapon_ReplacesDamageInsteadOfStacking()
        {
            _equipment.System.Equip(_sword);
            _equipment.System.Equip(_axe);

            Assert.AreEqual(
                BaseDamage + _axe.damage,
                _stats.FinalDamage,
                "换武器是替换不是叠加，不然换几次就无敌了");
        }

        [Test]
        public void Equip_Null_IsIgnored()
        {
            _equipment.System.Equip(null);

            Assert.IsNull(_equipment.MainHand);
            Assert.AreEqual(BaseDamage, _stats.FinalDamage);
            Assert.AreEqual(0, _equippedEventCount);
        }

        [Test]
        public void Unequip_GoesBackToBaseDamage()
        {
            _equipment.System.Equip(_sword);

            // 先钉住"确实涨上去了"。少了这句，装备那条链断掉时
            // 攻击力一直停在基础值，这个测试照样绿 —— 变异测试抓出来过。
            Assert.AreEqual(BaseDamage + _sword.damage, _stats.FinalDamage, "前提：武器确实装上了");

            _equipment.System.Unequip();

            Assert.IsNull(_equipment.MainHand);
            Assert.IsNull(_stats.EquippedWeapon);
            Assert.AreEqual(BaseDamage, _stats.FinalDamage, "卸了武器攻击力要掉回去");
        }

        [Test]
        public void Equip_SameWeaponTwice_OnlyNotifiesOnce()
        {
            _equipment.System.Equip(_sword);
            _equipment.System.Equip(_sword);

            Assert.AreEqual(1, _equippedEventCount, "重复装同一把不该惊动武器挂点去重建模型");
        }

        // ---------------------------------------------------------
        // 广播（Phase 07 的武器挂点靠它换模型）
        // ---------------------------------------------------------

        [Test]
        public void Equip_PublishesWeaponEquippedEvent()
        {
            _equipment.System.Equip(_sword);

            Assert.AreEqual(1, _equippedEventCount);
            Assert.AreSame(_sword, _lastEquipped);
        }

        [Test]
        public void Unequip_PublishesEventWithNoWeapon()
        {
            _equipment.System.Equip(_sword);

            _equipment.System.Unequip();

            Assert.AreEqual(2, _equippedEventCount);
            Assert.IsTrue(_lastEquippedWasNull, "卸下也要广播，否则挂点不知道要摘模型");
        }

        // ---------------------------------------------------------
        // EventBus -> 组件 这一跳
        // ---------------------------------------------------------

        [Test]
        public void EquipRequestedEvent_OnBus_EquipsTheWeapon()
        {
            // UI 走的就是这条路：详情区发请求，组件收到就装。
            // 这里验证订阅真的接上了，而不是只测了组件自己的方法。
            EventBus.Publish(new EquipRequestedEvent(_sword));

            Assert.AreSame(_sword, _equipment.MainHand);
            Assert.AreEqual(BaseDamage + _sword.damage, _stats.FinalDamage);
        }

        [Test]
        public void EquipRequestedEvent_WithNullWeapon_IsIgnored()
        {
            // 先装上一把，再发个空请求：不能把已经拿在手上的武器顶掉。
            // 空手状态下发 null 什么都不会发生，那种写法测不出问题来。
            EventBus.Publish(new EquipRequestedEvent(_sword));

            EventBus.Publish(new EquipRequestedEvent(null));

            Assert.AreSame(_sword, _equipment.MainHand, "空请求不该把武器卸了");
            Assert.AreEqual(BaseDamage + _sword.damage, _stats.FinalDamage);
            Assert.AreEqual(1, _equippedEventCount);
        }

        [Test]
        public void StopListening_StopsReactingToBus()
        {
            _equipment.StopListening();

            EventBus.Publish(new EquipRequestedEvent(_sword));

            Assert.IsNull(_equipment.MainHand, "退订之后不该还在响应总线事件");
        }

        // ---------------------------------------------------------
        // 容错
        // ---------------------------------------------------------

        [Test]
        public void Equip_WithoutPlayerStats_StillPublishesEvent()
        {
            // 场景里 playerStats 那个框忘了拖，装备依然应该生效一半
            // （数据存住、事件发出去），而不是空引用崩掉。
            _equipment.Bind(null);

            _equipment.System.Equip(_sword);

            Assert.AreSame(_sword, _equipment.MainHand);
            Assert.AreEqual(1, _equippedEventCount);
        }

        // ---------------------------------------------------------
        // 开局武器
        //
        // MC01 模型出厂时 weapon_r 底下就嵌着一把 OHS09_Sword。
        // 那把剑要算作"玩家开局就握在手上"的武器，否则换成别的武器时
        // 它只是被销毁，凭空消失 —— 玩家平白少一把剑，也不会进背包。
        // ---------------------------------------------------------

        [Test]
        public void EquipStartingWeapon_PutsItInTheHand()
        {
            _equipment.SetStartingWeapon(_sword);

            _equipment.EquipStartingWeapon();

            Assert.AreSame(_sword, _equipment.MainHand, "开局武器要真的握在手上");
        }

        [Test]
        public void EquipStartingWeapon_UpdatesPlayerStats()
        {
            // 开局就握着剑，攻击力不能还是空手的基础值
            _equipment.SetStartingWeapon(_sword);

            _equipment.EquipStartingWeapon();

            Assert.AreEqual(_stats.BaseDamage + _sword.damage, _stats.FinalDamage,
                "开局武器的伤害要算进去");
        }

        [Test]
        public void EquipStartingWeapon_PublishesWeaponEquippedEvent()
        {
            // 挂点靠这个事件把模型挂上去。不发的话开局手上是空的，
            // 而原生模型还留在骨骼底下 —— 正好是这次要修的现象
            _equipment.SetStartingWeapon(_sword);

            _equipment.EquipStartingWeapon();

            Assert.AreEqual(1, _equippedEventCount);
            Assert.AreSame(_sword, _lastEquipped);
        }

        [Test]
        public void EquipStartingWeapon_WithoutStartingWeapon_StaysEmpty()
        {
            // 场景里 startingWeapon 没拖：空手开局，不该凭空多出一把
            Assert.DoesNotThrow(() => _equipment.EquipStartingWeapon());

            Assert.IsNull(_equipment.MainHand);
            Assert.AreEqual(0, _equippedEventCount, "没装东西就不该发装备事件");
        }

        [Test]
        public void EquipStartingWeapon_Twice_DoesNotChangeAnything()
        {
            // Start 被重放（或场景重载）时不能把同一把重复播一遍
            _equipment.SetStartingWeapon(_sword);

            _equipment.EquipStartingWeapon();
            _equipment.EquipStartingWeapon();

            Assert.AreSame(_sword, _equipment.MainHand);
            Assert.AreEqual(1, _equippedEventCount, "同一把重复装备不该再发一次事件");
        }
    }
}
