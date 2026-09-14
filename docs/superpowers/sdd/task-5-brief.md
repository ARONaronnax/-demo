## Task 5: Locomotion —— 三个混合树与组内转换

实现三套移动混合树。这是锁定机制的核心：锁定时角色不转向，必须靠独立的方向动画表达移动。

**Files:**
- Modify: `Assets/Script/Editor/HeroAnimatorBuilder.cs`
- Test: `Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs`（追加）

**Interfaces:**
- Consumes: Task 4 的 `HeroAnimatorBuilder`
- Produces: 私有方法 `BuildLocomotion(AnimatorStateMachine root, AnimatorStateMachine locomotion)`

- [ ] **Step 1: 追加测试**

在 `HeroAnimatorBuilderTests` 里追加：

```csharp
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
```

- [ ] **Step 2: 运行测试，确认失败**

Expected: 混合树相关测试 FAIL（尚未实现）。

- [ ] **Step 3: 实现 Locomotion**

在 `HeroAnimatorBuilder.cs` 里，`CreateGroupStateMachines` 之后调用：

```csharp
        private static void BuildLocomotion(AnimatorStateMachine root, AnimatorStateMachine locomotion, AnimatorController ctrl)
        {
            // ---- 两个待机状态 ----
            var idleNormal = locomotion.AddState(AnimatorParams.States.IdleNormal, new Vector3(0, 0, 0));
            idleNormal.motion = AnimationClipLocator.LoadInPlace("Idle_Normal_SwordAndShield");

            var idleBattle = locomotion.AddState(AnimatorParams.States.IdleBattle, new Vector3(0, 80, 0));
            idleBattle.motion = AnimationClipLocator.LoadInPlace("Idle_Battle_SwordAndShiled");

            // ---- 三个混合树 ----
            var treeFreeNormal = Create1DTree(ctrl, AnimatorParams.BlendTrees.FreeNormal, new[]
            {
                (AnimationClipLocator.LoadInPlace("Idle_Normal_SwordAndShield"), 0f),
                (AnimationClipLocator.LoadInPlace("MoveFWD_Normal_InPlace_SwordAndShield"), 0.5f),
            });

            var treeFreeBattle = Create1DTree(ctrl, AnimatorParams.BlendTrees.FreeBattle, new[]
            {
                (AnimationClipLocator.LoadInPlace("Idle_Battle_SwordAndShiled"), 0f),
                (AnimationClipLocator.LoadInPlace("MoveFWD_Battle_InPlace_SwordAndShield"), 0.5f),
                (AnimationClipLocator.LoadInPlace("SprintFWD_Battle_InPlace_SwordAndShield"), 1f),
            });

            var treeLocked = CreateDirectional2DTree(ctrl, AnimatorParams.BlendTrees.Locked, new[]
            {
                (AnimationClipLocator.LoadInPlace("Idle_Battle_SwordAndShiled"),        new Vector2(0f, 0f)),
                (AnimationClipLocator.LoadInPlace("MoveFWD_Battle_InPlace_SwordAndShield"), new Vector2(0f, 1f)),
                (AnimationClipLocator.LoadInPlace("MoveBWD_Battle_InPlace_SwordAndShield"), new Vector2(0f, -1f)),
                (AnimationClipLocator.LoadInPlace("MoveLFT_Battle_InPlace_SwordAndShield"), new Vector2(-1f, 0f)),
                (AnimationClipLocator.LoadInPlace("MoveRGT_Battle_InPlace_SwordAndShield"), new Vector2(1f, 0f)),
            });

            // 混合树也要作为状态存在
            var stFreeNormal = locomotion.AddState(AnimatorParams.States.TreeFreeNormal, new Vector3(0, 160, 0));
            stFreeNormal.motion = treeFreeNormal;

            var stFreeBattle = locomotion.AddState(AnimatorParams.States.TreeFreeBattle, new Vector3(0, 240, 0));
            stFreeBattle.motion = treeFreeBattle;

            var stLocked = locomotion.AddState(AnimatorParams.States.TreeLocked, new Vector3(0, 320, 0));
            stLocked.motion = treeLocked;

            locomotion.defaultState = stFreeNormal;

            // ---- 组内转换 ----
            // 非战斗 → 战斗姿态
            AddTransition(stFreeNormal, stFreeBattle, 1f, true,
                (AnimatorParams.IsBattleStance, AnimatorConditionMode.If));

            // 战斗姿态 → 非战斗（必须先停下来）
            AddTransition(stFreeBattle, stFreeNormal, 1f, true,
                (AnimatorParams.IsBattleStance, AnimatorConditionMode.IfNot));

            // 自由 → 锁定
            AddTransition(stFreeBattle, stLocked, 1f, true,
                (AnimatorParams.IsLockedOn, AnimatorConditionMode.If));

            // 锁定 → 自由
            AddTransition(stLocked, stFreeBattle, 1f, true,
                (AnimatorParams.IsLockedOn, AnimatorConditionMode.IfNot));
        }
```

