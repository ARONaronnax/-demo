## Task 6: Combat 组 —— 两条独立连招链

轻攻击 4 段、重攻击 5 段，各自一条链。每条链的连接转换只在 `ComboLinkExitTime` 之后才允许接续，
链尾无条件返回 Locomotion。

**Files:**
- Modify: `Assets/Script/Editor/HeroAnimatorBuilder.cs`
- Test: `Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs`（追加）

**Interfaces:**
- Consumes: Task 5 的 `AddTransition` 辅助方法
- Produces: 私有方法 `BuildCombat(AnimatorStateMachine root, AnimatorStateMachine combat)`

- [ ] **Step 1: 追加测试**

```csharp
        [Test]
        public void Combat组_含十一个状态()
        {
            var names = Group(AnimatorParams.Groups.Combat).states.Select(s => s.state.name).ToList();
            Assert.AreEqual(11, names.Count, "4 轻攻击 + 5 重连招 + Defend + DefendHit");
        }

        [Test]
        public void 轻攻击链_四段顺序衔接()
        {
            var combat = Group(AnimatorParams.Groups.Combat);
            var chain = AnimatorParams.States.LightAttackChain;

            for (int i = 0; i < chain.Length - 1; i++)
            {
                var from = combat.states.First(s => s.state.name == chain[i]).state;
                var toName = chain[i + 1];

                var link = from.transitions.FirstOrDefault(t =>
                    t.destinationState != null && t.destinationState.name == toName);

                Assert.IsNotNull(link, $"{chain[i]} 没有指向 {toName} 的转换");
                Assert.IsTrue(link.hasExitTime, $"{chain[i]} → {toName} 应基于退出时间");
                Assert.AreEqual(HeroAnimatorBuilder.ComboLinkExitTime, link.exitTime, 0.001f);
                Assert.IsTrue(link.conditions.Any(c => c.parameter == AnimatorParams.LightAttack),
                    $"{chain[i]} → {toName} 应由 LightAttack 触发");
            }
        }

        [Test]
        public void 重攻击链_五段顺序衔接()
        {
            var combat = Group(AnimatorParams.Groups.Combat);
            var chain = AnimatorParams.States.HeavyAttackChain;

            for (int i = 0; i < chain.Length - 1; i++)
            {
                var from = combat.states.First(s => s.state.name == chain[i]).state;
                var toName = chain[i + 1];

                var link = from.transitions.FirstOrDefault(t =>
                    t.destinationState != null && t.destinationState.name == toName);

                Assert.IsNotNull(link, $"{chain[i]} 没有指向 {toName} 的转换");
                Assert.IsTrue(link.conditions.Any(c => c.parameter == AnimatorParams.HeavyAttack));
            }
        }

        [Test]
        public void 连招链尾_返回Locomotion()
        {
            var combat = Group(AnimatorParams.Groups.Combat);
            var loco = Group(AnimatorParams.Groups.Locomotion);
            var locoStates = loco.states.Select(s => s.state.name).ToHashSet();

            foreach (var tail in new[] { AnimatorParams.States.Attack04, AnimatorParams.States.Combo05 })
            {
                var st = combat.states.First(s => s.state.name == tail).state;
                bool returns = st.transitions.Any(t =>
                    t.destinationState != null && locoStates.Contains(t.destinationState.name));
                Assert.IsTrue(returns, $"{tail} 没有返回 Locomotion 的转换，会卡死");
            }
        }

        [Test]
        public void 轻攻击链_可从Locomotion进入()
        {
            var loco = Group(AnimatorParams.Groups.Locomotion);
            bool found = loco.states
                .SelectMany(s => s.state.transitions)
                .Any(t => t.destinationState != null
                          && t.destinationState.name == AnimatorParams.States.Attack01);
            Assert.IsTrue(found, "Locomotion 应能经 LightAttack 进入 Attack01");
        }

        [Test]
        public void 两条连招链_不共用状态()
        {
            var light = AnimatorParams.States.LightAttackChain.ToHashSet();
            var heavy = AnimatorParams.States.HeavyAttackChain.ToHashSet();
            Assert.IsFalse(light.Overlaps(heavy), "轻攻击链与重攻击链不能共用状态");
        }
```

- [ ] **Step 2: 运行测试，确认失败**

Expected: Combat 相关测试 FAIL。

- [ ] **Step 3: 实现 Combat 组**

在 `HeroAnimatorBuilder.cs` 里追加：

