using System;
using UnityEngine;
using Demo.Core;

namespace Demo.Combat
{
    /// <summary>
    /// 通用血量。玩家与怪物共用。
    /// 本类不认识任务、不认识掉落物——只负责扣血与广播。
    /// </summary>
    public class HealthComponent : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHp = 100f;
        [SerializeField] private string displayName = "Enemy";
        [SerializeField] private bool isPlayer;

        public float MaxHp { get { return maxHp; } }
        public float CurrentHp { get; private set; }
        public bool IsPlayer { get { return isPlayer; } }

        public bool IsAlive
        {
            get { return CurrentHp > 0f; }
        }

        public Transform Transform
        {
            get { return transform; }
        }

        /// <summary>受击但未死亡。</summary>
        public event Action<DamageInfo> Damaged;

        /// <summary>死亡。只会触发一次。</summary>
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

            CurrentHp -= info.Amount;

            if (CurrentHp <= 0f)
            {
                CurrentHp = 0f;

                EventBus.Publish(new EntityDamagedEvent(displayName, info.Amount, CurrentHp, isPlayer));

                if (Died != null)
                {
                    Died(info);
                }

                return;
            }

            EventBus.Publish(new EntityDamagedEvent(displayName, info.Amount, CurrentHp, isPlayer));

            if (Damaged != null)
            {
                Damaged(info);
            }
        }
    }
}
