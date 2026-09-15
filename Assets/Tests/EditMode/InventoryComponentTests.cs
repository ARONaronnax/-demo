using NUnit.Framework;
using UnityEngine;
using Demo.Core;
using Demo.Data;
using Demo.Inventory;
using Demo.Combat;

namespace Demo.Tests
{
    /// <summary>
    /// 背包桥接。除了把拾取落进 InventorySystem，还要维持**背包和手的一致性**：
    /// 装备 = 东西从背包挪到手上，换装时旧武器退回背包。
    ///
    /// 这条链子以前是断的 —— EquipmentComponent 只改 PlayerStats 和发事件，
    /// 谁都没碰过 InventorySystem，所以装备完那把武器会一直留在背包格子里。
    /// </summary>
    public class InventoryComponentTests
    {
        private WeaponData _sword;
        private WeaponData _axe;
        private ConsumableData _potion;
        private HealthComponent _health;

        private GameObject _root;
        private InventoryComponent _inventory;

        private int _changedCount;
        private bool _counting;

        [SetUp]
        public void SetUp()
        {
            _sword = MakeWeapon("蜥蜴战士之刃");
            _axe = MakeWeapon("战斧");
            _potion = ScriptableObject.CreateInstance<ConsumableData>();
            _potion.displayName = "回复药水";
            _potion.healAmount = 20;
            _potion.maxStack = 10;

            _root = new GameObject("InventoryComponentTest");
            _health = _root.AddComponent<HealthComponent>();
            _health.ResetToFull();
            _inventory = _root.AddComponent<InventoryComponent>();
            _inventory.BindHealth(_health);

            // EditMode 下 AddComponent 不触发 OnEnable，得自己开
            _inventory.Listen();
        }

        private static WeaponData MakeWeapon(string name)
        {
            WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.displayName = name;
            weapon.damage = 7;

            // 武器不堆叠：两把同名武器占两格，这样"只移走一把"才测得出区别
            weapon.maxStack = 1;
            return weapon;
        }

        [TearDown]
        public void TearDown()
        {
            if (_counting)
            {
                EventBus.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);
            }

