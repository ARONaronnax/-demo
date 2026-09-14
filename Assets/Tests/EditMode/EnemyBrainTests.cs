using NUnit.Framework;
using Demo.Core;
using Demo.Enemy;

namespace Demo.Tests
{
    public class EnemyBrainTests
    {
        private const float Detect = 12f;
        private const float Attack = 2f;
        private const float Leash = 25f;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            InputLock.Clear();
        }

        private const float Cooldown = 1.5f;
        private const float Frame = 1f / 60f;

        private static EnemyBrain MakeBrain(
            bool requireLineOfSight = false,
            float attackCooldown = Cooldown)
        {
            return new EnemyBrain(
                Detect,
                Attack,
                Leash,
                requireLineOfSight,
                attackCooldown);
        }

        private static EnemySensors Sensors(float distance, bool hasTarget = true, bool los = true)
        {
            return new EnemySensors(hasTarget, distance, los);
        }

        [Test]
        public void StartsInIdle()
        {
            Assert.AreEqual(EnemyState.Idle, MakeBrain().State);
        }

        [Test]
        public void Idle_NoTarget_StaysIdle_WithNoIntents()
        {
            EnemyBrain brain = MakeBrain();
            EnemyIntents intents = brain.Tick(Sensors(0f, hasTarget: false), Frame);

            Assert.AreEqual(EnemyState.Idle, brain.State);
            Assert.IsFalse(intents.Move);
            Assert.IsFalse(intents.TriggerAttack);
        }

        [Test]
        public void Idle_TargetInRange_EntersChase()
        {
            EnemyBrain brain = MakeBrain();
            EnemyIntents intents = brain.Tick(Sensors(5f), Frame);

            Assert.AreEqual(EnemyState.Chase, brain.State);
            Assert.IsTrue(intents.Move);
            Assert.IsTrue(intents.FaceTarget);
        }

