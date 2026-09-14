namespace Demo.Enemy
{
    /// <summary>
    /// 怪物状态机纯逻辑。不依赖任何场景对象，可直接单元测试。
    /// Idle → Chase → Attack → Hit → Dead，Dead 为不可回退终态。
    ///
    /// 收招后有一段 attackCooldown 的冷却，冷却走完前不会再次进入 Attack。
    /// 冷却与动画事件无关，所以 AttackEnd 事件漏打、或动画本身很长很短，
    /// 出手间隔都稳定。
    /// </summary>
    public class EnemyBrain
    {
        private readonly float _detectRange;
        private readonly float _attackRange;
        private readonly float _leashRange;
        private readonly bool _requireLineOfSight;
        private readonly float _attackCooldown;

        // 剩余冷却秒数，大于 0 时不许进入 Attack
        private float _cooldownTimer;

        public EnemyState State { get; private set; }

        public EnemyBrain(
            float detectRange,
            float attackRange,
            float leashRange,
            bool requireLineOfSight,
            float attackCooldown)
        {
            _detectRange = detectRange;
            _attackRange = attackRange;
            _leashRange = leashRange;
            _requireLineOfSight = requireLineOfSight;
            _attackCooldown = attackCooldown;

            State = EnemyState.Idle;
        }

        public EnemyIntents Tick(in EnemySensors sensors, float deltaTime)
        {
            TickCooldown(deltaTime);

            if (State == EnemyState.Dead)
            {
                return EnemyIntents.None;
            }

            switch (State)
            {
                case EnemyState.Idle:
                    return TickIdle(sensors);

                case EnemyState.Chase:
                    return TickChase(sensors);

                case EnemyState.Attack:
                    return TickAttack(sensors);

                case EnemyState.Hit:
                    return EnemyIntents.None;
            }

            return EnemyIntents.None;
        }

        public void OnDamaged()
        {
            if (State == EnemyState.Dead)
            {
                return;
            }

            State = EnemyState.Hit;
        }

        public void OnAttackEnded()
        {
            if (State != EnemyState.Attack)
            {
                return;
            }

            State = EnemyState.Chase;

            // 收招即开始冷却。放在这里而不是进入 Attack 时，
            // 保证"两次出手之间"恒定间隔，与攻击动画多长无关。
            _cooldownTimer = _attackCooldown;
        }

        public void OnHitEnded()
        {
            if (State != EnemyState.Hit)
            {
                return;
            }

            State = EnemyState.Chase;
        }

        public void Kill()
        {
            if (State == EnemyState.Dead)
            {
                return;
            }

            State = EnemyState.Dead;
        }

        /// <summary>
        /// 冷却只跟时间走，不受状态切换影响。
        /// 被打断进 Hit 时不重置，否则"挨打反而刷新了怪物的出手节奏"。
        /// </summary>
        private void TickCooldown(float deltaTime)
        {
            if (_cooldownTimer <= 0f)
            {
                return;
            }

            _cooldownTimer -= deltaTime;

            if (_cooldownTimer < 0f)
            {
                _cooldownTimer = 0f;
            }
        }

        private EnemyIntents TickIdle(in EnemySensors sensors)
        {
            if (!CanDetect(sensors))
            {
                return EnemyIntents.None;
            }

            State = EnemyState.Chase;

            return new EnemyIntents(true, true, false);
        }

        private EnemyIntents TickChase(in EnemySensors sensors)
        {
            if (!sensors.HasTarget || sensors.DistanceToTarget > _leashRange)
            {
                State = EnemyState.Idle;
                return EnemyIntents.None;
            }

            if (sensors.DistanceToTarget <= _attackRange && _cooldownTimer <= 0f)
            {
                State = EnemyState.Attack;

                // 触发只发生在 Chase→Attack 的跃迁帧，本帧直接返回 true。
                // 不要在此处置"待触发"标志：本帧已经返回过 true，
                // 再置标志会让下一帧 TickAttack 重复返回 true，
                // 导致 EnemyAI 反复 SetTrigger(Attack) 把攻击动画重置。
                return new EnemyIntents(false, true, true);
            }

            return new EnemyIntents(true, true, false);
        }

        private EnemyIntents TickAttack(in EnemySensors sensors)
        {
            // 攻击进行中不产生新意图；收招由动画事件 OnAttackEnded 驱动。
            return new EnemyIntents(false, true, false);
        }

        private bool CanDetect(in EnemySensors sensors)
        {
            if (!sensors.HasTarget)
            {
                return false;
            }

            if (sensors.DistanceToTarget > _detectRange)
            {
                return false;
            }

            if (_requireLineOfSight && !sensors.HasLineOfSight)
            {
                return false;
            }

            return true;
        }
    }
}