            _inventory.StopListening();

            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_sword);
            Object.DestroyImmediate(_axe);
            Object.DestroyImmediate(_potion);
        }

        private void OnInventoryChanged(InventoryChangedEvent e)
        {
            _changedCount++;
        }

        private void StartCountingChanges()
        {
            _counting = true;
            _changedCount = 0;
            EventBus.Subscribe<InventoryChangedEvent>(OnInventoryChanged);
        }

        private int BagCount()
        {
            return _inventory.GetItems().Count;
        }

        // ---------------------------------------------------------
        // 装备 = 从背包移出
        // ---------------------------------------------------------

        [Test]
        public void Equip_RemovesTheWeaponFromTheBag()
        {
            _inventory.System.AddItem(_sword);
            Assert.AreEqual(1, BagCount(), "前提：刀在背包里");

            EventBus.Publish(new WeaponEquippedEvent(_sword));

            Assert.AreEqual(0, BagCount(), "装备之后那把武器就不该还在背包里");
            Assert.IsFalse(_inventory.HasItem(_sword));
        }

        [Test]
        public void Equip_LeavesOtherItemsAlone()
        {
            // 移出的是装备的那一件，不是把背包清空
            _inventory.System.AddItem(_sword);
            _inventory.System.AddItem(_axe);

            EventBus.Publish(new WeaponEquippedEvent(_sword));

            Assert.AreEqual(1, BagCount(), "只该移走装上的那一把");
            Assert.IsTrue(_inventory.HasItem(_axe), "没装的那把要留在背包里");
            Assert.IsFalse(_inventory.HasItem(_sword));
        }

        [Test]
        public void Equip_TwoCopiesInTheBag_RemovesOnlyOne()
        {
            // 两把同款武器各占一格（maxStack = 1），装上的是其中一把
            _inventory.System.AddItem(_sword);
            _inventory.System.AddItem(_sword);
            Assert.AreEqual(2, _inventory.GetAmount(_sword), "前提：背包里有两把");

            EventBus.Publish(new WeaponEquippedEvent(_sword));

            Assert.AreEqual(1, _inventory.GetAmount(_sword), "装上手上的是其中一把，另一把该留在背包");
        }

        // ---------------------------------------------------------
        // 换装 = 旧的退回背包
        // ---------------------------------------------------------

        [Test]
        public void EquipAnotherWeapon_ReturnsThePreviousOneToTheBag()
        {
            _inventory.System.AddItem(_sword);
            _inventory.System.AddItem(_axe);

            EventBus.Publish(new WeaponEquippedEvent(_sword));
            Assert.IsFalse(_inventory.HasItem(_sword), "前提：刀已经拿在手上了");

            EventBus.Publish(new WeaponEquippedEvent(_axe));

            Assert.IsTrue(_inventory.HasItem(_sword), "换下来的刀要退回背包，不能凭空消失");
            Assert.IsFalse(_inventory.HasItem(_axe), "换上去的斧头要从背包移出");
        }

        [Test]
        public void Unequip_ReturnsTheWeaponToTheBag()
        {
            _inventory.System.AddItem(_sword);

            EventBus.Publish(new WeaponEquippedEvent(_sword));
            Assert.IsFalse(_inventory.HasItem(_sword), "前提：刀在手上");

            EventBus.Publish(new WeaponEquippedEvent(null));

            Assert.IsTrue(_inventory.HasItem(_sword), "卸下武器要回到背包");
        }

        [Test]
        public void EquipTheSameWeaponAgain_LeavesTheSpareCopyAlone()
        {
            // 背包里两把同款武器，装上一把之后还剩一把备用的。
            //
            // 这条专门钉住"同一把不算换"：少了那道判断，第二次收到同一把时
            // _equipped 会被当成换下来的退回背包，而 AddItem 会并进已有格子 ——
            // 接下去的 RemoveItem 正好把备用那格扣掉，玩家的东西就凭空少了一件。
            //
            // 注意：不能写成"只放一把、装两次、断言背包为空" —— 那种写法下
            // AddItem 和 RemoveItem 会互相抵消，守卫在不在都是绿的，测了个寂寞。
            _inventory.System.AddItem(_sword);
            _inventory.System.AddItem(_sword);

            EventBus.Publish(new WeaponEquippedEvent(_sword));
            Assert.AreEqual(1, _inventory.GetAmount(_sword), "前提：装到手上的是其中一把");

            EventBus.Publish(new WeaponEquippedEvent(_sword));

            Assert.AreEqual(1, _inventory.GetAmount(_sword), "备用的那把要还在，不能被上一把顶掉");
        }

        // ---------------------------------------------------------
        // 容错
        // ---------------------------------------------------------

        [Test]
        public void Equip_WeaponNotInTheBag_DoesNotAddIt()
        {
            // 来源不是背包的情况（调试、将来从别处装备）：
            // 移出失败就算了，但绝不能把手上那把"退回"进背包
            Assert.AreEqual(0, BagCount(), "前提：背包是空的");

            Assert.DoesNotThrow(() => EventBus.Publish(new WeaponEquippedEvent(_sword)));

            Assert.AreEqual(0, BagCount(), "背包里本来没有的武器，不该因为装备而冒出来");
        }

        [Test]
        public void Unequip_WhenNothingWasEquipped_DoesNothing()
        {
            _inventory.System.AddItem(_sword);

            EventBus.Publish(new WeaponEquippedEvent(null));

            Assert.AreEqual(1, BagCount(), "没装过东西的时候收到卸下事件，背包不该变");
        }

        // ---------------------------------------------------------
        // 刷新通知 + 原有拾取链路不能被改坏
        // ---------------------------------------------------------

        [Test]
        public void Equip_PublishesInventoryChanged()
        {
            // 背包 UI 只认 InventoryChangedEvent 刷新，不发它就等于 UI 不知道变过
            _inventory.System.AddItem(_sword);

            StartCountingChanges();
            EventBus.Publish(new WeaponEquippedEvent(_sword));

            Assert.Greater(_changedCount, 0, "装备改动了背包内容，必须通知 UI 刷新");
        }

        [Test]
        public void PickUp_StillAddsToTheBag()
        {
            // 重构订阅方式的回归保护：拾取这条老链路不能受影响
            EventBus.Publish(new ItemPickedUpEvent(_axe, 1));

            Assert.IsTrue(_inventory.HasItem(_axe));
        }

        [Test]
        public void UsePotion_RestoresTwentyHpAndConsumesOne()
        {
            _inventory.System.AddItem(_potion, 2);
            _health.TakeDamage(new DamageInfo(50f, null, Vector3.zero, Vector3.zero));

            EventBus.Publish(new UseConsumableRequestedEvent(_potion));

            Assert.AreEqual(70f, _health.CurrentHp);
            Assert.AreEqual(1, _inventory.GetAmount(_potion));
        }

        [Test]
        public void UsePotion_AtFullHealth_DoesNotConsumeIt()
        {
            _inventory.System.AddItem(_potion, 1);

            EventBus.Publish(new UseConsumableRequestedEvent(_potion));

            Assert.AreEqual(1, _inventory.GetAmount(_potion));
        }

        [Test]
        public void StopListening_StopsReactingToTheBus()
        {
            _inventory.System.AddItem(_sword);

            _inventory.StopListening();

            EventBus.Publish(new WeaponEquippedEvent(_sword));

            Assert.IsTrue(_inventory.HasItem(_sword), "退订之后装备事件不该再动背包");
        }
    }
}
