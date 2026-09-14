# 主角系统实现计划（子项目 ①）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 MC01 主角的动画由玩家输入驱动，并提供自由视角移动 + 可选锁定的第三人称控制。

**Architecture:** 分两半。**生成侧**：一个 Editor 工具用 `UnityEditor.Animations` API 重新生成干净的 Animator Controller（6 个子状态机、23 个参数、组间参数化转换），原资源控制器不动。**运行侧**：5 个职责单一的 MonoBehaviour，`OnAnimatorMove()` 作为唯一水平位移入口，把代码位移与根运动汇聚到同一条管线。

**Tech Stack:** Unity 2022.3.62f3c1 · Built-in Render Pipeline · Cinemachine 2.10.7 · 旧版 Input Manager · Unity Test Framework 1.1.33 (NUnit)

## Global Constraints

- **本项目不使用 git。** 用户明确选择不配置 git 身份，直接写代码。
  计划中各任务末尾的"提交"步骤**一律跳过**，`git add` / `git commit` 命令一律不执行。
  代码改动照常写入磁盘，只是不做版本控制。
- Unity 版本：**2022.3.62f3c1**（勿升级）
- 渲染管线：**Built-in RP**。禁止引入 URP/HDRP shader 或材质
- 输入系统：**旧版 Input Manager**（`activeInputHandler: 0`）。使用 `UnityEngine.Input`，禁止 `UnityEngine.InputSystem`
- 命名空间：运行时 `Demo`，编辑器工具 `Demo.EditorTools`，测试 `Demo.Tests`
- 新增程序集：`Demo.Runtime` / `Demo.Editor` / `Demo.Tests.EditMode` / `Demo.Tests.PlayMode`
- **不得修改** `Assets/RPGTinyHeroWavePBR/` 与 `Assets/RPGMonsterWave02PBR/` 下的任何文件（资源包保持原样，便于日后更新）
- 生成产物一律写到 `Assets/Animator/`、`Assets/Script/`、`Assets/Scenes/`、`Assets/Prefabs/`
- 输出控制器路径固定为 `Assets/Animator/Hero_SwordAndShield.controller`
- 连招衔接退出时间常量 `ComboLinkExitTime = 0.55f`，动作返回退出时间常量 `ActionReturnExitTime = 0.90f`
- Animator 参数名与状态名的**唯一真相来源**是 `Demo.AnimatorParams` 常量类。禁止在别处硬编码字符串字面量
- 所有 Editor 工具必须**幂等**：重复执行结果一致，不产生重复对象
- `m_ApplyRootMotion` 保持 `true`

---

## 文件结构

### 创建

| 文件 | 职责 |
|---|---|
| `Assets/Script/Demo.Runtime.asmdef` | 运行时程序集定义 |
| `Assets/Script/Editor/Demo.Editor.asmdef` | 编辑器程序集定义 |
| `Assets/Script/Editor/AnimationClipLocator.cs` | 从 fbx 路径定位并加载 AnimationClip |
| `Assets/Script/Editor/HeroAnimatorBuilder.cs` | 生成 Animator Controller |
| `Assets/Script/Editor/DemoSceneBuilder.cs` | 生成 Demo 场景 |
| `Assets/Script/Player/AnimatorParams.cs` | 参数名/状态名/组名常量（共享契约） |
| `Assets/Script/Player/PlayerInputReader.cs` | 读输入，翻译成语义化属性 |
| `Assets/Script/Player/PlayerMotor.cs` | 位移管线 |
| `Assets/Script/Player/PlayerAnimatorDriver.cs` | 写 Animator 参数 |
| `Assets/Script/Player/PlayerCameraRig.cs` | 相机与锁定 |
| `Assets/Script/Player/PlayerController.cs` | 门面 |
| `Assets/Script/Player/LockOnTarget.cs` | 可锁定目标标记 |
| `Assets/Script/Player/RootMotionTag.cs` | StateMachineBehaviour：标记根运动状态 |
| `Assets/Script/Player/LockMovementTag.cs` | StateMachineBehaviour：标记锁定移动的状态 |
| `Assets/Tests/EditMode/Demo.Tests.EditMode.asmdef` | 测试程序集定义 |
| `Assets/Tests/EditMode/AssemblySmokeTests.cs` | 程序集装配验证 |
| `Assets/Tests/EditMode/AnimationClipLocatorTests.cs` | 剪辑定位测试 |
| `Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs` | 生成器结构断言 |
| `Assets/Tests/EditMode/ComboBufferTests.cs` | 连招缓冲测试 |
| `Assets/Tests/EditMode/MotorPipelineTests.cs` | 位移管线测试 |
| `Assets/Tests/EditMode/LockOnSelectorTests.cs` | 锁定目标选择测试 |
| `Assets/Tests/PlayMode/Demo.Tests.PlayMode.asmdef` | 运行时测试程序集定义 |
| `Assets/Tests/PlayMode/AnimatorEntryTests.cs` | 图层入口的运行时验证 |

### 修改

| 文件 | 改动 |
|---|---|
| `Assets/Script/PlayerController.cs` → 删除 | 原文件被 `Assets/Script/Player/PlayerController.cs` 取代 |

---

## 依赖关系

```
Task 1  程序集骨架
   ├── Task 2  AnimatorParams
   ├── Task 3  AnimationClipLocator
   │       └── Task 4  生成器:骨架
   │              ├── Task 5  生成器:Locomotion
   │              │      └── Task 5b 图层入口 + PlayMode 验证
   │              ├── Task 6  生成器:Combat + 连招
   │              └── Task 7  生成器:其余四组
   │                     └── Task 8  Any State + Tag 挂载 + 幂等
   ├── Task 9  StateMachineBehaviour
   ├── Task 10 PlayerInputReader
   ├── Task 11 PlayerMotor          ← 依赖 Task 9
   ├── Task 12 PlayerAnimatorDriver ← 依赖 Task 2
   ├── Task 13 PlayerCameraRig + LockOnTarget
   │       └── Task 14 PlayerController 门面
   │              └── Task 15 DemoSceneBuilder
   └── Task 16 端到端手动验收
```

---

## Task 1: 程序集骨架与测试装配

先建立三个程序集，并用一个冒烟测试证明测试链路是通的。**这一步必须最先做**——没有程序集定义，后续所有测试都无法引用被测代码。

**Files:**
- Create: `Assets/Script/Demo.Runtime.asmdef`
- Create: `Assets/Script/Editor/Demo.Editor.asmdef`
- Create: `Assets/Tests/EditMode/Demo.Tests.EditMode.asmdef`
- Create: `Assets/Tests/EditMode/AssemblySmokeTests.cs`

**Interfaces:**
- Consumes: 无
- Produces: 程序集 `Demo.Runtime` / `Demo.Editor` / `Demo.Tests.EditMode`

- [ ] **Step 1: 创建运行时程序集定义**

`Assets/Script/Demo.Runtime.asmdef`：

