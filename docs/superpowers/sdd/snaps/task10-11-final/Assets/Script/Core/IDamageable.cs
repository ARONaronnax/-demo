using UnityEngine;

namespace Demo.Core
{
    /// <summary>
    /// 可受伤目标。玩家与怪物都实现本接口，
    /// 使攻击方不需要知道被打的是谁。
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        Transform Transform { get; }
        void TakeDamage(in DamageInfo info);
    }
}
