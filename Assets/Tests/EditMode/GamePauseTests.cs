using NUnit.Framework;
using UnityEngine;
using Demo.Core;

namespace Demo.Tests
{
    public class GamePauseTests
    {
        private readonly object _ownerA = new object();
        private readonly object _ownerB = new object();

        [SetUp]
        public void SetUp()
        {
            GamePause.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            // Time.timeScale 是全局的，测试弄脏了会影响后面所有测试。
            GamePause.Clear();
        }

        [Test]
        public void NotPaused_ByDefault()
        {
            Assert.IsFalse(GamePause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void Acquire_Pauses()
        {
            GamePause.Acquire(_ownerA);

            Assert.IsTrue(GamePause.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);
        }

        [Test]
        public void Release_Resumes()
        {
            GamePause.Acquire(_ownerA);
            GamePause.Release(_ownerA);

            Assert.IsFalse(GamePause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void AcquireTwice_FromSameOwner_StillOnePause()
        {
            GamePause.Acquire(_ownerA);
            GamePause.Acquire(_ownerA);

            GamePause.Release(_ownerA);

            Assert.IsFalse(GamePause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void TwoOwners_StayPausedUntilBothRelease()
        {
            GamePause.Acquire(_ownerA);
            GamePause.Acquire(_ownerB);

            GamePause.Release(_ownerA);
            Assert.IsTrue(GamePause.IsPaused, "还有一个持有者，不该恢复");
            Assert.AreEqual(0f, Time.timeScale);

            GamePause.Release(_ownerB);
            Assert.IsFalse(GamePause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void Release_UnknownOwner_DoesNotResume()
        {
            GamePause.Acquire(_ownerA);

            GamePause.Release(_ownerB);

            Assert.IsTrue(GamePause.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);
        }

        [Test]
        public void Acquire_WithNull_IsIgnored()
        {
            GamePause.Acquire(null);

            Assert.IsFalse(GamePause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void Release_WithNull_IsIgnored()
        {
            GamePause.Acquire(_ownerA);

            GamePause.Release(null);

            Assert.IsTrue(GamePause.IsPaused);
        }

        [Test]
        public void Clear_ResumesAndForgetsOwners()
        {
            GamePause.Acquire(_ownerA);
            GamePause.Acquire(_ownerB);

            GamePause.Clear();

            Assert.IsFalse(GamePause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);

            // Clear 之后旧持有者的 Release 不该把新会话弄坏
            GamePause.Release(_ownerA);
            Assert.IsFalse(GamePause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void RestoresPreviousTimeScale_NotHardcodedOne()
        {
            // 如果以后有子弹时间之类的机制，恢复的必须是进入暂停前的值
            Time.timeScale = 0.5f;

            GamePause.Acquire(_ownerA);
            Assert.AreEqual(0f, Time.timeScale);

            GamePause.Release(_ownerA);
            Assert.AreEqual(0.5f, Time.timeScale);
        }
    }
}
