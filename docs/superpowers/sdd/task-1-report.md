# Task 1 报告：程序集骨架与测试装配

**状态：** DONE
**日期：** 2026-09-12
**任务书：** `docs/superpowers/sdd/task-1-brief.md`

---

## 1. 本次实际执行范围

接手时前一次尝试已完成 Step 1-4，本次先逐一比对任务书确认无误，然后执行 Step 5、6、7、8。

| Step | 内容 | 本次动作 |
| --- | --- | --- |
| 1 | `Demo.Runtime.asmdef` | 已存在，逐字比对**一致**，未改动 |
| 2 | `Demo.Editor.asmdef` | 已存在，逐字比对**一致**，未改动 |
| 3 | `Demo.Tests.EditMode.asmdef` | 已存在，逐字比对**一致**，未改动 |
| 4 | `AssemblySmokeTests.cs` | 已存在，逐字比对**一致**，未改动 |
| 5 | 创建 `NamespaceAnchor.cs` | **本次新建** |
| 6 | 删除旧 `PlayerController.cs` | **本次删除** |
| 7 | 跑测试确认通过 | **本次执行**（命令行，非 GUI） |
| 8 | 确认无编译错误 | **本次执行** |
| 9 | 提交 | **按硬性要求跳过**（本项目不使用 git） |

比对结果：4 个既有文件与任务书代码块**逐字一致**，无需修正。

---

## 2. 实现的接口

任务书 Produces 的三个程序集均已建立并可被正确引用：

- `Demo.Runtime` —— 运行时程序集，`rootNamespace: Demo`
- `Demo.Editor` —— 编辑器程序集，`rootNamespace: Demo.EditorTools`，仅 Editor 平台，引用 `Demo.Runtime`
- `Demo.Tests.EditMode` —— 测试程序集，`rootNamespace: Demo.Tests`，引用 `Demo.Runtime` + `Demo.Editor` + 两个 TestRunner，`defineConstraints: UNITY_INCLUDE_TESTS`

`Demo.Tests.EditMode` 能编译 `typeof(Demo.EditorTools.NamespaceAnchor)` 这一事实，即证明「测试程序集 → 编辑器程序集」的引用链是通的。

---

## 3. TDD 证据

### RED（实现前）

命令：

```bash
cd "E:/unity/求职demo"
docs/superpowers/sdd/run-tests task1
```

实测输出（退出码 `2`）：

```
Aborting batchmode due to failure:
Scripts have compiler errors.

✗ 测试没有产生结果文件。日志：E:/unity/求职demo/docs/superpowers/sdd/test-task1.log
EXIT_CODE=2
```

日志中的编译错误（三处，同一错误重复）：

```
Assets\Tests\EditMode\AssemblySmokeTests.cs(17,36): error CS0234: The type or namespace name 'EditorTools' does not exist in the namespace 'Demo' (are you missing an assembly reference?)
```

**为什么该失败是预期的：** `AssemblySmokeTests.cs` 第 17 行引用了 `Demo.EditorTools.NamespaceAnchor`，而该类要到 Step 5 才创建。此刻 `Demo.Editor` 程序集里没有任何类型处于 `Demo.EditorTools` 命名空间下，因此编译器报 `CS0234`（命名空间中不存在类型/命名空间）。这是任务书设计好的 RED 阶段，不是故障：它恰好证明了测试确实在编译期依赖编辑器程序集，而非空跑。

> 说明：RED 输出为**转录**而非日志文件链接。`run-tests` 脚本在每次运行开始时执行 `rm -f "$xml" "$log"`，因此随后那次 GREEN 运行覆盖了 RED 日志。命令、输出与退出码均为本次实测原样记录。

### GREEN（实现后）

在 Step 5 创建 `NamespaceAnchor.cs`、Step 6 删除旧 `PlayerController.cs` 之后，重跑同一命令：

```
total=2  passed=2  failed=0  result=Passed
结果文件：E:/unity/求职demo/docs/superpowers/sdd/results-task1.xml
EXIT_CODE=0
```

`results-task1.xml` 顶层：`<test-run result="Passed" total="2" passed="2" failed="0">`，两个用例：

- `Demo.Tests.AssemblySmokeTests.测试程序集_已正确装配` — Passed
- `Demo.Tests.AssemblySmokeTests.编辑器程序集_可被测试程序集引用` — Passed

