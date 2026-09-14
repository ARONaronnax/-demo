namespace Demo.Enemy
{
    public enum EnemyState
    {
        Idle,
        Chase,
        Attack,
        Hit,
        Dead
    }

    public readonly struct EnemySensors
    {
        public readonly bool HasTarget;
        public readonly float DistanceToTarget;
        public readonly bool HasLineOfSight;

        public EnemySensors(bool hasTarget, float distanceToTarget, bool hasLineOfSight)
        {
            HasTarget = hasTarget;
            DistanceToTarget = distanceToTarget;
            HasLineOfSight = hasLineOfSight;
        }
    }

    public readonly struct EnemyIntents
    {
        public readonly bool Move;
        public readonly bool FaceTarget;
        public readonly bool TriggerAttack;

        public EnemyIntents(bool move, bool faceTarget, bool triggerAttack)
        {
            Move = move;
            FaceTarget = faceTarget;
            TriggerAttack = triggerAttack;
        }

        public static EnemyIntents None
        {
            get { return new EnemyIntents(false, false, false); }
        }
    }
}
