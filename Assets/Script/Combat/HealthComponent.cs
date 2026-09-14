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
    }
}
