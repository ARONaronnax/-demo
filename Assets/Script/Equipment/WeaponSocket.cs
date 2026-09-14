using UnityEngine;
using Demo.Core;
using Demo.Data;

namespace Demo.Equipment
{
    /// <summary>
    /// 武器挂点。装备一变就把手持模型挂到右手骨骼上，再按 WeaponData 里的
    /// socketLocal* 摆好姿势。订阅 WeaponEquippedEvent，不认识 EquipmentSystem。
    ///
    /// 挂点用的是 MC01 骨骼自带的 weapon_r（hand_r 的子物体），没有另造一个 ——
    /// 那个骨骼位置就在握把上，而且美术已经调好了朝向。
    ///
    /// 只往骨骼底下加子物体，绝不碰骨骼自己的变换：
    /// 动了它模型会歪，还可能连带动画表现。
    /// </summary>
    public class WeaponSocket : MonoBehaviour
    {
        [Tooltip("手持模型的父物体，挂到 MC01 的 weapon_r 骨骼上")]
        [SerializeField] private Transform socket;

        private GameObject _held;
        private bool _listening;

        /// <summary>当前挂在骨骼上的模型实例；没挂东西时为 null。</summary>
        public GameObject HeldInstance { get { return _held; } }

        /// <summary>当前挂着的模型是哪把武器。没有模型时为 null。</summary>
        public WeaponData HeldWeapon { get; private set; }

        /// <summary>
        /// 注入挂点骨骼并开始监听。给编辑器脚本和测试用（Inspector 里拖也行）。
        ///
        /// 和 EquipmentComponent 同样的理由：EditMode 下 AddComponent 不触发 OnEnable，
        /// 只靠生命周期回调订阅的话这个类在测试里零覆盖。
        /// </summary>
        public void Bind(Transform socketTransform)
        {
            socket = socketTransform;

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

            EventBus.Subscribe<WeaponEquippedEvent>(OnWeaponEquipped);
        }

        /// <summary>退订。OnDisable 会调；EditMode 下 OnDisable 不触发，测试得自己调。</summary>
        public void StopListening()
        {
            if (!_listening)
            {
                return;
            }

            _listening = false;

            EventBus.Unsubscribe<WeaponEquippedEvent>(OnWeaponEquipped);
        }

        private void OnWeaponEquipped(WeaponEquippedEvent e)
        {
            // 卸下时 Weapon 是 null，这里照样走一遍 —— 模型会被摘掉
            Show(e.Weapon);
        }

        /// <summary>
        /// 把武器模型换到手上。传 null 等于摘掉。
        ///
        /// 每次都是先摘后挂：同一个 prefab 拎在手里和掉在地上姿态不同，
        /// 所以 prefab 自带的变换会被 socketLocal* 整个覆盖掉。
        /// </summary>
        public void Show(WeaponData weapon)
        {
            Clear();

            // 没模型（还没做）或者挂点没接，就只是空着手，
            // 别留个空 GameObject 挂在骨骼底下
            if (weapon == null || weapon.weaponPrefab == null || socket == null)
            {
                return;
            }

            _held = Instantiate(weapon.weaponPrefab, socket, false);
            _held.name = "Held_" + weapon.displayName;

            Transform held = _held.transform;
            held.localPosition = weapon.socketLocalPosition;
            held.localEulerAngles = weapon.socketLocalEuler;
            held.localScale = weapon.socketLocalScale;

            HeldWeapon = weapon;
        }

        /// <summary>
        /// 把挂点底下的手持模型全摘掉（传 null 给 Show 也走这里）。
        ///
        /// 摘的是**挂点下所有子物体**，不只自己实例化的那个：
        /// MC01.prefab 的 weapon_r 底下本来就嵌着一把美术预摆的 OHS09_Sword，
        /// 它不是本组件 Instantiate 出来的。不连它一起摘，换上别的武器时
        /// 那把原生剑还原地不动 —— 右手上就是两把，正是最初报的那个现象。
        /// 整个挂点归本组件管，所以一次清干净是安全的。
        /// </summary>
        public void Clear()
        {
            if (socket != null)
            {
                // 倒着遍历：编辑器分支走 DestroyImmediate，会当场把子物体移走
                for (int i = socket.childCount - 1; i >= 0; i--)
                {
                    Remove(socket.GetChild(i).gameObject);
                }
            }

            _held = null;
            HeldWeapon = null;
        }

        private static void Remove(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            // 先关掉，别指望 Destroy 立刻见效。
            //
            // Destroy 是延迟到帧末才执行的：从 Show() 返回到本帧结束之间，
            // 模型还挂在骨骼上、activeInHierarchy 还是 true ——
            // 换武器就会看到两把叠在一起。DestroyImmediate 没这问题，
            // 所以 EditMode 测试一直是绿的，只有 Play 模式才照得出来。
            target.SetActive(false);

            // Destroy 只在 Play 模式有效，在编辑器里调它会报错，
            // 而 EditMode 测试跑在编辑器里，所以这里得分个岔
            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
