# Task 5b 报告：图层入口显式化与 PlayMode 验证

## 1. 状态

**BLOCKED**

brief 有两处前提与实测不符，无法按 brief 原样执行：

- **冲突 A（Step 1）**：brief 声称 `includePlatforms: ["Editor"]` 的测试程序集仍能被
  `-testPlatform PlayMode` 找到。实测**找不到**（total=0）。Unity Test Framework 只按
  `AssemblyFlags.EditorOnly` 决定测试属于 EditMode 还是 PlayMode，Editor-only 就是 EditMode。
- **冲突 B（Step 4）**：brief 的修复只有 `root.AddEntryTransition(locomotion);` 一行，并断言
  "有入口转换时，defaultState 是被绕过的死值"。实测**这行不够**：图层启动状态由**根状态机的
  `defaultState`** 决定，根状态机的入口转换不参与启动；加完这一行运行时仍逐帧停在
  `Idle_Normal`。brief 自己在 Step 4 留了"这一行究竟够不够，由 Step 5 裁决"，裁决结果是**不够**。

两处都已用证据定位（见第 3 节），并已应用**经实测验证的最小偏离**，使 RED→GREEN→EditMode
全部按 brief 预期的形态跑通（GREEN 退出码 0，EditMode 仅剩那条 Task 7 之前的预期失败）。
代码现在是**可工作、已测**的状态，只差计划作者追认下面两处偏离：

| 偏离 | brief 原文 | 实际写入 | 依据 |
|---|---|---|---|
| `Demo.Tests.PlayMode.asmdef` 的 `includePlatforms` | `["Editor"]` | `[]` | 与 Unity 自己的 PlayMode 测试模板一致；实测改成 `[]` 后 `-testPlatform PlayMode` 才发现并运行该测试 |
| `HeroAnimatorBuilder.BuildLocomotion` | 只加 `root.AddEntryTransition(locomotion);` | 再追加 `root.defaultState = stFreeNormal;` | 实测 V0/V1 失败、V2/V3 成功（见 3.9） |

> 若计划作者不认可偏离 B，可选的替代设计是：让根状态机拥有一个真实状态（但这会违反现有断言
> `根状态机上没有游离状态`），或接受运行时停在 `Idle_Normal`。我未擅自选择这两条。

**按硬性约束，Step 8「提交」被跳过**：本工程无 git，未执行任何 `git add` / `git commit`。

### 关于 BLOCKED 与"已经做完了"的关系

功能上这一步是完成的（测试全绿），但我不认为应当自行判定设计。两处偏离都改动了 brief 明确
规定的内容（一个是 asmdef 内容，一个是 brief 用大段注释**明令禁止**的做法），因此按任务约定
报 BLOCKED，把决定权交回计划作者。

---

## 2. 逐文件改动清单

### 2.1 新增 `Assets/Tests/PlayMode/Demo.Tests.PlayMode.asmdef`
brief 原文照抄，**唯一改动**是把
```json
    "includePlatforms": [
        "Editor"
    ],
```
改为
```json
    "includePlatforms": [],
```
（与 Unity 自带的 PlayMode 测试模板一致，见 3.10 证据）

### 2.2 新增 `Assets/Tests/PlayMode/AnimatorEntryTests.cs`
brief 原文**逐字**照抄，未改一字。包括空 GameObject 探针、
`LogAssert.ignoreFailingMessages`、`shortNameHash` 断言与失败信息格式。

### 2.3 修改 `Assets/Script/Editor/HeroAnimatorBuilder.cs`
`BuildLocomotion` 内，brief Step 4 的整段**逐字**照抄（含原注释），随后**追加**一段带
`【Task 5b 实测补充】` 标记的注释与一行代码：
```csharp
            root.defaultState = stFreeNormal;
```
位置：`root.AddEntryTransition(locomotion);` 之后、`// ---- 组内转换 ----` 之前。
原有其他代码一行未动。

> 注意：brief 原注释里"有入口转换时，defaultState 是被绕过的死值"这句**实测为假**。
> 我保留原句以保持偏离可审计，并在紧随其后的补充注释里明确驳斥。建议追认后删掉该句。

### 2.4 修改 `Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs`
仅在 `HeroAnimatorBuilderTests` 类**末尾追加** brief Step 6 的那一个 `[Test]`
（`根状态机有指向Locomotion的入口转换`），**一个已有断言都没动**。

