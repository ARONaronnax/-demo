## Task 8: Any State 转换、根运动标记与幂等性

收尾生成器：受击/死亡打断、给根运动状态挂 `StateMachineBehaviour`、保证可重复生成。

**Files:**
- Modify: `Assets/Script/Editor/HeroAnimatorBuilder.cs`
- Test: `Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs`（追加）

**Interfaces:**
- Consumes: Task 9 的 `RootMotionTag`、`LockMovementTag`（本任务先写测试，Task 9 补齐实现；若编译失败请先做 Task 9）
- Produces: 私有方法 `BuildAnyStateTransitions` / `AttachTags`

> **执行顺序提示**：本任务依赖 Task 9 的两个 `StateMachineBehaviour` 类型。
> 若选择顺序执行，请**先做 Task 9 再做本任务**。

- [ ] **Step 1: 追加测试**

```csharp
        [Test]
        public void AnyState转换_可打断进入受击与死亡()
        {
            var root = _ctrl.layers[0].stateMachine;
            var dests = root.anyStateTransitions
                .Where(t => t.destinationState != null)
                .Select(t => t.destinationState.name)
                .ToList();

            CollectionAssert.Contains(dests, AnimatorParams.States.GetHit01);
            CollectionAssert.Contains(dests, AnimatorParams.States.GetHit02);
            CollectionAssert.Contains(dests, AnimatorParams.States.Die01);
        }

        [Test]
        public void AnyState转换_不自我重入()
        {
            foreach (var t in _ctrl.layers[0].stateMachine.anyStateTransitions)
                Assert.IsFalse(t.canTransitionToSelf, "Any State 转换必须关闭自我重入");
        }

        [Test]
        public void AnyState受击_由Hit触发并按HitIndex分流()
        {
            var root = _ctrl.layers[0].stateMachine;

            var toHit1 = root.anyStateTransitions.First(t =>
                t.destinationState != null && t.destinationState.name == AnimatorParams.States.GetHit01);
            Assert.IsTrue(toHit1.conditions.Any(c => c.parameter == AnimatorParams.Hit));
            Assert.IsTrue(toHit1.conditions.Any(c =>
                c.parameter == AnimatorParams.HitIndex && c.mode == AnimatorConditionMode.Equals));

            var toHit2 = root.anyStateTransitions.First(t =>
                t.destinationState != null && t.destinationState.name == AnimatorParams.States.GetHit02);
            Assert.IsTrue(toHit2.conditions.Any(c => c.parameter == AnimatorParams.HitIndex));
        }

        [Test]
        public void 根运动状态_都挂了RootMotionTag()
        {
            foreach (var sm in AllStateMachines())
            {
                foreach (var s in sm.states)
                {
                    if (!HeroAnimatorBuilder.IsRootMotionState(s.state.name))
                        continue;

                    bool tagged = s.state.behaviours.Any(b => b is RootMotionTag);
                    Assert.IsTrue(tagged, $"根运动状态 {s.state.name} 没有挂 RootMotionTag");
                }
            }
        }

        [Test]
        public void 非根运动状态_没有RootMotionTag()
        {
            foreach (var sm in AllStateMachines())
            {
                foreach (var s in sm.states)
                {
                    if (HeroAnimatorBuilder.IsRootMotionState(s.state.name))
                        continue;

                    bool tagged = s.state.behaviours.Any(b => b is RootMotionTag);
                    Assert.IsFalse(tagged, $"非根运动状态 {s.state.name} 不应挂 RootMotionTag");
                }
            }
        }

        [Test]
        public void 动作状态_都挂了LockMovementTag()
        {
            var actionStates = AnimatorParams.States.LightAttackChain
                .Concat(AnimatorParams.States.HeavyAttackChain)
                .Concat(AnimatorParams.States.Rolls)
                .Concat(AnimatorParams.States.Dashes)
                .ToHashSet();

            foreach (var sm in AllStateMachines())
            {
                foreach (var s in sm.states)
                {
                    if (!actionStates.Contains(s.state.name)) continue;
                    bool tagged = s.state.behaviours.Any(b => b is LockMovementTag);
                    Assert.IsTrue(tagged, $"动作状态 {s.state.name} 没有挂 LockMovementTag");
                }
            }
        }

        [Test]
        public void 生成器是幂等的()
        {
            // 关键：Build() 会删除并重建 OutputPath 上的资源，
            // 调用之后 fixture 的 _ctrl 就变成"已销毁对象"，再访问会抛 MissingReferenceException。
            // 因此所有计数必须在 Build() 之前取好，并在重建后把 _ctrl 指向新资源。
            int beforeStates = AllStates().Count();
            int beforeParams = _ctrl.parameters.Length;
            int beforeGroups = _ctrl.layers[0].stateMachine.stateMachines.Length;

            _ctrl = HeroAnimatorBuilder.Build();

            Assert.AreEqual(beforeStates, AllStates().Count(), "重复生成改变了状态总数");
            Assert.AreEqual(beforeParams, _ctrl.parameters.Length, "重复生成产生了重复参数");
            Assert.AreEqual(beforeGroups, _ctrl.layers[0].stateMachine.stateMachines.Length,
                "重复生成产生了重复子状态机");
        }
```

