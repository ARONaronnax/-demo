using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Demo;
using Demo.EditorTools;

namespace Demo.Tests
{
    /// <summary>
    /// 运行时验证：控制器接上 Animator 之后，第一帧到底停在哪个状态。
    ///
    /// 为什么非要这一层：EditMode 能断言"状态存在、转换存在"，
    /// 断言不了"运行时真的走到了那里"。图层入口缺失就是这么漏过去的 ——
    /// 结构全对，角色却卡在一个零出边的孤立待机里。
    /// </summary>
    public class AnimatorEntryTests
    {
        private RuntimeAnimatorController _ctrl;

        [OneTimeSetUp]
        public void RegenerateAndLoad()
        {
            // 自己生成一次，不依赖"EditMode 那套先跑过"—— 测试必须自足。
            HeroAnimatorBuilder.Build();
            _ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                AnimatorParams.ControllerPath);
            Assert.IsNotNull(_ctrl, $"没找到控制器资源：{AnimatorParams.ControllerPath}");

            // 探针身上没有模型、没有 Avatar，人形剪辑的重定向告警是预期噪声，
            // 与被测对象（状态机走到哪）无关。不忽略它会把测试变成假失败。
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void RestoreLogAssert()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator 首帧停在Locomotion的混合树状态()
        {
            var animator = NewProbe();
            try
            {
                yield return null;   // 第 1 帧：Animator 初始化
                yield return null;   // 第 2 帧：状态机已求值

                AssertCurrentState(animator, AnimatorParams.States.TreeFreeNormal);
            }
            finally
            {
                Object.Destroy(animator.gameObject);
            }
        }

        private Animator NewProbe()
        {
            var go = new GameObject("AnimatorEntryProbe");
            var animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = _ctrl;
            animator.applyRootMotion = false;
            return animator;
        }

        private static void AssertCurrentState(Animator animator, string expectedState)
        {
            var info = animator.GetCurrentAnimatorStateInfo(0);
            var expected = Animator.StringToHash(expectedState);

            // 用 shortNameHash：fullPathHash 带着子状态机路径（Base Layer.Locomotion.X），
            // 路径一变断言就跟着飘；shortNameHash 就是状态本名。
            if (info.shortNameHash == expected)
                return;

            var clips = string.Join(", ",
                animator.GetCurrentAnimatorClipInfo(0)
                        .Select(c => c.clip != null ? c.clip.name : "<null>"));
            Assert.Fail(
                $"第 0 层当前状态应为 {expectedState}（shortNameHash={expected}），" +
                $"实际 shortNameHash={info.shortNameHash}、fullPathHash={info.fullPathHash}，" +
                $"正在播放：{clips}");
        }
    }
}
