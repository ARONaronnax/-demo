using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Demo.Core;
using Demo.Inventory;

namespace Demo.UI
{
    /// <summary>
    /// 背包面板。只做三件事：开关面板、把 InventorySystem 的数据铺到格子上、暂停游戏。
    ///
    /// 这里**不存任何物品数据** —— 数据只有 InventorySystem 一份，
    /// 本类每次刷新都重新问它要。UI 是数据的投影，不是副本。
    ///
    /// 数据流：
    ///     InventorySystem.Changed -> InventoryComponent -> EventBus(InventoryChangedEvent)
    ///         -> 本类 Refresh() -> 每个 InventorySlotUI.SetItem()
    ///
    /// 格子数不是固定的：场景里摆好的那批是"至少显示这么多"，物品超过之后
    /// 按需复制出新的格子，配合 ScrollRect 滚动查看。少了就收起来，
    /// 免得滚动区拖出一大片空白。收回来的格子留在池子里复用，不反复销毁重建。
    ///
    /// Update 里读 I / ESC 是安全的：Time.timeScale = 0 不影响 Update，
    /// 被冻结的是 FixedUpdate、deltaTime 和 Animator。所以暂停之后还能按键关背包。
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private InventoryComponent inventory;
        [SerializeField] private RectTransform slotContainer;
        [SerializeField] private InventorySlotUI[] slots;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private WeaponDetailUI detail;

        [SerializeField] private KeyCode toggleKey = KeyCode.I;
        [SerializeField] private KeyCode closeKey = KeyCode.Escape;

        private readonly List<InventorySlotUI> _pool = new List<InventorySlotUI>();

        /// <summary>场景里摆好的格子数 —— 也是不管有没有东西都至少显示的格数。</summary>
        private int _minimumSlots;

        // 打开前先把光标状态记下来，关闭时原样还回去，
        // 不硬编码成 Locked —— 万一是别的界面锁的 / 或者根本没锁。
        private CursorLockMode _cursorLockBefore;
        private bool _cursorVisibleBefore;

        public bool IsOpen { get; private set; }

        /// <summary>给编辑器脚本和测试用的注入点（Inspector 里拖也行）。</summary>
        public void Bind(
            GameObject panelObject,
            InventoryComponent inventoryComponent,
            ScrollRect scroll,
            RectTransform container,
            InventorySlotUI[] slotViews,
            WeaponDetailUI detailView)
        {
            panel = panelObject;
            inventory = inventoryComponent;
            scrollRect = scroll;
            slotContainer = container;
            slots = slotViews;
            detail = detailView;

            _pool.Clear();
            _minimumSlots = 0;

            // 立刻落到关闭态，省得调用方还要记得手动关一次面板
            ApplyOpenState();
        }

        private void Awake()
        {
            BuildPool();
            ApplyOpenState();
        }

        private void OnDisable()
        {
            // 开着背包时组件被禁用 / 物体被销毁，必须把暂停和输入锁收回来。
            // 少了这一句，timeScale 就永远停在 0，游戏再也动不了。
            Close();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                if (IsOpen)
                {
                    Close();
                }
                else
                {
                    Open();
                }

                return;
            }

            if (IsOpen && Input.GetKeyDown(closeKey))
            {
                Close();
            }
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;

            _cursorLockBefore = Cursor.lockState;
            _cursorVisibleBefore = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // 暂停 + 屏蔽输入一起上：
            //   GamePause 让怪和动画停下来
            //   InputLock 让攻击 / 翻滚 / 跳跃的按键不再排队（光靠 timeScale 拦不住）
            GamePause.Acquire(this);
            InputLock.Acquire(this);

            // 只在打开期间订阅：关着的时候面板看不见，没有刷新的必要，
            // 也就不用留一个没人消费的处理器在 EventBus 里。
            EventBus.Subscribe<InventoryChangedEvent>(OnInventoryChanged);

            ApplyOpenState();

            // 每次打开都从"没选中"开始：上次关背包时选中的那把
            // 可能已经被卖掉了，留着详情区显示过期的东西更容易误导
            if (detail != null)
            {
                detail.Clear();
            }

            Refresh();

            // 每次打开都从顶上开始看，不要停在上一轮的滚动位置
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;

            EventBus.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);

            GamePause.Release(this);
            InputLock.Release(this);

            Cursor.lockState = _cursorLockBefore;
            Cursor.visible = _cursorVisibleBefore;

            ApplyOpenState();
        }

        /// <summary>把当前背包数据铺到格子上，物品多了就补格子、少了就收起来。</summary>
        public void Refresh()
        {
            BuildPool();

            IReadOnlyList<InventoryItem> items = inventory != null ? inventory.GetItems() : null;
            int count = items != null ? items.Count : 0;

            int visible = Mathf.Max(count, _minimumSlots);
            EnsureSlotCount(visible);

            for (int i = 0; i < _pool.Count; i++)
            {
                InventorySlotUI slot = _pool[i];

                if (i >= visible)
                {
                    // 不销毁，只是收起来：LayoutGroup 会跳过非激活的子物体，
                    // 滚动区的高度自然跟着缩回去
                    slot.gameObject.SetActive(false);
                    continue;
                }

                slot.gameObject.SetActive(true);

                if (i < count)
                {
                    slot.SetItem(items[i]);
                }
                else
                {
                    slot.Clear();
                }
            }
        }

        /// <summary>把场景里摆好的那批格子收进池子，只需要做一次。</summary>
        private void BuildPool()
        {
            if (_pool.Count > 0 || slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                TrackSlot(slots[i]);
            }

            _minimumSlots = _pool.Count;
        }

        private void TrackSlot(InventorySlotUI slot)
        {
            if (slot == null)
            {
                return;
            }

            slot.Clicked += OnSlotClicked;
            _pool.Add(slot);
        }

        private void OnSlotClicked(InventorySlotUI slot)
        {
            if (detail != null)
            {
                detail.Show(slot.Item);
            }
        }

        /// <summary>按需扩池。复刻第一个格子，比预制体少一份要维护的资源。</summary>
        private void EnsureSlotCount(int required)
        {
            if (_pool.Count == 0 || _pool.Count >= required)
            {
                return;
            }

            InventorySlotUI template = _pool[0];
            Transform parent = slotContainer != null ? slotContainer : template.transform.parent;

            while (_pool.Count < required)
            {
                GameObject clone = Instantiate(template.gameObject, parent);
                clone.name = template.gameObject.name + "_" + _pool.Count;

                // 新格子也要接上点击，不然滚出来那批点了没反应
                TrackSlot(clone.GetComponent<InventorySlotUI>());
            }
        }

        private void ApplyOpenState()
        {
            if (panel != null)
            {
                panel.SetActive(IsOpen);
            }
        }

        private void OnInventoryChanged(InventoryChangedEvent e)
        {
            // 只在打开期间被订阅，所以这里不用再判断开关状态
            Refresh();
        }
    }
}
