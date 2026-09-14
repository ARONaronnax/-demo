using NUnit.Framework;
using UnityEngine;
using Demo.Core;
using Demo.Data;
using Demo.Quest;

namespace Demo.Tests
{
    public class QuestSystemTests
    {
        private QuestData _quest;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            InputLock.Clear();

            _quest = ScriptableObject.CreateInstance<QuestData>();
            _quest.id = "KillMonsters";
            _quest.title = "讨伐魔物";
            _quest.requiredAmount = 3;
        }

        [TearDown]
        public void TearDown()
        {
            if (_quest != null)
            {
                Object.DestroyImmediate(_quest);
            }
        }

        [Test]
        public void NewQuest_IsNotStarted()
        {
            var system = new QuestSystem();

            Assert.AreEqual(QuestStatus.NotStarted, system.GetStatus(_quest));
            Assert.AreEqual(0, system.GetProgress(_quest));
            Assert.IsFalse(system.IsActive(_quest));
        }

        [Test]
        public void Accept_MovesToInProgress()
        {
            var system = new QuestSystem();

            Assert.IsTrue(system.Accept(_quest));
            Assert.AreEqual(QuestStatus.InProgress, system.GetStatus(_quest));
            Assert.IsTrue(system.IsActive(_quest));
        }

        [Test]
        public void Accept_RaisesAcceptedEvent()
        {
            var system = new QuestSystem();
            QuestData received = null;
            system.Accepted += q => received = q;

            system.Accept(_quest);

            Assert.AreSame(_quest, received);
        }

        [Test]
        public void Accept_Twice_SecondReturnsFalse()
        {
            var system = new QuestSystem();

            Assert.IsTrue(system.Accept(_quest));
            Assert.IsFalse(system.Accept(_quest));
        }

        [Test]
        public void ReportKill_BeforeAccept_DoesNotCount()
        {
            var system = new QuestSystem();

            Assert.IsFalse(system.ReportKill("Werewolf"));
            Assert.AreEqual(0, system.GetProgress(_quest));
        }

        [Test]
        public void ReportKill_AfterAccept_IncrementsProgress()
        {
            var system = new QuestSystem();
            system.Accept(_quest);

            Assert.IsTrue(system.ReportKill("Werewolf"));
            Assert.AreEqual(1, system.GetProgress(_quest));
        }

        [Test]
        public void ReportKill_ReachingRequired_Completes()
        {
            var system = new QuestSystem();
            bool completed = false;
            system.Completed += q => completed = true;

            system.Accept(_quest);
            system.ReportKill("Werewolf");
            system.ReportKill("Werewolf");
            system.ReportKill("Werewolf");

            Assert.IsTrue(completed);
            Assert.AreEqual(QuestStatus.Completed, system.GetStatus(_quest));
            Assert.AreEqual(3, system.GetProgress(_quest));
        }

        [Test]
        public void ReportKill_AfterCompleted_DoesNotCountFurther()
        {
            var system = new QuestSystem();
            int completedCount = 0;
            system.Completed += q => completedCount++;

            system.Accept(_quest);
            for (int i = 0; i < 3; i++)
            {
                system.ReportKill("Werewolf");
            }

            Assert.IsFalse(system.ReportKill("Werewolf"));
            Assert.AreEqual(3, system.GetProgress(_quest));
            Assert.AreEqual(1, completedCount);
        }

        [Test]
        public void ReportKill_WithTargetFilter_IgnoresOtherTypes()
        {
            _quest.targetEnemyTypeId = "LizardWarrior";

            var system = new QuestSystem();
            system.Accept(_quest);

            Assert.IsFalse(system.ReportKill("Werewolf"));
            Assert.AreEqual(0, system.GetProgress(_quest));

            Assert.IsTrue(system.ReportKill("LizardWarrior"));
            Assert.AreEqual(1, system.GetProgress(_quest));
        }

        [Test]
        public void ProgressChanged_ReportsCurrentAndRequired()
        {
            var system = new QuestSystem();
            int lastCurrent = -1;
            int lastRequired = -1;
            system.ProgressChanged += (q, c, r) => { lastCurrent = c; lastRequired = r; };

            system.Accept(_quest);
            system.ReportKill("Werewolf");

            Assert.AreEqual(1, lastCurrent);
            Assert.AreEqual(3, lastRequired);
        }

        [Test]
        public void GetStatus_WithNullQuest_ReturnsNotStarted()
        {
            var system = new QuestSystem();

            Assert.DoesNotThrow(() => system.GetStatus(null));
            Assert.AreEqual(QuestStatus.NotStarted, system.GetStatus(null));
        }

        [Test]
        public void Accept_WithNullQuest_ReturnsFalse()
        {
            var system = new QuestSystem();

            Assert.IsFalse(system.Accept(null));
        }
    }
}
