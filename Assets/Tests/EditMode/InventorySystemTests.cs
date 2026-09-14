using NUnit.Framework;
using UnityEngine;
using Demo.Data;
using Demo.Inventory;

namespace Demo.Tests
{
    public class InventorySystemTests
    {
        private WeaponData _sword;
        private ConsumableData _potion;

        [SetUp]
        public void SetUp()
        {
            _sword = ScriptableObject.CreateInstance<WeaponData>();
            _sword.id = "LizardWarriorBlade";
            _sword.displayName = "螭龍戰士之刃";
            _sword.maxStack = 1;

            _potion = ScriptableObject.CreateInstance<ConsumableData>();
            _potion.id = "SmallPotion";
            _potion.displayName = "小药水";
            _potion.maxStack = 3;
        }

        [TearDown]
        public void TearDown()
        {
            if (_sword != null)
            {
                Object.DestroyImmediate(_sword);
            }

            if (_potion != null)
            {
                Object.DestroyImmediate(_potion);
            }
        }

        // ---------------------------------------------------------
        // 初始状态
        // ---------------------------------------------------------

        [Test]
        public void NewInventory_IsEmpty()
        {
            var system = new InventorySystem();

            Assert.AreEqual(0, system.GetItems().Count);
            Assert.IsFalse(system.HasItem(_sword));
            Assert.AreEqual(0, system.GetAmount(_sword));
        }

        // ---------------------------------------------------------
        // AddItem
        // ---------------------------------------------------------

        [Test]
        public void AddItem_StoresOneEntry()
        {
            var system = new InventorySystem();

            Assert.IsTrue(system.AddItem(_sword));

            Assert.AreEqual(1, system.GetItems().Count);
            Assert.AreSame(_sword, system.GetItems()[0].Data);
            Assert.AreEqual(1, system.GetItems()[0].Amount);
            Assert.AreEqual(1, system.GetAmount(_sword));
        }

        [Test]
        public void AddItem_WithCount_StoresThatAmount()
        {
            var system = new InventorySystem();

            system.AddItem(_potion, 2);

            Assert.AreEqual(2, system.GetAmount(_potion));
        }

        [Test]
        public void AddItem_RaisesChanged()
        {
            var system = new InventorySystem();
            int changedCount = 0;
            system.Changed += () => changedCount++;

            system.AddItem(_sword);

            Assert.AreEqual(1, changedCount);
        }

        [Test]
        public void AddItem_SameNonStackableItem_CreatesSeparateEntries()
        {
            var system = new InventorySystem();

            system.AddItem(_sword);
            system.AddItem(_sword);

            Assert.AreEqual(2, system.GetItems().Count);
            Assert.AreEqual(2, system.GetAmount(_sword));
        }

        [Test]
        public void AddItem_StackableItem_FillsExistingEntry()
        {
            var system = new InventorySystem();

            system.AddItem(_potion, 1);
            system.AddItem(_potion, 1);

            Assert.AreEqual(1, system.GetItems().Count);
            Assert.AreEqual(2, system.GetItems()[0].Amount);
        }

        [Test]
        public void AddItem_BeyondMaxStack_StartsNewEntry()
        {
            var system = new InventorySystem();

            system.AddItem(_potion, 3);
            system.AddItem(_potion, 1);

            Assert.AreEqual(2, system.GetItems().Count);
            Assert.AreEqual(3, system.GetItems()[0].Amount);
            Assert.AreEqual(1, system.GetItems()[1].Amount);
            Assert.AreEqual(4, system.GetAmount(_potion));
        }

        [Test]
        public void AddItem_AmountSpanningSeveralStacks_SplitsEvenly()
        {
            var system = new InventorySystem();

            system.AddItem(_potion, 7);

            Assert.AreEqual(3, system.GetItems().Count);
            Assert.AreEqual(3, system.GetItems()[0].Amount);
            Assert.AreEqual(3, system.GetItems()[1].Amount);
            Assert.AreEqual(1, system.GetItems()[2].Amount);
        }

