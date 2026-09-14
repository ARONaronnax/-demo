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
    public class InventoryUITests
    {
        /// <summary>场景里摆好的初始格子数，也是"至少显示几格"。</summary>
        private const int InitialSlots = 4;

        private WeaponData[] _weapons;

        private GameObject _root;
        private GameObject _panel;
        private RectTransform _container;

        private InventoryComponent _inventory;
        private InventoryUI _ui;
        private InventorySlotUI[] _slots;
        private WeaponDetailUI _detail;
        private GameObject _detailHint;

        [SetUp]
        public void SetUp()
        {
            GamePause.Clear();
            InputLock.Clear();

            _weapons = new WeaponData[30];
            for (int i = 0; i < _weapons.Length; i++)
            {
                _weapons[i] = ScriptableObject.CreateInstance<WeaponData>();
                _weapons[i].displayName = "武器" + i;
                _weapons[i].maxStack = 1;
            }

            _root = new GameObject("InventoryUI");

            _panel = new GameObject("Panel", typeof(RectTransform));
            _panel.transform.SetParent(_root.transform, false);

            GameObject containerObject = new GameObject("Content", typeof(RectTransform));
            containerObject.transform.SetParent(_panel.transform, false);
            _container = containerObject.GetComponent<RectTransform>();

            GameObject inventoryObject = new GameObject("GameSystems");
            inventoryObject.transform.SetParent(_root.transform, false);
            _inventory = inventoryObject.AddComponent<InventoryComponent>();

            _slots = new InventorySlotUI[InitialSlots];
            for (int i = 0; i < InitialSlots; i++)
            {
                _slots[i] = MakeSlot(_container);
            }

            _detail = MakeDetail(_panel.transform);

            _ui = _root.AddComponent<InventoryUI>();

            // scrollRect 传 null：滚动是 ScrollRect 自己的事，
            // 这里的逻辑只关心"哪几个格子该激活"
            _ui.Bind(_panel, _inventory, null, _container, _slots, _detail);
        }

        private WeaponDetailUI MakeDetail(Transform parent)
        {
            GameObject go = new GameObject("WeaponDetail", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            _detailHint = new GameObject("EmptyHint", typeof(RectTransform));
            _detailHint.transform.SetParent(go.transform, false);

            Image icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
            icon.transform.SetParent(go.transform, false);

            Button equip = new GameObject("EquipButton", typeof(RectTransform)).AddComponent<Button>();
            equip.transform.SetParent(go.transform, false);

            WeaponDetailUI detail = go.AddComponent<WeaponDetailUI>();
            detail.Bind(
                _detailHint,
                icon,
                MakeChildText("Name", go.transform),
                MakeChildText("Description", go.transform),
                MakeChildText("Damage", go.transform),
                equip);

            return detail;
        }

        private static TextMeshProUGUI MakeChildText(string name, Transform parent)
        {
            TextMeshProUGUI text = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            return text;
        }

        [TearDown]
        public void TearDown()
        {
            // 退订要对称，否则 EventBus 里会留下指向已销毁对象的处理器，
            // 污染后面的测试（静态状态跨测试是共享的）
            if (_ui != null)
            {
                _ui.Close();
            }

            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }

            foreach (WeaponData w in _weapons)
            {
                if (w != null)
                {
                    Object.DestroyImmediate(w);
                }
            }

            // 静态状态是全局的，弄脏了会影响后面所有测试
            GamePause.Clear();
            InputLock.Clear();
        }

        private static InventorySlotUI MakeSlot(Transform parent)
        {
            GameObject go = new GameObject("Slot", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Image icon = new GameObject("Icon").AddComponent<Image>();
            icon.transform.SetParent(go.transform, false);

            TextMeshProUGUI name = new GameObject("Name").AddComponent<TextMeshProUGUI>();
            name.transform.SetParent(go.transform, false);

            TextMeshProUGUI amount = new GameObject("Amount").AddComponent<TextMeshProUGUI>();
            amount.transform.SetParent(go.transform, false);

            InventorySlotUI slot = go.AddComponent<InventorySlotUI>();
            slot.Bind(icon, name, amount);
            return slot;
        }

        private void AddItems(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _inventory.System.AddItem(_weapons[i], 1);
            }
        }

        private static int ActiveSlotCount(Transform container)
        {
            int active = 0;
            foreach (Transform child in container)
            {
                if (child.gameObject.activeSelf)
                {
                    active++;
                }
            }

            return active;
        }

        // ---------------------------------------------------------
        // 开 / 关
        // ---------------------------------------------------------

        [Test]
        public void StartsClosed()
        {
            Assert.IsFalse(_ui.IsOpen);
            Assert.IsFalse(_panel.activeSelf);
            Assert.IsFalse(GamePause.IsPaused);
        }

        [Test]
        public void Open_ShowsPanelAndPauses()
        {
            _ui.Open();

            Assert.IsTrue(_ui.IsOpen);
            Assert.IsTrue(_panel.activeSelf);
            Assert.AreEqual(0f, Time.timeScale, "开着背包时游戏应该是暂停的");
        }

        [Test]
        public void Close_HidesPanelAndResumes()
        {
            _ui.Open();

            _ui.Close();

            Assert.IsFalse(_ui.IsOpen);
            Assert.IsFalse(_panel.activeSelf);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void Open_AlsoLocksInput()
        {
            // 只暂停不够：鼠标按键照样会被读到，攻击 / 跳跃会排进 Animator 队列
            _ui.Open();

            Assert.IsTrue(InputLock.IsLocked);
        }

        [Test]
        public void OpenTwice_ThenClose_StillResumes()
        {
            _ui.Open();
            _ui.Open();

            _ui.Close();

            Assert.IsFalse(_ui.IsOpen);
            Assert.AreEqual(1f, Time.timeScale, "重复 Open 不该多计一次暂停");
        }

        [Test]
        public void Close_WhenNeverOpened_LeavesOtherPeoplesPauseAlone()
        {
            object dialogue = new object();
            GamePause.Acquire(dialogue);

            _ui.Close();

            Assert.IsTrue(GamePause.IsPaused, "没开过背包就 Close，不该把别人持有的暂停放掉");
            Assert.AreEqual(0f, Time.timeScale);

            GamePause.Release(dialogue);
        }

        [Test]
        public void CloseThenOpenAgain_WorksNormally()
        {
            // 开关一轮之后状态得是干净的：暂停数、输入锁都不能残留，
            // 否则第二次开背包就暂停不了（或者相反，关不掉）
            _ui.Open();
            _ui.Close();

            _ui.Open();

            Assert.IsTrue(_ui.IsOpen);
            Assert.IsTrue(_panel.activeSelf);
            Assert.AreEqual(0f, Time.timeScale, "重新打开应该重新暂停");
            Assert.IsTrue(InputLock.IsLocked);
        }

        // ---------------------------------------------------------
        // 刷新
        // ---------------------------------------------------------

        [Test]
        public void Open_FillsSlotsInOrder()
        {
            AddItems(2);

            _ui.Open();

            Assert.AreEqual("武器0", _slots[0].Item.Data.displayName);
            Assert.AreEqual("武器1", _slots[1].Item.Data.displayName);
            Assert.IsNull(_slots[2].Item, "多出来的空格子应该是空的");
            Assert.IsNull(_slots[3].Item);
        }

        [Test]
        public void InventoryChangedEvent_WhileOpen_UpdatesSlots()
        {
            _ui.Open();
            Assert.IsNull(_slots[0].Item);

            AddItems(1);

            // 直接发事件，而不是靠 InventoryComponent 的 OnEnable 把
            // InventorySystem.Changed 接到 EventBus 上 —— EditMode 下
            // MonoBehaviour 的生命周期回调不会触发，那条链路在这里是断的。
            // 这里验证的是 InventoryUI 这一侧的契约：开着的时候收到通知就刷新。
            EventBus.Publish(new InventoryChangedEvent());

            Assert.IsNotNull(_slots[0].Item, "开着背包时捡到东西应该立刻显示");
            Assert.AreEqual("武器0", _slots[0].Item.Data.displayName);
        }

        [Test]
        public void InventoryChangedEvent_AfterClose_IsIgnored()
        {
            _ui.Open();
            _ui.Close();

            AddItems(1);
            EventBus.Publish(new InventoryChangedEvent());

            Assert.IsNull(_slots[0].Item, "关闭之后不该还订阅着事件");

            // 关着的时候不刷新没关系，但再打开必须看到最新的
            _ui.Open();

            Assert.AreEqual("武器0", _slots[0].Item.Data.displayName);
        }

        [Test]
        public void Open_WithEmptyInventory_ShowsAllSlotsEmpty()
        {
            _ui.Open();

            foreach (InventorySlotUI slot in _slots)
            {
                Assert.IsNull(slot.Item);
            }
        }

        [Test]
        public void Open_Twice_RefreshesAgain()
        {
            _ui.Open();
            _ui.Close();

            AddItems(1);

            _ui.Open();

            Assert.AreEqual("武器0", _slots[0].Item.Data.displayName);
        }

        // ---------------------------------------------------------
        // 装不下就滚动：格子数跟着物品数长
        // ---------------------------------------------------------

        [Test]
        public void MoreItemsThanSlots_GrowsTheGrid()
        {
            AddItems(InitialSlots + 3);

            _ui.Open();

            Assert.AreEqual(
                InitialSlots + 3,
                ActiveSlotCount(_container),
                "物品比格子多的时候要长出新的格子，而不是把装不下的丢掉");
        }

        [Test]
        public void MoreItemsThanSlots_ShowsEveryItem()
        {
            AddItems(InitialSlots + 2);

            _ui.Open();

            Transform container = _container;
            int shown = 0;
            foreach (Transform child in container)
            {
                if (!child.gameObject.activeSelf)
                {
                    continue;
                }

                InventorySlotUI slot = child.GetComponent<InventorySlotUI>();
                Assert.IsNotNull(slot.Item, "第 " + shown + " 格该有东西");
                Assert.AreEqual("武器" + shown, slot.Item.Data.displayName);
                shown++;
            }

            Assert.AreEqual(InitialSlots + 2, shown);
        }

        [Test]
        public void FewerItemsThanSlots_StillShowsTheFullGrid()
        {
            AddItems(1);

            _ui.Open();

            Assert.AreEqual(
                InitialSlots,
                ActiveSlotCount(_container),
                "东西少的时候也应该保持初始格子数，空格子留着");
        }

        [Test]
        public void ItemsShrink_ExtraSlotsAreHidden()
        {
            AddItems(InitialSlots + 5);
            _ui.Open();
            Assert.AreEqual(InitialSlots + 5, ActiveSlotCount(_container));

            _inventory.System.RemoveItem(_weapons[0], 1);
            _inventory.System.RemoveItem(_weapons[1], 1);
            _inventory.System.RemoveItem(_weapons[2], 1);
            EventBus.Publish(new InventoryChangedEvent());

            Assert.AreEqual(
                InitialSlots + 2,
                ActiveSlotCount(_container),
                "东西少了要多出来的格子收起来，否则会滚动一大片空白");
        }

        [Test]
        public void SlotsGrowThenShrinkThenGrow_DoesNotDuplicate()
        {
            _ui.Open();

            AddItems(InitialSlots + 4);
            EventBus.Publish(new InventoryChangedEvent());
            int peak = ActiveSlotCount(_container);

            for (int i = 0; i < 4; i++)
            {
                _inventory.System.RemoveItem(_weapons[i], 1);
            }

            EventBus.Publish(new InventoryChangedEvent());

            AddItems(4);
            EventBus.Publish(new InventoryChangedEvent());

            Assert.AreEqual(peak, ActiveSlotCount(_container), "缩了再长应该复用已有格子");
            Assert.AreEqual(peak, _container.childCount, "不该多造格子出来");
        }

        // ---------------------------------------------------------
        // 点格子 -> 右侧详情
        // ---------------------------------------------------------

        [Test]
        public void SlotClick_ShowsThatItemInDetail()
        {
            AddItems(1);
            _ui.Open();

            _slots[0].Click();

            Assert.AreSame(_weapons[0], _detail.CurrentWeapon);
            Assert.IsFalse(_detailHint.activeSelf);
        }

        [Test]
        public void SlotClick_OnEmptySlot_LeavesDetailAlone()
        {
            AddItems(1);
            _ui.Open();
            _slots[0].Click();

            _slots[3].Click();

            Assert.AreSame(_weapons[0], _detail.CurrentWeapon, "点空格子不该把已经选中的清掉");
        }

        [Test]
        public void SlotClick_OnGrownSlot_ShowsDetail()
        {
            // 滚动扩出来的格子也要接上点击，不然滚下去点没反应
            AddItems(InitialSlots + 2);
            _ui.Open();

            InventorySlotUI grown = _container.GetChild(InitialSlots + 1).GetComponent<InventorySlotUI>();
            Assert.IsNotNull(grown.Item, "前提：这一格确实有东西");

            grown.Click();

            Assert.AreSame(_weapons[InitialSlots + 1], _detail.CurrentWeapon);
        }

        [Test]
        public void Open_ClearsTheDetailPane()
        {
            _detail.Show(new InventoryItem(_weapons[0], 1));
            Assert.IsNotNull(_detail.CurrentWeapon);

            _ui.Open();

            Assert.IsNull(_detail.CurrentWeapon, "每次打开背包都该从没选中开始");
            Assert.IsTrue(_detailHint.activeSelf);
        }

    }
}