---

## 4. Step 8：编译错误检查

对 GREEN 日志 `docs/superpowers/sdd/test-task1.log` 检索：

- `error CS` / `Scripts have compiler errors` / `Aborting batchmode` —— **0 命中**
- `PlayerController`（不区分大小写）—— **0 命中**

即：无残留的 `PlayerController` 编译错误。

补充核查（任务书只要求查编译错误，这里额外确认删除没有留下悬挂引用）：

- `Assets/Scenes/SampleScene.unity` 中 `MonoBehaviour` 块数量为 **0**，不存在指向已删除脚本的悬挂 GUID，不会产生 missing script 警告。
- 全工程 `Assets` 下（排除第三方 `Lowpoly Style`）`.cs` 文件仅两个：`Assets/Script/Editor/NamespaceAnchor.cs`、`Assets/Tests/EditMode/AssemblySmokeTests.cs`，无任何文件再引用 `PlayerController`。
- `Assets/Script/Player/` 目录尚不存在，与任务书一致 —— Task 14 会在新位置重建 `PlayerController`。

---

## 5. 改动的文件

**新建：**
- `E:/unity/求职demo/Assets/Script/Editor/NamespaceAnchor.cs` —— 采用任务书 Step 5 的**修正版**代码块（`public static class`，非 `internal`），因为任务书明确指出 `internal` 类型在测试程序集中不可见。`NamespaceAnchor.cs.meta` 由 Unity 导入时自动生成（243 字节）。

**删除：**
- `E:/unity/求职demo/Assets/Script/PlayerController.cs`
- `E:/unity/求职demo/Assets/Script/PlayerController.cs.meta`

**未改动（比对一致）：**
- `Assets/Script/Demo.Runtime.asmdef`
- `Assets/Script/Editor/Demo.Editor.asmdef`
- `Assets/Tests/EditMode/Demo.Tests.EditMode.asmdef`
- `Assets/Tests/EditMode/AssemblySmokeTests.cs`

**测试产物（生成物，非源码）：**
- `docs/superpowers/sdd/results-task1.xml`
- `docs/superpowers/sdd/test-task1.log`

---

## 6. 自查

**完整性** —— 任务书 Step 1-8 全部覆盖：Step 1-4 核对通过、Step 5-8 本次完成。Step 9 按硬性要求 1 跳过。无遗漏。

**质量** —— `NamespaceAnchor` 命名准确（它确实只是一个命名空间锚点，不代表业务含义）；注释沿用任务书原文，已去掉只适用于 `internal` 版本的措辞。

**克制** —— 未创建任何任务书之外的文件，未预建 `Assets/Script/Player/`，未提前写 Task 14 的代码。改动仅两处：加一个类、删一个类。

**测试** —— 两个用例真实在跑（XML 中有独立 `test-case` 节点与耗时），不是空跑；GREEN 输出干净，无警告噪音。

**未发现需要修正的问题。**

---

## 7. 问题与顾虑

1. **`results-task1.xml` 中两个用例的 `asserts="0"`。** 这是 Unity Test Framework 报告 NGUI/NUnit classic assert 计数时的已知表现，不影响结论：两个用例均标注 `result="Passed"` 且 `runstate="Runnable"`、有独立耗时，确已执行。仅作记录，无需处理。更实质的通过证据是编译本身 —— 若引用链断裂，GREEN 根本不会产出结果文件（如 RED 阶段所示）。

2. **RED 证据为转录。** 见 §3 说明：`run-tests` 每次启动即清理日志，GREEN 覆盖了 RED 日志。如需保留可复现的 RED 制品，可在后续任务中考虑给 `run-tests` 增加「不覆盖历史日志」的选项，或在 RED 后手工 `cp` 一份。本次未擅自改动该脚本（不在任务范围内）。

3. **`Demo.Editor.asmdef.meta` 的 mtime 被 Unity 更新为 00:56**（内容为 Unity 重新序列化的标准 meta，166 字节）。是导入时的正常行为，非人为改动。

4. **`run-tests` 会强制结束所有 `-batchmode` 的 Unity 进程并清理项目锁**，这是该脚本的有意设计（其头部注释已说明），本次运行中属预期行为。