- [ ] **Step 2: 运行测试，确认失败**

Expected: Any State 与 Tag 相关测试 FAIL。

- [ ] **Step 3: 实现 Any State 转换与 Tag 挂载**

在 `HeroAnimatorBuilder.cs` 里追加：

```csharp
        /// <summary>该状态是否使用根运动剪辑驱动位移。</summary>
        public static bool IsRootMotionState(string stateName) => RootMotionStates.Contains(stateName);

        /// <summary>需要屏蔽移动输入的动作状态。</summary>
        private static readonly HashSet<string> MovementLockedStates = new HashSet<string>(
            AnimatorParams.States.LightAttackChain
                .Concat(AnimatorParams.States.HeavyAttackChain)
                .Concat(AnimatorParams.States.Rolls)
                .Concat(AnimatorParams.States.Dashes));

        private static void BuildAnyStateTransitions(AnimatorStateMachine root)
        {
            // 受击 01 / 02 按 HitIndex 分流
            var hit1 = FindStateInRoot(AnimatorParams.States.GetHit01);
            var t1 = root.AddAnyStateTransition(hit1);
            ConfigureAnyState(t1);
            t1.AddCondition(AnimatorConditionMode.If, 0f, AnimatorParams.Hit);
            t1.AddCondition(AnimatorConditionMode.Equals, 0f, AnimatorParams.HitIndex);

            var hit2 = FindStateInRoot(AnimatorParams.States.GetHit02);
            var t2 = root.AddAnyStateTransition(hit2);
            ConfigureAnyState(t2);
            t2.AddCondition(AnimatorConditionMode.If, 0f, AnimatorParams.Hit);
            t2.AddCondition(AnimatorConditionMode.Equals, 1f, AnimatorParams.HitIndex);

            // 死亡
            var die01 = FindStateInRoot(AnimatorParams.States.Die01);
            var td = root.AddAnyStateTransition(die01);
            ConfigureAnyState(td);
            td.AddCondition(AnimatorConditionMode.If, 0f, AnimatorParams.Die);

            var die02 = FindStateInRoot(AnimatorParams.States.Die02);
            var td2 = root.AddAnyStateTransition(die02);
            ConfigureAnyState(td2);
            td2.AddCondition(AnimatorConditionMode.If, 0f, AnimatorParams.Die);
            td2.AddCondition(AnimatorConditionMode.Equals, 1f, AnimatorParams.HitIndex);
        }

        /// <summary>
        /// Any State 转换必须关闭自我重入，否则会每帧重入同一状态，表现为动画卡在第一帧。
        /// </summary>
        private static void ConfigureAnyState(AnimatorStateTransition t)
        {
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = 0.1f;
            t.canTransitionToSelf = false;
        }

        private static void AttachTags(AnimatorStateMachine root)
        {
            foreach (var sm in root.stateMachines)
            {
                foreach (var child in sm.stateMachine.states)
                {
                    var st = child.state;
                    if (RootMotionStates.Contains(st.name))
                        st.AddStateMachineBehaviour<RootMotionTag>();
                    if (MovementLockedStates.Contains(st.name))
                        st.AddStateMachineBehaviour<LockMovementTag>();
                }
            }
        }
```

在 `Build()` 的末尾、`AssetDatabase.SaveAssets()` 之前：

```csharp
            BuildAnyStateTransitions(root);
            AttachTags(root);
```

- [ ] **Step 4: 运行测试，确认全部通过**

Run: Unity Test Runner → EditMode → Run All
Expected: **全部测试 PASS**，无 FAIL。

- [ ] **Step 5: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Editor/HeroAnimatorBuilder.cs Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs
git commit -m "feat: Any State 受击死亡打断、根运动标记挂载、生成器幂等性"
```

---