```json
{
    "name": "Demo.Runtime",
    "rootNamespace": "Demo",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: 创建编辑器程序集定义**

`Assets/Script/Editor/Demo.Editor.asmdef`：

```json
{
    "name": "Demo.Editor",
    "rootNamespace": "Demo.EditorTools",
    "references": [
        "Demo.Runtime"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 3: 创建测试程序集定义**

`Assets/Tests/EditMode/Demo.Tests.EditMode.asmdef`：

```json
{
    "name": "Demo.Tests.EditMode",
    "rootNamespace": "Demo.Tests",
    "references": [
        "Demo.Runtime",
        "Demo.Editor",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 4: 写冒烟测试**

`Assets/Tests/EditMode/AssemblySmokeTests.cs`：

```csharp
using NUnit.Framework;

namespace Demo.Tests
{
    public class AssemblySmokeTests
    {
        [Test]
        public void 测试程序集_已正确装配()
        {
            // 只断言 NotNull 等于没断言：程序集永远不为 null，asmdef 装配错了照样过。
            // 断言名字才能真正验证 asmdef 落在了预期的程序集里。
            Assert.AreEqual("Demo.Tests.EditMode",
                typeof(AssemblySmokeTests).Assembly.GetName().Name);
        }

        [Test]
        public void 编辑器程序集_可被测试程序集引用()
        {
            var type = typeof(Demo.EditorTools.NamespaceAnchor);
            Assert.IsNotNull(type);
            Assert.AreEqual("Demo.EditorTools", type.Namespace);
        }
    }
}
```

- [ ] **Step 5: 创建 `NamespaceAnchor`（让上一步的断言有意义）**

`Assets/Script/Editor/NamespaceAnchor.cs`：

```csharp
namespace Demo.EditorTools
{
    /// <summary>
    /// 仅用于验证 Demo.Editor 程序集可被测试程序集引用。
    /// 不代表任何业务含义，后续任务加入真正的编辑器工具后可保留。
    /// </summary>
    internal static class NamespaceAnchor
    {
    }
}
```

> 注意：`internal` 类型在测试程序集中不可见，除非加 `InternalsVisibleTo`。
> 因此这里必须是 `public`。改为：

```csharp
namespace Demo.EditorTools
{
    /// <summary>
    /// 仅用于验证 Demo.Editor 程序集可被测试程序集引用。
    /// </summary>
    public static class NamespaceAnchor
    {
    }
}
```

- [ ] **Step 6: 删除旧的 PlayerController.cs**

```bash
cd "E:/unity/求职demo"
rm Assets/Script/PlayerController.cs Assets/Script/PlayerController.cs.meta
```

旧文件在 `Assets/Script/` 根下，会与 `Assets/Script/Player/` 下的新文件重名冲突（同名类 `PlayerController`）。
Task 14 会在新位置重建它。

- [ ] **Step 7: 运行测试，确认通过**

图形界面方式：Unity 菜单 `Window > General > Test Runner` → `EditMode` 标签 → `Run All`。

命令行方式（**需要先关闭 Unity 编辑器**，否则会因项目被占用而失败）：

```bash
"/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
  -batchmode -nographics \
  -projectPath "E:/unity/求职demo" \
  -runTests -testPlatform EditMode \
  -testResults "E:/unity/求职demo/Temp/editmode-results.xml" \
  -logFile -
```

Expected: 两个测试通过，退出码 `0`。结果文件中 `<test-run result="Passed" passed="2" failed="0">`。

- [ ] **Step 8: 确认 Unity 未报编译错误**

打开 Unity，检查 Console 无红色错误。特别确认 `Assembly-CSharp` 中没有残留的 `PlayerController` 报错。

- [ ] **Step 9: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Demo.Runtime.asmdef Assets/Script/Editor/ \
        Assets/Tests/ Assets/Script/PlayerController.cs Assets/Script/PlayerController.cs.meta
git commit -m "build: 建立运行时/编辑器/测试三个程序集，移除旧 PlayerController"
```

---

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

## Task 3: AnimationClipLocator 剪辑定位

把"从哪个 fbx 里取哪个剪辑"这件事收进一个类。资源包的布局**三套并存**，必须逐一区分：移动类在 `InPlace/` 与 `RootMotion/` 各一份（名字中缀不同，分别是 `_InPlace_` 与 `_RM_`）；动作类（待机/防御/受击/死亡等）只有一份且放在**架势根目录**；连招类两份分散在前两个目录里。路径必须显式构造，不能靠猜。

**Files:**
- Create: `Assets/Script/Editor/AnimationClipLocator.cs`
- Test: `Assets/Tests/EditMode/AnimationClipLocatorTests.cs`

**Interfaces:**
- Consumes: 无
- Produces:
  - `Demo.EditorTools.AnimationClipLocator.InPlacePath(string clipName) -> string`
  - `Demo.EditorTools.AnimationClipLocator.RootMotionPath(string clipName) -> string`
  - `Demo.EditorTools.AnimationClipLocator.LoadClip(string fbxPath) -> AnimationClip`
  - `Demo.EditorTools.AnimationClipLocator.LoadInPlace(string) -> AnimationClip`
    （先查 `InPlace/`，取不到回退到架势根目录 —— 动作类剪辑只存在于根目录）
  - `Demo.EditorTools.AnimationClipLocator.StanceRootPath(string) -> string`
  - `Demo.EditorTools.AnimationClipLocator.LoadRootMotion(string) -> AnimationClip`

- [ ] **Step 1: 写测试**

`Assets/Tests/EditMode/AnimationClipLocatorTests.cs`：

```csharp
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
```

- [ ] **Step 2: 运行测试，确认失败**

Expected: 编译失败，`AnimationClipLocator` 不存在。

- [ ] **Step 3: 实现 AnimationClipLocator**

`Assets/Script/Editor/AnimationClipLocator.cs`：

```csharp
using UnityEditor;
using UnityEngine;

namespace Demo.EditorTools
{
    /// <summary>
    /// 从资源的 fbx 文件中定位并加载 AnimationClip。
    ///
    /// 资源包的布局三套并存，已逐一核对过磁盘：
    ///   - 位移类：InPlace/ 与 RootMotion/ 各一份，名字中缀分别是 `_InPlace_` 与 `_RM_`，
    ///     Animation/SwordAndShield/InPlace/&lt;名字&gt;.fbx
    ///     Animation/SwordAndShield/RootMotion/&lt;名字&gt;.fbx
    ///   - 动作类（待机/防御/受击/死亡等）：只有一份，且放在**架势根目录**，
    ///     Animation/SwordAndShield/&lt;名字&gt;.fbx
    ///   - 连招类：两份分散在上面两个目录里
    /// 因此必须显式构造路径，不能只靠名字查找。
    /// </summary>
    public static class AnimationClipLocator
    {
        public const string HeroAnimationRoot = "Assets/RPGTinyHeroWavePBR/Animation/SwordAndShield";
        public const string InPlaceDir = HeroAnimationRoot + "/InPlace";
        public const string RootMotionDir = HeroAnimationRoot + "/RootMotion";

        // 架势根目录：动作类剪辑唯一的一份就放在这里
        public static string StanceRootPath(string clipName) => $"{HeroAnimationRoot}/{clipName}.fbx";

        public static string InPlacePath(string clipName) => $"{InPlaceDir}/{clipName}.fbx";

        public static string RootMotionPath(string clipName) => $"{RootMotionDir}/{clipName}.fbx";

        /// <summary>
        /// 加载「不驱动根运动」的那一版剪辑。
        ///
        /// 资源包的布局并不统一（已逐一核对过磁盘）：
        ///   - 移动类（Move*/Sprint*/Jump*）：InPlace/ 与 RootMotion/ 各一份，名字中缀不同
        ///   - 动作类（Idle/Defend/DefendHit/Dizzy/GetHit*/GetUp/Die*）：**只有一份，放在架势根目录**，
        ///     InPlace/ 与 RootMotion/ 里都没有
        ///   - 连招类：Combo01_RM_* 在 RootMotion/，Combo05_InPlaceWithRMHeight_* 在 InPlace/
        /// 所以先查 InPlace/，取不到再回退到架势根目录。
        /// 「不驱动根运动」对动作类剪辑而言本来就是根目录那一份，语义一致，不是权宜之计。
        /// 当前 19 个被引用的名字里没有任何一个同时存在于这两处，回退不会造成歧义。
        /// </summary>
        public static AnimationClip LoadInPlace(string clipName) =>
            LoadClip(InPlacePath(clipName)) ?? LoadClip(StanceRootPath(clipName));

        public static AnimationClip LoadRootMotion(string clipName) => LoadClip(RootMotionPath(clipName));

        /// <summary>
        /// 加载 fbx 中的主 AnimationClip。
        /// fbx 里可能含多个子资源（网格、材质、多个 take），
        /// 这里取名字最匹配的那个，并跳过 Unity 生成的 __preview__ 剪辑。
        /// </summary>
        public static AnimationClip LoadClip(string fbxPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            if (assets == null || assets.Length == 0)
                return null;

            string wanted = System.IO.Path.GetFileNameWithoutExtension(fbxPath);

            AnimationClip best = null;
            foreach (var asset in assets)
            {
                if (asset is not AnimationClip clip)
                    continue;
                if (clip.name.StartsWith("__preview__"))
                    continue;

                // 完全同名，直接采用
                if (clip.name == wanted)
                    return clip;

                // 否则记住第一个候选，作为兜底
                if (best == null)
                    best = clip;
            }

            return best;
        }
    }
}
```

- [ ] **Step 4: 运行测试，确认通过**

Run: Unity Test Runner → EditMode → Run All
Expected: 6 个测试全部 PASS。

若"加载InPlace剪辑"失败，说明 fbx 内剪辑命名与文件名不一致。用下面的调试代码确认实际名字，然后调整匹配逻辑：

```csharp
foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
    Debug.Log($"{a.GetType().Name} :: {a.name}");
```

- [ ] **Step 5: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Editor/AnimationClipLocator.cs Assets/Tests/EditMode/AnimationClipLocatorTests.cs
git commit -m "feat: 新增 AnimationClipLocator 按目录区分 InPlace 与 RootMotion 剪辑"
```

---

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

            // ---- 图层入口 ----
            // 不写这一行的后果是实测出来的：Unity 会把**根状态机**的 defaultState
            // 自动填成图里第一个创建的状态，也就是 Idle_Normal —— 而 Idle_Normal
            // 是 Locomotion 的子节点、并且 m_Transitions 为空（零出边）。
            // 于是角色一进场就卡在那个孤立待机里，永远到不了混合树：
            // 锁定、战斗架势、移动全部失效，而全部 EditMode 测试依然通过。
            //
            // 只写入口转换，**不要**同时去赋根状态机的 defaultState：
            // 根状态机没有直接子状态（六个组都是子状态机），defaultState 要求
            // 指向本机的子状态，赋值只能指向一个 root 并不拥有的状态（就是上面那个
            // Idle_Normal），等于把一个说不通的语义写进资产。有入口转换时，
            // defaultState 是被绕过的死值。
            //
            // 这一行究竟够不够，由 Task 5b 的 PlayMode 测试裁决 —— EditMode 看不见运行时行为。
            root.AddEntryTransition(locomotion);

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

## Task 5b: 图层入口显式化与 PlayMode 验证

Task 5 的代码评审发现一个**只有在运行时才暴露**的缺陷：生成出的控制器，图层根状态机没有入口。

已核实的证据（Task 5 生成的那份 `Assets/Animator/Hero_SwordAndShield.controller`，三个事实都在 YAML 里）：

- 根状态机 `Base Layer` 的 `m_ChildStates: []`（没有直接子状态，六个组都是子状态机）
- 同一台机器的 `m_EntryTransitions: []`（没有入口转换）
- 它的 `m_DefaultState` 指向 `Idle_Normal` —— 而 `Idle_Normal` 是 `Locomotion` 子状态机的子节点，
  并且 `m_Transitions: []`（零出边）

于是运行时 Animator 落在 `Idle_Normal`，没有任何出边，永远到不了 `Tree_Free_Normal`：
移动、锁定、战斗架势全部失效。而 Task 4 / Task 5 的全部 EditMode 断言照样通过，
因为它们只检查**结构存在**，检查不了**运行时走到哪**。

所以这一步做两件事：生成器里显式写入口（Step 4），加一套 PlayMode 测试**真正证明**入口通了（Step 1–3、5），
再加一条 EditMode 结构断言防止日后被人删掉（Step 6）。

**Files:**
- Modify: `Assets/Script/Editor/HeroAnimatorBuilder.cs`（`BuildLocomotion` 内，`locomotion.defaultState = stFreeNormal;` 之后）
- Modify: `Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs`（追加一条入口断言）
- Create: `Assets/Tests/PlayMode/Demo.Tests.PlayMode.asmdef`
- Create: `Assets/Tests/PlayMode/AnimatorEntryTests.cs`

**Interfaces:**
- Consumes: `Demo.AnimatorParams`（Task 2）的 `ControllerPath` / `LayerName` / `Groups.Locomotion` / `States.TreeFreeNormal`；`HeroAnimatorBuilder.Build()`（Task 4）
- Produces: 程序集 `Demo.Tests.PlayMode`。后续任何需要"运行时才看得见"的验证（Task 8 的根运动、Task 12 的参数写入）都可以复用这套探针写法

- [ ] **Step 1: 创建 PlayMode 测试程序集定义**

`Assets/Tests/PlayMode/Demo.Tests.PlayMode.asmdef`：

```json
{
    "name": "Demo.Tests.PlayMode",
    "rootNamespace": "Demo.Tests",
    "references": [
        "Demo.Runtime",
        "Demo.Editor",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

> **`includePlatforms` 必须是空数组，不能照抄 EditMode 那套写 `["Editor"]`** ——
> 这是实测出来的：`includePlatforms: ["Editor"]` 时 `-testPlatform PlayMode` 一个测试都找不到
> （`total=0`，退出码还是 `0`，看起来像"跑过了"，其实压根没跑）。
> Unity Test Framework 是按程序集的 `EditorOnly` 标志给测试分类的，Editor-only 就等于 EditMode。
> 源码原文（`Library/PackageCache/com.unity.test-framework@1.1.33/UnityEditor.TestRunner/`
> `TestRunner/Utils/EditorLoadedTestAssemblyProvider.cs:60`）：
>
> ```csharp
> var assemblyType = (assemblyFlags & AssemblyFlags.EditorOnly) == AssemblyFlags.EditorOnly
>     ? TestPlatform.EditMode : TestPlatform.PlayMode;
> ```
>
> Unity 自己生成的 PlayMode 测试模板本来也不带 `includePlatforms`。
>
> ⚠️ `total=0` 配 `result="Passed"` 是个危险的假绿：判断 PlayMode 是否真的跑起来，
> 要看 `total` 是不是 1，不能只看退出码。

- [ ] **Step 2: 写 PlayMode 测试**

`Assets/Tests/PlayMode/AnimatorEntryTests.cs`：

```csharp
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
        }

        [UnityTest]
        public IEnumerator 首帧停在Locomotion的混合树状态()
        {
            // 探针身上没有模型、没有 Avatar，人形剪辑的重定向告警是预期噪声，
            // 与被测对象（状态机走到哪）无关。不忽略它会把测试变成假失败。
            //
            // ⚠️ 这行**必须在测试体里**，不能挪进 [OneTimeSetUp]：
            // LogAssert.ignoreFailingMessages 是**当前 LogScope 实例**的属性
            // （UTF 源码 UnityEngine.TestRunner/Assertions/LogScope/LogScope.cs，
            // 静态属性只是转发到 LogScope.Current），而每个测试各有自己的 scope、
            // 在测试结束时销毁。写在 [OneTimeSetUp] 里的话，那个 scope 在方法返回时
            // 就没了，设置随之失效 —— 实测日志里只出现一次 `IgnoreFailingMessages:true`
            // 而从未出现 `false`，正是 [TearDown] 那次复位打在了一个本就为 false 的
            // 新 scope 上。不确定这行到底有没有生效，就去日志里数
            // `IgnoreFailingMessages` 出现的次数与位置。
            LogAssert.ignoreFailingMessages = true;

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
```

> **探针为什么用空 GameObject**：状态机求值不依赖 Avatar，只有骨骼重定向才依赖。
> 被测对象是"走到哪个状态"，不需要模型，空物体最轻、也不碰受保护的资源包。
> 若实测发现状态机压根没求值（失败信息里 `shortNameHash=0`），
> 就把 `NewProbe()` 换成实例化带 Avatar 的预制体：
> `Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
> "Assets/RPGTinyHeroWavePBR/Prefab/ModularCharacters/MC01.prefab"))`，
> 再取其上的 Animator 覆盖 `runtimeAnimatorController`。用哪个都要在报告里写清实测结果。

- [ ] **Step 3: 运行 PlayMode 测试，确认它失败（RED）**

**先跑这一步再改生成器** —— 现在磁盘上的控制器就是 Task 5 生成的、没有入口的那一份，
RED 是这个缺陷的直接证据。

```bash
"/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
  -batchmode -nographics \
  -projectPath "E:/unity/求职demo" \
  -runTests -testPlatform PlayMode \
  -testResults "E:/unity/求职demo/Temp/playmode-results.xml" \
  -logFile -
```

Expected: 退出码 `1`，`首帧停在Locomotion的混合树状态` FAIL，
失败信息里当前状态是 `Idle_Normal` 的哈希与剪辑名 —— 不是 `Tree_Free_Normal`。
实测到的原文长这样：

```
第 0 层当前状态应为 Tree_Free_Normal（shortNameHash=193745133），
实际 shortNameHash=2124725196、fullPathHash=312396153，正在播放：Idle_Normal_SwordAndShield
```

结果文件中 `<test-run result="Failed(Child)" total="1" passed="0" failed="1">`。

> **判 RED 之前先看 `total`。** 程序集装配不对时（例如 `includePlatforms` 写成了 `["Editor"]`），
> UTF 一个测试都发现不了，结果是 `total="0"`、`result="Passed"`、退出码 `0` —— 一个看起来
> 全绿的假象。`total` 不是 1 就说明测试没跑起来，不构成 RED 证据。
>
> 若失败原因是编译错误（退出码 `2`、没有结果 XML），同样不是 RED 证据，先解决装配问题。

- [ ] **Step 4: 在生成器里显式写入口**

`Assets/Script/Editor/HeroAnimatorBuilder.cs` 的 `BuildLocomotion` 中，
在 `locomotion.defaultState = stFreeNormal;` 之后追加：

```csharp
            locomotion.defaultState = stFreeNormal;

            // ---- 图层入口 ----
            //
            // 缺陷现场：不写这两行时，根状态机 Base Layer 的 m_ChildStates 与
            // m_EntryTransitions 都是空的，m_DefaultState 被 Unity 自动填成
            // Idle_Normal —— 那是 Locomotion 的子节点，而且 m_Transitions 为空（零出边）。
            // 角色一进场就卡在这个孤立待机里，永远到不了混合树：移动、锁定、
            // 战斗架势全部失效。而所有 EditMode 断言照样通过，因为它们只验证结构存在。
            //
            // **两行的分量不一样，这是 PlayMode 探针逐帧实测出来的**（四种组合各跑一遍）：
            //   · 只有入口转换 + Unity 自动填的 defaultState → 首帧停在 Idle_Normal，坏；
            //   · 入口转换 + defaultState = null            → 仍是 Idle_Normal
            //     （赋 null 是空操作，读回还是旧值，不能靠清空让别的接管）；
            //   · 入口转换 + defaultState = Tree_Free_Normal → 正确；
            //   · **没有入口转换** + defaultState = Tree_Free_Normal → 同样正确。
            //
            // 结论：**决定图层启动状态的是 defaultState，根状态机的入口转换不参与启动。**
            // 入口转换仍然保留，但它的作用是让 Animator 窗口里 Entry 正确连到 Locomotion 组、
            // 让图不悬空 —— 不是运行时的必要条件。所以注释里别再写"缺任一行动画都跑不起来"，
            // 那是实测证伪的说法。
            //
            // defaultState 指向的是 Locomotion 的子状态（root 并不拥有它）——
            // 这与 Unity 自动填入 Idle_Normal 属于同一种"外部指针"，区别只在于
            // 这里指向的是设计上正确的入口。根状态机没有直接子状态（六个组都是子状态机），
            // 没有"root 自己拥有"的候选可选。
            root.AddEntryTransition(locomotion);
            root.defaultState = stFreeNormal;
```

- [ ] **Step 5: 运行 PlayMode 测试，确认它通过（GREEN）**

```bash
"/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
  -batchmode -nographics \
  -projectPath "E:/unity/求职demo" \
  -runTests -testPlatform PlayMode \
  -testResults "E:/unity/求职demo/Temp/playmode-results.xml" \
  -logFile -
```

Expected: 退出码 `0`，`首帧停在Locomotion的混合树状态` PASS。
结果文件中 `<test-run result="Passed" passed="1" failed="0">`。

> 测试的 `[OneTimeSetUp]` 会调用 `HeroAnimatorBuilder.Build()` 重新生成控制器，
> 所以磁盘上的 `Assets/Animator/Hero_SwordAndShield.controller` 此时已经带上入口转换。
> 无需手动执行菜单项。

- [ ] **Step 6: 补一条 EditMode 结构断言（快速回归网）**

在 `Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs` 的 `HeroAnimatorBuilderTests` 类里追加：

```csharp
        [Test]
        public void 根状态机的入口指向Locomotion()
        {
            var root = _ctrl.layers[0].stateMachine;

            // PlayMode 测试才能证明"运行时走得到"，这条断言只负责在有人删掉入口时快速报警。
            Assert.AreEqual(1, root.entryTransitions.Length, "根状态机应当只有一条入口转换");
            var dest = root.entryTransitions[0].destinationStateMachine;
            Assert.IsNotNull(dest, "入口转换的终点应当是一个子状态机");
            Assert.AreEqual(AnimatorParams.Groups.Locomotion, dest.name);

            // defaultState 必须一起钉住：实测表明"图层启动状态由它决定、入口转换不参与启动"，
            // 只钉入口转换的话，把 defaultState 那行删掉测试照样全绿，
            // 而运行时又会退回 Unity 自动填入的 Idle_Normal（零出边）—— 缺陷原样复现。
            var def = root.defaultState;
            Assert.IsNotNull(def, "根状态机必须有 defaultState —— 它才是图层实际的启动状态");
            Assert.AreEqual(AnimatorParams.States.TreeFreeNormal, def.name,
                "defaultState 应指向 Locomotion 组的入口状态，不能是那个零出边的 Idle_Normal");
        }
```

- [ ] **Step 7: 运行 EditMode 测试，确认全部通过且无回归**

```bash
"/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
  -batchmode -nographics \
  -projectPath "E:/unity/求职demo" \
  -runTests -testPlatform EditMode \
  -testResults "E:/unity/求职demo/Temp/editmode-results.xml" \
  -logFile -
```

Expected: 新增的 `根状态机的入口指向Locomotion` PASS；
Task 5 之前的结果里那条预期失败（`每个状态都被归入某个子状态机`）保持不变，其余全部 PASS。

- [ ] **Step 8: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Editor/HeroAnimatorBuilder.cs \
        Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs \
        Assets/Tests/PlayMode/
git commit -m "fix: 显式指定图层入口，加 PlayMode 测试证明运行时进得去"
```

---

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

## Task 9: 两个 StateMachineBehaviour

位移管线靠这两个标记决定"这一帧的水平位移从哪来"。放在 Task 8 之前执行可以避免临时编译错误。

**Files:**
- Create: `Assets/Script/Player/RootMotionTag.cs`
- Create: `Assets/Script/Player/LockMovementTag.cs`

**Interfaces:**
- Consumes: `Demo.PlayerMotor`（Task 11 提供 `UseRootMotion` 与 `MovementLocked` 属性）
- Produces:
  - `Demo.RootMotionTag : StateMachineBehaviour`
  - `Demo.LockMovementTag : StateMachineBehaviour`

> **依赖提示**：这两个类引用 `PlayerMotor`。若先做本任务，会因 `PlayerMotor` 不存在而编译失败。
> 解决办法：先做 Task 11，再做本任务；或先做本任务并把 `PlayerMotor` 的两个属性提前建好。
> **推荐顺序：Task 11 → Task 9 → Task 8。**

- [ ] **Step 1: 实现 RootMotionTag**

`Assets/Script/Player/RootMotionTag.cs`：

```csharp
using UnityEngine;

namespace Demo
{
    /// <summary>
    /// 挂在所有使用根运动剪辑的状态上（翻滚、冲刺、重攻击）。
    ///
    /// PlayerMotor 在 OnAnimatorMove 里读这个标志，决定这一帧的水平位移
    /// 是用 animator.deltaPosition（根运动）还是用代码计算的速度。
    ///
    /// 为什么用 StateMachineBehaviour 而不是在代码里判断 deltaPosition 的大小：
    /// 后者是隐式约定，换一套动画资源就会静默失效；前者是显式声明，
    /// 在 Animator 窗口里能一眼看见哪些状态在用根运动。
    /// </summary>
    public class RootMotionTag : StateMachineBehaviour
    {
        // 注意：StateMachineBehaviour 是资源，所有使用该控制器的 Animator 共享同一个实例。
        // 因此绝不能把 PlayerMotor 缓存在字段里——那会把不同角色的引用串在一起。
        // 每次都从 animator 上取，这是唯一正确的做法。

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            Set(animator, true);
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            Set(animator, false);
        }

        /// <summary>
        /// 兜底。状态机被整体退出时 Unity 不保证逐个调用 OnStateExit，
        /// 不补这一手会出现"翻滚结束后角色再也不受控"的诡异 bug。
        /// </summary>
        public override void OnStateMachineExit(Animator animator, int stateMachinePathHash)
        {
            Set(animator, false);
        }

        private static void Set(Animator animator, bool value)
        {
            var motor = animator.GetComponent<PlayerMotor>();
            if (motor != null)
                motor.UseRootMotion = value;
        }
    }
}
```

- [ ] **Step 2: 实现 LockMovementTag**

`Assets/Script/Player/LockMovementTag.cs`：

```csharp
using UnityEngine;

namespace Demo
{
    /// <summary>
    /// 挂在攻击、翻滚、冲刺这些动作状态上。
    ///
    /// 作用是在动作播放期间屏蔽移动输入，防止角色一边挥剑一边被 WASD 推着滑走。
    /// 与 RootMotionTag 的区别：RootMotionTag 决定"位移从哪来"，
    /// LockMovementTag 决定"这一帧还要不要接受玩家的移动输入"。
    /// 两者可以同时挂在一个状态上（翻滚就是这种情况）。
    /// </summary>
    public class LockMovementTag : StateMachineBehaviour
    {
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            Set(animator, true);
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            Set(animator, false);
        }

        public override void OnStateMachineExit(Animator animator, int stateMachinePathHash)
        {
            Set(animator, false);
        }

        private static void Set(Animator animator, bool value)
        {
            var motor = animator.GetComponent<PlayerMotor>();
            if (motor != null)
                motor.MovementLocked = value;
        }
    }
}
```

- [ ] **Step 3: 确认编译通过**

回到 Unity，确认 Console 无编译错误。

- [ ] **Step 4: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Player/RootMotionTag.cs Assets/Script/Player/LockMovementTag.cs
git commit -m "feat: 新增 RootMotionTag 与 LockMovementTag 两个状态标记行为"
```

---

## Task 10: PlayerInputReader

读键鼠输入，翻译成语义化属性。这是**唯一**接触 `UnityEngine.Input` 的地方——将来若迁移到新输入系统，只改这一个文件。

为了让死区与缩放逻辑可测，把它们抽成纯静态方法。

**Files:**
- Create: `Assets/Script/Player/PlayerInputReader.cs`
- Test: `Assets/Tests/EditMode/PlayerInputReaderTests.cs`

**Interfaces:**
- Consumes: 无
- Produces:
  - `Demo.PlayerInputReader.MoveInput -> Vector2`
  - `Demo.PlayerInputReader.SprintHeld / LightAttackPressed / HeavyAttackPressed / RollPressed / JumpPressed / DefendHeld / LockOnPressed / SwitchTargetPressed -> bool`
  - `Demo.PlayerInputReader.ApplyDeadzone(Vector2 raw, float deadzone) -> Vector2`（静态）
  - `Demo.PlayerInputReader.SnapToEightDirections(Vector2 v) -> Vector2`（静态）

- [ ] **Step 1: 写测试**

`Assets/Tests/EditMode/PlayerInputReaderTests.cs`：

```csharp
using NUnit.Framework;
using UnityEngine;
using Demo;

namespace Demo.Tests
{
    public class PlayerInputReaderTests
    {
        [Test]
        public void 死区内的输入_被归零()
        {
            var r = PlayerInputReader.ApplyDeadzone(new Vector2(0.1f, 0.1f), 0.2f);
            Assert.AreEqual(Vector2.zero, r);
        }

        [Test]
        public void 死区外的输入_被保留并重新归一()
        {
            var r = PlayerInputReader.ApplyDeadzone(new Vector2(1f, 0f), 0.2f);
            Assert.AreEqual(1f, r.magnitude, 0.001f);
        }

        [Test]
        public void 斜向输入_合成量不超过一()
        {
            var r = PlayerInputReader.ApplyDeadzone(new Vector2(1f, 1f), 0.2f);
            Assert.LessOrEqual(r.magnitude, 1.001f);
        }

        [Test]
        public void 零输入_返回零()
        {
            Assert.AreEqual(Vector2.zero, PlayerInputReader.ApplyDeadzone(Vector2.zero, 0.2f));
        }

        [Test]
        public void 八方向吸附_斜向被吸附到四十五度()
        {
            var v = PlayerInputReader.SnapToEightDirections(new Vector2(1f, 0.9f));
            Assert.AreEqual(45f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg, 1f);
        }

        [Test]
        public void 八方向吸附_正前方不被改动()
        {
            var v = PlayerInputReader.SnapToEightDirections(new Vector2(0f, 1f));
            Assert.AreEqual(0f, v.x, 0.001f);
            Assert.AreEqual(1f, v.y, 0.001f);
        }

        [Test]
        public void 八方向吸附_零输入返回零()
        {
            Assert.AreEqual(Vector2.zero, PlayerInputReader.SnapToEightDirections(Vector2.zero));
        }
    }
}
```

- [ ] **Step 2: 运行测试，确认失败**

Expected: 编译失败，`PlayerInputReader` 不存在。

- [ ] **Step 3: 实现 PlayerInputReader**

`Assets/Script/Player/PlayerInputReader.cs`：

```csharp
using UnityEngine;

namespace Demo
{
    /// <summary>
    /// 唯一接触 UnityEngine.Input 的地方。
    /// 把原始键鼠输入翻译成有语义的属性，其余脚本一律不直接读 Input。
    /// 将来迁移到新输入系统时，只需要重写这一个类。
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("输入手感")]
        [Tooltip("左摇杆 / WASD 的死区半径")]
        [SerializeField] private float moveDeadzone = 0.2f;

        [Tooltip("是否把移动方向吸附到八个方向。锁定战斗时开启手感更稳")]
        [SerializeField] private bool snapToEightDirections = true;

        public Vector2 MoveInput { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool LightAttackPressed { get; private set; }
        public bool HeavyAttackPressed { get; private set; }
        public bool RollPressed { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool DefendHeld { get; private set; }
        public bool LockOnPressed { get; private set; }

        private void Update()
        {
            var raw = new Vector2(
                Input.GetAxis("Horizontal"),
                Input.GetAxis("Vertical"));

            var filtered = ApplyDeadzone(raw, moveDeadzone);
            MoveInput = snapToEightDirections
                ? SnapToEightDirections(filtered)
                : filtered;

            SprintHeld = Input.GetKey(KeyCode.LeftShift);
            DefendHeld = Input.GetKey(KeyCode.F);

            LightAttackPressed = Input.GetMouseButtonDown(0);
            HeavyAttackPressed = Input.GetMouseButtonDown(1);

            RollPressed = Input.GetKeyDown(KeyCode.LeftAlt);
            JumpPressed = Input.GetKeyDown(KeyCode.Space);
            LockOnPressed = Input.GetKeyDown(KeyCode.Q);
        }

        /// <summary>
        /// 死区处理：死区内归零，死区外把剩余区间重新拉伸回 0..1，
        /// 这样摇杆离开死区的瞬间不会突然跳变。
        /// </summary>
        public static Vector2 ApplyDeadzone(Vector2 raw, float deadzone)
        {
            float magnitude = raw.magnitude;

            if (magnitude <= deadzone || magnitude <= Mathf.Epsilon)
                return Vector2.zero;

            // 把 [deadzone, 1] 映射到 [0, 1]
            float rescaled = Mathf.Clamp01((magnitude - deadzone) / (1f - deadzone));
            return raw.normalized * rescaled;
        }

        /// <summary>
        /// 把任意方向吸附到最近的 45 度倍数。
        /// 好处是混合树的权重不会在四个方向动画之间来回抖动。
        /// </summary>
        public static Vector2 SnapToEightDirections(Vector2 v)
        {
            if (v.sqrMagnitude < Mathf.Epsilon)
                return Vector2.zero;

            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            float snapped = Mathf.Round(angle / 45f) * 45f;
            float rad = snapped * Mathf.Deg2Rad;

            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * v.magnitude;
        }
    }
}
```

- [ ] **Step 4: 运行测试，确认通过**

Run: Unity Test Runner → EditMode → Run All
Expected: 7 个测试全部 PASS。

- [ ] **Step 5: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Player/PlayerInputReader.cs Assets/Tests/EditMode/PlayerInputReaderTests.cs
git commit -m "feat: PlayerInputReader，输入翻译与死区/八方向吸附"
```

---

## Task 11: PlayerMotor —— 统一位移管线

本项目的核心技术点。`OnAnimatorMove()` 是**唯一**的水平位移入口，代码位移与根运动在此汇聚。

**Files:**
- Create: `Assets/Script/Player/PlayerMotor.cs`
- Test: `Assets/Tests/EditMode/PlayerMotorTests.cs`

**Interfaces:**
- Consumes: 无（`RootMotionTag` / `LockMovementTag` 反过来依赖本类）
- Produces:
  - `Demo.PlayerMotor.UseRootMotion -> bool { get; set; }`
  - `Demo.PlayerMotor.MovementLocked -> bool { get; set; }`
  - `Demo.PlayerMotor.PlanarSpeed -> float { get; }`
  - `Demo.PlayerMotor.ResolveHorizontalDelta(bool, Vector3, Vector3, float) -> Vector3`（静态）
  - `Demo.PlayerMotor.ComputeSpeedParameter(bool, bool) -> float`（静态）
  - `Demo.PlayerMotor.SetMoveDirection(Vector3 worldDirection, bool sprinting)`
  - `Demo.PlayerMotor.Jump()`
  - `Demo.PlayerMotor.FaceDirection(Vector3 worldDirection, float deltaTime)`

- [ ] **Step 1: 写测试**

`Assets/Tests/EditMode/PlayerMotorTests.cs`：

```csharp
using NUnit.Framework;
using UnityEngine;
using Demo;

namespace Demo.Tests
{
    public class PlayerMotorTests
    {
        // ---------- ResolveHorizontalDelta ----------

        [Test]
        public void 根运动模式_采用动画位移并抹平Y轴()
        {
            var d = PlayerMotor.ResolveHorizontalDelta(
                useRootMotion: true,
                rootMotionDelta: new Vector3(1f, 0.5f, 2f),
                codeVelocity: new Vector3(99f, 0f, 99f),
                deltaTime: 0.02f);

            Assert.AreEqual(1f, d.x, 0.001f);
            Assert.AreEqual(0f, d.y, "水平位移必须抹掉 Y 轴，垂直方向由重力单独负责");
            Assert.AreEqual(2f, d.z, 0.001f);
        }

        [Test]
        public void 代码模式_采用速度乘以时间()
        {
            var d = PlayerMotor.ResolveHorizontalDelta(
                useRootMotion: false,
                rootMotionDelta: new Vector3(99f, 0f, 99f),
                codeVelocity: new Vector3(3f, 0f, 4f),
                deltaTime: 0.5f);

            Assert.AreEqual(1.5f, d.x, 0.001f);
            Assert.AreEqual(0f, d.y);
            Assert.AreEqual(2f, d.z, 0.001f);
        }

        [Test]
        public void 代码模式_速度为竖直时水平位移为零()
        {
            var d = PlayerMotor.ResolveHorizontalDelta(
                false, Vector3.zero, new Vector3(0f, -20f, 0f), 0.02f);
            Assert.AreEqual(Vector3.zero, d);
        }

        // ---------- ComputeSpeedParameter ----------

        [Test]
        public void 无输入时_Speed参数为零()
        {
            Assert.AreEqual(0f, PlayerMotor.ComputeSpeedParameter(false, false));
            Assert.AreEqual(0f, PlayerMotor.ComputeSpeedParameter(false, true));
        }

        [Test]
        public void 有输入不冲刺_Speed参数为半()
        {
            Assert.AreEqual(0.5f, PlayerMotor.ComputeSpeedParameter(true, false));
        }

        [Test]
        public void 有输入且冲刺_Speed参数为一()
        {
            Assert.AreEqual(1f, PlayerMotor.ComputeSpeedParameter(true, true));
        }

        [Test]
        public void Speed参数_始终落在混合树定义域内()
        {
            foreach (bool moving in new[] { false, true })
            foreach (bool sprint in new[] { false, true })
            {
                float v = PlayerMotor.ComputeSpeedParameter(moving, sprint);
                Assert.GreaterOrEqual(v, 0f);
                Assert.LessOrEqual(v, 1f);
            }
        }
    }
}
```

- [ ] **Step 2: 运行测试，确认失败**

Expected: 编译失败，`PlayerMotor` 不存在。

- [ ] **Step 3: 实现 PlayerMotor**

`Assets/Script/Player/PlayerMotor.cs`：

```csharp
using UnityEngine;

namespace Demo
{
    /// <summary>
    /// 位移的唯一负责人。
    ///
    /// 核心设计：OnAnimatorMove() 是水平位移的唯一出口。无论位移来自代码速度
    /// 还是来自动画的根运动，都汇聚到那一个 controller.Move() 调用上。
    /// 这样就不会出现"两处同时调 Move 互相打架"这个经典 bug。
    ///
    /// 本类完全不知道动画状态机里有什么状态，只读两个由 StateMachineBehaviour
    /// 设置的标志位。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("速度")]
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float sprintSpeed = 6.0f;

        [Header("转向")]
        [Tooltip("角色朝向移动方向的平滑速度。锁定时不使用")]
        [SerializeField] private float facingRotationSpeed = 12f;

        [Header("跳跃与重力")]
        [SerializeField] private float jumpHeight = 1.5f;
        [SerializeField] private float gravity = -20f;

        [Header("着地检测")]
        [SerializeField] private float groundedStickVelocity = -2f;

        /// <summary>由 RootMotionTag 设置。</summary>
        public bool UseRootMotion { get; set; }

        /// <summary>由 LockMovementTag 设置。</summary>
        public bool MovementLocked { get; set; }

        /// <summary>当前水平速度大小，供动画层读取。</summary>
        public float PlanarSpeed { get; private set; }

        public bool IsGrounded => _controller.isGrounded;

        private CharacterController _controller;
        private Animator _animator;

        private Vector3 _codeVelocity;     // 水平，代码驱动
        private float _verticalVelocity;   // 垂直，始终由代码驱动
        private bool _jumpRequested;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponent<Animator>();
        }

        private void Update()
        {
            ApplyGravityAndJump();
        }

        /// <summary>
        /// 由 PlayerAnimatorDriver 每帧调用，告知期望的移动方向。
        /// 这里只记录意图，真正的位移在 OnAnimatorMove 里统一执行。
        /// </summary>
        public void SetMoveDirection(Vector3 worldDirection, bool sprinting)
        {
            if (MovementLocked)
            {
                _codeVelocity = Vector3.zero;
                PlanarSpeed = 0f;
                return;
            }

            worldDirection.y = 0f;
            worldDirection = Vector3.ClampMagnitude(worldDirection, 1f);

            float speed = sprinting ? sprintSpeed : moveSpeed;
            _codeVelocity = worldDirection * speed;

            // 注意：这里用的是角色"实际"移动速度而不是期望速度。
            // 根运动状态下手动置零，避免混合树误判为在移动。
            PlanarSpeed = UseRootMotion ? 0f : _codeVelocity.magnitude;
        }

        /// <summary>把角色平滑转向指定方向。锁定状态下不调用。</summary>
        public void FaceDirection(Vector3 worldDirection, float deltaTime)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f)
                return;

            var target = Quaternion.LookRotation(worldDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, target, facingRotationSpeed * deltaTime);
        }

        /// <summary>立即转向，不插值。锁定时使用。</summary>
        public void FaceDirectionImmediate(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f)
                return;
            transform.rotation = Quaternion.LookRotation(worldDirection);
        }

        public void Jump()
        {
            if (_controller.isGrounded)
                _jumpRequested = true;
        }

        private void ApplyGravityAndJump()
        {
            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = groundedStickVelocity;

            if (_jumpRequested)
            {
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                _jumpRequested = false;
            }

            _verticalVelocity += gravity * Time.deltaTime;
        }

        /// <summary>
        /// 位移的唯一出口。
        ///
        /// 一旦实现了 OnAnimatorMove，Unity 就不再自动应用根运动，
        /// 必须由我们自己把它交给 CharacterController —— 这正是本设计依赖的行为。
        /// </summary>
        private void OnAnimatorMove()
        {
            if (_animator == null)
                return;

            Vector3 horizontal = ResolveHorizontalDelta(
                UseRootMotion, _animator.deltaPosition, _codeVelocity, Time.deltaTime);

            Vector3 vertical = Vector3.up * _verticalVelocity * Time.deltaTime;
            _controller.Move(horizontal + vertical);

            // 根运动状态同时接管旋转（旋转跳需要）
            if (UseRootMotion)
                transform.rotation *= _animator.deltaRotation;
        }

        /// <summary>
        /// 决定这一帧的水平位移。抽成纯函数以便脱离运行时被测试。
        /// </summary>
        public static Vector3 ResolveHorizontalDelta(
            bool useRootMotion, Vector3 rootMotionDelta, Vector3 codeVelocity, float deltaTime)
        {
            Vector3 delta = useRootMotion
                ? rootMotionDelta
                : codeVelocity * deltaTime;

            delta.y = 0f;   // 垂直方向永远由重力管线负责
            return delta;
        }

        /// <summary>
        /// 把移动状态映射到混合树的 Speed 参数。
        /// 混合树节点阈值定义：0 = 待机，0.5 = 走/跑，1 = 冲刺。
        /// </summary>
        public static float ComputeSpeedParameter(bool moving, bool sprinting)
        {
            if (!moving) return 0f;
            return sprinting ? 1f : 0.5f;
        }
    }
}
```

- [ ] **Step 4: 运行测试，确认通过**

Run: Unity Test Runner → EditMode → Run All
Expected: 7 个测试全部 PASS。

- [ ] **Step 5: 回到 Task 9 完成两个 Tag**

现在 `PlayerMotor` 存在了，`RootMotionTag` / `LockMovementTag` 可以编译。
按 Task 9 的步骤创建这两个文件，然后回到 Task 8。

- [ ] **Step 6: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Player/PlayerMotor.cs Assets/Tests/EditMode/PlayerMotorTests.cs
git commit -m "feat: PlayerMotor 统一位移管线，OnAnimatorMove 作为唯一位移出口"
```

