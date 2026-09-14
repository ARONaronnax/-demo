using NUnit.Framework;
using Demo.Combat;

namespace Demo.Tests
{
    public class ComboChainTests
    {
        private const int Length = 5;
        private const float Window = 0.5f;
        private const float FinalMultiplier = 2f;

        private static ComboChain NewChain()
        {
            return new ComboChain(Length, Window, FinalMultiplier);
        }

        [Test]
        public void FirstPress_StartsAtFirstCombo()
        {
            ComboChain chain = NewChain();

            Assert.AreEqual(0, chain.Press());
            Assert.AreEqual(0, chain.CurrentIndex);
            Assert.IsTrue(chain.IsPlaying);
        }

        [Test]
        public void PressWhilePlaying_IsQueued_NotPlayedImmediately()
        {
            ComboChain chain = NewChain();
            chain.Press();

            Assert.AreEqual(-1, chain.Press());
            Assert.IsTrue(chain.HasQueuedInput);
            Assert.AreEqual(0, chain.CurrentIndex);
        }

        [Test]
        public void Release_WithoutQueuedInput_EndsChain()
        {
            ComboChain chain = NewChain();
            chain.Press();

            Assert.AreEqual(-1, chain.Release());
            Assert.IsFalse(chain.IsPlaying);
        }

        [Test]
        public void Release_WithQueuedInput_AdvancesExactlyOne()
        {
            ComboChain chain = NewChain();
            chain.Press();
            chain.Press();

            Assert.AreEqual(1, chain.Release());
            Assert.AreEqual(1, chain.CurrentIndex);
            Assert.IsTrue(chain.IsPlaying);
        }

        [Test]
        public void FullChain_AdvancesToLastSegment()
        {
            ComboChain chain = NewChain();
            chain.Press();

            for (int i = 1; i < Length; i++)
            {
                chain.Press();
                int played = chain.Release();

                Assert.AreEqual(i, played, "第 " + (i + 1) + " 段");
                Assert.AreEqual(i, chain.CurrentIndex);
            }
        }

        [Test]
        public void AfterLastSegment_NextPressWrapsToFirst()
        {
            ComboChain chain = NewChain();
            chain.Press();

            for (int i = 1; i < Length; i++)
            {
                chain.Press();
                chain.Release();
            }

            Assert.AreEqual(Length - 1, chain.CurrentIndex);

            chain.Press();

            Assert.AreEqual(0, chain.Release());
            Assert.AreEqual(0, chain.CurrentIndex);
        }

        [Test]
        public void PressWithinWindowAfterRelease_Advances()
        {
            ComboChain chain = NewChain();
            chain.Press();
            chain.Release();

            chain.Tick(0.3f);

            Assert.AreEqual(1, chain.Press());
        }

        [Test]
        public void PressAfterWindowElapsed_RestartsFromFirst()
        {
            ComboChain chain = NewChain();
            chain.Press();
            chain.Release();
            chain.Press();
            chain.Release();

            Assert.AreEqual(1, chain.CurrentIndex);

            chain.Tick(Window + 0.1f);

            Assert.AreEqual(0, chain.Press());
        }

        [Test]
        public void Tick_PastWindowWhileIdle_ResetsChain()
        {
            ComboChain chain = NewChain();
            chain.Press();
            chain.Release();

            chain.Tick(Window + 0.01f);

            Assert.AreEqual(-1, chain.CurrentIndex);
        }

        [Test]
        public void Tick_WhilePlaying_DoesNotReset()
        {
            ComboChain chain = NewChain();
            chain.Press();

            chain.Tick(5f);

            Assert.AreEqual(0, chain.CurrentIndex);
            Assert.IsTrue(chain.IsPlaying);
        }

        [Test]
        public void Break_ResetsChainToStart()
        {
            ComboChain chain = NewChain();
            chain.Press();
            chain.Press();
            chain.Release();

            Assert.AreEqual(1, chain.CurrentIndex);

            chain.Break();

            Assert.AreEqual(-1, chain.CurrentIndex);
            Assert.IsFalse(chain.IsPlaying);
            Assert.IsFalse(chain.HasQueuedInput);
            Assert.AreEqual(0, chain.Press());
        }

        [Test]
        public void Break_WhilePlaying_DropsQueuedInput()
        {
            ComboChain chain = NewChain();
            chain.Press();
            chain.Press();

            chain.Break();

            Assert.AreEqual(-1, chain.Release());
            Assert.AreEqual(-1, chain.CurrentIndex);
        }

        [Test]
        public void DamageMultiplier_IsOneOnEverySegmentExceptLast()
        {
            ComboChain chain = NewChain();

            chain.Press();

            for (int i = 0; i < Length - 1; i++)
            {
                Assert.AreEqual(1f, chain.DamageMultiplier, "第 " + (i + 1) + " 段");

                chain.Press();
                chain.Release();
            }

            Assert.AreEqual(Length - 1, chain.CurrentIndex);
            Assert.AreEqual(FinalMultiplier, chain.DamageMultiplier, "最后一段翻倍");
        }

        [Test]
        public void DamageMultiplier_IsOneWhenNotInCombo()
        {
            ComboChain chain = NewChain();

            Assert.AreEqual(1f, chain.DamageMultiplier);
        }

        [Test]
        public void LengthBelowOne_IsClampedToSingleSegment()
        {
            ComboChain chain = new ComboChain(0, Window, FinalMultiplier);

            Assert.AreEqual(0, chain.Press());
            Assert.AreEqual(FinalMultiplier, chain.DamageMultiplier);
        }
    }
}
