## Task 7: Movement / Reaction / Special / Death 四组

补齐剩余 23 个状态，让 `每个状态都被归入某个子状态机` 这条断言转绿。

**Files:**
- Modify: `Assets/Script/Editor/HeroAnimatorBuilder.cs`
- Test: `Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs`（追加）

**Interfaces:**
- Consumes: Task 6 的 `FindStateInRoot`、`AddReturnTransition`
- Produces: 私有方法 `BuildMovement` / `BuildReaction` / `BuildSpecial` / `BuildDeath`

- [ ] **Step 1: 追加测试**

```csharp
        [Test]
        public void Movement组_含四个翻滚与四个冲刺与两个跳跃()
        {
            var names = Group(AnimatorParams.Groups.Movement).states.Select(s => s.state.name).ToList();
            Assert.AreEqual(10, names.Count);
            foreach (var r in AnimatorParams.States.Rolls) CollectionAssert.Contains(names, r);
            foreach (var d in AnimatorParams.States.Dashes) CollectionAssert.Contains(names, d);
            CollectionAssert.Contains(names, AnimatorParams.States.JumpNormal);
            CollectionAssert.Contains(names, AnimatorParams.States.JumpSpin);
        }

        [Test]
        public void Reaction组_含两个受击与眩晕()
        {
            var names = Group(AnimatorParams.Groups.Reaction).states.Select(s => s.state.name).ToList();
            Assert.AreEqual(3, names.Count);
            CollectionAssert.Contains(names, AnimatorParams.States.GetHit01);
            CollectionAssert.Contains(names, AnimatorParams.States.GetHit02);
            CollectionAssert.Contains(names, AnimatorParams.States.Dizzy);
        }

        [Test]
        public void Special组_含六个特殊动作()
        {
            Assert.AreEqual(6, Group(AnimatorParams.Groups.Special).states.Length);
        }

        [Test]
        public void Death组_含四个状态且死亡链完整()
        {
            var death = Group(AnimatorParams.Groups.Death);
            Assert.AreEqual(4, death.states.Length);

            var die01 = death.states.First(s => s.state.name == AnimatorParams.States.Die01).state;
            var stay = die01.transitions.FirstOrDefault(t =>
                t.destinationState != null
                && t.destinationState.name == AnimatorParams.States.Die01Stay);
            Assert.IsNotNull(stay, "Die01 应能转到 Die01_Stay");

            var getUp = death.states.First(s => s.state.name == AnimatorParams.States.GetUp).state;
            bool returns = getUp.transitions.Any(t =>
                t.destinationState != null
                && t.destinationState.name == AnimatorParams.States.TreeFreeBattle);
            Assert.IsTrue(returns, "GetUp 结束后应回到 Locomotion");
        }

        [Test]
        public void 躺尸状态_是循环的()
        {
            var death = Group(AnimatorParams.Groups.Death);
            var stay = death.states.First(s => s.state.name == AnimatorParams.States.Die01Stay).state;
            var clip = stay.motion as AnimationClip;
            Assert.IsNotNull(clip);
            Assert.IsTrue(clip.isLooping, "Die01_Stay 必须设为循环，否则躺尸会卡在末帧");
        }
```

- [ ] **Step 2: 运行测试，确认失败**

Expected: 四组相关测试 FAIL。

- [ ] **Step 3: 实现四组**

在 `HeroAnimatorBuilder.cs` 里追加：