        [Test]
        public void Idle_TargetBeyondDetectRange_StaysIdle()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(Detect + 1f), Frame);

            Assert.AreEqual(EnemyState.Idle, brain.State);
        }

        [Test]
        public void Idle_RequireLineOfSight_AndBlocked_StaysIdle()
        {
            EnemyBrain brain = MakeBrain(requireLineOfSight: true);
            brain.Tick(Sensors(5f, los: false), Frame);

            Assert.AreEqual(EnemyState.Idle, brain.State);
        }

        [Test]
        public void Chase_WithinAttackRange_EntersAttack_AndTriggersOnce()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f), Frame);

            EnemyIntents first = brain.Tick(Sensors(1.5f), Frame);

            Assert.AreEqual(EnemyState.Attack, brain.State);
            Assert.IsTrue(first.TriggerAttack);

            EnemyIntents second = brain.Tick(Sensors(1.5f), Frame);

            Assert.IsFalse(second.TriggerAttack);
        }

        [Test]
        public void Attack_DoesNotMove()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f), Frame);
            brain.Tick(Sensors(1.5f), Frame);

            EnemyIntents intents = brain.Tick(Sensors(1.5f), Frame);

            Assert.IsFalse(intents.Move);
            Assert.IsTrue(intents.FaceTarget);
        }

        [Test]
        public void Attack_OnAttackEnded_ReturnsToChase()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f), Frame);
            brain.Tick(Sensors(1.5f), Frame);

            brain.OnAttackEnded();

            Assert.AreEqual(EnemyState.Chase, brain.State);
        }

        [Test]
        public void Chase_BeyondLeash_ReturnsToIdle()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f), Frame);

            brain.Tick(Sensors(Leash + 1f), Frame);

            Assert.AreEqual(EnemyState.Idle, brain.State);
        }

        [Test]
        public void Any_OnDamaged_EntersHit()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f), Frame);

            brain.OnDamaged();

            Assert.AreEqual(EnemyState.Hit, brain.State);
        }

        [Test]
        public void Hit_DoesNotMove()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f), Frame);
            brain.OnDamaged();

            EnemyIntents intents = brain.Tick(Sensors(5f), Frame);

            Assert.IsFalse(intents.Move);
            Assert.IsFalse(intents.TriggerAttack);
        }

        [Test]
        public void Hit_OnHitEnded_ReturnsToChaseWhenTargetPresent()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f), Frame);
            brain.OnDamaged();

            brain.OnHitEnded();

            Assert.AreEqual(EnemyState.Chase, brain.State);
        }

        [Test]
        public void Hit_OnHitEnded_ThenTickWithoutTarget_FallsBackToIdle()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f), Frame);
            brain.OnDamaged();

            brain.OnHitEnded();

            Assert.AreEqual(EnemyState.Chase, brain.State);

            brain.Tick(Sensors(0f, hasTarget: false), Frame);

            Assert.AreEqual(EnemyState.Idle, brain.State);
        }

        [Test]
        public void Kill_EntersDead()
        {
            EnemyBrain brain = MakeBrain();
            brain.Kill();

            Assert.AreEqual(EnemyState.Dead, brain.State);
        }

        [Test]
        public void Dead_IsTerminal_IgnoresDamagedAndAttackEnded()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f), Frame);
            brain.Kill();

            brain.OnDamaged();
            brain.OnAttackEnded();
            brain.OnHitEnded();
            EnemyIntents intents = brain.Tick(Sensors(1f), Frame);

            Assert.AreEqual(EnemyState.Dead, brain.State);
            Assert.IsFalse(intents.Move);
            Assert.IsFalse(intents.FaceTarget);
            Assert.IsFalse(intents.TriggerAttack);
        }

        [Test]
        public void Dead_NoTarget_StaysDead()
        {
            EnemyBrain brain = MakeBrain();
            brain.Kill();
            brain.Tick(Sensors(0f, hasTarget: false), Frame);

            Assert.AreEqual(EnemyState.Dead, brain.State);
        }

        [Test]
        public void AttackEnded_BlocksReattackUntilCooldownElapses()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f), Frame);
            brain.Tick(Sensors(1.5f), Frame);

            brain.OnAttackEnded();

            EnemyIntents blocked = brain.Tick(Sensors(1.5f), Frame);

            Assert.AreEqual(EnemyState.Chase, brain.State);
            Assert.IsFalse(blocked.TriggerAttack);

            EnemyIntents allowed = brain.Tick(Sensors(1.5f), Cooldown + 0.1f);

            Assert.AreEqual(EnemyState.Attack, brain.State);
            Assert.IsTrue(allowed.TriggerAttack);
        }

        [Test]
        public void Cooldown_KeepsCountingDownWhileHit()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f), Frame);
            brain.Tick(Sensors(1.5f), Frame);
            brain.OnAttackEnded();

            brain.OnDamaged();

            // 受击期间冷却照走，挨打不会刷新怪物的出手节奏
            brain.Tick(Sensors(1.5f), Cooldown + 0.1f);
            brain.OnHitEnded();

            Assert.IsTrue(brain.Tick(Sensors(1.5f), Frame).TriggerAttack);
        }

        [Test]
        public void CooldownZero_AttacksAgainImmediately()
        {
            EnemyBrain brain = MakeBrain(attackCooldown: 0f);
            brain.Tick(Sensors(5f), Frame);
            brain.Tick(Sensors(1.5f), Frame);
            brain.OnAttackEnded();

            Assert.IsTrue(brain.Tick(Sensors(1.5f), Frame).TriggerAttack);
        }

        [Test]
        public void Cooldown_DoesNotBlockFirstAttack()
        {
            EnemyBrain brain = MakeBrain();

            brain.Tick(Sensors(5f), Frame);

            Assert.IsTrue(brain.Tick(Sensors(1.5f), Frame).TriggerAttack);
        }
    }
}