---

## Task 12: PlayerAnimatorDriver —— 参数写入与连招缓冲

把游戏状态翻译成 Animator 参数。含连招输入缓冲，避免玩家过早按键攒出意外连段。

**Files:**
- Create: `Assets/Script/Player/PlayerAnimatorDriver.cs`
- Test: `Assets/Tests/EditMode/ComboBufferTests.cs`

**Interfaces:**
- Consumes: `Demo.AnimatorParams`, `Demo.PlayerInputReader`, `Demo.PlayerMotor`
- Produces:
  - `Demo.ComboBuffer`（纯 C# 类）
  - `Demo.ComboBuffer.ComboBuffer(float window)`
  - `Demo.ComboBuffer.Press(float now)`
  - `Demo.ComboBuffer.Consume(float now) -> bool`
  - `Demo.ComboBuffer.HasBuffered(float now) -> bool`
  - `Demo.ComboBuffer.Clear()`
  - `Demo.ComboBuffer.LastPressTime -> float`
  - `Demo.PlayerAnimatorDriver.Tick(...)`

- [ ] **Step 1: 写测试**

`Assets/Tests/EditMode/ComboBufferTests.cs`：

```csharp
using NUnit.Framework;
using Demo;

namespace Demo.Tests
{
    public class ComboBufferTests
    {
        private const float Window = 0.25f;

        [Test]
        public void 未按过键_无缓冲()
        {
            var b = new ComboBuffer(Window);
            Assert.IsFalse(b.HasBuffered(10f));
        }

        [Test]
        public void 刚按下的键_有缓冲()
        {
            var b = new ComboBuffer(Window);
            b.Press(10f);
            Assert.IsTrue(b.HasBuffered(10.1f));
        }

        [Test]
        public void 超窗口的按键_缓冲失效()
        {
            var b = new ComboBuffer(Window);
            b.Press(10f);
            Assert.IsFalse(b.HasBuffered(10.3f), "超过 0.25 秒的输入应被丢弃，否则会攒出意外连段");
        }

        [Test]
        public void 消费后_缓冲清空()
        {
            var b = new ComboBuffer(Window);
            b.Press(10f);

            Assert.IsTrue(b.Consume(10.1f));
            Assert.IsFalse(b.HasBuffered(10.1f), "消费过的输入不能再被消费一次");
        }

        [Test]
        public void 消费超窗口输入_返回假且不清空别的状态()
        {
            var b = new ComboBuffer(Window);
            b.Press(10f);
            Assert.IsFalse(b.Consume(10.5f));
        }

        [Test]
        public void 连续按键_以最后一次为准()
        {
            var b = new ComboBuffer(Window);
            b.Press(10f);
            b.Press(10.2f);
            Assert.IsTrue(b.HasBuffered(10.4f));
            Assert.AreEqual(10.2f, b.LastPressTime, 0.001f);
        }

        [Test]
        public void 手动清空_缓冲失效()
        {
            var b = new ComboBuffer(Window);
            b.Press(10f);
            b.Clear();
            Assert.IsFalse(b.HasBuffered(10.01f));
        }

        [Test]
        public void 窗口边界_恰好等于窗口时仍有效()
        {
            var b = new ComboBuffer(Window);
            b.Press(10f);
            Assert.IsTrue(b.HasBuffered(10f + Window));
        }
    }
}
```

