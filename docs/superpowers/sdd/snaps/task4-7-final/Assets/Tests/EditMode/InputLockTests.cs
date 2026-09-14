using NUnit.Framework;
using Demo.Core;

namespace Demo.Tests
{
    public class InputLockTests
    {
        [SetUp]
        public void SetUp()
        {
            InputLock.Clear();
        }

        [Test]
        public void Initially_IsNotLocked()
        {
            Assert.IsFalse(InputLock.IsLocked);
        }

        [Test]
        public void Acquire_Locks()
        {
            object owner = new object();
            InputLock.Acquire(owner);

            Assert.IsTrue(InputLock.IsLocked);
        }

        [Test]
        public void Release_Unlocks()
        {
            object owner = new object();
            InputLock.Acquire(owner);
            InputLock.Release(owner);

            Assert.IsFalse(InputLock.IsLocked);
        }

        [Test]
        public void TwoOwners_OneReleases_RemainsLocked()
        {
            object a = new object();
            object b = new object();

            InputLock.Acquire(a);
            InputLock.Acquire(b);
            InputLock.Release(a);

            Assert.IsTrue(InputLock.IsLocked);
        }

        [Test]
        public void Release_FromNonOwner_DoesNotUnlock()
        {
            object owner = new object();
            object stranger = new object();

            InputLock.Acquire(owner);
            InputLock.Release(stranger);

            Assert.IsTrue(InputLock.IsLocked);
        }

        [Test]
        public void DoubleRelease_DoesNotThrow()
        {
            object owner = new object();
            InputLock.Acquire(owner);

            Assert.DoesNotThrow(() =>
            {
                InputLock.Release(owner);
                InputLock.Release(owner);
            });
        }
    }
}
