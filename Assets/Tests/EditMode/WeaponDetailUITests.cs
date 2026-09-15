using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Demo.Core;
using Demo.Data;
using Demo.Inventory;
using Demo.UI;

namespace Demo.Tests
{
    public class WeaponDetailUITests
    {
        private WeaponData _sword;
        private ConsumableData _potion;
        private Sprite _icon;

        private GameObject _root;
        private GameObject _emptyHint;
        private Image _iconImage;
        private TextMeshProUGUI _nameLabel;
        private TextMeshProUGUI _descriptionLabel;
        private TextMeshProUGUI _damageLabel;
        private Button _equipButton;

        private WeaponDetailUI _detail;

        // 详情区不再自己攒一个 C# 事件，而是往 EventBus 上发请求
        // （和 DroppedItem 发 ItemPickedUpEvent 一个路子）。
        // 测试也就跟着订阅总线来观察。
        private int _requestedCount;
        private WeaponData _requestedWeapon;
        private int _useRequestedCount;
        private ConsumableData _requestedConsumable;

        private void OnEquipRequested(EquipRequestedEvent e)
        {
            _requestedCount++;
            _requestedWeapon = e.Weapon;
        }

        private void OnUseRequested(UseConsumableRequestedEvent e)
        {
            _useRequestedCount++;
            _requestedConsumable = e.Consumable;
        }

        [SetUp]
        public void SetUp()
        {
            _requestedCount = 0;
            _requestedWeapon = null;
            _useRequestedCount = 0;
            _requestedConsumable = null;

            EventBus.Subscribe<EquipRequestedEvent>(OnEquipRequested);
            EventBus.Subscribe<UseConsumableRequestedEvent>(OnUseRequested);

            _sword = ScriptableObject.CreateInstance<WeaponData>();
            _sword.displayName = "蜥蜴战士之刃";
            _sword.description = "从蜥蜴战士手里抢来的，刀口有缺口。";
            _sword.damage = 7;
            _sword.maxStack = 1;

            _potion = ScriptableObject.CreateInstance<ConsumableData>();
            _potion.displayName = "小药水";
            _potion.description = "喝下去能回一点血。";
            _potion.maxStack = 3;

            _icon = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 4f, 4f),
                new Vector2(0.5f, 0.5f));

            _root = new GameObject("WeaponDetail");

            _emptyHint = new GameObject("EmptyHint", typeof(RectTransform));
            _emptyHint.transform.SetParent(_root.transform, false);

            _iconImage = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
            _iconImage.transform.SetParent(_root.transform, false);
            _iconImage.enabled = false;

            _nameLabel = MakeText("Name");
            _descriptionLabel = MakeText("Description");
            _damageLabel = MakeText("Damage");

            _equipButton = new GameObject("EquipButton", typeof(RectTransform)).AddComponent<Button>();
            _equipButton.transform.SetParent(_root.transform, false);