- [ ] **Step 2: 运行测试，确认失败**

Expected: 编译失败，`ComboBuffer` 不存在。

- [ ] **Step 3: 实现 ComboBuffer**

`Assets/Script/Player/ComboBuffer.cs`：

```csharp
namespace Demo
{
    /// <summary>
    /// 连招输入缓冲。纯 C# 类，不依赖 Unity，可直接单测。
    ///
    /// 为什么需要它：玩家在攻击动画早期就按下了下一段，此时 Animator 的连招窗口
    /// 还没打开。如果直接 SetTrigger，trigger 会一直挂起，等到很久以后某个转换
    /// 评估它时才被消费，表现为"莫名其妙多打了一段"。
    /// 缓冲窗口的作用就是给这类过早输入设一个保质期。
    /// </summary>
    public class ComboBuffer
    {
        private readonly float _window;
        private float _pressedAt = float.NegativeInfinity;

        public ComboBuffer(float window)
        {
            _window = window;
        }

        public float LastPressTime => _pressedAt;

        /// <summary>记录一次按键。</summary>
        public void Press(float now)
        {
            _pressedAt = now;
        }

        /// <summary>缓冲是否仍在有效期内。</summary>
        public bool HasBuffered(float now)
        {
            return now - _pressedAt <= _window;
        }

        /// <summary>
        /// 取出缓冲。有效则返回 true 并清空，否则返回 false 且不改变状态。
        /// </summary>
        public bool Consume(float now)
        {
            if (!HasBuffered(now))
                return false;

            _pressedAt = float.NegativeInfinity;
            return true;
        }

        public void Clear()
        {
            _pressedAt = float.NegativeInfinity;
        }
    }
}
```

