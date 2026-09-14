## Task 4: 生成器骨架 —— 参数与六个空子状态机

**这一步是技术风险验证点。** `AnimatorStateMachine.AddStateMachineTransition` 等 API 的可用性在此确认。先建最简结构并验证能编译、能产出合法资源，再往上堆内容。

**Files:**
- Create: `Assets/Script/Editor/HeroAnimatorBuilder.cs`
- Test: `Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs`

**Interfaces:**
- Consumes: `Demo.AnimatorParams`, `Demo.EditorTools.AnimationClipLocator`
- Produces:
  - `Demo.EditorTools.HeroAnimatorBuilder.Build() -> AnimatorController`
  - `Demo.EditorTools.HeroAnimatorBuilder.BuildFromMenu()` （MenuItem `Tools/角色/生成主角状态机`）
  - `Demo.EditorTools.HeroAnimatorBuilder.ComboLinkExitTime` 常量 `0.55f`
  - `Demo.EditorTools.HeroAnimatorBuilder.ActionReturnExitTime` 常量 `0.90f`

- [ ] **Step 1: 写测试**

`Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs`：

```csharp
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
```

- [ ] **Step 2: 运行测试，确认失败**

Expected: 编译失败，`HeroAnimatorBuilder` 不存在。

- [ ] **Step 3: 实现生成器骨架**

`Assets/Script/Editor/HeroAnimatorBuilder.cs`：

```csharp
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Demo;

namespace Demo.EditorTools
{
    /// <summary>
    /// 用 UnityEditor.Animations API 生成主角的 Animator Controller。
    ///
    /// 为什么是"生成"而不是"手工改"：
    /// 原资源控制器有 44 个平铺状态、44 条无条件的退出时间过渡，是作者的宣传片播放器。
    /// 手工把它改成参数化状态机需要在 Inspector 里连上百条线，既慢又容易出错。
    /// 用代码生成则快、可重复、可被测试断言。
    ///
    /// 原控制器 Hero_SwordAndShield.controller 生成到 Assets/Animator/ 下，
    /// Assets/RPGTinyHeroWavePBR/ 下的原文件一个字都不动。
    /// </summary>
    public static class HeroAnimatorBuilder
    {
        /// <summary>连招衔接的退出时间：到达这个进度时允许接下一段。</summary>
        public const float ComboLinkExitTime = 0.55f;

        /// <summary>一次性动作返回 Locomotion 的退出时间。</summary>
        public const float ActionReturnExitTime = 0.90f;

        public const string OutputPath = AnimatorParams.ControllerPath;

        private static readonly Vector3 GroupSpacing = new Vector3(400f, 0f, 0f);

        [MenuItem("Tools/角色/生成主角状态机")]
        public static void BuildFromMenu()
        {
            var ctrl = Build();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = ctrl;
            Debug.Log($"[HeroAnimatorBuilder] 已生成 {OutputPath}");
        }

        /// <summary>
        /// 生成控制器。幂等：每次调用都会先删除旧资源再重建。
        /// </summary>
        public static AnimatorController Build()
        {
            DeleteExistingAsset();

            EnsureFolder("Assets/Animator");

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(OutputPath);
            ctrl.layers[0].name = AnimatorParams.LayerName;

            AddParameters(ctrl);

            var root = ctrl.layers[0].stateMachine;
            root.name = AnimatorParams.LayerName;

            CreateGroupStateMachines(root);

            AssetDatabase.SaveAssets();
            return ctrl;
        }

        private static void DeleteExistingAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(OutputPath) != null)
                AssetDatabase.DeleteAsset(OutputPath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void AddParameters(AnimatorController ctrl)
        {
            void F(string n) => ctrl.AddParameter(n, AnimatorControllerParameterType.Float);
            void B(string n) => ctrl.AddParameter(n, AnimatorControllerParameterType.Bool);
            void T(string n) => ctrl.AddParameter(n, AnimatorControllerParameterType.Trigger);

            F(AnimatorParams.Speed);
            F(AnimatorParams.MoveDirX);
            F(AnimatorParams.MoveDirZ);
            B(AnimatorParams.IsLockedOn);
            B(AnimatorParams.IsBattleStance);
            B(AnimatorParams.IsGrounded);

            T(AnimatorParams.LightAttack);
            T(AnimatorParams.HeavyAttack);
            T(AnimatorParams.Roll);
            T(AnimatorParams.Jump);
            B(AnimatorParams.IsDefending);

            T(AnimatorParams.Hit);
            ctrl.AddParameter(AnimatorParams.HitIndex, AnimatorControllerParameterType.Int);
            T(AnimatorParams.DefendHit);
            T(AnimatorParams.Dizzy);
            T(AnimatorParams.Die);
            T(AnimatorParams.GetUp);
            B(AnimatorParams.IsDead);

            T(AnimatorParams.Victory);
            T(AnimatorParams.Dance);
            T(AnimatorParams.LevelUp);
            T(AnimatorParams.Challenging);
            T(AnimatorParams.SenseSomething);
        }

        /// <summary>建立 6 个空的子状态机，仅作分组容器。</summary>
        private static void CreateGroupStateMachines(AnimatorStateMachine root)
        {
            for (int i = 0; i < AnimatorParams.Groups.All.Length; i++)
            {
                var group = root.AddStateMachine(
                    AnimatorParams.Groups.All[i],
                    new Vector3(300f + i * GroupSpacing.x, 0f, 0f));
                group.name = AnimatorParams.Groups.All[i];
            }
        }
    }
}
```

- [ ] **Step 4: 运行测试，确认通过**

Run: Unity Test Runner → EditMode → Run All
Expected:
- `生成了控制器资源` PASS
- `参数齐全且类型正确` PASS
- `六个子状态机全部存在` PASS
- `每个状态都被归入某个子状态机` **FAIL**（状态还没建，符合预期）
- `根状态机上没有游离状态` PASS
- `没有重复状态名` PASS（空集合）
- `状态名已修正拼写错误` PASS（空集合）

**这一步的关键是确认 `AddStateMachine` 和整个 API 链路能编译通过、能产出资源。** 状态相关的断言在 Task 5–7 逐组补齐。

- [ ] **Step 5: 在 Unity 里目视确认生成的资源**

打开 `Assets/Animator/Hero_SwordAndShield.controller`，确认 Animator 窗口里能看到 6 个子状态机节点，Parameters 面板能看到 23 个参数。

- [ ] **Step 6: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Editor/HeroAnimatorBuilder.cs Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs
git commit -m "feat: 生成器骨架，产出 23 个参数与 6 个空子状态机"
```

---