- [ ] **Step 4: 补齐混合树与转换的辅助方法**

在 `HeroAnimatorBuilder.cs` 里追加：

```csharp
        private static BlendTree Create1DTree(
            AnimatorController ctrl, string name,
            (AnimationClip clip, float threshold)[] children)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.Simple1D,
                blendParameter = AnimatorParams.Speed,
                useAutomaticThresholds = false,
            };
            AssetDatabase.AddObjectToAsset(tree, ctrl);

            foreach (var (clip, threshold) in children)
            {
                if (clip == null)
                {
                    Debug.LogError($"[HeroAnimatorBuilder] {name} 有剪辑加载失败");
                    continue;
                }
                tree.AddChild(clip, threshold);
            }
            return tree;
        }

        private static BlendTree CreateDirectional2DTree(
            AnimatorController ctrl, string name,
            (AnimationClip clip, Vector2 pos)[] children)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.SimpleDirectional2D,
                blendParameter = AnimatorParams.MoveDirX,
                blendParameterY = AnimatorParams.MoveDirZ,
            };
            AssetDatabase.AddObjectToAsset(tree, ctrl);

            foreach (var (clip, pos) in children)
            {
                if (clip == null)
                {
                    Debug.LogError($"[HeroAnimatorBuilder] {name} 有剪辑加载失败");
                    continue;
                }
                tree.AddChild(clip, pos);
            }
            return tree;
        }

        private static AnimatorStateTransition AddTransition(
            AnimatorState from, AnimatorState to,
            float exitTime, bool hasExitTime,
            params (string param, AnimatorConditionMode mode)[] conditions)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = hasExitTime;
            t.exitTime = exitTime;
            t.hasFixedDuration = true;
            t.duration = 0.15f;
            t.canTransitionToSelf = false;

            foreach (var (param, mode) in conditions)
                t.AddCondition(mode, 0f, param);

            return t;
        }
```

在 `Build()` 里，`CreateGroupStateMachines(root)` 之后追加调用：

```csharp
            var groups = GetGroups(root);
            BuildLocomotion(root, groups[AnimatorParams.Groups.Locomotion], ctrl);
```

以及辅助方法：

```csharp
        private static System.Collections.Generic.Dictionary<string, AnimatorStateMachine> GetGroups(
            AnimatorStateMachine root)
        {
            var map = new System.Collections.Generic.Dictionary<string, AnimatorStateMachine>();
            foreach (var child in root.stateMachines)
                map[child.stateMachine.name] = child.stateMachine;
            return map;
        }
```

> **关于剪辑名拼写**：资源里的待机剪辑文件名是 `Idle_Battle_SwordAndShiled.fbx`
> （原作者把 Shield 拼成了 Shiled）。状态名我们修正为 `Idle_Battle`，
> 但**加载剪辑时必须用文件原名**，否则会加载失败。

- [ ] **Step 5: 运行测试，确认通过**

Run: Unity Test Runner → EditMode → Run All
Expected: Locomotion 相关测试全部 PASS。`每个状态都被归入某个子状态机` 仍 FAIL（其余四组未建）。

- [ ] **Step 6: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Editor/HeroAnimatorBuilder.cs Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs
git commit -m "feat: Locomotion 三个混合树与组内转换"
```

---