- [ ] **Step 4: 运行测试，确认通过**

Run: Unity Test Runner → EditMode → Run All
Expected: 8 个测试全部 PASS。

- [ ] **Step 5: 实现 PlayerAnimatorDriver**

`Assets/Script/Player/PlayerAnimatorDriver.cs`：

```csharp
using UnityEngine;

namespace Demo
{
    /// <summary>
    /// 唯一调用 Animator.SetFloat / SetBool / SetTrigger 的地方。
    ///
    /// 本类不碰 Transform，也不读 Input —— 它只接收已经翻译好的状态，
    /// 然后写进 Animator。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimatorDriver : MonoBehaviour
    {
        [Header("连招缓冲窗口（秒）")]
        [Tooltip("攻击键按下后多久之内算有效输入。太大会攒连段，太小会吃键")]
        [SerializeField] private float comboBufferWindow = 0.25f;

        [Header("锁定时转向速度")]
        [SerializeField] private float lockOnTurnSpeed = 15f;

        private Animator _animator;
        private PlayerMotor _motor;

        private ComboBuffer _lightBuffer;
        private ComboBuffer _heavyBuffer;

        // 缓存的参数哈希，避免每帧字符串哈希开销
        private static readonly int HashSpeed = Animator.StringToHash(AnimatorParams.Speed);
        private static readonly int HashMoveDirX = Animator.StringToHash(AnimatorParams.MoveDirX);
        private static readonly int HashMoveDirZ = Animator.StringToHash(AnimatorParams.MoveDirZ);
        private static readonly int HashIsLockedOn = Animator.StringToHash(AnimatorParams.IsLockedOn);
        private static readonly int HashIsBattleStance = Animator.StringToHash(AnimatorParams.IsBattleStance);
        private static readonly int HashIsGrounded = Animator.StringToHash(AnimatorParams.IsGrounded);
        private static readonly int HashIsDefending = Animator.StringToHash(AnimatorParams.IsDefending);
        private static readonly int HashLightAttack = Animator.StringToHash(AnimatorParams.LightAttack);
        private static readonly int HashHeavyAttack = Animator.StringToHash(AnimatorParams.HeavyAttack);
        private static readonly int HashRoll = Animator.StringToHash(AnimatorParams.Roll);
        private static readonly int HashJump = Animator.StringToHash(AnimatorParams.Jump);

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _motor = GetComponent<PlayerMotor>();
            _animator.applyRootMotion = true;

            // 用序列化的窗口值构造，这样 Inspector 里调完立刻生效
            _lightBuffer = new ComboBuffer(comboBufferWindow);
            _heavyBuffer = new ComboBuffer(comboBufferWindow);

            // 开局默认非战斗姿态，避免一进场景就举着盾
            _animator.SetBool(HashIsBattleStance, false);
        }

        /// <summary>
        /// 每帧由 PlayerController 调用。
        /// </summary>
        /// <param name="input">已翻译好的输入</param>
        /// <param name="moveDirection">世界空间的期望移动方向</param>
        /// <param name="lockedOn">是否处于锁定状态</param>
        /// <param name="lockTarget">锁定目标，未锁定传 null</param>
        /// <param name="deltaTime">帧间隔</param>
        public void Tick(
            PlayerInputReader input,
            Vector3 moveDirection,
            bool lockedOn,
            Transform lockTarget,
            float deltaTime)
        {
            WriteLocomotion(input, moveDirection, lockedOn, deltaTime);
            WriteActions(input, lockedOn);
            WriteStance(input, moveDirection, lockedOn);
        }

        private void WriteLocomotion(
            PlayerInputReader input, Vector3 moveDirection, bool lockedOn, float deltaTime)
        {
            bool moving = moveDirection.sqrMagnitude > 0.0001f;
            bool sprinting = moving && input.SprintHeld && !lockedOn;

            _animator.SetFloat(HashSpeed,
                PlayerMotor.ComputeSpeedParameter(moving, sprinting));

            if (lockedOn)
            {
                // 锁定：把世界空间移动方向换算到角色本地空间，
                // 因为此时角色朝向由锁定目标决定，不随移动方向转。
                Vector3 local = transform.InverseTransformDirection(moveDirection);
                _animator.SetFloat(HashMoveDirX, local.x);
                _animator.SetFloat(HashMoveDirZ, local.z);
            }
            else
            {
                _animator.SetFloat(HashMoveDirX, 0f);
                _animator.SetFloat(HashMoveDirZ, 0f);
            }

            _animator.SetBool(HashIsLockedOn, lockedOn);
            _animator.SetBool(HashIsGrounded, _motor.IsGrounded);
        }

        private void WriteActions(PlayerInputReader input, bool lockedOn)
        {
            float now = Time.time;

            // 记录攻击输入，然后只在缓冲有效时真正触发。
            // 这样过早的按键会在窗口过期后被丢弃，而不是挂起成意外连段。
            if (input.LightAttackPressed)
            {
                _lightBuffer.Press(now);
                _animator.SetBool(HashIsBattleStance, true);
            }
            if (input.HeavyAttackPressed)
            {
                _heavyBuffer.Press(now);
                _animator.SetBool(HashIsBattleStance, true);
            }

            if (_lightBuffer.Consume(now))
                _animator.SetTrigger(HashLightAttack);

            if (_heavyBuffer.Consume(now))
                _animator.SetTrigger(HashHeavyAttack);

            if (input.RollPressed)
                _animator.SetTrigger(HashRoll);

            if (input.JumpPressed)
            {
                _animator.SetTrigger(HashJump);
                _motor.Jump();
            }

            _animator.SetBool(HashIsDefending, input.DefendHeld);
        }

        /// <summary>
        /// 姿态与朝向。非锁定时角色转向移动方向；锁定时转向敌人。
        /// </summary>
        private void WriteStance(
            PlayerInputReader input, Vector3 moveDirection, bool lockedOn)
        {
            if (lockedOn && _currentTarget != null)
            {
                _motor.FaceDirectionImmediate(_currentTarget.position - transform.position);
                return;
            }

            if (input.LightAttackPressed || input.HeavyAttackPressed)
                return; // 攻击瞬间不要抢转向，交由动画决定

            _motor.FaceDirection(moveDirection, Time.deltaTime);
        }

        private Transform _currentTarget;

        /// <summary>由 PlayerCameraRig 在锁定目标变化时调用。</summary>
        public void SetLockOnTarget(Transform target)
        {
            _currentTarget = target;
            if (target != null)
                _animator.SetBool(HashIsBattleStance, true);
        }
    }
}
```