        [Test]
        public void AddItem_WithNull_ReturnsFalseAndDoesNotChange()
        {
            var system = new InventorySystem();
            int changedCount = 0;
            system.Changed += () => changedCount++;

            Assert.IsFalse(system.AddItem(null));
            Assert.AreEqual(0, system.GetItems().Count);
            Assert.AreEqual(0, changedCount);
        }

        [Test]
        public void AddItem_WithNonPositiveAmount_ReturnsFalseAndDoesNotChange()
        {
            var system = new InventorySystem();

            Assert.IsFalse(system.AddItem(_sword, 0));
            Assert.IsFalse(system.AddItem(_sword, -1));
            Assert.AreEqual(0, system.GetItems().Count);
        }

        // ---------------------------------------------------------
        // RemoveItem
        // ---------------------------------------------------------

        [Test]
        public void RemoveItem_DecrementsAmount()
        {
            var system = new InventorySystem();
            system.AddItem(_potion, 3);

            Assert.IsTrue(system.RemoveItem(_potion));

            Assert.AreEqual(2, system.GetAmount(_potion));
        }

        [Test]
        public void RemoveItem_RemovingLastUnit_DropsEntry()
        {
            var system = new InventorySystem();
            system.AddItem(_sword);

            Assert.IsTrue(system.RemoveItem(_sword));

            Assert.AreEqual(0, system.GetItems().Count);
            Assert.IsFalse(system.HasItem(_sword));
        }

        [Test]
        public void RemoveItem_RaisesChanged()
        {
            var system = new InventorySystem();
            system.AddItem(_sword);

            int changedCount = 0;
            system.Changed += () => changedCount++;

            system.RemoveItem(_sword);

            Assert.AreEqual(1, changedCount);
        }

        [Test]
        public void RemoveItem_MoreThanHeld_ReturnsFalseAndKeepsEverything()
        {
            var system = new InventorySystem();
            system.AddItem(_potion, 2);

            Assert.IsFalse(system.RemoveItem(_potion, 3));
            Assert.AreEqual(2, system.GetAmount(_potion));
        }

        [Test]
        public void RemoveItem_AbsentItem_ReturnsFalse()
        {
            var system = new InventorySystem();

            Assert.IsFalse(system.RemoveItem(_sword));
        }

        [Test]
        public void RemoveItem_WithNull_ReturnsFalse()
        {
            var system = new InventorySystem();

            Assert.IsFalse(system.RemoveItem(null));
        }

        [Test]
        public void RemoveItem_FromEarlierStack_LeavesLaterStacksIntact()
        {
            var system = new InventorySystem();
            system.AddItem(_potion, 4);

            Assert.IsTrue(system.RemoveItem(_potion, 3));

            Assert.AreEqual(1, system.GetAmount(_potion));
            Assert.AreEqual(1, system.GetItems().Count);
            Assert.AreEqual(1, system.GetItems()[0].Amount);
        }

        // ---------------------------------------------------------
        // HasItem / GetAmount
        // ---------------------------------------------------------

        [Test]
        public void HasItem_ChecksAgainstRequestedAmount()
        {
            var system = new InventorySystem();
            system.AddItem(_potion, 2);

            Assert.IsTrue(system.HasItem(_potion));
            Assert.IsTrue(system.HasItem(_potion, 2));
            Assert.IsFalse(system.HasItem(_potion, 3));
        }

        [Test]
        public void HasItem_WithNull_ReturnsFalse()
        {
            var system = new InventorySystem();
            system.AddItem(_sword);

            Assert.IsFalse(system.HasItem(null));
        }

        // ---------------------------------------------------------
        // GetItems
        // ---------------------------------------------------------

        [Test]
        public void GetItems_PreservesInsertionOrder()
        {
            var system = new InventorySystem();

            system.AddItem(_sword);
            system.AddItem(_potion);

            Assert.AreSame(_sword, system.GetItems()[0].Data);
            Assert.AreSame(_potion, system.GetItems()[1].Data);
        }

        [Test]
        public void GetItems_ReturnsLiveList_NotACopy()
        {
            var system = new InventorySystem();
            system.AddItem(_sword);

            Assert.AreEqual(1, system.GetItems().Count);

            system.AddItem(_potion);

            Assert.AreEqual(2, system.GetItems().Count);
        }
    }
}
