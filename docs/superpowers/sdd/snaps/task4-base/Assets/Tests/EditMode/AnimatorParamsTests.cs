using NUnit.Framework;
using Demo;

namespace Demo.Tests
{
    public class AnimatorParamsTests
    {
        [Test]
        public void 组名_恰好六个且无重复()
        {
            var groups = AnimatorParams.AllGroups;
            Assert.AreEqual(6, groups.Length);
            CollectionAssert.AllItemsAreUnique(groups);
        }

        [Test]
        public void 参数名_无重复()
        {
            CollectionAssert.AllItemsAreUnique(AnimatorParams.AllParameters);
        }

        [Test]
        public void 状态名_恰好三十九个且无重复()
        {
            var states = AnimatorParams.AllStates;
            Assert.AreEqual(39, states.Length,
                "5 待机/混合树 + 11 战斗 + 10 移动 + 3 反应 + 6 特殊 + 4 死亡 = 39");
            CollectionAssert.AllItemsAreUnique(states);
        }

        [Test]
        public void 状态名_已修正拼写错误()
        {
            foreach (var s in AnimatorParams.AllStates)
            {
                StringAssert.DoesNotContain("Shiled", s, $"状态名 {s} 仍含原作者拼写错误 Shiled");
            }
        }

        [Test]
        public void 状态名_不含重复的IdleBattle副本()
        {
            int count = 0;
            foreach (var s in AnimatorParams.AllStates)
            {
                if (s == AnimatorParams.States.IdleBattle) count++;
            }
            Assert.AreEqual(1, count, "Idle_Battle 应只有一个，原资源的 0/1 副本必须已合并");
        }
    }
}
