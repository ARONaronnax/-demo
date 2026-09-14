using UnityEngine;
using Demo.Core;
using Demo.Data;
using Demo.Player;

namespace Demo.Equipment
{
    /// <summary>
    /// 装备桥接。持有一份 EquipmentSystem，把 UI 的"想装这把"落到 PlayerStats 上，
    /// 再广播 WeaponEquippedEvent 给武器挂点（Phase 07 接）。
    ///
    /// 位置和 InventoryComponent 一样：纯数据在 System 里，
    /// Unity 那一侧的事（改攻击力、发事件）在这里。
    /// 本类是 EquipRequestedEvent 唯一的订阅方 —— UI 和核心数据之间就隔着这一层。
    /// </summary>
    public class EquipmentComponent : MonoBehaviour
    {
        [Tooltip("留空则该组件只维护装备数据与广播事件，不改攻击力")]
        [SerializeField] private PlayerStats playerStats;

        [Tooltip("开局就握在手上的武器。留空则空手开局")]
        [SerializeField] private WeaponData startingWeapon;

        private readonly EquipmentSystem _system = new EquipmentSystem();
        private bool _listening;

        /// <summary>只读访问入口。想改装备请走 System，不要另存一份。</summary>
        public EquipmentSystem System { get { return _system; } }

        public WeaponData MainHand { get { return _system.MainHand; } }

        /// <summary>注入开局武器。给编辑器脚本和测试用（Inspector 里拖也行）。</summary>
        public void SetStartingWeapon(WeaponData weapon)
        {
            startingWeapon = weapon;
        }

        /// <summary>
        /// 把开局武器握到手上。Start 会调；EditMode 下 Start 不触发，测试得自己调。
        ///
        /// 为什么要有这个：MC01 模型出厂时 weapon_r 底下就嵌着一把 OHS09_Sword，
        /// 那把剑就是玩家的开局武器。不把它登记进 EquipmentSystem 的话，
        /// 换成别的武器时它只会被销毁 —— 凭空消失，也不会退回背包。
        /// </summary>
        public void EquipStartingWeapon()
        {
            if (startingWeapon == null)
            {
                return;
            }

            // Equip 对同一把是幂等的，所以 Start 被重放也不会重复广播
            _system.Equip(startingWeapon);
        }

        private void Start()
        {
            EquipStartingWeapon();
        }

        /// <summary>
        /// 注入 PlayerStats 并开始监听。给编辑器脚本和测试用（Inspector 里拖也行）。
        ///
        /// 为什么不能只靠 OnEnable：EditMode 下 AddComponent 不触发 OnEnable，
        /// 那样这个类在测试里一行都覆盖不到。订阅动作两边共用一个方法，幂等。
        /// </summary>
        public void Bind(PlayerStats stats)
        {
            playerStats = stats;

            Listen();
        }

        private void OnEnable()
        {
            Listen();
        }

        private void OnDisable()
        {
            StopListening();
        }

        private void Listen()
        {
            if (_listening)
            {
                return;
            }

            _listening = true;

            EventBus.Subscribe<EquipRequestedEvent>(OnEquipRequested);

            _system.Changed += OnEquipmentChanged;
        }

        /// <summary>退订。OnDisable 会调；EditMode 下 OnDisable 不触发，测试得自己调。</summary>
        public void StopListening()
        {
            if (!_listening)
            {
                return;
            }

            _listening = false;

            EventBus.Unsubscribe<EquipRequestedEvent>(OnEquipRequested);

            _system.Changed -= OnEquipmentChanged;
        }

        private void OnEquipRequested(EquipRequestedEvent e)
        {
            // UI 只是喊了一声"想装这把"，装不装、怎么装由这里定
            _system.Equip(e.Weapon);
        }

        private void OnEquipmentChanged()
        {
            // 攻击力由 PlayerStats / DamageCalculator 算，UI 不得自行计算
            if (playerStats != null)
            {
                playerStats.SetWeapon(_system.MainHand);
            }

            // 卸下也要发（Weapon 为 null），否则武器挂点不知道要摘模型
            EventBus.Publish(new WeaponEquippedEvent(_system.MainHand));
        }
    }
}
