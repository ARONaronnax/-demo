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
        public void StanceRoot路径_指向架势根目录()
        {
            var p = AnimationClipLocator.StanceRootPath("Idle_Normal_SwordAndShield");
            StringAssert.Contains("/SwordAndShield/", p);
            StringAssert.DoesNotContain("/InPlace/", p);
            StringAssert.DoesNotContain("/RootMotion/", p);
            StringAssert.EndsWith(".fbx", p);
        }

        [Test]
        public void 加载只在架势根目录的动作剪辑_回退后返回非空且名称匹配()
        {
            // Idle 类动作剪辑不在 InPlace/ 下，只有架势根目录那一份，靠回退才拿得到
            Assert.IsNull(AnimationClipLocator.LoadClip(
                    AnimationClipLocator.InPlacePath("Idle_Normal_SwordAndShield")),
                "前提：InPlace/ 下确实没有这个剪辑，否则本条测不到回退");

            var clip = AnimationClipLocator.LoadInPlace("Idle_Normal_SwordAndShield");
            Assert.IsNotNull(clip, "回退到架势根目录后仍加载失败");
            StringAssert.Contains("Idle_Normal_SwordAndShield", clip.name);
        }

        [Test]
        public void 不存在的剪辑_回退也不会误命中()
        {
            // 回退只应按精确文件名命中，不能把拼错的名字吞掉
            Assert.IsNull(AnimationClipLocator.LoadInPlace("Idle_Battle_SwordAndShield"));
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