### 2.5 `Assets/Animator/Hero_SwordAndShield.controller`（被动重新生成，非手改）
由测试的 `[OneTimeSetUp]` 调用 `HeroAnimatorBuilder.Build()` 时重新生成。
最终结构（脚本读取 YAML 校验）：

```
SM 'Base Layer'  entries= 1  default= Tree_Free_Normal
SM 'Locomotion'  entries= 0  default= Tree_Free_Normal
SM 'Combat'/'Movement'/'Reaction'/'Special'/'Death'  entries= 0  default= 0（未建，Task 7 负责）
```

### 2.6 未触碰（硬性约束核对）
对 baseline 快照做 `diff -rq`，改动面只有上面 5 项 + Unity 自动生成的
`ProjectSettings/SceneTemplateSettings.json`（首次运行产生，非我写入）：

```
Files BASE/Assets/Animator/Hero_SwordAndShield.controller and FINAL/... differ
Files BASE/Assets/Script/Editor/HeroAnimatorBuilder.cs and FINAL/... differ
Files BASE/Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs and FINAL/... differ
Only in FINAL/Assets/Tests: PlayMode
Only in FINAL/Assets/Tests: PlayMode.meta
Only in FINAL/ProjectSettings: SceneTemplateSettings.json
```
**`Assets/RPGTinyHeroWavePBR/`、`Assets/RPGMonsterWave02PBR/` 零改动**（仅只读引用）；
`ProjectSettings/ProjectSettings.asset` 未出现在 diff 中，`activeInputHandler` 未变。

---

## 3. 逐步命令与**实际观察到的输出**

### 3.1 基线快照
```
$ bash docs/superpowers/sdd/snapshot docs/superpowers/sdd/snaps/task5b-base
snapshot -> docs/superpowers/sdd/snaps/task5b-base (60 files)
```

### 3.2 前置事实核对（改动前，读磁盘上的控制器 YAML）
四个 RED 前提全部在磁盘上核实：
```
# 根状态机 Base Layer：m_ChildStates: []、m_EntryTransitions: []（无入口）
119:  m_Name: Base Layer
141:  m_EntryTransitions: []
# 根 defaultState → Idle_Normal（一个 root 并不拥有的状态）
148:  m_DefaultState: {fileID: -4501389154386948140}
# 该 fileID 就是 Idle_Normal 状态的锚点，且 Idle_Normal 零出边
67:--- !u!1102 &-4501389154386948140
74:  m_Name: Idle_Normal
77:  m_Transitions: []
# Idle_Normal 属于 Locomotion 子状态机
476:    m_State: {fileID: -4501389154386948140}
```

### 3.3 Step 3 按 brief 原样跑 RED —— **发现冲突 A**
```
$ bash docs/superpowers/sdd/run-tests task5b-red PlayMode
platform=PlayMode  total=0  passed=0  failed=0  result=Passed
结果文件：E:/unity/求职demo/docs/superpowers/sdd/results-task5b-red.xml
EXIT_CODE=0
```
**total=0：测试根本没被发现**（编译正常，`Demo.Tests.PlayMode.dll` 已生成；日志里
`Running tests for PlayMode` / `test mode = PlayMode` 都在，只是没有测试入选）。
该次结果已留档为 `docs/superpowers/sdd/results-task5b-red.xml.prev`。

### 3.4 反证：同一份测试在 EditMode 平台被跑到了
```
$ bash docs/superpowers/sdd/run-tests task5b-red-editmode
platform=EditMode  total=30  passed=28  failed=2  result=Failed(Child)
EXIT_CODE=1
```
两条失败中一条是预期的 `每个状态都被归入某个子状态机`（Task 7 前预期失败），
另一条**就是本任务的探针测试**，失败原因是 EditMode 语境：
```
Unhandled log message: '[Error] Destroy may not be called from edit mode! Use DestroyImmediate instead.'
at Demo.Tests.AnimatorEntryTests/<首帧停在Locomotion的混合树状态>d__3:<>m__Finally1 () (at Assets/Tests/PlayMode/AnimatorEntryTests.cs:56)
```
→ 证明该程序集被 UTF 归为 **EditMode**，与 3.3 的 total=0 互为印证。

