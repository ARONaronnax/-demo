using System;
using System.Collections.Generic;
using Demo.Data;

namespace Demo.Inventory
{
    /// <summary>
    /// 纯逻辑背包。不认识 UI、不认识场景、不认识 EventBus。
    /// 数据唯一来源，UI 只读不写。
    ///
    /// 堆叠规则由 ItemData.maxStack 决定：
    ///   maxStack &lt;= 1  每件物品各占一格（武器就是这样）
    ///   maxStack &gt;  1  先填满已有格子的剩余空间，再开新格
    /// </summary>
    public class InventorySystem
    {
        private readonly List<InventoryItem> _items = new List<InventoryItem>();

        /// <summary>背包内容变化。UI 订阅它来刷新，不要轮询。</summary>
        public event Action Changed;

        /// <summary>
        /// 只读视图，且是实时的（不是快照）——拿到的对象会随后续增删而变。
        /// 返回接口而非 List，防止外部直接 Add/Remove 绕过本类。
        /// </summary>
        public IReadOnlyList<InventoryItem> GetItems()
        {
            return _items;
        }

        /// <summary>背包里该物品的总数，跨所有堆叠格累加。</summary>
        public int GetAmount(ItemData data)
        {
            if (data == null)
            {
                return 0;
            }

            int total = 0;

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Data == data)
                {
                    total += _items[i].Amount;
                }
            }

            return total;
        }

        public bool HasItem(ItemData data, int amount = 1)
        {
            if (data == null || amount <= 0)
            {
                return false;
            }

            return GetAmount(data) >= amount;
        }

        /// <summary>
        /// 放入物品。amount 超过 maxStack 时会自动拆成多格。
        /// data 为空或 amount 非正时返回 false 且不触发 Changed。
        /// </summary>
        public bool AddItem(ItemData data, int amount = 1)
        {
            if (data == null || amount <= 0)
            {
                return false;
            }

            int maxStack = Math.Max(1, data.maxStack);
            int remaining = amount;

            // 先往已有的格子里塞，塞不下的部分才是新格。
            for (int i = 0; i < _items.Count && remaining > 0; i++)
            {
                InventoryItem entry = _items[i];

                if (entry.Data != data)
                {
                    continue;
                }

                int room = maxStack - entry.Amount;

                if (room <= 0)
                {
                    continue;
                }

                int moved = Math.Min(room, remaining);
                entry.Amount += moved;
                remaining -= moved;
            }

            while (remaining > 0)
            {
                int take = Math.Min(maxStack, remaining);
                _items.Add(new InventoryItem(data, take));
                remaining -= take;
            }

            RaiseChanged();
            return true;
        }

        /// <summary>
        /// 取出物品。库存不足时返回 false 且**不做任何改动**（不是部分取出）。
        /// </summary>
        public bool RemoveItem(ItemData data, int amount = 1)
        {
            if (data == null || amount <= 0)
            {
                return false;
            }

            if (GetAmount(data) < amount)
            {
                return false;
            }

            int remaining = amount;

            for (int i = 0; i < _items.Count && remaining > 0;)
            {
                InventoryItem entry = _items[i];

                if (entry.Data != data)
                {
                    i++;
                    continue;
                }

                int taken = Math.Min(entry.Amount, remaining);
                entry.Amount -= taken;
                remaining -= taken;

                if (entry.Amount <= 0)
                {
                    _items.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }

            RaiseChanged();
            return true;
        }

        private void RaiseChanged()
        {
            if (Changed != null)
            {
                Changed();
            }
        }
    }
}
