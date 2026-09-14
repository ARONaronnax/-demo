using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Demo.Core;
using Demo.Data;
using Demo.Inventory;

namespace Demo.UI
{
    /// <summary>
    /// 背包右侧的详情区：显示选中的物品，是武器的话多显示攻击力和装备按钮。
    ///
    /// 这里**没有装备逻辑** —— 点装备只是往总线上发一个 EquipRequestedEvent，
    /// 真正改数据的是 EquipmentComponent / EquipmentSystem。
    /// UI 不碰核心游戏数据，这是硬规矩。
    ///
    /// 也只读不写：物品数据从 InventoryItem 拿，自己不缓存副本，
    /// 唯一留着的是 CurrentWeapon 这个"当前选中的是哪把"的指针。
    /// </summary>
    public class WeaponDetailUI : MonoBehaviour
    {
        [SerializeField] private GameObject emptyHint;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private TMP_Text damageLabel;
        [SerializeField] private Button equipButton;

        /// <summary>当前选中的武器；选中的不是武器时为 null。</summary>
        public WeaponData CurrentWeapon { get; private set; }

        /// <summary>给编辑器脚本和测试用的注入点（Inspector 里拖也行）。</summary>
        public void Bind(
            GameObject hint,
            Image icon,
            TMP_Text name,
            TMP_Text description,
            TMP_Text damage,
            Button equip)
        {
            emptyHint = hint;
            iconImage = icon;
            nameLabel = name;
            descriptionLabel = description;
            damageLabel = damage;
            equipButton = equip;

            Clear();
        }

        public void Show(InventoryItem item)
        {
            if (item == null || item.Data == null)
            {
                Clear();
                return;
            }

            ItemData data = item.Data;

            if (emptyHint != null)
            {
                emptyHint.SetActive(false);
            }

            if (nameLabel != null)
            {
                nameLabel.text = data.displayName;
            }

            if (descriptionLabel != null)
            {
                descriptionLabel.text = data.description;
            }

            if (iconImage != null)
            {
                iconImage.sprite = data.icon;
                iconImage.enabled = data.icon != null;
            }

            // 先决定"这是不是武器"，后面显示什么全看它
            CurrentWeapon = data as WeaponData;

            if (CurrentWeapon != null)
            {
                if (damageLabel != null)
                {
                    damageLabel.text = "攻击力 +" + CurrentWeapon.damage;
                }

                if (equipButton != null)
                {
                    equipButton.gameObject.SetActive(true);
                }
            }
            else
            {
                // 药水之类的没有攻击力也没有装备按钮。
                // 这里必须把 CurrentWeapon 清掉，否则先点武器再点药水，
                // 再按装备会把上一把武器装上去。
                if (damageLabel != null)
                {
                    damageLabel.text = string.Empty;
                }

                if (equipButton != null)
                {
                    equipButton.gameObject.SetActive(false);
                }
            }
        }

        public void Clear()
        {
            CurrentWeapon = null;

            if (emptyHint != null)
            {
                emptyHint.SetActive(true);
            }

            if (nameLabel != null)
            {
                nameLabel.text = string.Empty;
            }

            if (descriptionLabel != null)
            {
                descriptionLabel.text = string.Empty;
            }

            if (damageLabel != null)
            {
                damageLabel.text = string.Empty;
            }

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            if (equipButton != null)
            {
                equipButton.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 装备按钮的点击入口，由 Button 的持久化监听调用。
        ///
        /// 只是往总线上喊一声"想装这把"，谁去装、装到哪都不关这里的事 ——
        /// 由 EquipmentComponent 接。UI 不碰核心游戏数据，这是硬规矩。
        /// </summary>
        public void OnEquipClicked()
        {
            if (CurrentWeapon == null)
            {
                return;
            }

            EventBus.Publish(new EquipRequestedEvent(CurrentWeapon));
        }
    }
}