            _detail = _root.AddComponent<WeaponDetailUI>();
            _detail.Bind(_emptyHint, _iconImage, _nameLabel, _descriptionLabel, _damageLabel, _equipButton);
        }

        [TearDown]
        public void TearDown()
        {
            // 静态总线是全局的，漏退订会污染后面所有测试
            EventBus.Unsubscribe<EquipRequestedEvent>(OnEquipRequested);
            EventBus.Unsubscribe<UseConsumableRequestedEvent>(OnUseRequested);

            if (_root != null)
            {
                Object.DestroyImmediate(_root);
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

        private TextMeshProUGUI MakeText(string name)
        {
            TextMeshProUGUI text = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(_root.transform, false);
            return text;
        }

        // ---------------------------------------------------------
        // 显示武器
        // ---------------------------------------------------------

        [Test]
        public void Show_Weapon_ShowsNameAndDescription()
        {
            _detail.Show(new InventoryItem(_sword, 1));

            Assert.AreEqual("蜥蜴战士之刃", _nameLabel.text);
            Assert.AreEqual("从蜥蜴战士手里抢来的，刀口有缺口。", _descriptionLabel.text);
        }

        [Test]
        public void Show_Weapon_ShowsDamage()
        {
            _detail.Show(new InventoryItem(_sword, 1));

            StringAssert.Contains("7", _damageLabel.text, "武器要能看到攻击力");
        }

        [Test]
        public void Show_Weapon_ShowsEquipButton()
        {
            _detail.Show(new InventoryItem(_sword, 1));

            Assert.IsTrue(_equipButton.gameObject.activeSelf, "武器才有装备按钮");
        }

        [Test]
        public void Show_Weapon_ExposesCurrentWeapon()
        {
            _detail.Show(new InventoryItem(_sword, 1));

            Assert.AreSame(_sword, _detail.CurrentWeapon);
        }

        [Test]
        public void Show_Weapon_HidesEmptyHint()
        {
            _detail.Show(new InventoryItem(_sword, 1));

            Assert.IsFalse(_emptyHint.activeSelf);
        }

        // ---------------------------------------------------------
        // 非武器
        // ---------------------------------------------------------

        [Test]
        public void Show_Consumable_HidesDamageAndShowsUseButton()
        {
            _detail.Show(new InventoryItem(_potion, 2));

            Assert.IsTrue(_equipButton.gameObject.activeSelf, "药水应复用操作按钮显示使用入口");
            Assert.AreEqual(string.Empty, _damageLabel.text, "药水没有攻击力");
        }

        [Test]
        public void Show_NonWeapon_StillShowsNameAndDescription()
        {
            _detail.Show(new InventoryItem(_potion, 2));

            Assert.AreEqual("小药水", _nameLabel.text);
            Assert.AreEqual("喝下去能回一点血。", _descriptionLabel.text);
            Assert.IsNull(_detail.CurrentWeapon, "药水不该被当成当前武器");
        }

        // ---------------------------------------------------------
        // 图标
        // ---------------------------------------------------------

        [Test]
        public void Show_WithoutIcon_HidesIconImage()
        {
            _detail.Show(new InventoryItem(_sword, 1));

            Assert.IsFalse(_iconImage.enabled);
        }

        [Test]
        public void Show_WithIcon_ShowsIconAndSprite()
        {
            _sword.icon = _icon;

            _detail.Show(new InventoryItem(_sword, 1));

            Assert.IsTrue(_iconImage.enabled);
            Assert.AreSame(_icon, _iconImage.sprite);
        }

        // ---------------------------------------------------------
        // 清空
        // ---------------------------------------------------------

        [Test]
        public void Clear_ShowsEmptyHintAndHidesEquipButton()
        {
            _detail.Show(new InventoryItem(_sword, 1));

            _detail.Clear();

            Assert.IsTrue(_emptyHint.activeSelf);
            Assert.IsFalse(_equipButton.gameObject.activeSelf);
            Assert.AreEqual(string.Empty, _nameLabel.text);
            Assert.AreEqual(string.Empty, _descriptionLabel.text);
            Assert.AreEqual(string.Empty, _damageLabel.text);
            Assert.IsFalse(_iconImage.enabled);
            Assert.IsNull(_detail.CurrentWeapon);
            Assert.IsNull(_detail.CurrentConsumable);
        }

        [Test]
        public void Show_WithNull_Clears()
        {
            _detail.Show(new InventoryItem(_sword, 1));

            _detail.Show(null);

            Assert.IsNull(_detail.CurrentWeapon);
            Assert.IsTrue(_emptyHint.activeSelf);
        }

        [Test]
        public void StartsEmpty()
        {
            Assert.IsNull(_detail.CurrentWeapon);
            Assert.IsTrue(_emptyHint.activeSelf, "一开始没选中任何东西，应该显示提示");
            Assert.IsFalse(_equipButton.gameObject.activeSelf);
        }

        // ---------------------------------------------------------
        // 装备按钮：只发请求，不做装备
        // ---------------------------------------------------------

        [Test]
        public void EquipClicked_WithWeapon_PublishesEquipRequest()
        {
            _detail.Show(new InventoryItem(_sword, 1));
            _detail.OnEquipClicked();

            Assert.AreEqual(1, _requestedCount);
            Assert.AreSame(_sword, _requestedWeapon, "按钮该把要装备的武器报出去");
        }

        [Test]
        public void ActionClicked_WithConsumable_PublishesUseRequest()
        {
            _detail.Show(new InventoryItem(_potion, 1));
            _detail.OnEquipClicked();

            Assert.AreEqual(0, _requestedCount, "没选中武器时点装备不该有反应");
            Assert.AreEqual(1, _useRequestedCount);
            Assert.AreSame(_potion, _requestedConsumable);
        }

        [Test]
        public void EquipClicked_WithNothingSelected_PublishesNothing()
        {
            _detail.OnEquipClicked();

            Assert.AreEqual(0, _requestedCount);
            Assert.AreEqual(0, _useRequestedCount);
        }

        [Test]
        public void EquipClicked_AfterSwitchingToNonWeapon_PublishesNothing()
        {
            // 先选武器再选药水：CurrentWeapon 必须跟着清掉，
            // 否则点装备会把上一次的武器装上去
            _detail.Show(new InventoryItem(_sword, 1));
            _detail.Show(new InventoryItem(_potion, 1));
            _detail.OnEquipClicked();

            Assert.AreEqual(0, _requestedCount);
            Assert.AreEqual(1, _useRequestedCount);
            Assert.IsNull(_detail.CurrentWeapon);
            Assert.AreSame(_potion, _detail.CurrentConsumable);
        }
    }
}
