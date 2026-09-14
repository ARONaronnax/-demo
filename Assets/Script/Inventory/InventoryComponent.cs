using System.Collections.Generic;
using UnityEngine;
using Demo.Core;
using Demo.Data;

namespace Demo.Inventory
{
    /// <summary>
    /// 背包桥接。持有唯一的 InventorySystem 实例，
    /// 把拾取事件转成 AddItem，再把 C# 事件转发为 EventBus 事件供 UI 使用。
    ///
    /// DroppedItem 和 InventorySystem 互不认识，本类是唯一的连接点。
    /// 场景里挂一个即可（挂在 Player 上）。
    /// </summary>
    public class InventoryComponent : MonoBehaviour
    {
        private readonly InventorySystem _system = new InventorySystem();
        private bool _listening;

        /// <summary>当前拿在手上的武器。装备是把东西从背包挪到手上，得记住上一把是谁。</summary>
        private WeaponData _equipped;

        /// <summary>只读访问入口。UI 通过它拿数据，不要自己存一份。</summary>
        public InventorySystem System { get { return _system; } }

        public IReadOnlyList<InventoryItem> GetItems()
        {
            return _system.GetItems();
        }

        public int GetAmount(ItemData data)
        {
            return _system.GetAmount(data);
        }

        public bool HasItem(ItemData data, int amount = 1)
        {
            return _system.HasItem(data, amount);
        }

        private void OnEnable()
        {
            Listen();
        }

        private void OnDisable()
        {
            StopListening();
        }

        /// <summary>
        /// 订阅动作两边共用一个方法，幂等。
        /// 为什么不能只靠 OnEnable：EditMode 下 AddComponent 不触发 OnEnable，
        /// 那样这个类在测试里一行都覆盖不到 —— 和 EquipmentComponent / WeaponSocket 同一个理由。
        ///
        /// 那两个类靠 Bind(...) 当入口所以 Listen 是私有的；这里没有要注入的东西，
        /// 就公开出来给测试调，和 StopListening 配成一对。
        /// </summary>
        public void Listen()
        {
            if (_listening)
            {
                return;
            }

            _listening = true;

            EventBus.Subscribe<ItemPickedUpEvent>(OnItemPickedUp);
            EventBus.Subscribe<WeaponEquippedEvent>(OnWeaponEquipped);

            _system.Changed += OnInventoryChanged;
        }

        /// <summary>退订。OnDisable 会调；EditMode 下 OnDisable 不触发，测试得自己调。</summary>
        public void StopListening()
        {
            if (!_listening)
            {
                return;
            }

            _listening = false;

            EventBus.Unsubscribe<ItemPickedUpEvent>(OnItemPickedUp);
            EventBus.Unsubscribe<WeaponEquippedEvent>(OnWeaponEquipped);

            _system.Changed -= OnInventoryChanged;
        }

        /// <summary>
        /// 装备 = 东西从背包挪到手上，所以这里要维持背包和手的一致性：
        /// 拿上手的移出背包，换下来的退回背包。
        ///
        /// 为什么归背包管：EquipRequestedEvent 那边只管改 PlayerStats 和广播结果，
        /// 从头到尾没碰过 InventorySystem。而"什么在背包里"是背包自己的事，
        /// 让它订阅结果事件去跟，比给 EquipmentComponent 塞一个背包引用干净 ——
        /// 也不用多一条场景接线。
        /// </summary>
        private void OnWeaponEquipped(WeaponEquippedEvent e)
        {
            // 换下来的退回背包。
            //
            // 这里**故意不判断"同一把不算换"**：那样写看着更严谨，实际会吃掉玩家的东西 ——
            // 背包里若还剩一把备用同款，AddItem 会并进那一格，紧跟的 RemoveItem
            // 就正好把备用扣掉。不判断的话 Add 和 Remove 自己抵消，备用还在。
            // （真实链路里 EquipmentSystem.Equip 对同一把本来就早退，重复事件到不了这儿。）
            if (_equipped != null)
            {
                _system.AddItem(_equipped);
            }

            // 拿上手的移出背包。本来就不在背包里时 RemoveItem 返回 false 什么都不做，
            // 不能反过来往背包里塞东西。
            if (e.Weapon != null)
            {
                _system.RemoveItem(e.Weapon);
            }

            _equipped = e.Weapon;
        }

        private void OnItemPickedUp(ItemPickedUpEvent e)
        {
            if (e.Item == null)
            {
                return;
            }

            _system.AddItem(e.Item, e.Amount);
        }

        private void OnInventoryChanged()
        {
            EventBus.Publish(new InventoryChangedEvent());
        }
    }
}
