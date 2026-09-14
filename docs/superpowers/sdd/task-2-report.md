# Task 2 报告：AnimatorParams 共享契约

**状态：** DONE
**日期：** 2026-09-12
**任务书：** `docs/superpowers/sdd/task-2-brief.md`

---

## 1. 本次实际执行范围

严格按任务书 Step 1-4 顺序执行（TDD：先写测试 → 确认 RED → 实现 → 确认 GREEN）。

| Step | 内容 | 本次动作 |
| --- | --- | --- |
| 1 | 写测试 `AnimatorParamsTests.cs` | **本次新建**（逐字采用任务书代码块） |
| 2 | 运行测试，确认失败 | **本次执行**，实测退出码 `2`（RED 成立） |
| 3 | 实现 `AnimatorParams.cs` | **本次新建**（逐字采用任务书代码块） |
| 4 | 运行测试，确认通过 | **本次执行**，实测退出码 `0`（GREEN 成立） |
| 5 | 提交 | **按硬性要求跳过**（本项目不使用 git，未执行任何 git 命令） |

补充：任务书 Interfaces 一节声称 `States` 为「42 个状态名」，与其 Step 1 测试（断言 39）及 Step 3 实现（数组 39 项）自相矛盾。已按后者（39）执行——见 §6 顾虑 1。

---

## 2. 实现的接口

`Demo.AnimatorParams`（`Assets/Script/Player/AnimatorParams.cs`，`Demo.Runtime` 程序集的**第一个脚本**）：

- `LayerName` / `ControllerPath` —— 层名与 Controller 资源路径常量
- 23 个参数名常量 + `AllParameters`
- `AnimatorParams.Groups` —— 6 个子状态机组名 + `Groups.All` + 顶层别名 `AllGroups`
- `AnimatorParams.BlendTrees` —— 3 个混合树名 + `BlendTrees.All` + 顶层别名 `AllBlendTrees`
- `AnimatorParams.States` —— 39 个状态名常量，外加速查表 `LightAttackChain` / `HeavyAttackChain` / `Rolls` / `Dashes`
- `AllStates` —— 39 项，按 6 组分组并带数量注释

**分组计数（与任务书注释一致）：**

| 组 | 数量 | 成员 |
| --- | --- | --- |
| Locomotion | 5 | `Idle_Normal`, `Idle_Battle` + 3 个混合树状态 |
| Combat | 11 | `Attack01-04` + `Combo01-05` + `Defend` + `DefendHit` |
| Movement | 10 | 4 翻滚 + 4 冲刺 + 2 跳跃 |
| Reaction | 3 | `GetHit01`, `GetHit02`, `Dizzy` |
| Special | 6 | `Challenging`/`Dance`/`Victory`/`LevelUp`/`SenseSomething_Start`/`_Searching` |
| Death | 4 | `Die01`, `Die02`, `Die01_Stay`, `GetUp` |
| **合计** | **39** | |

---

## 3. TDD 证据

### RED（实现前）

命令：

```bash
cd "E:/unity/求职demo"
docs/superpowers/sdd/run-tests task2
```

实测输出（退出码 `2`）：

```
Aborting batchmode due to failure:
Scripts have compiler errors.

✗ 测试没有产生结果文件。日志：E:/unity/求职demo/docs/superpowers/sdd/test-task2.log
EXIT_CODE=2
```

日志中的编译错误（`test-task2.log`，6 处唯一位置）：

```
Assets\Tests\EditMode\AnimatorParamsTests.cs(11,26): error CS0103: The name 'AnimatorParams' does not exist in the current context
Assets\Tests\EditMode\AnimatorParamsTests.cs(19,48): error CS0103: The name 'AnimatorParams' does not exist in the current context
Assets\Tests\EditMode\AnimatorParamsTests.cs(25,26): error CS0103: The name 'AnimatorParams' does not exist in the current context
Assets\Tests\EditMode\AnimatorParamsTests.cs(34,31): error CS0103: The name 'AnimatorParams' does not exist in the current context
Assets\Tests\EditMode\AnimatorParamsTests.cs(44,31): error CS0103: The name 'AnimatorParams' does not exist in the current context
Assets\Tests\EditMode\AnimatorParamsTests.cs(46,26): error CS0103: The name 'AnimatorParams' does not exist in the current context
```

**为什么该失败是预期的：** 6 处错误**全部且仅**落在 `AnimatorParamsTests.cs` 内部对 `AnimatorParams` 的引用上，而 `AnimatorParams` 正是 Step 3 才创建的类。`CS0103`（当前上下文中不存在该名称）证明测试确实在编译期依赖被实现类型，而非空跑——若测试写成永不失败的形状，RED 阶段根本不会出现这 6 处错误。这与任务书 Step 2 的预期（「编译失败，`Demo.AnimatorParams` 不存在」）完全一致。

