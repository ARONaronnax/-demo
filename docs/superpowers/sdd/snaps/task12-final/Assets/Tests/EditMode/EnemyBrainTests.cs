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

        private static EnemyBrain MakeBrain(bool requireLineOfSight = false)
        {
            return new EnemyBrain(Detect, Attack, Leash, requireLineOfSight);
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
            EnemyIntents intents = brain.Tick(Sensors(0f, hasTarget: false));

            Assert.AreEqual(EnemyState.Idle, brain.State);
            Assert.IsFalse(intents.Move);
            Assert.IsFalse(intents.TriggerAttack);
        }

        [Test]
        public void Idle_TargetInRange_EntersChase()
        {
            EnemyBrain brain = MakeBrain();
            EnemyIntents intents = brain.Tick(Sensors(5f));

            Assert.AreEqual(EnemyState.Chase, brain.State);
            Assert.IsTrue(intents.Move);
            Assert.IsTrue(intents.FaceTarget);
        }

        [Test]
        public void Idle_TargetBeyondDetectRange_StaysIdle()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(Detect + 1f));

            Assert.AreEqual(EnemyState.Idle, brain.State);
        }

        [Test]
        public void Idle_RequireLineOfSight_AndBlocked_StaysIdle()
        {
            EnemyBrain brain = MakeBrain(requireLineOfSight: true);
            brain.Tick(Sensors(5f, los: false));

            Assert.AreEqual(EnemyState.Idle, brain.State);
        }

        [Test]
        public void Chase_WithinAttackRange_EntersAttack_AndTriggersOnce()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f));

            EnemyIntents first = brain.Tick(Sensors(1.5f));

            Assert.AreEqual(EnemyState.Attack, brain.State);
            Assert.IsTrue(first.TriggerAttack);

            EnemyIntents second = brain.Tick(Sensors(1.5f));

            Assert.IsFalse(second.TriggerAttack);
        }

        [Test]
        public void Attack_DoesNotMove()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f));
            brain.Tick(Sensors(1.5f));

            EnemyIntents intents = brain.Tick(Sensors(1.5f));

            Assert.IsFalse(intents.Move);
            Assert.IsTrue(intents.FaceTarget);
        }

        [Test]
        public void Attack_OnAttackEnded_ReturnsToChase()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f));
            brain.Tick(Sensors(1.5f));

            brain.OnAttackEnded();

            Assert.AreEqual(EnemyState.Chase, brain.State);
        }

        [Test]
        public void Chase_BeyondLeash_ReturnsToIdle()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f));

            brain.Tick(Sensors(Leash + 1f));

            Assert.AreEqual(EnemyState.Idle, brain.State);
        }

        [Test]
        public void Any_OnDamaged_EntersHit()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f));

            brain.OnDamaged();

            Assert.AreEqual(EnemyState.Hit, brain.State);
        }

        [Test]
        public void Hit_DoesNotMove()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f));
            brain.OnDamaged();

            EnemyIntents intents = brain.Tick(Sensors(5f));

            Assert.IsFalse(intents.Move);
            Assert.IsFalse(intents.TriggerAttack);
        }

        [Test]
        public void Hit_OnHitEnded_ReturnsToChaseWhenTargetPresent()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f));
            brain.OnDamaged();

            brain.OnHitEnded();

            Assert.AreEqual(EnemyState.Chase, brain.State);
        }

        [Test]
        public void Hit_OnHitEnded_ThenTickWithoutTarget_FallsBackToIdle()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f));
            brain.OnDamaged();

            brain.OnHitEnded();

            Assert.AreEqual(EnemyState.Chase, brain.State);

            brain.Tick(Sensors(0f, hasTarget: false));

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
            brain.Tick(Sensors(5f));
            brain.Kill();

            brain.OnDamaged();
            brain.OnAttackEnded();
            brain.OnHitEnded();
            EnemyIntents intents = brain.Tick(Sensors(1f));

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
            brain.Tick(Sensors(0f, hasTarget: false));

            Assert.AreEqual(EnemyState.Dead, brain.State);
        }
    }
}
