## Task 2: AnimatorParams 共享契约

参数名和状态名的常量集中在一个类里。这是**唯一真相来源**——生成器和运行时脚本都引用它，杜绝字符串拼写漂移。

**Files:**
- Create: `Assets/Script/Player/AnimatorParams.cs`
- Test: `Assets/Tests/EditMode/AnimatorParamsTests.cs`

**Interfaces:**
- Consumes: 无
- Produces:
  - `Demo.AnimatorParams` — 参数名字符串常量
  - `Demo.AnimatorParams.Groups` — 6 个子状态机名
  - `Demo.AnimatorParams.States` — 39 个状态名
  - `Demo.AnimatorParams.BlendTrees` — 3 个混合树名
  - `Demo.AnimatorParams.LayerName` — 层名常量

- [ ] **Step 1: 写测试**

`Assets/Tests/EditMode/AnimatorParamsTests.cs`：

```csharp
using NUnit.Framework;
using Demo;

namespace Demo.Tests
{
    public class AnimatorParamsTests
    {
        [Test]
        public void 组名_恰好六个且无重复()
        {
            var groups = AnimatorParams.AllGroups;
            Assert.AreEqual(6, groups.Length);
            CollectionAssert.AllItemsAreUnique(groups);
        }

        [Test]
        public void 参数名_无重复()
        {
            CollectionAssert.AllItemsAreUnique(AnimatorParams.AllParameters);
        }

        [Test]
        public void 状态名_恰好三十九个且无重复()
        {
            var states = AnimatorParams.AllStates;
            Assert.AreEqual(39, states.Length,
                "5 待机/混合树 + 11 战斗 + 10 移动 + 3 反应 + 6 特殊 + 4 死亡 = 39");
            CollectionAssert.AllItemsAreUnique(states);
        }

        [Test]
        public void 状态名_已修正拼写错误()
        {
            foreach (var s in AnimatorParams.AllStates)
            {
                StringAssert.DoesNotContain("Shiled", s, $"状态名 {s} 仍含原作者拼写错误 Shiled");
            }
        }

        [Test]
        public void 状态名_不含重复的IdleBattle副本()
        {
            int count = 0;
            foreach (var s in AnimatorParams.AllStates)
            {
                if (s == AnimatorParams.States.IdleBattle) count++;
            }
            Assert.AreEqual(1, count, "Idle_Battle 应只有一个，原资源的 0/1 副本必须已合并");
        }
    }
}
```

- [ ] **Step 2: 运行测试，确认失败**

Run: Unity Test Runner → EditMode → Run All
Expected: 编译失败，`Demo.AnimatorParams` 不存在。

- [ ] **Step 3: 实现 AnimatorParams**

`Assets/Script/Player/AnimatorParams.cs`：

