using System;
using UnityEngine;
using Demo.Core;

namespace Demo.Combat
{
    public class HealthComponent : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(0f)] private float maxHp = 100f;
        [SerializeField] private string displayName = "Enemy";
        [SerializeField] private bool isPlayer;

        public float MaxHp { get { return maxHp; } }
        public float CurrentHp { get; private set; }
        public bool IsPlayer { get { return isPlayer; } }
        public bool IsAlive { get { return CurrentHp > 0f; } }
        public Transform Transform { get { return transform; } }

        public event Action<DamageInfo> Damaged;
        public event Action<DamageInfo> Died;

        private void Awake()
        {
            ResetToFull();
        }

        public void ResetToFull()
        {
            CurrentHp = maxHp;
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive)
            {
                return;
            }

            CurrentHp = Mathf.Max(0f, CurrentHp - info.Amount);
            EventBus.Publish(new EntityDamagedEvent(displayName, info.Amount, CurrentHp, isPlayer));

            if (CurrentHp <= 0f)
            {
                if (Died != null)
                {
                    Died(info);
                }

                return;
            }

            if (Damaged != null)
            {
                Damaged(info);
            }
        }

        /// <summary>恢复生命并返回实际恢复量；死亡或满血时不会恢复。</summary>
        public float Heal(float amount)
        {
            if (!IsAlive || amount <= 0f || CurrentHp >= maxHp)
            {
                return 0f;
            }

            float before = CurrentHp;
            CurrentHp = Mathf.Min(maxHp, CurrentHp + amount);
            float restored = CurrentHp - before;
            EventBus.Publish(new EntityHealedEvent(restored, CurrentHp, maxHp, isPlayer));
            return restored;
        }
    }
}