> **关于两个参数缓存**：`_lightBuffer` / `_heavyBuffer` 在 `Awake()` 里用序列化字段
> `comboBufferWindow` 构造，所以你在 Inspector 里改窗口值后重进 Play 模式即可生效，
> 不需要改代码。

- [ ] **Step 6: 运行测试，确认通过**

Run: Unity Test Runner → EditMode → Run All
Expected: 全部 PASS。

- [ ] **Step 7: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Player/ComboBuffer.cs Assets/Script/Player/PlayerAnimatorDriver.cs \
        Assets/Tests/EditMode/ComboBufferTests.cs
git commit -m "feat: PlayerAnimatorDriver 参数写入与连招输入缓冲"
```

---

## Task 13: PlayerCameraRig 与 LockOnTarget

锁定目标的搜索、切换与相机切换。目标选择的打分逻辑抽成纯函数，可测。

**Files:**
- Create: `Assets/Script/Player/LockOnTarget.cs`
- Create: `Assets/Script/Player/PlayerCameraRig.cs`
- Test: `Assets/Tests/EditMode/LockOnSelectorTests.cs`

**Interfaces:**
- Consumes: `Cinemachine.CinemachineFreeLook`, `Cinemachine.CinemachineVirtualCamera`
- Produces:
  - `Demo.LockOnTarget`（含 `IsValid -> bool`）
  - `Demo.PlayerCameraRig.CurrentTarget -> Transform`
  - `Demo.PlayerCameraRig.ToggleLockOn()`
  - `Demo.PlayerCameraRig.SelectBest(Vector3, Vector3, LockOnCandidate[], float) -> Transform`（静态）

- [ ] **Step 1: 写测试**

`Assets/Tests/EditMode/LockOnSelectorTests.cs`：

```csharp
using NUnit.Framework;
using UnityEngine;
using Demo;

namespace Demo.Tests
{
    public class LockOnSelectorTests
    {
        private GameObject _a, _b, _c;

        [SetUp]
        public void SetUp()
        {
            _a = new GameObject("A");
            _b = new GameObject("B");
            _c = new GameObject("C");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_a);
            Object.DestroyImmediate(_b);
            Object.DestroyImmediate(_c);
        }

        private static LockOnCandidate Cand(Transform t, Vector3 pos, bool visible = true)
            => new LockOnCandidate { Target = t, Position = pos, Visible = visible };

        [Test]
        public void 无候选_返回空()
        {
            var r = PlayerCameraRig.SelectBest(
                Vector3.zero, Vector3.forward, new LockOnCandidate[0], 20f);
            Assert.IsNull(r);
        }

        [Test]
        public void 超出距离的候选_被排除()
        {
            var cands = new[] { Cand(_a.transform, new Vector3(0, 0, 100f)) };
            var r = PlayerCameraRig.SelectBest(
                Vector3.zero, Vector3.forward, cands, 20f);
            Assert.IsNull(r);
        }

        [Test]
        public void 被遮挡的候选_被排除()
        {
            var cands = new[] { Cand(_a.transform, new Vector3(0, 0, 5f), visible: false) };
            var r = PlayerCameraRig.SelectBest(
                Vector3.zero, Vector3.forward, cands, 20f);
            Assert.IsNull(r);
        }

        [Test]
        public void 同样距离时_优先选更靠近镜头正前方的()
        {
            var front = Cand(_a.transform, new Vector3(0, 0, 5f));
            var side = Cand(_b.transform, new Vector3(5, 0, 0));

            var r = PlayerCameraRig.SelectBest(
                Vector3.zero, Vector3.forward, new[] { side, front }, 20f);

            Assert.AreSame(_a.transform, r, "正前方的目标应优先于侧面的");
        }

        [Test]
        public void 正前方但更远_与侧面但更近_按综合得分选择()
        {
            // 正前方 12 米 vs 正侧方 2 米，侧方综合得分应更高。
            // 校验：正面 = 距离分 0.4 × 0.6 + 角度分 1.0 × 0.4 = 0.64
            //       侧面 = 距离分 0.9 × 0.6 + 角度分 0.5 × 0.4 = 0.74 → 侧面胜
            // 正面距离须 ≥ 8.67 米，否则角度优势会反超（见 Step 4 的说明）。
            var front = Cand(_a.transform, new Vector3(0, 0, 12f));
            var side = Cand(_b.transform, new Vector3(2f, 0, 0));

            var r = PlayerCameraRig.SelectBest(
                Vector3.zero, Vector3.forward, new[] { front, side }, 20f);

            Assert.AreSame(_b.transform, r);
        }

        [Test]
        public void 只有一个合法候选_直接返回它()
        {
            var only = Cand(_c.transform, new Vector3(3f, 0, 3f));
            var r = PlayerCameraRig.SelectBest(
                Vector3.zero, Vector3.forward, new[] { only }, 20f);
            Assert.AreSame(_c.transform, r);
        }
    }
}
```

- [ ] **Step 2: 运行测试，确认失败**

Expected: 编译失败，`PlayerCameraRig` / `LockOnCandidate` 不存在。

- [ ] **Step 3: 实现 LockOnTarget**

`Assets/Script/Player/LockOnTarget.cs`：

```csharp
using UnityEngine;

namespace Demo
{
    /// <summary>
    /// 挂在可被锁定的物体上（敌人、木桩）。
    /// </summary>
    public class LockOnTarget : MonoBehaviour
    {
        [Tooltip("锁定时相机注视的点，留空则用自身位置")]
        [SerializeField] private Transform aimPoint;

        [Tooltip("该目标是否当前可被锁定")]
        [SerializeField] private bool lockable = true;

        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position;

        public bool IsValid => lockable && isActiveAndEnabled && gameObject.activeInHierarchy;
    }
}
```

- [ ] **Step 4: 实现 PlayerCameraRig**

`Assets/Script/Player/PlayerCameraRig.cs`：

```csharp
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

namespace Demo
{
    /// <summary>锁定候选。抽成结构体是为了让打分逻辑可脱离运行时被测试。</summary>
    public struct LockOnCandidate
    {
        public Transform Target;
        public Vector3 Position;
        public bool Visible;
    }

    /// <summary>
    /// 相机与锁定。管理两个 Cinemachine 相机的优先级切换、目标搜索与切换。
    /// 不碰位移，不碰 Animator。
    /// </summary>
    public class PlayerCameraRig : MonoBehaviour
    {
        [Header("Cinemachine 相机")]
        [SerializeField] private CinemachineFreeLook freeLookCamera;
        [SerializeField] private CinemachineVirtualCamera lockOnCamera;

        [Header("相机优先级")]
        [SerializeField] private int activePriority = 20;
        [SerializeField] private int inactivePriority = 10;

        [Header("锁定参数")]
        [SerializeField] private float maxLockDistance = 18f;
        [SerializeField] private float fieldOfView = 120f;
        [SerializeField] private LayerMask targetLayers = ~0;
        [SerializeField] private KeyCode switchTargetKey = KeyCode.Q;

        public Transform CurrentTarget { get; private set; }
        public bool IsLockedOn => CurrentTarget != null;

        private readonly List<LockOnCandidate> _candidates = new List<LockOnCandidate>();
        private readonly Collider[] _overlapBuffer = new Collider[32];

        private void Start()
        {
            ApplyCameraPriorities();
        }

        /// <summary>由 PlayerController 每帧调用。</summary>
        public void Tick(bool toggleRequested)
        {
            if (toggleRequested)
            {
                if (CurrentTarget == null)
                    AcquireTarget();
                else
                    SwitchToNextTarget();
            }

            // 目标失效则自动解锁
            if (CurrentTarget != null && !IsStillValid(CurrentTarget))
                ClearTarget();

            ApplyCameraPriorities();
        }

        private bool IsStillValid(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
                return false;

            var lt = target.GetComponent<LockOnTarget>();
            if (lt != null && !lt.IsValid)
                return false;

            return Vector3.Distance(transform.position, target.position) <= maxLockDistance;
        }

        private void AcquireTarget()
        {
            var best = FindBestTarget();
            if (best != null)
                SetTarget(best);
        }