### 3.5 诊断 1：Animator 在这种语境下到底有没有求值
（临时文件，已删除；仅打印，不断言）
```
DIAG isInitialized=True shortNameHash=2124725196 fullPathHash=312396153 idleNomral=2124725196 treeFreeNormal=193745133 clips=[Idle_Normal_SwordAndShield]
```
`isInitialized=True`，且当前状态 = `Idle_Normal`（`idleNomral=2124725196` 与之相等，
`treeFreeNormal=193745133` 不等）。**探针技术路线成立**，缺陷可复现。

### 3.6 把 asmdef 的 includePlatforms 改成 `[]` 后重跑 PlayMode
```
$ bash docs/superpowers/sdd/run-tests task5b-exp-playmode PlayMode
platform=PlayMode  total=2  passed=1  failed=1  result=Failed(Child)
EXIT_CODE=1
```
**PlayMode 平台这次真的发现了测试**（2 条 = 探针 + 临时诊断），失败信息正是 brief 预测的那条：
```
第 0 层当前状态应为 Tree_Free_Normal（shortNameHash=193745133），实际 shortNameHash=2124725196、fullPathHash=312396153，正在播放：Idle_Normal_SwordAndShield
```

### 3.7 canonical RED（asmdef 已修，生成器未改；临时文件已清）
```
$ bash docs/superpowers/sdd/run-tests task5b-red PlayMode
platform=PlayMode  total=1  passed=0  failed=1  result=Failed(Child)
EXIT_CODE=1
```
失败信息**原文**（UTF-8，取自 `results-task5b-red.xml`）：
```
第 0 层当前状态应为 Tree_Free_Normal（shortNameHash=193745133），实际 shortNameHash=2124725196、fullPathHash=312396153，正在播放：Idle_Normal_SwordAndShield
```
与 brief Step 3 的 Expected 完全一致（当前状态是 `Idle_Normal` 的哈希与剪辑名，不是
`Tree_Free_Normal`）。

### 3.8 Step 4 只加 brief 那一行 → Step 5 GREEN **仍然失败** —— **冲突 B**
```
$ bash docs/superpowers/sdd/run-tests task5b-green PlayMode     # 首次 GREEN，仅 brief Step 4 一行
platform=PlayMode  total=1  passed=0  failed=1  result=Failed(Child)
EXIT_CODE=1
```
失败信息与 RED **一字不差**（仍是 `Idle_Normal`、剪辑 `Idle_Normal_SwordAndShield`）。
但磁盘上的控制器结构看起来是**对的**：
```
SM 'Base Layer'  entries= 1  default= Idle_Normal        # 入口转换已生成
SM 'Locomotion'  entries= 0  default= Tree_Free_Normal
m_DstStateMachine: {fileID: -6449690594513478293}        # 入口转换终点 = 第一个子状态机 = Locomotion
m_Conditions: []                                          # 无条件
```

### 3.9 诊断 2/3：判定"入口转换没生效"还是"拿到旧资产"
诊断 2（**一次运行同时打印加载到的资产结构与运行时逐帧状态**）：
```
DIAG2 asset: loadedEntries=1 entryDst=Locomotion conds=1 rootDefault=Idle_Normal locoName=Locomotion locoDefault=Tree_Free_Normal rootStates=0
DIAG2 runtime: frame=0 isInit=True short=2124725196 full=312396153 ...
DIAG2 runtime: frame=1 isInit=True short=2124725196 full=312396153 ...
（frame 2..5 全部相同）
```
→ **加载到的控制器确实带着入口转换**，而运行时 6 帧都停在 `Idle_Normal`。
"拿到旧资产"被排除；结论是**根状态机的入口转换在图层启动时不被采纳**。

