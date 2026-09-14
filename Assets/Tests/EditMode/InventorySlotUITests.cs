using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Demo.Data;
using Demo.Inventory;
using Demo.UI;

namespace Demo.Tests
{
    public class InventorySlotUITests
    {
        private WeaponData _sword;
        private ConsumableData _potion;
        private Sprite _icon;

        private GameObject _slotObject;
        private InventorySlotUI _slot;

        private Image _iconImage;
        private TextMeshProUGUI _nameLabel;
        private TextMeshProUGUI _amountLabel;

        [SetUp]
        public void SetUp()
        {
            _sword = ScriptableObject.CreateInstance<WeaponData>();
            _sword.displayName = "蜥蜴战士之刃";
            _sword.maxStack = 1;

            _potion = ScriptableObject.CreateInstance<ConsumableData>();
            _potion.displayName = "小药水";
            _potion.maxStack = 3;

            _icon = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 4f, 4f),
                new Vector2(0.5f, 0.5f));

            _slotObject = new GameObject("Slot");

            _iconImage = new GameObject("Icon").AddComponent<Image>();
            _iconImage.transform.SetParent(_slotObject.transform, false);
            _iconImage.enabled = false;

            _nameLabel = new GameObject("Name").AddComponent<TextMeshProUGUI>();
            _nameLabel.transform.SetParent(_slotObject.transform, false);

            _amountLabel = new GameObject("Amount").AddComponent<TextMeshProUGUI>();
            _amountLabel.transform.SetParent(_slotObject.transform, false);

            _slot = _slotObject.AddComponent<InventorySlotUI>();
            _slot.Bind(_iconImage, _nameLabel, _amountLabel);
        }

        [TearDown]
        public void TearDown()
        {
            if (_slotObject != null)
            {
                Object.DestroyImmediate(_slotObject);
            }

            if (_sword != null)
            {
                Object.DestroyImmediate(_sword);
            }

            if (_potion != null)
            {
                Object.DestroyImmediate(_potion);
            }

            if (_icon != null)
            {
                Object.DestroyImmediate(_icon);
            }
        }

        // ---------------------------------------------------------
        // 名称
        // ---------------------------------------------------------

        [Test]
        public void SetItem_ShowsDisplayName()
        {
            _slot.SetItem(new InventoryItem(_sword, 1));

            Assert.AreEqual("蜥蜴战士之刃", _nameLabel.text);
        }

        [Test]
        public void SetItem_WithNull_ShowsNothing()
        {
            _slot.SetItem(null);

            Assert.AreEqual(string.Empty, _nameLabel.text);
        }

        [Test]
        public void Clear_BlanksEverything()
        {
            _slot.SetItem(new InventoryItem(_sword, 2));

            _slot.Clear();

            Assert.AreEqual(string.Empty, _nameLabel.text);
            Assert.AreEqual(string.Empty, _amountLabel.text);
            Assert.IsFalse(_iconImage.enabled);
            Assert.IsNull(_slot.Item);
        }

        // ---------------------------------------------------------
        // 图标
        // ---------------------------------------------------------

        [Test]
        public void SetItem_WithoutIcon_HidesIconImage()
        {
            _slot.SetItem(new InventoryItem(_sword, 1));

            Assert.IsFalse(_iconImage.enabled, "没有图标时不该显示一块空白图");
        }

        [Test]
        public void SetItem_WithIcon_ShowsIconImage()
        {
            _sword.icon = _icon;

            _slot.SetItem(new InventoryItem(_sword, 1));

            Assert.IsTrue(_iconImage.enabled);
            Assert.AreSame(_icon, _iconImage.sprite);
        }

        // ---------------------------------------------------------
        // 数量
        // ---------------------------------------------------------

        [Test]
        public void SetItem_AmountOne_HidesAmount()
        {
            _slot.SetItem(new InventoryItem(_sword, 1));

            Assert.AreEqual(string.Empty, _amountLabel.text, "只有一件时不显示数量");
        }

        [Test]
        public void SetItem_AmountGreaterThanOne_ShowsAmount()
        {
            _slot.SetItem(new InventoryItem(_potion, 3));

            Assert.AreEqual("3", _amountLabel.text);
        }

        // ---------------------------------------------------------
        // 选中态
        // ---------------------------------------------------------

        [Test]
        public void Item_ExposesWhatWasSet()
        {
            InventoryItem item = new InventoryItem(_sword, 1);

            _slot.SetItem(item);

            Assert.AreSame(item, _slot.Item);
        }

        // ---------------------------------------------------------
        // 点击
        // ---------------------------------------------------------

        [Test]
        public void Click_OnFilledSlot_RaisesClicked()
        {
            InventorySlotUI received = null;
            _slot.Clicked += s => received = s;

            _slot.SetItem(new InventoryItem(_sword, 1));
            _slot.Click();

            Assert.AreSame(_slot, received, "点格子要能告诉外面点的是哪一个");
        }

        [Test]
        public void Click_OnEmptySlot_RaisesNothing()
        {
            int raised = 0;
            _slot.Clicked += s => raised++;

            _slot.Click();

            Assert.AreEqual(0, raised, "空格子点了不该弹详情出来");
        }

        [Test]
        public void Click_AfterClear_RaisesNothing()
        {
            int raised = 0;
            _slot.Clicked += s => raised++;

            _slot.SetItem(new InventoryItem(_sword, 1));
            _slot.Clear();
            _slot.Click();

            Assert.AreEqual(0, raised);
        }
    }
}