        private void SwitchToNextTarget()
        {
            GatherCandidates();
            if (_candidates.Count == 0)
            {
                ClearTarget();
                return;
            }

            // 找到当前目标在候选列表里的位置，取下一个
            int currentIndex = _candidates.FindIndex(c => c.Target == CurrentTarget);
            int nextIndex = (currentIndex + 1) % _candidates.Count;

            SetTarget(_candidates[nextIndex].Target);
        }

        private Transform FindBestTarget()
        {
            GatherCandidates();
            return SelectBest(
                transform.position,
                freeLookCamera != null ? freeLookCamera.transform.forward : transform.forward,
                _candidates.ToArray(),
                maxLockDistance);
        }

        private void GatherCandidates()
        {
            _candidates.Clear();

            int count = Physics.OverlapSphereNonAlloc(
                transform.position, maxLockDistance, _overlapBuffer, targetLayers);

            var camForward = freeLookCamera != null
                ? freeLookCamera.transform.forward
                : transform.forward;

            for (int i = 0; i < count; i++)
            {
                var lt = _overlapBuffer[i].GetComponentInParent<LockOnTarget>();
                if (lt == null || !lt.IsValid)
                    continue;

                var aim = lt.AimPosition;
                var toTarget = aim - transform.position;

                // 视野锥外的忽略
                if (Vector3.Angle(camForward, toTarget) > fieldOfView * 0.5f)
                    continue;

                // 视线遮挡检测
                bool visible = !Physics.Linecast(
                    transform.position + Vector3.up * 1.5f, aim, ~0, QueryTriggerInteraction.Ignore);

                _candidates.Add(new LockOnCandidate
                {
                    Target = lt.transform,
                    Position = aim,
                    Visible = visible,
                });
            }
        }

        private void SetTarget(Transform target)
        {
            CurrentTarget = target;

            if (lockOnCamera != null)
            {
                lockOnCamera.Follow = transform;
                lockOnCamera.LookAt = target;
            }
        }

        private void ClearTarget()
        {
            CurrentTarget = null;
            if (lockOnCamera != null)
                lockOnCamera.LookAt = null;
        }

        private void ApplyCameraPriorities()
        {
            bool locked = CurrentTarget != null;

            if (freeLookCamera != null)
                freeLookCamera.Priority = locked ? inactivePriority : activePriority;

            if (lockOnCamera != null)
                lockOnCamera.Priority = locked ? activePriority : inactivePriority;
        }

        /// <summary>
        /// 综合打分选择最佳目标。距离越近、越接近镜头正前方，得分越高。
        /// 抽成纯函数以便单测。
        /// </summary>
        public static Transform SelectBest(
            Vector3 playerPosition, Vector3 cameraForward,
            LockOnCandidate[] candidates, float maxDistance)
        {
            if (candidates == null || candidates.Length == 0)
                return null;

            Transform best = null;
            float bestScore = float.NegativeInfinity;

            foreach (var c in candidates)
            {
                if (c.Target == null || !c.Visible)
                    continue;

                float distance = Vector3.Distance(playerPosition, c.Position);
                if (distance > maxDistance)
                    continue;

                var toTarget = c.Position - playerPosition;
                if (toTarget.sqrMagnitude < 0.0001f)
                    continue;

                float angle = Vector3.Angle(cameraForward, toTarget);

                // 两项都归一化到 0..1，距离权重更高
                float distanceScore = 1f - Mathf.Clamp01(distance / maxDistance);
                float angleScore = 1f - Mathf.Clamp01(angle / 180f);

                float score = distanceScore * 0.6f + angleScore * 0.4f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = c.Target;
                }
            }

            return best;
        }
    }
}
```

- [ ] **Step 5: 运行测试，确认通过**

Run: Unity Test Runner → EditMode → Run All
Expected: 6 个测试全部 PASS。

两个打分测试的期望值都可手算验证，不要靠改权重去迁就失败：
正面 12m / 侧方 2m 时，正面 = 0.4×0.6 + 1.0×0.4 = 0.64，侧面 = 0.9×0.6 + 0.5×0.4 = 0.74。
若实测不符，说明 `SelectBest` 的归一化写错了，而不是权重不对。

- [ ] **Step 6: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Player/LockOnTarget.cs Assets/Script/Player/PlayerCameraRig.cs \
        Assets/Tests/EditMode/LockOnSelectorTests.cs
git commit -m "feat: 锁定系统，目标打分选择与 Cinemachine 相机切换"
```

---

## Task 14: PlayerController 门面

把四个组件按顺序驱动起来。这是挂在预制体上的唯一入口。

**Files:**
- Create: `Assets/Script/Player/PlayerController.cs`

**Interfaces:**
- Consumes: `PlayerInputReader` / `PlayerMotor` / `PlayerAnimatorDriver` / `PlayerCameraRig`
- Produces: `Demo.PlayerController`

- [ ] **Step 1: 实现 PlayerController**

`Assets/Script/Player/PlayerController.cs`：

```csharp
using UnityEngine;

namespace Demo
{
    /// <summary>
    /// 门面。持有四个组件并按固定顺序驱动它们，自身不含任何逻辑。
    ///
    /// 保留这个类而不是让四个组件各自 Update，是为了明确执行顺序：
    /// 输入 → 相机/锁定 → 位移 → 动画参数。
    /// 顺序错乱会导致明显的操作延迟感。
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(PlayerMotor))]
    [RequireComponent(typeof(PlayerAnimatorDriver))]
    [RequireComponent(typeof(PlayerCameraRig))]
    public class PlayerController : MonoBehaviour
    {
        [Header("相机")]
        [Tooltip("移动方向参照的相机。留空则用 Camera.main")]
        [SerializeField] private Transform cameraTransform;

        private PlayerInputReader _input;
        private PlayerMotor _motor;
        private PlayerAnimatorDriver _animatorDriver;
        private PlayerCameraRig _cameraRig;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _motor = GetComponent<PlayerMotor>();
            _animatorDriver = GetComponent<PlayerAnimatorDriver>();
            _cameraRig = GetComponent<PlayerCameraRig>();

            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // 1. 相机与锁定（决定"前方"是哪个方向）
            _cameraRig.Tick(_input.LockOnPressed);
            _animatorDriver.SetLockOnTarget(_cameraRig.CurrentTarget);

            // 2. 把输入方向换算到相机空间
            Vector3 moveDirection = CameraRelative(_input.MoveInput);

            // 3. 位移意图
            bool sprinting = _input.SprintHeld && !_cameraRig.IsLockedOn;
            _motor.SetMoveDirection(moveDirection, sprinting);

            // 4. 动画参数
            _animatorDriver.Tick(
                _input, moveDirection, _cameraRig.IsLockedOn, _cameraRig.CurrentTarget, dt);
        }

        /// <summary>
        /// 把二维输入换算成相机空间下的世界方向。
        /// 这是"按 W 就是往镜头前方走"这条直觉的实现。
        /// </summary>
        private Vector3 CameraRelative(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f)
                return Vector3.zero;

            Vector3 forward, right;
            if (cameraTransform != null)
            {
                forward = cameraTransform.forward;
                right = cameraTransform.right;
            }
            else
            {
                forward = Vector3.forward;
                right = Vector3.right;
            }

            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        }
    }
}
```

- [ ] **Step 2: 确认编译通过**

回到 Unity，确认 Console 无编译错误。

- [ ] **Step 3: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Player/PlayerController.cs
git commit -m "feat: PlayerController 门面，固定执行顺序驱动四个组件"
```

---

## Task 15: DemoSceneBuilder —— 搭建 Demo 场景

生成可运行的场景。**这一步无法自动化验证**，必须靠 Task 16 的手动验收。

**Files:**
- Create: `Assets/Script/Editor/DemoSceneBuilder.cs`

**Interfaces:**
- Consumes: `Demo.EditorTools.HeroAnimatorBuilder`, `Demo.AnimationClipLocator`
- Produces: `Demo.EditorTools.DemoSceneBuilder.Build()`

> **前置条件**：Task 4–8 必须已执行过，`Assets/Animator/Hero_SwordAndShield.controller` 必须已存在。

- [ ] **Step 1: 实现 DemoSceneBuilder**

`Assets/Script/Editor/DemoSceneBuilder.cs`：

```csharp
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Cinemachine;
using Demo;

namespace Demo.EditorTools
{
    /// <summary>
    /// 生成 Demo_Combat 场景。
    ///
    /// 为什么不用资源包自带的 Desert 演示场景（33000 行）：
    /// 那个场景全场景只有 9 个 MeshCollider，沙丘和村落都能穿过去，
    /// 而且含大量瀑布粒子，性能和手感都不适合做战斗测试。
    /// 这里改用 Desert 的 60 个 prefab 搭一个干净的、有完整碰撞的竞技场。
    /// </summary>
    public static class DemoSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Demo_Combat.unity";

        private const string PrefabRoot = "Assets/Lowpoly Style/Desert/Prefabs";
        private const string GroundMaterial =
            "Assets/Lowpoly Style/Shared Materials and Textures/MAT_MAIN.mat";

        private const float ArenaSize = 60f;

        [MenuItem("Tools/角色/搭建 Demo 场景")]
        public static void BuildFromMenu()
        {
            Build();
            Debug.Log($"[DemoSceneBuilder] 已生成场景 {ScenePath}");
        }

        public static void Build()
        {
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
                AnimatorParams.ControllerPath);

            if (controller == null)
            {
                EditorUtility.DisplayDialog(
                    "先做这一步",
                    "找不到 Hero_SwordAndShield.controller。\n" +
                    "请先执行菜单 工具/角色/生成主角状态机。",
                    "好");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            BuildGround();
            BuildArena();
            BuildProps();

            var player = BuildPlayer(controller);
            BuildCameras(player);
            BuildTargets();

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ------------------------------------------------------------------

        private static void BuildLighting()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.96f, 0.88f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            light.shadows = LightShadows.Soft;
        }