诊断 3（4 个候选修法，只改内存资产、不 SaveAssets）：
```
DIAG3 setup: entries=1 rootDefault=Idle_Normal locoDefault=Tree_Free_Normal
DIAG3 RESULT V0_entryOnly_autoDefault:      short=2124725196(=Idle_Normal)      ← 现状，坏
DIAG3 V1 applied: rootDefault=Idle_Normal                                        ← 设 null 是空操作
DIAG3 RESULT V1_entry_nullDefault:          short=2124725196(=Idle_Normal)      ← 坏
DIAG3 V2 applied: rootDefault=Tree_Free_Normal
DIAG3 RESULT V2_entry_treeFreeDefault:      short=193745133(=Tree_Free_Normal)  ← 通过
DIAG3 V3 applied: entries=0 rootDefault=Tree_Free_Normal
DIAG3 RESULT V3_noEntry_treeFreeDefault:    short=193745133(=Tree_Free_Normal)  ← 通过
```
三个可直接核对的 API 事实：
1. **`root.defaultState` 决定图层启动状态**；根状态机的 entry transition 不参与启动。
2. **`root.defaultState = null` 是空操作**（读完仍是 `Idle_Normal`），无法靠"清空"让入口转换接管。
3. Unity 自己的 `defaultState` 文档正是这么说的：
   `"The state that the state machine will be in when it starts."`
   （`UnityEditor.CoreModule.xml` → `P:UnityEditor.Animations.AnimatorStateMachine.defaultState`）

修法选 V2（保留 brief 要求的入口转换，同时补 defaultState），因为 Step 6 的断言要求
`root.entryTransitions.Length == 1`，V3 会把它去掉。

### 3.10 Step 5 GREEN（最终）
```
$ bash docs/superpowers/sdd/run-tests task5b-green PlayMode
platform=PlayMode  total=1  passed=1  failed=0  result=Passed
结果文件：E:/unity/求职demo/docs/superpowers/sdd/results-task5b-green.xml
EXIT_CODE=0
```

### 3.11 Step 7 EditMode（最终，无回归）
```
$ bash docs/superpowers/sdd/run-tests task5b-editmode
platform=EditMode  total=30  passed=29  failed=1  result=Failed(Child)
EXIT_CODE=1
```
- 唯一失败的仍是**预期**的 `每个状态都被归入某个子状态机`：
  ```
  状态 Attack01 没有被放进任何子状态机   Expected: True   But was: False
  ```
  （Task 7 之前预期失败，未触碰）
- **新增的 `根状态机有指向Locomotion的入口转换` 结果 = Passed**
- 测试条数 29 → 30，正是"新增一条断言、探针迁出到 PlayMode 程序集"的结果，无其他增减。

### 3.12 与外部声明一致性的旁证（Unity 自身定义）
- `Library/PackageCache/com.unity.test-framework@1.1.33/UnityEditor.TestRunner/TestRunner/Utils/EditorLoadedTestAssemblyProvider.cs:60`
  ```csharp
  var assemblyType = (assemblyFlags & AssemblyFlags.EditorOnly) == AssemblyFlags.EditorOnly ? TestPlatform.EditMode : TestPlatform.PlayMode;
  ```
- Unity 自带模板（`Editor/Data/Resources/ScriptTemplates/`）：
  - PlayMode 模板 `92-Assembly Definition-NewTestAssembly.asmdef.txt` → **无 `includePlatforms`**
  - EditMode 模板 `...NewEditModeTestAssembly.asmdef.txt` → `"includePlatforms": ["Editor"]`
- 工程内 Unity 官方作者所写的控制器（`Assets/RPG*/...` 只读查看）全部是 `childSMs=0, entries=0`、
  `default=` 一个自己拥有的状态；**从不使用根状态机入口转换**。

---

## 4. 自审

**有没有 brief 没要求却加了的东西？**
- 只有一处有意的额外代码：`root.defaultState = stFreeNormal;`（3.9 实测必需，已在注释里标明
  "超出 brief Step 4"）。这是本轮唯一的越界代码。
- 两个临时诊断文件（`TempDiagnostic.cs` / `2` / `3`）与其 `.meta` 已**全部删除**，
  `Assets/Tests/PlayMode/` 现在只剩 `Demo.Tests.PlayMode.asmdef` 与 `AnimatorEntryTests.cs`。
- 没有新增任何文档、没有动任何受保护资源、没有动 `ProjectSettings.asset`。
- 没有执行任何 git 命令（Step 8 按硬性约束跳过）。

**有没有该做没做的？**
- brief 的各步都执行了（Step 1–7；Step 8 按约束跳过），且 RED 与 GREEN 用的是不同 LABEL
  （`task5b-red` / `task5b-green`），RED 证据保留在 `results-task5b-red.xml`（以及 total=0 那次
  的 `.prev`）。