```csharp
        private static void BuildMovement(AnimatorStateMachine movement)
        {
            // 翻滚：四个方向，用根运动剪辑
            var rollClips = new[]
            {
                "RollFWD_Battle_RM_SwordAndShield",
                "RollBWD_Battle_RM_SwordAndShield",
                "RollLFT_Battle_RM_SwordAndShield",
                "RollRGT_Battle_RM_SwordAndShield",
            };
            for (int i = 0; i < AnimatorParams.States.Rolls.Length; i++)
            {
                var st = movement.AddState(AnimatorParams.States.Rolls[i], new Vector3(0, i * 70f, 0));
                st.motion = AnimationClipLocator.LoadRootMotion(rollClips[i]);
            }

            // 冲刺：四个方向，用根运动剪辑
            var dashClips = new[]
            {
                "DashFWD_RM_SwordAndShield",
                "DashBWD_RM_SwordAndShield",
                "DashLFT_RM_SwordAndShield",
                "DashRHT_RM_SwordAndShield",
            };
            for (int i = 0; i < AnimatorParams.States.Dashes.Length; i++)
            {
                var st = movement.AddState(AnimatorParams.States.Dashes[i], new Vector3(250, i * 70f, 0));
                st.motion = AnimationClipLocator.LoadRootMotion(dashClips[i]);
            }

            // 跳跃：用 InPlace 版本，位移由代码驱动
            var jumpNormal = movement.AddState(AnimatorParams.States.JumpNormal, new Vector3(500, 0, 0));
            jumpNormal.motion = AnimationClipLocator.LoadInPlace("JumpFull_Normal_InPlace_SwordAndShield");

            var jumpSpin = movement.AddState(AnimatorParams.States.JumpSpin, new Vector3(500, 70f, 0));
            jumpSpin.motion = AnimationClipLocator.LoadInPlace("JumpFull_Spin_InPlace_SwordAndShield");
        }

        private static void BuildReaction(AnimatorStateMachine reaction)
        {
            var hit1 = reaction.AddState(AnimatorParams.States.GetHit01, new Vector3(0, 0, 0));
            hit1.motion = AnimationClipLocator.LoadInPlace("GetHit01_SwordAndShield");

            var hit2 = reaction.AddState(AnimatorParams.States.GetHit02, new Vector3(0, 70f, 0));
            hit2.motion = AnimationClipLocator.LoadInPlace("GetHit02_SwordAndShield");

            var dizzy = reaction.AddState(AnimatorParams.States.Dizzy, new Vector3(0, 140f, 0));
            dizzy.motion = AnimationClipLocator.LoadInPlace("Dizzy_SwordAndShield");

            // 眩晕是循环状态，需要外部触发才能离开
            var dizzyClip = dizzy.motion as AnimationClip;
            if (dizzyClip != null) dizzyClip.isLooping = true;
        }

        private static void BuildSpecial(AnimatorStateMachine special)
        {
            var map = new (string state, string clip)[]
            {
                (AnimatorParams.States.Challenging,  "Challenging_Battle_SwordAndShield"),
                (AnimatorParams.States.Dance,        "Dance_SwordAndShield"),
                (AnimatorParams.States.Victory,      "Victory_Battle_SwordAndShield"),
                (AnimatorParams.States.LevelUp,      "LevelUp_Battle_SwordAndShield"),
                (AnimatorParams.States.SenseStart,   "SenseSomething_Start_SwordAndShield"),
                (AnimatorParams.States.SenseSearching, "SenseSomething_Searching_SwordAndShield"),
            };

            for (int i = 0; i < map.Length; i++)
            {
                var st = special.AddState(map[i].state, new Vector3(0, i * 70f, 0));
                st.motion = AnimationClipLocator.LoadInPlace(map[i].clip);
            }
        }

        private static void BuildDeath(AnimatorStateMachine death)
        {
            var die01 = death.AddState(AnimatorParams.States.Die01, new Vector3(0, 0, 0));
            die01.motion = AnimationClipLocator.LoadInPlace("Die01_SwordAndShield");

            var die01Stay = death.AddState(AnimatorParams.States.Die01Stay, new Vector3(0, 70f, 0));
            die01Stay.motion = AnimationClipLocator.LoadInPlace("Die01_Stay_SwordAndShield");

            var die02 = death.AddState(AnimatorParams.States.Die02, new Vector3(0, 140f, 0));
            die02.motion = AnimationClipLocator.LoadInPlace("Die02_SwordAndShield");

            var getUp = death.AddState(AnimatorParams.States.GetUp, new Vector3(0, 210f, 0));
            getUp.motion = AnimationClipLocator.LoadInPlace("GetUp_SwordAndShield");

            death.defaultState = die01;

            // 躺尸：循环剪辑，靠外部 GetUp 触发离开
            var stayClip = die01Stay.motion as AnimationClip;
            if (stayClip != null) stayClip.isLooping = true;

            // Die01 → 躺尸（播完即进）
            var toStay = AddTransition(die01, die01Stay, 1f, true);
            toStay.exitTime = 0.95f;

            // Die02 → 躺尸
            var toStay2 = AddTransition(die02, die01Stay, 1f, true);
            toStay2.exitTime = 0.95f;

            // 躺尸 → 起身（由 GetUp 触发）
            AddTransition(die01Stay, getUp, 1f, false,
                (AnimatorParams.GetUp, AnimatorConditionMode.If));

            // 起身 → 回 Locomotion
            AddReturnTransition(getUp, AnimatorParams.States.TreeFreeBattle, 0.90f);
        }
```

- [ ] **Step 4: 在 `Build()` 里调用**

```csharp
            BuildLocomotion(root, groups[AnimatorParams.Groups.Locomotion], ctrl);
            BuildCombat(root, groups[AnimatorParams.Groups.Combat], groups[AnimatorParams.Groups.Locomotion]);
            BuildMovement(groups[AnimatorParams.Groups.Movement]);
            BuildReaction(groups[AnimatorParams.Groups.Reaction]);
            BuildSpecial(groups[AnimatorParams.Groups.Special]);
            BuildDeath(groups[AnimatorParams.Groups.Death]);
```

- [ ] **Step 5: 运行测试，确认通过**

Run: Unity Test Runner → EditMode → Run All
Expected: 除 `Any State` 与 Tag 相关（Task 8 才加）外的**全部测试 PASS**，
特别是 `每个状态都被归入某个子状态机` 转绿。

- [ ] **Step 6: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Editor/HeroAnimatorBuilder.cs Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs
git commit -m "feat: 补齐 Movement/Reaction/Special/Death 四组，39 个状态全部就位"
```

---

