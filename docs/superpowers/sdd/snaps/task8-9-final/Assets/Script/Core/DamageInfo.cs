using UnityEngine;

namespace Demo.Core
{
    /// <summary>
    /// 一次伤害结算所需的全部信息。
    /// readonly struct：按值传递、零 GC，且不可在传递途中被改写。
    /// </summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly GameObject Attacker;
        public readonly Vector3 HitPoint;
        public readonly Vector3 HitDirection;

        public DamageInfo(float amount, GameObject attacker, Vector3 hitPoint, Vector3 hitDirection)
        {
            Amount = amount;
            Attacker = attacker;
            HitPoint = hitPoint;
            HitDirection = hitDirection;
        }
    }
}