> RED 日志已由 `run-tests` 的 `.prev` 轮转机制保留：`docs/superpowers/sdd/test-task2.log.prev`（含 18 行 `error CS0103`）。GREEN 运行会将上一份日志重命名为 `.prev`，因此 RED 制品可直接复核，非转录。

### GREEN（实现后）

在 Step 3 创建 `AnimatorParams.cs` 之后，重跑同一命令：

```
total=7  passed=7  failed=0  result=Passed
结果文件：E:/unity/求职demo/docs/superpowers/sdd/results-task2.xml
EXIT_CODE=0
```

`results-task2.xml` 顶层：`<test-run result="Passed" total="7" passed="7" failed="0" inconclusive="0" skipped="0">`。

7 个用例全部 Passed（新增 5 个 + Task 1 的 2 个回归）：

- `Demo.Tests.AnimatorParamsTests.组名_恰好六个且无重复`
- `Demo.Tests.AnimatorParamsTests.参数名_无重复`
- `Demo.Tests.AnimatorParamsTests.状态名_恰好三十九个且无重复`
- `Demo.Tests.AnimatorParamsTests.状态名_已修正拼写错误`
- `Demo.Tests.AnimatorParamsTests.状态名_不含重复的IdleBattle副本`
- `Demo.Tests.AssemblySmokeTests.测试程序集_已正确装配`
- `Demo.Tests.AssemblySmokeTests.编辑器程序集_可被测试程序集引用`

**关键点：`39` 与各组计数在首次 GREEN 运行中即通过，未做任何数值调整。**

---

## 4. 逐字比对（防漂移核查）

除人眼比对外，用机械方式核对了实现与任务书代码块：

```bash
sed -n '86,259p' docs/superpowers/sdd/task-2-brief.md > /tmp/brief_impl.cs
diff /tmp/brief_impl.cs Assets/Script/Player/AnimatorParams.cs   # → 无差异
sed -n '23,73p'  docs/superpowers/sdd/task-2-brief.md > /tmp/brief_test.cs
diff /tmp/brief_test.cs Assets/Tests/EditMode/AnimatorParamsTests.cs  # → 无差异
```

两个文件与任务书代码块**逐字节一致**（含注释、分组顺序、尾随逗号、缩进）。这直接满足「唯一真相来源」的要求：既然契约必须与任务书字面一致，任何手写改动都可能是漂移的来源，故逐字复制而非重排。

**契约完整性（下游任务交叉核查）：** 对 `task-*-brief.md` 全部 16 份任务书做了正则扫描，提取所有 `AnimatorParams.*` 引用并逐一对照本实现（共 90 个唯一引用字符串，其中绝大多数为成员引用，另含少量非成员串如文件名 `AnimatorParams.cs` 及链式调用后缀 `.Length` / `.ToHashSet`）：

```bash
grep -ohE "AnimatorParams(\.[A-Za-z_0-9]+)+" task-*.md | sort -u
```

所有被下游引用的成员**均已存在**，且未出现任何本实现未提供的成员。包括易被截断的 `States.Attack01/04`、`States.Combo01-05`、`States.Die01/Die01Stay/Die02`、`States.GetHit01/02`、`States.Rolls.Length`、`States.Dashes.Length`、`Groups.All.Length`、`LightAttackChain.ToHashSet` 等。

---

## 5. 改动的文件

**新建（源码）：**
- `E:/unity/求职demo/Assets/Script/Player/AnimatorParams.cs` —— 任务书 Step 3 代码块逐字；`AnimatorParams.cs.meta` 由 Unity 导入时自动生成（243 字节）
- `E:/unity/求职demo/Assets/Tests/EditMode/AnimatorParamsTests.cs` —— 任务书 Step 1 代码块逐字；`.meta` 同上由 Unity 生成

**未改动：** 除上述两个新文件外，未创建、未修改、未删除任何其他文件。特别确认：

- `Assets/RPGTinyHeroWavePBR/` 与 `Assets/RPGMonsterWave02PBR/`（实际目录名，非任务背景中一度写错的 `...02BPR`）**未被触碰**。
- 未新增任何 URP/HDRP 引用，未改动 `activeInputHandler`，未触碰 Input System 设置。
- Task 1 的 4 个文件（两个 asmdef、`Demo.Tests.EditMode.asmdef`、`AssemblySmokeTests.cs`）未改动。

