using UnityEngine;
using Demo.Combat;
using Demo.Core;

namespace Demo.Player
{
    /// <summary>
    /// HealthComponent 与 PlayerController 之间的桥。
    /// HealthComponent 不认识 PlayerController，PlayerController 也不认识
    /// HealthComponent——受击/死亡表现由本类牵线。
    /// </summary>
    public class PlayerHealthBridge : MonoBehaviour
    {
        [SerializeField] private HealthComponent health;
        [SerializeField] private PlayerController controller;

        private void Reset()
        {
            health = GetComponent<HealthComponent>();
            controller = GetComponent<PlayerController>();
        }

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<HealthComponent>();
            }

            if (controller == null)
            {
                controller = GetComponent<PlayerController>();
            }
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }
        }

        private void OnDamaged(DamageInfo info)
        {
            if (controller == null || controller.IsDead)
            {
                return;
            }

            // 带击倒标记的伤害（蜥蜴怪第三段攻击）走倒地流程
            if (info.CausesKnockdown)
            {
                controller.Knockdown();
                return;
            }

            // 已经倒地时不再叠加普通受击，否则会从 Die01 被打回 GetHit
            if (!controller.IsKnockedDown)
            {
                controller.GetHit();
            }
        }

        private void OnDied(DamageInfo info)
        {
            if (controller != null)
            {
                controller.Die();
            }

            // 广播出去，怪物据此脱战。EnemyAI 不引用 PlayerController，
            // 由事件牵线。
            EventBus.Publish(new PlayerDiedEvent(transform.position));
        }
    }
}
