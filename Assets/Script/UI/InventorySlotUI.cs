using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Demo.Inventory;

namespace Demo.UI
{
    /// <summary>
    /// 背包里的一个格子。只负责"照着 InventoryItem 把字和图标显示出来"，
    /// 不持有数据、不修改数据 —— 数据永远只有 InventorySystem 一份。
    ///
    /// 点击行为留给 Phase 05 接（那时候才需要"点武器 -> 显示详情"）。
    /// </summary>
    public class InventorySlotUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text amountLabel;

        /// <summary>当前显示的物品，空格子为 null。</summary>
        public InventoryItem Item { get; private set; }

        /// <summary>被点击时抛出自己，由 InventoryUI 决定拿它干什么。</summary>
        public event Action<InventorySlotUI> Clicked;

        /// <summary>
        /// 由格子上的 Button 调用（编辑器脚本挂的是持久化监听，
        /// 所以在场景里就接好了，不依赖运行时生命周期回调）。
        /// </summary>
        public void Click()
        {
            if (Item == null || Item.Data == null)
            {
                return;
            }

            Action<InventorySlotUI> handler = Clicked;
            if (handler != null)
            {
                handler(this);
            }
        }

        /// <summary>给编辑器脚本 / 测试用的注入点（Inspector 里拖也行）。</summary>
        public void Bind(Image icon, TMP_Text name, TMP_Text amount)
        {
            iconImage = icon;
            nameLabel = name;
            amountLabel = amount;
        }

        public void SetItem(InventoryItem item)
        {
            if (item == null || item.Data == null)
            {
                Clear();
                return;
            }

            Item = item;

            nameLabel.text = item.Data.displayName;

            // 数量为 1 时不显示，避免满屏的 "x1" 噪声
            amountLabel.text = item.Amount > 1 ? item.Amount.ToString() : string.Empty;

            // 没配图标的物品就是没有，别留一块空白图
            iconImage.sprite = item.Data.icon;
            iconImage.enabled = item.Data.icon != null;
        }

        public void Clear()
        {
            Item = null;

            nameLabel.text = string.Empty;
            amountLabel.text = string.Empty;

            iconImage.sprite = null;
            iconImage.enabled = false;
        }
    }
}
