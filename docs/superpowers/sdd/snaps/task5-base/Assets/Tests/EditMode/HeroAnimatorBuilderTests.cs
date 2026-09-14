using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using Demo;
using Demo.EditorTools;

namespace Demo.Tests
{
    /// <summary>
    /// 生成器结构断言。
    ///
    /// 这些测试共用一个生成结果：Unity 的 EditMode 测试是按 fixture 实例化的，
    /// 每个 [Test] 都会重新跑 OneTimeSetUp，所以这里在 OneTimeSetUp 里生成一次。
    /// 完整生成耗时较长（要加载 40+ 个 fbx），这是可接受的。
    /// </summary>
    public class HeroAnimatorBuilderTests
    {
        private static AnimatorController _ctrl;

        [OneTimeSetUp]
        public void GenerateOnce()
        {
            _ctrl = HeroAnimatorBuilder.Build();
        }

        private static IEnumerable<AnimatorStateMachine> AllStateMachines()
        {
            var root = _ctrl.layers[0].stateMachine;
            yield return root;
            foreach (var child in root.stateMachines)
                yield return child.stateMachine;
        }

        private static IEnumerable<AnimatorState> AllStates()
        {
            foreach (var sm in AllStateMachines())
            {
                foreach (var s in sm.states)
                    yield return s.state;
            }
        }

        [Test]
        public void 生成了控制器资源()
        {
            Assert.IsNotNull(_ctrl);
            Assert.AreEqual(1, _ctrl.layers.Length, "只需要一个 Base Layer");
            Assert.AreEqual(AnimatorParams.LayerName, _ctrl.layers[0].name);
        }

        [Test]
        public void 参数齐全且类型正确()
        {
            var byName = _ctrl.parameters.ToDictionary(p => p.name);

            foreach (var name in AnimatorParams.AllParameters)
                Assert.IsTrue(byName.ContainsKey(name), $"缺少参数 {name}");

            Assert.AreEqual(AnimatorControllerParameterType.Float, byName[AnimatorParams.Speed].type);
            Assert.AreEqual(AnimatorControllerParameterType.Float, byName[AnimatorParams.MoveDirX].type);
            Assert.AreEqual(AnimatorControllerParameterType.Float, byName[AnimatorParams.MoveDirZ].type);
            Assert.AreEqual(AnimatorControllerParameterType.Bool, byName[AnimatorParams.IsLockedOn].type);
            Assert.AreEqual(AnimatorControllerParameterType.Bool, byName[AnimatorParams.IsBattleStance].type);
            Assert.AreEqual(AnimatorControllerParameterType.Bool, byName[AnimatorParams.IsGrounded].type);
            Assert.AreEqual(AnimatorControllerParameterType.Bool, byName[AnimatorParams.IsDefending].type);
            Assert.AreEqual(AnimatorControllerParameterType.Bool, byName[AnimatorParams.IsDead].type);
            Assert.AreEqual(AnimatorControllerParameterType.Int, byName[AnimatorParams.HitIndex].type);
            Assert.AreEqual(AnimatorControllerParameterType.Trigger, byName[AnimatorParams.LightAttack].type);
            Assert.AreEqual(AnimatorControllerParameterType.Trigger, byName[AnimatorParams.HeavyAttack].type);
            Assert.AreEqual(AnimatorControllerParameterType.Trigger, byName[AnimatorParams.Roll].type);
            Assert.AreEqual(AnimatorControllerParameterType.Trigger, byName[AnimatorParams.Jump].type);
            Assert.AreEqual(AnimatorControllerParameterType.Trigger, byName[AnimatorParams.Die].type);
        }

        [Test]
        public void 六个子状态机全部存在()
        {
            var names = _ctrl.layers[0].stateMachine.stateMachines
                .Select(sm => sm.stateMachine.name).ToList();

            foreach (var g in AnimatorParams.AllGroups)
                Assert.IsTrue(names.Contains(g), $"缺少子状态机 {g}");
        }

        [Test]
        public void 每个状态都被归入某个子状态机()
        {
            var root = _ctrl.layers[0].stateMachine;
            var inGroups = new HashSet<string>(
                root.stateMachines.SelectMany(sm => sm.stateMachine.states)
                    .Select(s => s.state.name));

            foreach (var s in AnimatorParams.AllStates)
                Assert.IsTrue(inGroups.Contains(s), $"状态 {s} 没有被放进任何子状态机");
        }

        [Test]
        public void 根状态机上没有游离状态()
        {
            Assert.AreEqual(0, _ctrl.layers[0].stateMachine.states.Length,
                "所有状态都应归入子状态机，根层不应有游离状态");
        }

        [Test]
        public void 没有重复状态名()
        {
            var names = AllStates().Select(s => s.name).ToList();
            CollectionAssert.AllItemsAreUnique(names);
        }

        [Test]
        public void 状态名已修正拼写错误()
        {
            foreach (var s in AllStates())
                StringAssert.DoesNotContain("Shiled", s.name);
        }
    }
}
