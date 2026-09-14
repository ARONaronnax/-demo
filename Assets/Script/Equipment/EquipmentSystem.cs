using System;
using Demo.Data;

namespace Demo.Equipment
{
    /// <summary>
    /// 装备数据。纯 C#，不碰场景、不碰 MonoBehaviour —— 和 InventorySystem 一样，
    /// 装备信息只有这一份，PlayerStats 和武器挂点都是被它推着走的。
    ///
    /// 目前只有主手一个位置。以后要加副手/头盔再往上堆，
    /// 但别提前造一堆空槽位出来。
    /// </summary>
    public class EquipmentSystem
    {
        private WeaponData _mainHand;

        /// <summary>主手武器；空手时为 null。</summary>
        public WeaponData MainHand { get { return _mainHand; } }

        public bool HasWeapon { get { return _mainHand != null; } }

        /// <summary>装备发生变化。装上、换掉、卸下都会发一次。</summary>
        public event Action Changed;

        /// <summary>
        /// 装备到主手。传 null 是**调用方的 bug**，不是"卸下"——
        /// 卸下有专门的 Unequip，所以这里直接忽略，也不发事件。
        /// </summary>
        public void Equip(WeaponData weapon)
        {
            // 装同一把不算变化：订阅方收到事件就要重建武器模型，
            // 白发一次就是白重建一次。
            if (weapon == null || weapon == _mainHand)
            {
                return;
            }

            _mainHand = weapon;
            RaiseChanged();
        }

        public void Unequip()
        {
            if (_mainHand == null)
            {
                return;
            }

            _mainHand = null;
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            Action handler = Changed;
            if (handler != null)
            {
                handler();
            }
        }
    }
}