```csharp
namespace Demo
{
    /// <summary>
    /// Animator 参数名、状态名、子状态机名的唯一真相来源。
    /// 生成器（Demo.EditorTools.HeroAnimatorBuilder）与运行时脚本都引用这里，
    /// 绝对不要在别处硬编码这些字符串。
    /// </summary>
    public static class AnimatorParams
    {
        public const string LayerName = "Base Layer";

        /// <summary>Animator Controller 资源路径。</summary>
        public const string ControllerPath = "Assets/Animator/Hero_SwordAndShield.controller";

        // ---------------- 参数名 ----------------
        public const string Speed = "Speed";
        public const string MoveDirX = "MoveDirX";
        public const string MoveDirZ = "MoveDirZ";
        public const string IsLockedOn = "IsLockedOn";
        public const string IsBattleStance = "IsBattleStance";
        public const string IsGrounded = "IsGrounded";

        public const string LightAttack = "LightAttack";
        public const string HeavyAttack = "HeavyAttack";
        public const string Roll = "Roll";
        public const string Jump = "Jump";
        public const string IsDefending = "IsDefending";

        public const string Hit = "Hit";
        public const string HitIndex = "HitIndex";
        public const string DefendHit = "DefendHit";
        public const string Dizzy = "Dizzy";
        public const string Die = "Die";
        public const string GetUp = "GetUp";
        public const string IsDead = "IsDead";

        public const string Victory = "Victory";
        public const string Dance = "Dance";
        public const string LevelUp = "LevelUp";
        public const string Challenging = "Challenging";
        public const string SenseSomething = "SenseSomething";

        public static readonly string[] AllParameters =
        {
            Speed, MoveDirX, MoveDirZ, IsLockedOn, IsBattleStance, IsGrounded,
            LightAttack, HeavyAttack, Roll, Jump, IsDefending,
            Hit, HitIndex, DefendHit, Dizzy, Die, GetUp, IsDead,
            Victory, Dance, LevelUp, Challenging, SenseSomething,
        };

        // ---------------- 子状态机组名 ----------------
        public static class Groups
        {
            public const string Locomotion = "Locomotion";
            public const string Combat = "Combat";
            public const string Movement = "Movement";
            public const string Reaction = "Reaction";
            public const string Special = "Special";
            public const string Death = "Death";

            public static readonly string[] All =
            {
                Locomotion, Combat, Movement, Reaction, Special, Death,
            };
        }

        public static readonly string[] AllGroups = Groups.All;

        // ---------------- 混合树名 ----------------
        public static class BlendTrees
        {
            public const string FreeNormal = "Locomotion_Free_Normal";
            public const string FreeBattle = "Locomotion_Free_Battle";
            public const string Locked = "Locomotion_Locked";

            public static readonly string[] All = { FreeNormal, FreeBattle, Locked };
        }

        public static readonly string[] AllBlendTrees = BlendTrees.All;

        // ---------------- 状态名 ----------------
        public static class States
        {
            // Locomotion
            public const string IdleNormal = "Idle_Normal";
            public const string IdleBattle = "Idle_Battle";

            // Locomotion 组内的三个混合树状态（混合树本身也是状态节点）
            public const string TreeFreeNormal = "Tree_Free_Normal";
            public const string TreeFreeBattle = "Tree_Free_Battle";
            public const string TreeLocked = "Tree_Locked";

            // Combat
            public const string Attack01 = "Attack01";
            public const string Attack02 = "Attack02";
            public const string Attack03 = "Attack03";
            public const string Attack04 = "Attack04";
            public const string Combo01 = "Combo01";
            public const string Combo02 = "Combo02";
            public const string Combo03 = "Combo03";
            public const string Combo04 = "Combo04";
            public const string Combo05 = "Combo05";
            public const string Defend = "Defend";
            public const string DefendHit = "DefendHit";

            // Movement
            public const string RollFwd = "RollFWD";
            public const string RollBwd = "RollBWD";
            public const string RollLft = "RollLFT";
            public const string RollRgt = "RollRGT";
            public const string DashFwd = "DashFWD";
            public const string DashBwd = "DashBWD";
            public const string DashLft = "DashLFT";
            public const string DashRht = "DashRHT";
            public const string JumpNormal = "JumpFull_Normal";
            public const string JumpSpin = "JumpFull_Spin";

            // Reaction
            public const string GetHit01 = "GetHit01";
            public const string GetHit02 = "GetHit02";
            public const string Dizzy = "Dizzy";

            // Special
            public const string Challenging = "Challenging";
            public const string Dance = "Dance";
            public const string Victory = "Victory";
            public const string LevelUp = "LevelUp";
            public const string SenseStart = "SenseSomething_Start";
            public const string SenseSearching = "SenseSomething_Searching";

            // Death
            public const string Die01 = "Die01";
            public const string Die02 = "Die02";
            public const string Die01Stay = "Die01_Stay";
            public const string GetUp = "GetUp";

            /// <summary>轻攻击链，顺序即连招顺序。</summary>
            public static readonly string[] LightAttackChain =
                { Attack01, Attack02, Attack03, Attack04 };

            /// <summary>重攻击链，顺序即连招顺序。</summary>
            public static readonly string[] HeavyAttackChain =
                { Combo01, Combo02, Combo03, Combo04, Combo05 };

            /// <summary>四个翻滚方向，顺序与 AllRolls 对应。</summary>
            public static readonly string[] Rolls = { RollFwd, RollBwd, RollLft, RollRgt };

            /// <summary>四个冲刺方向。</summary>
            public static readonly string[] Dashes = { DashFwd, DashBwd, DashLft, DashRht };
        }

        public static readonly string[] AllStates =
        {
            // Locomotion (5 = 2 个待机 + 3 个混合树状态)
            States.IdleNormal, States.IdleBattle,
            States.TreeFreeNormal, States.TreeFreeBattle, States.TreeLocked,
            // Combat (11)
            States.Attack01, States.Attack02, States.Attack03, States.Attack04,
            States.Combo01, States.Combo02, States.Combo03, States.Combo04, States.Combo05,
            States.Defend, States.DefendHit,
            // Movement (10)
            States.RollFwd, States.RollBwd, States.RollLft, States.RollRgt,
            States.DashFwd, States.DashBwd, States.DashLft, States.DashRht,
            States.JumpNormal, States.JumpSpin,
            // Reaction (3)
            States.GetHit01, States.GetHit02, States.Dizzy,
            // Special (6)
            States.Challenging, States.Dance, States.Victory,
            States.LevelUp, States.SenseStart, States.SenseSearching,
            // Death (4)
            States.Die01, States.Die02, States.Die01Stay, States.GetUp,
        };
    }
}
```

- [ ] **Step 4: 运行测试，确认通过**

Run: Unity Test Runner → EditMode → Run All
Expected: `AnimatorParamsTests` 5 个测试全部 PASS。

若"恰好三十九个"断言失败，检查 `AllStates` 数组的实际元素数——数组写错数量时这个测试会直接指出。

- [ ] **Step 5: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Player/AnimatorParams.cs Assets/Tests/EditMode/AnimatorParamsTests.cs
git commit -m "feat: 新增 AnimatorParams 作为动画参数与状态名的唯一真相来源"
```

---