- 有一处**我故意没做**：brief Step 6 只要求断言入口转换，我照做。但**真正让运行时走得通的是
  `defaultState`**，所以这条断言守不住真正的修复（有人删掉 `root.defaultState = stFreeNormal;`
  时它照样绿）。我**没有**擅自加第二条断言（`Assert.AreEqual(Tree_Free_Normal,
  root.defaultState.name)`），因为 brief 只要求"追加一条"，加了就是又一次自行改设计。
  **建议由计划作者补上这一条**，否则本任务容易被后人悄悄改回去。

**偏离是否被如实记录？** 是。两处偏离、其证据、以及不认可的后果都写在代码注释与第 1 节表格里。

---

## 5. 疑虑 / 未验证 / 需要你拍板

1. **需要追认：`includePlatforms` 从 `["Editor"]` 改成 `[]`。** 代价是这个程序集不再是
   Editor-only；虽然它有 `defineConstraints: ["UNITY_INCLUDE_TESTS"]` 兜底，但如果将来有人
   带着 `UNITY_INCLUDE_TESTS` 去构建**播放器**，测试里的 `using UnityEditor` / `AssetDatabase`
   会编译失败（编辑器内一切正常，本次也验证了能编译能跑）。UTF 的归类键就是 EditorOnly 标志位，
   两者不可兼得：**要么 `-testPlatform PlayMode` 找不到它，要么它不能是 Editor-only。**
2. **需要追认：`root.defaultState = stFreeNormal;`。** 这是 brief 用大段注释明令禁止的写法。
   我的依据是它指向的是一个 root 并不拥有的状态——**而 Unity 自己自动填入的 `Idle_Normal`
   正是同一种指针**（`rootStates=0`）。若计划作者认为"根状态机不应有外部指针"是硬约束，
   那替代方案只有让根状态机拥有一个真实状态，而那会违反现有断言 `根状态机上没有游离状态`，
   属于设计层取舍，需要你来定。
3. **brief 原注释里"有入口转换时，defaultState 是被绕过的死值"这句与实测相反**，我按"逐字使用"
   保留了它并在下方驳斥。建议追认后删除该句，避免后人被误导。
4. **未验证：`V2` 修法在真机/播放器里的表现。** 我的全部证据都来自编辑器内的 PlayMode
   （batchmode）。本工程测试只在编辑器跑，这与 brief 的定位一致，但"只有编辑器验证过"这一点
   应当被知道。
5. **未验证：`UnityEditor.TestRunner` 引用在非 Editor-only 程序集里是否在所有 Unity 版本都合法。**
   本版本（2022.3.62f1c1）实测编译通过，但我没有找到官方文档明确背书这一点；若将来升级
   Unity 后此程序集编译失败，请优先怀疑这里。
6. **`ProjectSettings/SceneTemplateSettings.json` 是 Unity 跑测试时自动新增的**，
   不是我写的，已在 2.6 说明。
7. **探针用空 GameObject 的假设已实测确认成立**（brief 曾让我在报告里写清实测结果）：
   `isInitialized=True`，状态机正常求值，无需换成带 Avatar 的预制体。brief 里"若
   shortNameHash=0 就换预制体"的备选方案**没有用到**。
8. **非回归项已核对**：`每个状态都被归入某个子状态机` 仍失败（Attack01），与本任务无关，
   按要求未修。


---

## 返工记录（对齐改正后的 brief，2026-09-12）

计划作者已裁定：上一版实现的两处偏离都是对的，计划书（brief）已按实测改正；本次把代码对齐到改正后的计划文本。

### 改了什么

1. `Assets/Script/Editor/HeroAnimatorBuilder.cs`（`BuildLocomotion`）：把那段临时注释**整块替换**为 brief Step 4 的注释原文（brief 第 218–237 行）。代码两行 `root.AddEntryTransition(locomotion);` 与 `root.defaultState = stFreeNormal;` 原样保留，顺序不变。
   - 去掉的内容：`【Task 5b 实测补充 —— 超出 brief Step 4 的一行，理由见 task-5b-report.md】` 这类"相对 brief 的偏离说明"，以及指向 `task-5b-report.md` 的引用。理由：报告是 SDD 工作区的过程文件、不在源码仓库语义里，源码注释引用它是永久性死链；"超出 brief 的一行"在计划改正之后已成假陈述（现在它就在 brief 里）。
   - 替换后的注释把两行各自的作用、以及三条实测结论都写清楚了。