        private static void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            // Unity 的 Plane 默认 10x10，缩放 6 得到 60x60
            ground.transform.localScale = Vector3.one * (ArenaSize / 10f);
            ground.transform.position = Vector3.zero;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterial);
            if (mat != null)
                ground.GetComponent<MeshRenderer>().sharedMaterial = mat;

            ground.isStatic = true;
        }

        private static void BuildArena()
        {
            var root = new GameObject("ArenaWalls");

            // 四面墙，留南侧一个缺口作为入口
            const float half = ArenaSize * 0.5f;
            const float step = 4f;

            for (float x = -half; x <= half; x += step)
            {
                // 北墙
                Place($"{PrefabRoot}/WoodenWall1.prefab", new Vector3(x, 0, half), 0f, root.transform);

                // 东、西墙（跳过缺口）
                if (x > -6f && x < 6f)
                    continue;

                Place($"{PrefabRoot}/WoodenWall1.prefab", new Vector3(x, 0, -half), 180f, root.transform);
            }

            for (float z = -half + step; z < half - step; z += step)
            {
                Place($"{PrefabRoot}/OldWall1.prefab", new Vector3(-half, 0, z), 90f, root.transform);
                Place($"{PrefabRoot}/OldWall1.prefab", new Vector3(half, 0, z), -90f, root.transform);
            }
        }

        private static void BuildProps()
        {
            var root = new GameObject("Props");

            var props = new (string prefab, Vector3 pos, float yaw)[]
            {
                ("TorchBig",        new Vector3(-20f, 0f,  20f),   0f),
                ("TorchBig",        new Vector3( 20f, 0f,  20f),   0f),
                ("TorchBig",        new Vector3(-20f, 0f, -20f),   0f),
                ("TorchBig",        new Vector3( 20f, 0f, -20f),   0f),
                ("SaguaroCactus1",  new Vector3(-26f, 0f,  10f),  30f),
                ("SaguaroCactus2",  new Vector3( 26f, 0f,  -8f), -45f),
                ("RockGrey1",       new Vector3( 12f, 0f,  25f),  15f),
                ("RockGrey2",       new Vector3(-14f, 0f, -25f),  70f),
                ("Palmtree1",       new Vector3(-25f, 0f, -18f),   0f),
                ("Palmtree1",       new Vector3( 25f, 0f,  18f), 120f),
                ("Barrel",          new Vector3(  6f, 0f,  16f),   0f),
                ("Barrel",          new Vector3(  8f, 0f,  17f),  40f),
                ("SandduneLow1",    new Vector3(-30f, 0f,  30f),   0f),
                ("SandduneLow1",    new Vector3( 30f, 0f, -30f),  90f),
            };

            foreach (var (prefab, pos, yaw) in props)
                Place($"{PrefabRoot}/{prefab}.prefab", pos, yaw, root.transform);
        }

        private static GameObject Place(string prefabPath, Vector3 pos, float yaw, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[DemoSceneBuilder] 找不到 prefab {prefabPath}");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        // ------------------------------------------------------------------

        private static GameObject BuildPlayer(UnityEditor.Animations.AnimatorController controller)
        {
            var mc01 = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/RPGTinyHeroWavePBR/Prefab/ModularCharacters/MC01.prefab");

            if (mc01 == null)
            {
                Debug.LogError("[DemoSceneBuilder] 找不到 MC01.prefab");
                return null;
            }

            // 直接用 MC01 作为玩家根节点，不做多余的父子包装。
            // 原因：OnAnimatorMove 是发给 Animator 所在的那个 GameObject 的，
            // 所以 Animator 与 PlayerMotor / CharacterController 必须在同一个对象上。
            var player = (GameObject)PrefabUtility.InstantiatePrefab(mc01);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 0.1f, -8f);
            player.transform.rotation = Quaternion.identity;

            // 复用 MC01 自带的 Animator（它的 Avatar 已经配好了），只换控制器
            var animator = player.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("[DemoSceneBuilder] MC01 上没有 Animator，无法配置");
                return null;
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = true;

            // 挂武器
            AttachWeapon(player, "weapon_r",
                "Assets/RPGTinyHeroWavePBR/Prefab/Weapons/OHS03_Sword.prefab");
            AttachWeapon(player, "weapon_l",
                "Assets/RPGTinyHeroWavePBR/Prefab/Weapons/Shield01.prefab");

            // 物理与控制器组件
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;

            player.AddComponent<PlayerInputReader>();
            player.AddComponent<PlayerMotor>();
            player.AddComponent<PlayerAnimatorDriver>();
            player.AddComponent<PlayerCameraRig>();
            player.AddComponent<PlayerController>();

            return player;
        }

        private static void AttachWeapon(GameObject character, string boneName, string weaponPath)
        {
            var bone = FindDeep(character.transform, boneName);
            if (bone == null)
            {
                Debug.LogWarning($"[DemoSceneBuilder] 角色上没有找到骨骼 {boneName}");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(weaponPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[DemoSceneBuilder] 找不到武器 {weaponPath}");
                return;
            }

            var weapon = (GameObject)PrefabUtility.InstantiatePrefab(prefab, bone);
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.identity;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;

            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }

            return null;
        }

        // ------------------------------------------------------------------

        private static void BuildCameras(GameObject player)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CinemachineBrain>();

            // 自由视角相机：环绕玩家
            var freeLookGo = new GameObject("CM_FreeLook");
            var freeLook = freeLookGo.AddComponent<CinemachineFreeLook>();
            freeLook.Follow = player.transform;
            freeLook.LookAt = player.transform;
            freeLook.m_XAxis.m_MaxSpeed = 300f;
            freeLook.m_YAxis.m_MaxSpeed = 2f;

            // 锁定相机：始终注视目标
            var lockGo = new GameObject("CM_LockOn");
            var lockCam = lockGo.AddComponent<CinemachineVirtualCamera>();
            lockCam.Follow = player.transform;
            lockCam.m_Lens.FieldOfView = 50f;
            lockGo.SetActive(true);

            // 把两个相机接到 PlayerCameraRig 上
            var rig = player.GetComponent<PlayerCameraRig>();
            var so = new SerializedObject(rig);
            so.FindProperty("freeLookCamera").objectReferenceValue = freeLook;
            so.FindProperty("lockOnCamera").objectReferenceValue = lockCam;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildTargets()
        {
            var root = new GameObject("Targets");

            var positions = new[]
            {
                new Vector3(-6f, 0f, 8f),
                new Vector3( 6f, 0f, 10f),
                new Vector3( 0f, 0f, 16f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var go = Place($"{PrefabRoot}/WoodenPole1.prefab", positions[i], 0f, root.transform);
                if (go == null) continue;

                go.name = $"Dummy_{i + 1:00}";
                go.AddComponent<LockOnTarget>();

                // 木桩要有碰撞体才能被 Physics.OverlapSphere 找到
                if (go.GetComponentInChildren<Collider>() == null)
                {
                    var col = go.AddComponent<CapsuleCollider>();
                    col.height = 2f;
                    col.radius = 0.3f;
                    col.center = new Vector3(0f, 1f, 0f);
                }
            }
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
    }
}
```

- [ ] **Step 2: 执行生成器**

Unity 菜单 `Tools/角色/搭建 Demo 场景`。

Expected: 场景生成，Console 无错误。若提示找不到 controller，先执行 `Tools/角色/生成主角状态机`。

- [ ] **Step 3: 目视检查场景层级**

确认 Hierarchy 里有 `Ground` / `ArenaWalls` / `Props` / `PlayerRoot` / `Main Camera` / `CM_FreeLook` / `CM_LockOn` / `Targets`。

- [ ] **Step 4: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Editor/DemoSceneBuilder.cs Assets/Scenes/Demo_Combat.unity \
        Assets/Scenes/Demo_Combat.unity.meta
git commit -m "feat: DemoSceneBuilder 搭建沙漠竞技场 Demo 场景"
```

---

## Task 16: 端到端手动验收

自动化测试覆盖不到手感与视觉。这一任务必须**在 Unity 里实际运行**逐条确认。

**Files:** 无（纯验证）

- [ ] **Step 1: 运行场景**

打开 `Assets/Scenes/Demo_Combat.unity`，点 Play。

- [ ] **Step 2: 逐条验收**

| # | 操作 | 预期 | 通过 |
|---|---|---|---|
| 1 | 什么都不按 | 角色**原地待机，不再自动播放动画** | ☐ |
| 2 | 按 W/A/S/D | 角色朝该方向移动并播放行走动画 | ☐ |
| 3 | 按住 Shift | 切冲刺动画，速度提升 | ☐ |
| 4 | 按空格 | 起跳、滞空、落地，动画与位移匹配 | ☐ |
| 5 | 按左 Alt | 翻滚，位移与动画吻合，**无明显滑步** | ☐ |
| 6 | 移动鼠标 | 镜头自由旋转，贴墙时不自穿 | ☐ |
| 7 | 走近木桩按 Q | 镜头平滑过渡到锁定视角 | ☐ |
| 8 | 锁定下按 A/D | 角色**横移而不转身**，播放横移动画 | ☐ |
| 9 | 锁定下按 S | 角色后退，播放后退动画 | ☐ |
| 10 | 锁定下再按 Q | 在三个木桩间循环切换目标 | ☐ |
| 11 | 连按鼠标左键 | Attack01→02→03→04 正确连段 | ☐ |
| 12 | 单按一次左键 | 只出一段然后回待机 | ☐ |
| 13 | 连按鼠标右键 | Combo 五段正确连段 | ☐ |
| 14 | 按住 F | 举起盾牌保持防御姿态 | ☐ |
| 15 | 走远后目标自动解锁 | 镜头平滑切回自由视角 | ☐ |

- [ ] **Step 3: 记录未通过项**

任何一条未通过，记录现象（不是猜测原因），进入 superpowers:systematic-debugging 流程排查。

**第 1 条是本次改造的核心验收点**——它直接验证"角色不再自动按顺序播放动画"这个原始问题是否解决。

- [ ] **Step 4: 调整手感数值并提交**

验收过程中大概率需要微调以下数值，调完提交：

| 数值 | 位置 | 初始值 |
|---|---|---|
| `ComboLinkExitTime` | `HeroAnimatorBuilder.cs` | 0.55 |
| `ActionReturnExitTime` | `HeroAnimatorBuilder.cs` | 0.90 |
| `comboBufferWindow` | `PlayerAnimatorDriver` | 0.25 |
| `moveSpeed` / `sprintSpeed` | `PlayerMotor` | 3.5 / 6.0 |
| `maxLockDistance` | `PlayerCameraRig` | 18 |

**注意**：改 `HeroAnimatorBuilder.cs` 里的常量后，必须重新执行 `Tools/角色/生成主角状态机` 才会生效。

```bash
cd "E:/unity/求职demo"
git add -A
git commit -m "tune: 依据实机验收调整连招窗口与移动速度"
```

---

## 自查

### 规范覆盖检查

| 规范章节 | 覆盖任务 |
|---|---|
| 5. 环境事实 | Global Constraints |
| 5.1 资源遗留问题（拼写、重复状态） | Task 2（常量层修正）+ Task 4 Step 1 断言 |
| 6.1 场景结构 | Task 15 |
| 6.2 脚本架构（5 文件） | Task 10–14 |
| 6.3 统一位移管线 | Task 11 + Task 9 |
| 6.4 相机与锁定 | Task 13 |
| 7.1 参数（23 个） | Task 2 + Task 4 Step 3 + Task 4 断言 |
| 7.2 子状态机分组（6 组） | Task 4（骨架）+ Task 5–7（内容） |
| 7.2 三个混合树 | Task 5 |
| 7.3 transition 规则 | Task 5 + Task 6 + Task 8 |
| 7.4 连招窗口 | Task 6 + Task 12 |
| 8. 状态机修改清单 | Task 4–8 整体 |
| 9. 两个 Editor 工具 | Task 4–8 + Task 15 |
| 11.1 自动化验证 | 各任务的测试步骤 |
| 11.2 手动验收 | Task 16 |
| 12. 风险：Tag 未复位 | Task 9 `OnStateMachineExit` |
| 12. 风险：重跑覆盖手工改动 | Task 4 Step 3 注释 + Task 16 Step 4 提示 |

### 已识别的执行顺序约束

1. **Task 1 必须最先做**——没有程序集定义，所有测试无法编译。
2. **Task 11 必须先于 Task 9**——两个 Tag 引用 `PlayerMotor`。
3. **Task 9 必须先于 Task 8**——Task 8 给状态挂 Tag。
4. **Task 4 必须先于 Task 15**——场景需要 controller 已生成。

### 需要在执行中确认的不确定点

| 点 | 风险 | 确认方式 |
|---|---|---|
| `AnimatorStateMachine.AddStateMachineTransition` API 是否存在 | 生成器骨架编译失败 | Task 4 Step 4 首次编译即可暴露 |
| fbx 内 AnimationClip 的实际命名 | 剪辑加载为 null | Task 3 Step 4 的断言 + 调试输出 |
| `AddStateMachineBehaviour<T>()` 的签名 | Task 8 编译失败 | Task 8 Step 4 编译 |
| `CinemachineFreeLook.m_XAxis.m_MaxSpeed` 在 2.10.7 的字段名 | Task 15 编译失败 | Task 15 Step 2 |
| `PrefabUtility.InstantiatePrefab` 加载武器后坐标是否需要手调 | 武器挂载位置不对手感 | Task 16 目视检查 |

这些点都在计划的早期步骤里被暴露，不会堆积到最后才炸。
