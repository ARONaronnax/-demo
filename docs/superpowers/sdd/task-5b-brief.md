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