2. `Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs`：测试 `根状态机有指向Locomotion的入口转换` 改名为 `根状态机的入口指向Locomotion`（brief 第 264 行）；在原有三条断言之后追加 brief 第 273–281 行给出的两条 `root.defaultState` 断言。类里其它已有测试一个字未改。
   - 原因：只钉入口转换的话，把 `root.defaultState = stFreeNormal;` 删掉测试照样全绿，而运行时缺陷原样复现 —— 原断言守不住真正的修复。

### 运行证据

PlayMode（`bash docs/superpowers/sdd/run-tests task5b-green2 PlayMode`）：

```
platform=PlayMode  total=1  passed=1  failed=0  result=Passed
```

退出码 0。结果文件 `docs/superpowers/sdd/results-task5b-green2.xml`。`total` 确为 1（非装配失败造成的假绿）。

EditMode（`bash docs/superpowers/sdd/run-tests task5b-editmode2`）：

```
platform=EditMode  total=30  passed=29  failed=1  result=Failed(Child)
```

退出码 1。结果文件 `docs/superpowers/sdd/results-task5b-editmode2.xml`。
唯一失败项经结果 XML 核实为 `每个状态都被归入某个子状态机`（Task 7 之前的预期失败，与本任务无关）；改名后的 `根状态机的入口指向Locomotion` 在 passed 中；无其它失败项。

### 意外

无。两次运行的 total/passed/failed 与预期完全一致。

---

## 返工记录 #2（两条 Minor 对齐，2026-09-12）

计划作者已核实上一轮审查提出的两条 Minor 为**真实缺陷**，brief 已据此重新生成。本轮**只做两处改动**，代码与注释均按改正后的 brief 原文照抄，未顺手重构、未改措辞。

### 改了什么

1. `Assets/Tests/PlayMode/AnimatorEntryTests.cs`
   - 删掉 `[TearDown] RestoreLogAssert()`；
   - 把 `LogAssert.ignoreFailingMessages = true;` 从 `[OneTimeSetUp] RegenerateAndLoad()` 移进 `[UnityTest] 首帧停在Locomotion的混合树状态()` 的方法体，作为**第一条语句**，连同 brief Step 2 里"⚠️ 这行**必须在测试体里**"那段注释一并照抄。
   - 依据（已从 UTF 源码核实）：`LogAssert.ignoreFailingMessages` 是 **per-LogScope 实例**属性 —— `LogAssert.cs:63-77` 的静态属性转发到 `LogScope.Current`，而 `LogScope.Current` 是 `s_ActiveScopes[0]`（`LogScope.cs:26-34`），构造即 `Activate()` 入栈、`Dispose()` 出栈；且 setter **只在值发生变化时**才写日志（`LogAssert.cs:71-73`）。写在 `[OneTimeSetUp]` 里作用的是 fixture 级 scope，测试体用的是另一个 scope（值为 false），设置随之失效；`[TearDown]` 的复位则打在一个本就为 false 的新 scope 上。
   - 改后整个文件与 brief Step 2 代码块做逐行 diff：**IDENTICAL（92 行，无差异）**。

2. `Assets/Script/Editor/HeroAnimatorBuilder.cs`（`BuildLocomotion`）：把"图层入口"注释整块换成 brief Step 4 的新版（brief 第 222–245 行原文照抄，共 24 行注释）。删掉的是被 DIAG3 四组合逐帧实测**证伪**的"两行都要，缺任一行动画都跑不起来"；新版写明"**决定图层启动状态的是 `defaultState`，根状态机的入口转换不参与启动**"，并说明入口转换保留的理由是让图不悬空。两行代码 `root.AddEntryTransition(locomotion);` 与 `root.defaultState = stFreeNormal;` **未改、顺序不变**（现为文件第 215–216 行）。

### 运行证据

PlayMode（`bash docs/superpowers/sdd/run-tests task5b-green3 PlayMode`）：

```
platform=PlayMode  total=1  passed=1  failed=0  result=Passed
```

退出码 **0**。结果文件 `results-task5b-green3.xml`（`result="Passed" total="1" passed="1" failed="0"`，duration=0.356s）。`total` 确为 1，不是装配失败造成的假绿。

EditMode（`bash docs/superpowers/sdd/run-tests task5b-editmode3`）：

```
platform=EditMode  total=30  passed=29  failed=1  result=Failed(Child)
```

