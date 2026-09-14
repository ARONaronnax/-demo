using NUnit.Framework;
using UnityEngine;
using Demo.EditorTools;

namespace Demo.Tests
{
    public class AnimationClipLocatorTests
    {
        [Test]
        public void InPlace路径_指向InPlace目录()
        {
            var p = AnimationClipLocator.InPlacePath("MoveFWD_Battle_InPlace_SwordAndShield");
            StringAssert.Contains("/InPlace/", p);
            StringAssert.EndsWith(".fbx", p);
        }

        [Test]
        public void RootMotion路径_指向RootMotion目录()
        {
            var p = AnimationClipLocator.RootMotionPath("RollFWD_Battle_RM_SwordAndShield");
            StringAssert.Contains("/RootMotion/", p);
        }

        [Test]
        public void 加载InPlace剪辑_返回非空且名称匹配()
        {
            var clip = AnimationClipLocator.LoadInPlace("MoveFWD_Battle_InPlace_SwordAndShield");
            Assert.IsNotNull(clip, "找不到剪辑，检查 fbx 路径或剪辑命名");
            StringAssert.Contains("MoveFWD_Battle_InPlace_SwordAndShield", clip.name);
        }

        [Test]
        public void 加载RootMotion剪辑_返回非空()
        {
            var clip = AnimationClipLocator.LoadRootMotion("RollFWD_Battle_RM_SwordAndShield");
            Assert.IsNotNull(clip);
        }

        [Test]
        public void 加载不存在的剪辑_返回null而非抛异常()
        {
            var clip = AnimationClipLocator.LoadInPlace("完全不存在的剪辑名");
            Assert.IsNull(clip);
        }

        [Test]
        public void 同名的InPlace与RootMotion剪辑_是两个不同对象()
        {
            var a = AnimationClipLocator.LoadInPlace("MoveFWD_Battle_InPlace_SwordAndShield");
            var b = AnimationClipLocator.LoadRootMotion("MoveFWD_Battle_RM_SwordAndShield");
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.AreNotSame(a, b);
        }
    }
}