**测试产物（生成物，非源码）：**
- `docs/superpowers/sdd/results-task2.xml`、`test-task2.log`、`test-task2.log.prev`（RED 留档）

---

## 6. 自查

**完整性** —— 任务书列出的每个成员均已实现，无遗漏：`LayerName`、`ControllerPath`、23 个参数名 + `AllParameters`、`Groups`（6 名 + `All`）、`AllGroups`、`BlendTrees`（3 名 + `All`）、`AllBlendTrees`、`States`（39 名 + 4 张速查表）、`AllStates`（39 项）。状态数组中无遗漏成员，6 组划分与注释数量一一对应。

**质量** —— 命名与字符串值逐字取自任务书（已用 `diff` 机械确认）。注释沿用任务书中文风格（`///` 摘要 + `// ---------------- 分区 ----------` + 组内数量注释）。`Shiled → Shield` 类拼写问题在原资源中的痕迹已由 `状态名_已修正拼写错误` 用例守卫。

**克制（YAGNI）** —— 未添加任务书之外的任何成员、状态、辅助方法或数组别名。未提前实现 Task 3+ 的生成器代码。未新建 `Assets/Animator/`（`ControllerPath` 指向的资源属后续任务创建，本任务只声明路径常量）。

**测试** —— 5 个用例均验证**真实行为**：数组长度（39 / 6）、唯一性、拼写守卫、`Idle_Battle` 副本合并计数。其中「恰好三十九个」是硬数值断言，不会因数组为空而空过；`AllItemsAreUnique` 在空数组上会空过，但被长度断言兜住，两者组合有效。GREEN 输出干净：

- `error CS` / `warning CS` 命中数 = **0**
- `Scripts have compiler errors` / `Aborting batchmode` 命中数 = **0**
- **`Demo.Runtime` 的「程序集无脚本」警告已消失**（本任务为其加入首个脚本，正是预期效果）；全日志检索 `no scripts` / `Assembly Definition File` / `has no` 均 0 命中
- 全 0 编译警告，无噪音

**发现问题：** 无。逐字比对、契约完整性核查、输出洁净度核查均通过，无需修正。

---

## 7. 问题与顾虑

1. **任务书内部自相矛盾：`States` 的数量（42 对 39）。** 任务书的 Interfaces 一节写道「`Demo.AnimatorParams.States` — 42 个状态名」，但同任务书 Step 1 的测试断言 `AreEqual(39, states.Length, "5 + 11 + 10 + 3 + 6 + 4 = 39")`、Step 3 的 `AllStates` 数组实际也是 39 项。**已按 39 执行**（测试与实现一致，且两者互相印证），未改动任何数值。判断依据：Step 1 与 Step 3 是任务书的可执行部分且彼此一致，Interfaces 一节的「42」是叙述性笔误；若 42 才是真值，则 6 组的划分（5+11+10+3+6+4）必须重新定义，属架构决策而非本任务可擅自决定的事。**顺带提示：** 背景资料称本任务「The `AllStates` array length assertion (39)」为已核对过的既定值，与本次执行一致，故判定 39 正确。若后续任一任务确实依赖 42，请在下一任务开始前指出——这是唯一需要上游确认的点，但它不影响本任务的交付正确性（测试与实现自洽、逐字符合任务书可执行部分）。

2. **`AllStates` 的 Combat 组含 11 项而非 12 项。** 组内注释与数组内容一致（`Attack01-04` + `Combo01-05` + `Defend` + `DefendHit` = 11），无遗漏；此处仅记录已核对，非疑虑。

3. **`results-task2.xml` 中 `asserts="0"`。** 与 Task 1 报告 §7.1 所述为同一已知表现（Unity Test Framework 对 NUnit classic assert 的计数），不影响结论：7 个用例均有独立 `test-case` 节点、`result="Passed"`、`runstate="Runnable"` 与独立耗时，确已执行。更实质的证据是 RED/GREEN 的对比——若断言未真正执行，RED 阶段的 6 处 `CS0103` 与 GREEN 后的 `total=7 passed=7` 不会呈现如此干净的反差。

4. **日志中 `abort_threads: Failed aborting id: ...` 若干行。** 位置在 `Saving results to: ...results-task2.xml` **之后**，属 `run-tests` 强制结束 batchmode 进程时的 mono 线程收尾噪音（该脚本头注释已说明设计意图），非本工程代码产物，与测试结果无关，不计为告警。

5. **`run-tests` 会强制结束所有 `-batchmode` 的 Unity 进程并清理项目锁**，本次运行中属预期行为。