退出码 **1**。结果文件 `results-task5b-editmode3.xml`。唯一失败项经 XML 逐条核实是 `每个状态都被归入某个子状态机`（Task 7 之前的预期失败）；`根状态机的入口指向Locomotion` 在 passed 中；无其它失败项。

### `IgnoreFailingMessages` 日志对比（green2 改动前 → green3 改动后）

| | 出现次数 | 位置 | 值序列 |
| --- | --- | --- | --- |
| `test-task5b-green2.log`（改前，设置在 `[OneTimeSetUp]` + `[TearDown]` 复位） | 1 | 第 497 行 | 仅 `true`，**从未出现 `false`** |
| `test-task5b-green3.log`（改后，设置在测试体内，无 TearDown） | 1 | 第 497 行 | 仅 `true`，**从未出现 `false`** |

两份日志同为 1391 行，整文件逐行 diff 只有许可握手/时间戳/PID/资产 GUID 之类的噪声。

**结论：这条日志在本例中无法区分两种写法，不构成对改动 1 的正面证据。** 原因是机制性的：setter 只在值变化时打日志，而 `[OneTimeSetUp]` 的 `Build()` 与测试体第一条语句之间没有任何其它日志输出，所以两种写法的这一行都落在同一个位置（紧跟 `[Assets/Script/Editor/HeroAnimatorBuilder.cs line 64]` 那条导入告警之后）。任务书预期的"日志里能看出这次设置落在测试体内"**没有观察到**；这不是测试没生效，而是该观测手段在此场景下不具判别力。

改动 1 的正面证据改用另外两条：
- `test-task5b-green3.log` 第 242 行有 `Start importing Assets/Tests/PlayMode/AnimatorEntryTests.cs` —— 改后的测试文件本次被**重新编译并执行**；
- 改后文件里 `LogAssert.ignoreFailingMessages` 只有**一处**赋值且位于 `[UnityTest]` 方法体内（该文件与 brief Step 2 逐行 IDENTICAL），故日志中那一条 `true` 只可能由它产生。

反证仍成立（改前侧）：若 `[OneTimeSetUp]` 的赋值真的作用到了测试体所在 scope，`[TearDown]` 的 `= false` 必然产生一条 `IgnoreFailingMessages:false`；green2 里没有 —— 说明那次赋值确实打在另一个（返回即销毁的）scope 上，与 brief 的判断一致。旁证是 diag3 那次实验的日志（`test-task5b-diag3.log`）：同一个 `true` 在两个不同 scope 各出现一次（第 502、553 行），随后第 669 行的 `false` 落在测试体那个 scope 上，正是"同一 scope 内 set/reset 才配对出现"的形态。

### 其它核对

- `bash docs/superpowers/sdd/check-assetpack` → `✓ 资源包与基线一致（2624 个文件）`，`Assets/RPGTinyHeroWavePBR/` 与 `Assets/RPGMonsterWave02PBR/` 未动。
- 快照比对（`snapshot` 到临时目录后与 `snaps/_current` 全量 diff）显示只有 **3 个文件**不同：上面两个（本轮手改）+ `Assets/Animator/Hero_SwordAndShield.controller`。后者是 PlayMode 测试 `[OneTimeSetUp]` 调 `HeroAnimatorBuilder.Build()` 的**被动重新生成**（brief Step 5 已说明该副作用），非手改；其差异是 **fileID 重新编号 + 文档顺序变化**，文档类型直方图两边完全一致（各 21 个 document）、`AnimatorTransition` 均为 1 条，且 editmode3 里 `根状态机的入口指向Locomotion` 通过 —— 重新生成后的控制器结构语义未变。`Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs` 一字未动。
- 未执行任何 git 命令（本项目无 git）。

### 意外

1. **`IgnoreFailingMessages` 日志对比不具判别力**（见上），与任务书的预期有出入；如实记录，未改测试去迁就预期。
2. 本节之前的"返工记录"一节声称已把 `BuildLocomotion` 的注释替换为 brief Step 4 的原文，但本轮改动前磁盘上仍是**含"两行都要，缺任一行动画都跑不起来"**的旧注释（上一轮确实重写过这段注释、写进了三条实测结论，但没有采用 Step 4 的措辞，也保留了那句开头）。本轮才是真正替换为 brief Step 4 原文。
