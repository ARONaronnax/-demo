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

        private AnimatorStateMachine Group(string name) =>
            _ctrl.layers[0].stateMachine.stateMachines
                .First(sm => sm.stateMachine.name == name).stateMachine;

        [Test]
        public void Locomotion组_含两个待机状态()
        {
            var names = Group(AnimatorParams.Groups.Locomotion).states
                .Select(s => s.state.name).ToList();
            CollectionAssert.Contains(names, AnimatorParams.States.IdleNormal);
            CollectionAssert.Contains(names, AnimatorParams.States.IdleBattle);
        }

        [Test]
        public void Locomotion组_含三个混合树()
        {
            var trees = new List<BlendTree>();
            foreach (var child in Group(AnimatorParams.Groups.Locomotion).states)
            {
                if (child.state.motion is BlendTree bt)
                    trees.Add(bt);
            }

            Assert.AreEqual(3, trees.Count, "应恰好有 3 个混合树");
            var names = trees.Select(t => t.name).ToList();
            foreach (var expected in AnimatorParams.AllBlendTrees)
                CollectionAssert.Contains(names, expected);
        }

        [Test]
        public void 自由姿态混合树_是一维且用Speed参数()
        {
            var bt = FindTree(AnimatorParams.BlendTrees.FreeBattle);
            Assert.AreEqual(BlendTreeType.Simple1D, bt.blendType);
            Assert.AreEqual(AnimatorParams.Speed, bt.blendParameter);
        }

        [Test]
        public void 锁定混合树_是二维且用方向参数()
        {
            var bt = FindTree(AnimatorParams.BlendTrees.Locked);
            Assert.AreEqual(BlendTreeType.SimpleDirectional2D, bt.blendType);
            Assert.AreEqual(AnimatorParams.MoveDirX, bt.blendParameter);
            Assert.AreEqual(AnimatorParams.MoveDirZ, bt.blendParameterY);
        }

        [Test]
        public void 锁定混合树_含中心待机与四个方向()
        {
            var bt = FindTree(AnimatorParams.BlendTrees.Locked);
            Assert.AreEqual(5, bt.children.Length,
                "锁定混合树应含 1 个中心待机 + 4 个方向移动");
        }

        [Test]
        public void 待机与锁定切换由IsLockedOn驱动()
        {
            var loco = Group(AnimatorParams.Groups.Locomotion);
            bool found = false;
            foreach (var s in loco.states)
            {
                foreach (var t in s.state.transitions)
                {
                    foreach (var c in t.conditions)
                    {
                        if (c.parameter == AnimatorParams.IsLockedOn)
                            found = true;
                    }
                }
            }
            Assert.IsTrue(found, "Locomotion 组内应有由 IsLockedOn 驱动的转换");
        }

        private BlendTree FindTree(string name)
        {
            foreach (var sm in AllStateMachines())
            {
                foreach (var s in sm.states)
                {
                    if (s.state.motion is BlendTree bt && bt.name == name)
                        return bt;
                }
            }
            Assert.Fail($"找不到混合树 {name}");
            return null;
        }

        [Test]
        public void 根状态机有指向Locomotion的入口转换()
        {
            var root = _ctrl.layers[0].stateMachine;

            // 没有入口转换时，运行时退回到根状态机的 defaultState —— 它被 Unity
            // 自动填成 Locomotion 组里那个零出边的 Idle_Normal，角色进场即卡死。
            // PlayMode 测试才能证明"走得到"，这条断言只负责在有人删掉入口时快速报警。
            Assert.AreEqual(1, root.entryTransitions.Length, "根状态机应当只有一条入口转换");
            var dest = root.entryTransitions[0].destinationStateMachine;
            Assert.IsNotNull(dest, "入口转换的终点应当是一个子状态机");
            Assert.AreEqual(AnimatorParams.Groups.Locomotion, dest.name);
        }
    }
}
