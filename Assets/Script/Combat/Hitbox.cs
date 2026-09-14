using System.Collections.Generic;
using UnityEngine;
using Demo.Core;

namespace Demo.Combat
{
    /// <summary>
    /// 攻击判定。窗口打开期间逐帧做球形重叠查询，
    /// 同一次窗口内对同一目标只结算一次伤害。
    ///
    /// 刻意不使用 OnTriggerEnter：触发回调依赖 Rigidbody / isTrigger /
    /// Layer 碰撞矩阵三处配置全部正确，任一处配错都会静默失效。
    /// 显式重叠查询零配置、行为确定。
    /// </summary>
    public class Hitbox : MonoBehaviour
    {
        [Tooltip("非空则使用 PlayerStats.FinalDamage，否则使用 flatDamage")]
        [SerializeField] private Player.PlayerStats stats;

        [SerializeField] private float flatDamage = 10f;
        [SerializeField] private LayerMask targetLayers = ~0;
        [SerializeField] private float radius = 0.8f;
        [SerializeField] private Vector3 localOffset = Vector3.zero;

        private readonly Collider[] _buffer = new Collider[16];
        private readonly HashSet<IDamageable> _hitThisWindow = new HashSet<IDamageable>();

        private bool _windowOpen;

        // 本次判定窗口的附加属性，窗口关闭时清掉，避免带到下一次攻击
        private bool _knockdown;
        private float _damageMultiplier = 1f;

        /// <summary>
        /// 标记下一次判定窗口打出的伤害是否击倒目标。
        /// 攻击方在开窗前设置，例如怪物只有第三段攻击击倒玩家。
        /// </summary>
        public void SetKnockdown(bool value)
        {
            _knockdown = value;
        }

        /// <summary>
        /// 由动画事件触发：打开判定窗口并清空本窗口的命中记录。
        /// multiplier 为本次攻击的伤害倍率，连招最后一段会大于 1。
        /// </summary>
        public void OpenWindow(float multiplier = 1f)
        {
            _damageMultiplier = multiplier;
            _hitThisWindow.Clear();
            _windowOpen = true;
        }

        /// <summary>由动画事件触发：关闭判定窗口。</summary>
        public void CloseWindow()
        {
            _windowOpen = false;
            _hitThisWindow.Clear();

            // 击倒与倍率都是一次性的，跟着窗口一起清，
            // 否则连招结束后倍率会一直挂着。
            _knockdown = false;
            _damageMultiplier = 1f;
        }

        private void OnDisable()
        {
            CloseWindow();
        }

        private void Update()
        {
            if (!_windowOpen)
            {
                return;
            }

            Vector3 center = transform.TransformPoint(localOffset);

            int count = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                _buffer,
                targetLayers,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider col = _buffer[i];

                if (col == null)
                {
                    continue;
                }

                IDamageable target = col.GetComponentInParent<IDamageable>();

                if (target == null)
                {
                    continue;
                }

                if (!target.IsAlive)
                {
                    continue;
                }

                // 不伤害持有者自身
                if (target.Transform == transform.root ||
                    target.Transform == transform)
                {
                    continue;
                }

                if (_hitThisWindow.Contains(target))
                {
                    continue;
                }

                _hitThisWindow.Add(target);

                Vector3 hitPoint = col.ClosestPoint(center);
                Vector3 direction = (target.Transform.position - center).normalized;

                var info = new DamageInfo(
                    ResolveDamage(),
                    gameObject,
                    hitPoint,
                    direction,
                    _knockdown);

                target.TakeDamage(info);
            }
        }

        private float ResolveDamage()
        {
            if (stats != null)
            {
                return stats.FinalDamage * _damageMultiplier;
            }

            return flatDamage * _damageMultiplier;
        }
    }
}