```csharp
        /// <summary>重攻击链用根运动剪辑，轻攻击链用原地的。</summary>
        private static readonly HashSet<string> RootMotionStates = new HashSet<string>
        {
            AnimatorParams.States.RollFwd, AnimatorParams.States.RollBwd,
            AnimatorParams.States.RollLft, AnimatorParams.States.RollRgt,
            AnimatorParams.States.DashFwd, AnimatorParams.States.DashBwd,
            AnimatorParams.States.DashLft, AnimatorParams.States.DashRht,
            AnimatorParams.States.Combo01, AnimatorParams.States.Combo02,
            AnimatorParams.States.Combo03, AnimatorParams.States.Combo04,
            AnimatorParams.States.Combo05,
        };

        private static void BuildCombat(AnimatorStateMachine root, AnimatorStateMachine combat,
                                        AnimatorStateMachine locomotion)
        {
            // 进入 Locomotion 的关键状态名（用于"返回"转换的目的地）
            const string returnTarget = AnimatorParams.States.TreeFreeBattle;

            // ---- 轻攻击链：原地剪辑 ----
            var light = AnimatorParams.States.LightAttackChain;
            var lightStates = new AnimatorState[light.Length];
            for (int i = 0; i < light.Length; i++)
            {
                var st = combat.AddState(light[i], new Vector3(0, i * 70f, 0));
                st.motion = AnimationClipLocator.LoadInPlace($"Attack0{i + 1}_SwordAndShiled");
                lightStates[i] = st;
            }

            // ---- 重攻击链：根运动剪辑 ----
            var heavy = AnimatorParams.States.HeavyAttackChain;
            var heavyStates = new AnimatorState[heavy.Length];
            for (int i = 0; i < heavy.Length; i++)
            {
                var st = combat.AddState(heavy[i], new Vector3(250, i * 70f, 0));
                // 第 5 段用的是带根运动高度的特殊剪辑
                string clipName = i == 4
                    ? "Combo05_InPlaceWithRMHeight_SwordAndShield"
                    : $"Combo0{i + 1}_InPlace_SwordAndShield";
                st.motion = AnimationClipLocator.LoadRootMotion(
                    i == 4 ? "Combo05_RM_SwordAndShield" : $"Combo0{i + 1}_RM_SwordAndShield")
                    ?? AnimationClipLocator.LoadInPlace(clipName);
                heavyStates[i] = st;
            }

            // ---- 防御与格挡受击 ----
            var defend = combat.AddState(AnimatorParams.States.Defend, new Vector3(500, 0, 0));
            defend.motion = AnimationClipLocator.LoadInPlace("Defend_SwordAndShield");

            var defendHit = combat.AddState(AnimatorParams.States.DefendHit, new Vector3(500, 70f, 0));
            defendHit.motion = AnimationClipLocator.LoadInPlace("DefendHit_SwordAndShield");

            // ---- 连招衔接 ----
            LinkChain(lightStates, AnimatorParams.LightAttack, returnTarget);
            LinkChain(heavyStates, AnimatorParams.HeavyAttack, returnTarget);
        }

        /// <summary>
        /// 把一条连招链逐段连接起来，并为每一段配一条返回 Locomotion 的兜底转换。
        /// </summary>
        private static void LinkChain(AnimatorState[] chain, string triggerParam, string returnTargetName)
        {
            for (int i = 0; i < chain.Length; i++)
            {
                // 接下一段：需要退出时间 + 触发参数
                if (i < chain.Length - 1)
                {
                    AddTransition(chain[i], chain[i + 1], ComboLinkExitTime, true,
                        (triggerParam, AnimatorConditionMode.If));
                }

                // 兜底返回：只靠退出时间，无条件
                AddReturnTransition(chain[i], returnTargetName, ActionReturnExitTime);
            }
        }
```

- [ ] **Step 4: 增加"返回 Locomotion"的辅助方法**

`AddTransition` 只能连状态到状态，但返回的目标在另一个子状态机里。追加一个按名字解析目标的版本：

```csharp
        private static AnimatorStateMachine _locomotionGroup;

        /// <summary>
        /// 从动作状态连一条返回 Locomotion 的兜底转换。
        /// 目标状态在另一个子状态机内，需要按名字查找。
        /// </summary>
        private static AnimatorStateTransition AddReturnTransition(
            AnimatorState from, string targetStateName, float exitTime)
        {
            var target = _locomotionGroup.states
                .First(s => s.state.name == targetStateName).state;

            var t = from.AddTransition(target);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.hasFixedDuration = true;
            t.duration = 0.15f;
            t.canTransitionToSelf = false;
            return t;
        }
```

需要在 `HeroAnimatorBuilder.cs` 顶部加 `using System.Linq;`。

在 `Build()` 里赋值静态字段并调用：

```csharp
            var groups = GetGroups(root);
            _locomotionGroup = groups[AnimatorParams.Groups.Locomotion];

            BuildLocomotion(root, groups[AnimatorParams.Groups.Locomotion], ctrl);
            BuildCombat(root, groups[AnimatorParams.Groups.Combat], _locomotionGroup);
```

- [ ] **Step 5: 从 Locomotion 进入战斗**

在 `BuildLocomotion` 末尾追加两条进入转换：

```csharp
            // 进入轻攻击
            var attack01 = FindStateInRoot(AnimatorParams.States.Attack01);
            AddTransition(stFreeBattle, attack01, 1f, true,
                (AnimatorParams.LightAttack, AnimatorConditionMode.If));
            AddTransition(stLocked, attack01, 1f, true,
                (AnimatorParams.LightAttack, AnimatorConditionMode.If));

            // 进入重攻击
            var combo01 = FindStateInRoot(AnimatorParams.States.Combo01);
            AddTransition(stFreeBattle, combo01, 1f, true,
                (AnimatorParams.HeavyAttack, AnimatorConditionMode.If));
            AddTransition(stLocked, combo01, 1f, true,
                (AnimatorParams.HeavyAttack, AnimatorConditionMode.If));
```

辅助方法：

```csharp
        private static AnimatorStateMachine _root;

        private static AnimatorState FindStateInRoot(string stateName)
        {
            foreach (var sm in _root.stateMachines)
                foreach (var s in sm.stateMachine.states)
                    if (s.state.name == stateName)
                        return s.state;
            throw new System.InvalidOperationException($"找不到状态 {stateName}");
        }
```

在 `Build()` 里加 `_root = root;`。

- [ ] **Step 6: 运行测试，确认通过**

Run: Unity Test Runner → EditMode → Run All
Expected: Combat 相关 6 个测试 PASS。

- [ ] **Step 7: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Editor/HeroAnimatorBuilder.cs Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs
git commit -m "feat: Combat 组，轻攻击四段与重攻击五段两条独立连招链"
```

---

