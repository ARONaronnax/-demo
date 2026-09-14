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
            if (controller != null && !controller.IsDead)
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
        }
    }
}
