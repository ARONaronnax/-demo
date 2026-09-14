# 子项目① 主角系统 —— SDD 进度台账

计划：`docs/superpowers/plans/2026-09-12-player-controller.md`
设计：`docs/superpowers/specs/2026-09-12-player-controller-design.md`

**本项目不使用 git。** 因此：
- 不做任何 `git add` / `git commit`，计划里各任务末尾的"提交"步骤一律跳过
- 审查包用「快照 + diff」代替 `git diff`：见 `docs/superpowers/sdd/snapshot` 与 `docs/superpowers/sdd/review-package`
- 台账记录的是**快照标签**，不是 commit 区间

## 验证方式

EditMode 测试走命令行 batchmode（Unity 必须处于关闭状态）：

```bash
"/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
  -batchmode -nographics -projectPath "E:/unity/求职demo" \
  -runTests -testPlatform EditMode \
  -testResults "E:/unity/求职demo/docs/superpowers/sdd/results.xml" \
  -logFile "E:/unity/求职demo/docs/superpowers/sdd/test.log"
```

退出码：`0` = 全通过，`2` = 有失败，`1` = 运行错误。

### 派发子代理时必须交代的三件事

1. **不用 git** —— 跳过各任务末尾的"提交"步骤，不执行任何 `git` 命令。
2. **测试走 `docs/superpowers/sdd/run-tests <标签>`**，不要自己调 `Unity.exe`。
   计划书里写的是「Unity Test Runner → Run All」，那是 GUI 操作，子代理点不了。
3. **调用 `run-tests` 要显式传长超时**（Bash 工具 `timeout: 600000`）。
   单次运行 2-3 分钟，默认 2 分钟超时会把它挪到后台。
4. **所有 GUI / 目视步骤一律跳过**，改为程序化等价物并记入报告；真正只能靠眼睛的部分
   统一推迟到 Task 16 由用户验收。已定位的此类步骤：
   - 各简报里的 `Run: Unity Test Runner → EditMode → Run All`（Task 4/5/6/7/8/10/11/12/13）
     —— 只是"怎么跑测试"的说明，按第 2 条改用 `run-tests`。
   - **Task 4 Step 5「在 Unity 里目视确认生成的资源」** —— 改为把生成资产的
     程序化结构（参数数、子状态机数、状态数，从 `AnimatorController` API 读出）写进报告。
   - **Task 15 Step 3「目视检查场景层级」** —— 同上，改为程序化核对层级；
     手感与观感留给 Task 16（Task 15 简报第 3 行自己也写明"无法自动化验证"）。
   - **Task 16 整任务** —— 必须由用户重新打开 Unity 手动验收，不是子代理能做的。

### 工具链已知坑（都已处理）

- **Unity 关闭阶段会挂起**：本工程跑完测试后进程不退出，一直占着 `Temp/UnityLockfile`，
  导致下一次运行报「另一个 Unity 实例已打开了此项目」。`run-tests` 以结果 XML 为完成信号，
  之后强制收尾并清理残留锁（只在确认无 Unity 进程时才删锁）。
- **收尾逻辑曾整体失效（Task 3 暴露，已修）**：两个独立缺陷叠在一起 ——
  1. `$child` 是 Git Bash 的 **MSYS PID，不等于 Windows PID**，`taskkill //PID $child` 打不中目标；
  2. 兜底用的 `wmic` **在新版 Win11 上已被移除**，命令不存在，而错误又被 `2>/dev/null` 吞掉，
     于是兜底从未生效。
  两者相加 = 每次运行都留下一个占着锁的 Unity 进程，靠子代理手动 `taskkill` 才没爆掉。
  已改为 PowerShell `Get-CimInstance Win32_Process` 按命令行匹配 `-batchmode` 清理，
  并且**清不掉就明确报警**，不再静默。
  **验证**：修后以 `run-tests task3verify` 重跑，13/13 通过、退出码 0、
  收尾后 Unity 进程数 = 0、锁文件已清（用独立标签跑，避免轮转挤掉 Task 3 的 RED 证据）。
- **冷启动首次运行会明显更久**：`Library` 为空时（例如资源包刚导入）首轮超过 10 分钟，
  脚本自身预算是 20 分钟，够用但不要按 2-3 分钟估。
- **编译失败时 Unity 提前退出且不产结果 XML**：`run-tests` 会检测子进程消失并立即返回退出码 2，
  不再空等满 20 分钟。
- **项目根目录不能放未知的点开头目录**：会触发 Unity 的
  "`<name>` is not a valid directory name" 警告。SDD 工作区因此放在 `docs/superpowers/sdd/`。
- **一次只能有一个 batchmode 实例**：执行期间用户不能打开 Unity。
- **`run-tests` 曾会冲掉 TDD 证据**：每轮启动即 `rm` 日志与结果，RED 的证据会被随后的
  GREEN 覆盖。已改为把上一轮的日志/结果挪成 `.prev` 保留留档。
- **审查包的中文路径转义**：工程路径含中文，直接把绝对路径交给 `diff` 会得到
  `$'\346\261\202...'` 这种没法读的输出（`QUOTING_STYLE` 环境变量对 diff 无效）。
  已改为在 `snaps/` 目录内用相对路径跑 diff，彻底绕开。

## 进度

**⏸ 2026-09-12 暂停点（用户要求"先收个尾，没完成的功能先不做"）。**
已完成到 **Task 5b**，即「生成侧」的 Locomotion 部分 + 图层入口修复 + PlayMode 测试基础设施。
**「运行侧」一行都还没写**：Task 6/7（Combat 与其余四组）、Task 8–15 全部未开始，
所以现在**没有可操作的角色** —— 场景、控制器脚本、输入、相机都还不存在。
此刻能手工验证的是「生成出来的状态机本身」（怎么验见下面的「暂停点的可测清单」）。

基线快照：`docs/superpowers/sdd/snaps/baseline`

| 任务 | 状态 | 快照标签 | 备注 |
|---|---|---|---|
| 1 程序集骨架与测试装配 | **完成** | baseline..task1 | 审查通过；2/2 通过 |
| 2 AnimatorParams 共享契约 | **完成** | task2-base..task2 | 审查通过；7/7 通过 |
| 3 AnimationClipLocator | **完成** | task3-base..task3 | 审查通过；13/13 通过 |
| 4 生成器骨架 | **完成** | task4-base..task4 | 审查通过；19/20，唯一失败为预期 |
| 5 Locomotion 三混合树 | **完成** | task5-base..task5 | 审查通过；28/29，唯一失败为预期。1 项 Important（图层入口）转 Task 5b |
| 5b 图层入口 + PlayMode 验证 | **完成** | task5b-base..task5b-final | 审查 Spec ✅ / Approved；PlayMode 1/1，EditMode 29/30（唯一失败为预期） |
| 6 Combat 两条连招链 | 未开始 | | |
| 7 其余四组 | 未开始 | | |
| 8 Any State 与幂等性 | 未开始 | | |
| 9 两个 StateMachineBehaviour | 未开始 | | |
| 10 PlayerInputReader | 未开始 | | |
| 11 PlayerMotor 统一位移管线 | 未开始 | | |
| 12 PlayerAnimatorDriver | 未开始 | | |
| 13 PlayerCameraRig 与锁定 | 未开始 | | |
| 14 PlayerController 门面 | 未开始 | | |
| 15 DemoSceneBuilder | 未开始 | | |
| 16 端到端手动验收 | 未开始 | | |

### 暂停点的可测清单（当前状态能手工验证什么）

先把话说清楚：**现在还没有可操作的角色**。"运行侧"的五个脚本（PlayerInputReader /
PlayerMotor / PlayerAnimatorDriver / PlayerCameraRig / PlayerController）与 Demo 场景
一个都没写，所以没有能走能砍的东西。现在能验的是**生成出来的那份状态机**。

0. 确认没有残留的 batchmode Unity 与锁文件（已核对：0 个 Unity 进程、无 `Temp/UnityLockfile`），
   然后正常打开 Unity。
1. **Test Runner**：`Window > General > Test Runner`。
   - `EditMode` → Run All：30 个用例，29 通过，**唯一失败 `每个状态都被归入某个子状态机` 是预期的**
     （其余四组要到 Task 7 才建）。别把它当成回归。
   - `PlayMode` → Run All：1 个用例，通过。它验证的是"运行时首帧确实停在 `Tree_Free_Normal`"。
2. **看状态机图**：`Assets/Animator/Hero_SwordAndShield.controller` 双击打开 Animator 窗口。
   应当看到 `Entry → Locomotion`，Locomotion 组里两个待机 + 三个混合树状态，
   以及组内转换（Free↔Battle、Battle↔Locked）和参数条件。
3. **手工拨参数**：随便建个空场景 → 空物体加 `Animator` 组件 → 指定上面这份 controller，
   然后在 Animator 窗口的 Parameters 里拨：
   - `IsBattleStance` → 从 `Tree_Free_Normal` 切到 `Tree_Free_Battle`
   - `IsLockedOn`（需先 `IsBattleStance`）→ 切到 `Tree_Locked`
   - `Speed` / `MoveDirX` / `MoveDirZ` → 混合树内部的剪辑之间混合
   - ⚠️ 组内转换是**带退出时间**的（`HasExitTime=1`、`ExitTime=1`、过渡 0.15s），
     所以拨完参数要**等当前剪辑放完一轮**才会切过去，不是立刻切。看不到变化先等一轮再判断。
4. **想看模型动**：把 `Assets/RPGTinyHeroWavePBR/Prefab/ModularCharacters/MC01.prefab`
   拖进场景，改**这个实例**的 Animator 的 Controller 为我们的那份。
   **不要点 Inspector 上的 Apply** —— 那会把改动写回资源包里的预制体，
   直接违反"不得修改 `Assets/RPGTinyHeroWavePBR/`"这条约束。
5. **现在还没反应的参数**：`LightAttack` / `HeavyAttack` / `Roll` / `Jump` / `Hit` / `Die` 等
   全部无效 —— 对应的状态在 Task 6/7 才会建出来，现在那五个组还是空的。

## 执行顺序（**不等于任务号顺序**）

计划的 ASCII 依赖图（第 69-86 行）漏画了一条边，但正文点明了：
`Task 8` 要用 `Task 9` 定义的 `RootMotionTag` / `LockMovementTag`（见计划 1840、1843 行），
而这两个 Tag 又反过来引用 `Task 11` 的 `PlayerMotor`（见计划 2382、2671 行）。
所以实际顺序是：

```
1 → 2 → 3 → 4 → 5 → 5b → 6 → 7 → 11 → 9 → 8 → 10 → 12 → 13 → 14 → 15 → 16
                                    └──────┬──────┘
                                必须先有 PlayerMotor，Tag 才编译得过
```

- `Task 4 → Task 15`：Task 4 必须先于 Task 15
- `Task 11 → Task 9 → Task 8`：**不要把 8 排在 9 前面**，会编译失败
- `Task 16` 是手动验收，必须最后

## 已知并接受的计划内事项

- `每个状态都被归入某个子状态机` 这条断言在 Task 4 引入，但要到 **Task 7** 六个组全部建好才能转绿。
  计划在每一步的 Expected 里都明确标注了它当时应为 FAIL，属于有意为之的 TDD 目标，不是回归。

## 工具修正（任务执行中）

- **`snapshot` 漏拷 `Assets/*.meta`**（审查者 ⚠️ 项）：只递归拷了各目录的内容，
  目录自身的 meta（`Assets/Script.meta` 等）落在 `Assets/` 下，不在任何被拷目录里，
  整轮被漏掉。这些 meta 记录文件夹 GUID，遗漏会让审查包看不出 GUID 变动。已补拷。
- **`Assets/Tests/EditMode/AssemblySmokeTests.cs` 的空洞断言**（审查者 Important 项，
  且是计划书原文要求的）：`Assert.IsNotNull(assembly)` 永远为真，asmdef 装配错了照样过。
  经确认后**计划与实现同步改**为断言程序集名 `Demo.Tests.EditMode`。

## Minor findings 累积（留给最终整分支审查分诊）

### Task 2 审查（全部标注为 plan-mandated，未返工）

- `AnimatorParamsTests.cs:16-20` `参数名_无重复` 只断言唯一性、无长度断言，
  空数组也能通过。建议补 `Assert.AreEqual(23, AnimatorParams.AllParameters.Length)`。
- **无任何测试钉住单个字符串值** —— 5 个测试全在查聚合性质（长度、唯一性、不含某子串）。
  把 `RollFwd = "RollFWD"` 改成 `"RollForward"` 测试依然全绿，但 Task 3 的生成器会静默失配。
  对一个"存在的意义就是那些精确字符串"的类，这是唯一没被守住的契约。
- `AnimatorParams.cs:43,56,67,79,152` 公开可变 `string[]`，消费者可改
  （`AllStates[0] = "x"` 会污染全进程）；且别名 `AllGroups = Groups.All`、
  `AllBlendTrees = BlendTrees.All` 让两个公开入口指向**同一个数组对象**。
  宜用 `IReadOnlyList<string>` 或返回副本。
- 7 处字面量在顶层参数常量与 `States.*` 之间重复（Dizzy / DefendHit / GetUp /
  Challenging / Dance / Victory / LevelUp），可各自漂移。另有遮蔽隐患：
  在 `States` 内部裸写 `Dizzy` 绑到的是 `States.Dizzy` 而非 `AnimatorParams.Dizzy`，
  后续任务必须写全限定名。（参数与状态在 Animator 里是不同命名空间，未必该统一。）
- `AnimatorParamsTests.cs:2` `using Demo;` 在 `namespace Demo.Tests` 内是多余的。

### Task 4 审查（0 Critical / 0 Important，4 项 Minor 全部 plan-mandated，未返工）

- **⚠️ 已出现第二次同类问题：「永久注释里的事实性错误」**（第一次见 Task 3 的「同名剪辑」）。
  `HeroAnimatorBuilderTests.cs:12-16` 的 fixture 注释断言"每个 `[Test]` 都会重新跑 `OneTimeSetUp`"，
  这**与 NUnit 的 `[OneTimeSetUp]` 语义相反**（每 fixture 一次）；还声称生成过程要加载
  "40+ 个 fbx"，而 Task 4 的 `Build()` 根本不碰 fbx。两条都不影响正确性，
  但会误导 Task 5-7 的作者去估算运行成本。简报原文。
- `HeroAnimatorBuilder.cs:49` 目录路径硬编码 `"Assets/Animator"`，而文件路径来自
  `AnimatorParams.ControllerPath` —— 同一路径写了两遍。若 `ControllerPath` 将来移动，
  `EnsureFolder` 会静默建错目录。宜改为从 `OutputPath` 派生。
- `HeroAnimatorBuilder.cs:71-79` `EnsureFolder` 递归对畸形路径（无目录分量）无终止保护，
  当前常量路径不可达，但构造上递归无界。
- `HeroAnimatorBuilderTests.cs:54-71` 无断言钉住参数总数 23：只查
  `AllParameters ⊆ controller.parameters` 并抽查了 23 个里的 14 个类型，
  剩下 9 个（全是 Trigger）与任何**多余**的同名参数都不会被发现。
  重复项会被顺带抓到（`ToDictionary` 对重复键抛异常），但那是 `ArgumentException` 而非有效断言。
  产物已由手工 YAML 解析确认是 23，所以是覆盖缺口不是缺陷。

### Task 3 审查（0 Critical / 0 Important，6 项 Minor 全部 plan-mandated，未返工）

- **`AnimationClipLocator.cs:10` 文档注释事实有误**（值得处理）：注释说资源包每个动作有
  「两套**同名**剪辑」，但实际名字差在中缀 —— `MoveFWD_Battle_InPlace_SwordAndShield` 对
  `MoveFWD_Battle_RM_SwordAndShield`。所以"不能只靠名字查找"这个理由站不住（知道确切名字是能查到的）。
  API 形状仍然正确，但这条错误说法会**永久留在仓库里**被后来者当真。
  这是从简报逐字继承的，不是实现者的错。建议改为"两个目录存的是**并行变体**，名字中缀不同"。
- `AnimationClipLocatorTests.cs:47` 测试名 `同名的InPlace与RootMotion剪辑_是两个不同对象`
  继承同一处不准确。断言本身是对的，只是名字误导。同为简报原文。
- `AnimationClipLocator.cs:33` 公开的 `LoadClip` **未防 `fbxPath` 为 null**：
  `AssetDatabase.LoadAllAssetsAtPath(null)` 可能抛异常。该类"不抛异常"的性质只对内部构造的路径成立。
  今天风险低（两个内部调用方都传构造好的路径），但 `LoadClip` 在 Produces 清单里，外部可达。
- `AnimationClipLocatorTests.cs:18` 与 `:33` RootMotion 侧断言比 InPlace 侧少：
  前者缺 `EndsWith(".fbx")`，后者缺 `clip.name` 校验。简报原文。（审查者已单独核实过
  RootMotion 分支确实走的是精确匹配分支，所以并非"未验证"。）
- `AnimationClipLocator.cs:54` 无精确匹配时回退取 `AssetDatabase` 顺序首个非预览剪辑：
  简报明确规定，且今天**不可达** —— 审查者核对了实现者担心的最坏情况
  `Combo05_InPlaceWithRMHeight_SwordAndShield.fbx`，其内部剪辑名与文件名一致，走的是精确分支。
  若将来真有多 take 的 fbx，这个回退会变成**静默选错剪辑**。
- `test-task3.log` 里的 `[Licensing::Module]` 失败行属环境噪音（授权客户端握手失败），
  与本次改动无关，但也别据此认为整个 runner 是干净的。

### Task 5b 审查（Spec ✅ / Approved；4 项 Minor **当场修完**，未留待最终分诊）

与前面几轮不同，这 4 项里没有"计划就是这么要求的"这一挡：
其中 3 项是**仓库文档/注释里的假陈述**（失效的 LogAssert 安全网、被实测证伪的"两行都要"、
过期的测试名），留到最终分诊就等于把假话长期留在源码里。所以计划与代码都已同步改掉，
细节见「计划修正记录」第 13 条。

### Task 1 审查

- **计划第 206 行**：`AssemblySmokeTests.编辑器程序集_可被测试程序集引用` 里的
  `Assert.IsNotNull(type)` 是多余断言 —— `type` 来自 `typeof(...)`，永不为 null
  （类型不存在时根本编译不过）。真正的断言是紧随其后的 `Assert.AreEqual("Demo.EditorTools", type.Namespace)`。
  不单独修，留待最终审查决定是否清理。
- 已扫描全计划其余 `Assert.IsNotNull` 用法（614/622/637/638/807/1376/1398/1667/1682 行），
  都是检查 `LoadAssetAtPath` / 状态机查找的返回值，属于有效断言，不是同类问题。

## 已核实的工具事实（勿重复怀疑）

- **`asserts="0"` 是正常的**，不代表断言没执行。NUnit 3 移除了 `Assert.Count`，
  Unity Test Framework 的 XML writer 因此始终写 0。task1 与 task2 的 XML 都是 `asserts="0"`，
  而两者的测试都在做真实断言。判断测试是否真的跑起来，看 `total` / `passed` / `failed` 与退出码。
- **`Assets/Script/` 的 asmdef 覆盖范围已验证**：`Demo.Runtime.asmdef` 直接位于 `Assets/Script/`，
  唯一嵌套的是 `Assets/Script/Editor/Demo.Editor.asmdef`（只挖走 Editor 子目录）。
  因此 `Assets/Script/Player/*.cs` 归 `Demo.Runtime`，`Assets/Script/Editor/*.cs` 归 `Demo.Editor`。
- **`DashRht = "DashRHT"` 不是笔误**，已核对源资源：SwordAndShield 全场唯一使用
  `DashRHT_RM_*.fbx` / `DashRHT_InPlace_*.fbx`，其余架势才用 `DashRGT_*`。
  这个不对称继承自资源本身，不是本次引入的。
- **`Shiled` 不是笔误，不得"修正"为 `Shield`。** 资源包自身就是错的：
  磁盘上确实存在 `Animation/SwordAndShield/Idle_Battle_SwordAndShiled.fbx`
  与 `Attack01..04_SwordAndShiled.fbx`。Task 5 简报里的
  `LoadInPlace("Idle_Battle_SwordAndShiled")` 是正确的。把它"改对"会让整个生成器失配。
  Task 2 里已有一条专门守 `Shiled` 的断言（`AnimatorParamsTests.cs:31-38`）。
- **生成的 `.controller` 不能用字节/md5 比较来判断幂等**（Task 5 报出）：
  Unity 每次重新生成会给子资源**重新分配 fileID**，所以两次运行产出的文件 md5 必然不同，
  即使结构完全一致。判断幂等要看**结构指纹**（状态数、树数、参数数、转换数、条件），
  不能看字节。**这会影响后续任务的快照 diff**：测试运行时会重跑 `Build()` 重写该文件，
  于是从 Task 6 起每个审查包里都会出现这个 `.controller` 的 YAML 变更 —— 那是噪音不是回归，
  审查者需按"输出产物"而非"源码改动"来看待它。
- **`m_ApplyRootMotion` 不在 controller 上，在 prefab 的 Animator 组件上**（Task 5 报出）。
  这纠正了 Task 4 审查的一个假设 —— 当时说"Tasks 5-7 会变成真检查点"。
  实际上 Tasks 5-7 都碰不到它，因为 `Assets/Prefabs/` 要到 Task 15 才建。
  真正能验证这条约束的是 Task 15/16。
- **全计划硬编码字面量复扫（2026-09-12，Task 3 执行期间）**：扫过 task-3..16 全部简报，
  排除以 `_SwordAndShield` / `_SwordAndShiled` 结尾的资源剪辑名、以及 `CM_FreeLook` /
  `CM_LockOn` 这类 GameObject 名之后，**无任何遗留的 Animator 状态名或参数名硬编码**。
  开工前那次修正（6 处 `Tree_Free_Battle` 等提为常量）是完整的。

## 计划修正记录（开工前审查，2026-09-12）

1. `AllStates` 元素数与注释不符：注释写 Combat 12（实为 11），总数断言 42（实为 36）→ 补 3 个混合树状态后定为 **39**
2. Task 6 测试 `Combat组_含十二个状态` 期望 12，构建器只建 11 → 改为 **11**
3. Task 13 `正前方但更远_与侧面但更近` 断言与算分公式矛盾（8m 正面 0.76 > 2m 侧面 0.74）→ 正面改 **12m**（0.64 < 0.74）
4. Task 8 `生成器是幂等的` 在 `Build()` 之后访问已销毁的 `_ctrl` → 计数前置并重建后重新指向
5. 三个混合树状态名硬编码 6 处，违反"状态名唯一真相来源"约束 → 提为 `States.TreeFree*` 常量
6. Task 13 的"失败就调权重"提示会诱导迁就错误测试 → 换为手算验算说明
7. **`BlendTree.blendParameterX` 在 2022.3 不存在**（Task 5 实现时撞到，编译失败）。
   已查 Unity 自身 `UnityEditor.xml` 证实：2022.3 的 `BlendTree` 只有
   `blendParameter`（即 X 轴）与 `blendParameterY`。计划第 1108、1264 行两处已改为
   `blendParameter`。这是本项目第一次撞到**真正的 API 不存在**（Task 4 那批 API 全部可用）。
8. **`LoadInPlace` 取不到动作类剪辑**（Task 5 实现时撞到，且是**系统性**缺陷，覆盖 Task 5/6/7）。
   已逐一核对磁盘：资源包布局**三套并存**——
   - 移动类（Move*/Sprint*/Jump*）：`InPlace/` 与 `RootMotion/` 各一份，名字中缀 `_InPlace_` / `_RM_`
   - 动作类（Idle/Defend/DefendHit/Dizzy/GetHit01-02/GetUp/Die01/Die01_Stay/Die02）：
     **只有一份，放在架势根目录**，两个子目录里都没有
   - 连招类：`Combo01_RM_*` 在 `RootMotion/`，`Combo05_InPlaceWithRMHeight_*` 在 `InPlace/`

   而被引用的 19 个名字里**没有任何一个同时存在于两处**，所以回退不会产生歧义。
   经用户裁定采用 **A：`LoadInPlace` 加根目录回退**（新增 `StanceRootPath`，
   `LoadClip(InPlacePath(n)) ?? LoadClip(StanceRootPath(n))`），一处改动解锁三个任务，
   调用点一行不改。`LoadRootMotion` **不加**同样的回退（无任务需要，保持最小改动）。
   实现落在 Task 5 的改动集里，因此 `AnimationClipLocator.cs` 这个 Task 3 已审查过的文件
   会出现在 Task 5 的审查包中，需在 Task 5 审查里一并覆盖。
9. 计划中"资源里同一个动作有 InPlace/ 和 RootMotion/ 两份**同名**剪辑"的说法**事实错误**
   （名字中缀不同），已随修正 9 一并改为准确的三套布局描述，涉及第 567、662 行的类文档注释
   与 Interfaces 清单。这同时也是 Task 3 审查提出的 Minor #1（永久注释里的错误陈述）。

10. 计划 4 处写"22 个参数"（第 7、1037、1044、4001 行），实际 `AnimatorParams.AllParameters`
   有 **23** 个（6+5+7+5）。因无断言钉住这个数字，实现不受影响，但 **Task 16 手动验收
   会照 22 去核对**。已全部改为 23。发现者：Task 4 的实现子代理。
   （与修正 1 的 42→39 同类：都是"聚合计数写在散文里、无人钉住、于是慢慢漂移"。）

11. **图层入口缺失（Task 5 审查的 Important 项）→ 新增 Task 5b**（2026-09-12，用户裁定）。

    缺陷：生成的控制器里，根状态机 `Base Layer` 的 `m_ChildStates: []`、`m_EntryTransitions: []`，
    而 `m_DefaultState` 被 Unity 自动填成 `Idle_Normal` —— 它是 `Locomotion` 子状态机的子节点，
    且 `m_Transitions: []`（零出边）。运行时一进场就卡在这个孤立待机里，
    移动/锁定/战斗架势全部失效。**全部 EditMode 断言照样通过**，因为它们只验证结构存在。

    计划原文从来没有规定图层入口：整个计划里只有 `locomotion.defaultState = stFreeNormal;`（1227 行）
    与 `death.defaultState = die01;`（1820 行）两处 `defaultState`，根状态机的那一个是 Unity
    自己填的。三处 YAML 事实由我逐一核对确认（控制器第 112–148、476 行）。

    用户裁定：**加显式 Entry 转换 + 新建 PlayMode 测试验证**。
    计划改动：(a) `BuildLocomotion` 里追加 `root.AddEntryTransition(locomotion);`，
    **不**同时赋根状态机的 `defaultState`（root 没有直接子状态，任何赋值都只能指向一个
    root 并不拥有的状态，是说不通的语义）；(b) 新增 `Demo.Tests.PlayMode` 程序集与
    `AnimatorEntryTests`，运行时断言首帧状态是 `Tree_Free_Normal`；(c) `HeroAnimatorBuilderTests`
    补一条快速结构断言。

    这是本项目第一个**只有运行时才暴露**的缺陷，也是引入 PlayMode 测试基础设施的原因。
    计划文件结构表、Global Constraints 的程序集清单、依赖图已同步更新。

12. **Task 5b 实施中被实测推翻的两条计划断言（2026-09-12，均已按实测改计划）。**

    a. **`includePlatforms: ["Editor"]` 的测试程序集跑不了 PlayMode。**
       计划原文断言"Editor 程序集在编辑器的 PlayMode 下照样被加载，`-testPlatform PlayMode`
       能找到它们" —— **错**。实测 `total=0`，退出码仍是 `0`，是个"看着全绿其实没跑"的假象。
       原因已从 UTF 源码确认（`com.unity.test-framework@1.1.33/UnityEditor.TestRunner/TestRunner/Utils/`
       `EditorLoadedTestAssemblyProvider.cs:60`）：
       `assemblyType = (flags & EditorOnly) == EditorOnly ? EditMode : PlayMode` ——
       按程序集的 EditorOnly 标志分类，Editor-only 一律归 EditMode。
       改为 `"includePlatforms": []`，与 Unity 自带的 PlayMode 模板一致。

    b. **只加根入口转换不够，`defaultState` 才是图层的启动状态。**
       计划原文（以及我在修正 11 里写的理由）断言"有入口转换时 defaultState 是被绕过的死值"—— **错**。
       PlayMode 探针逐帧实测：只有 `AddEntryTransition` 时首帧仍停在 `Idle_Normal`，与修复前逐帧一致；
       补上 `root.defaultState = stFreeNormal;` 首帧才落到 `Tree_Free_Normal`；
       `defaultState = null` 是空操作（读回旧值）。
       两行都必须写。我在修正 11 里反对赋值的理由（"指向 root 不拥有的状态"）站不住：
       Unity 自动填入的 `Idle_Normal` 本来就是同一种外部指针，而 root 没有任何直接子状态可指。
       计划 Step 4 已改为两行并附实测结论。

    由此新增的教训写进了计划：**PlayMode 的 RED/GREEN 判据必须看 `total`，不能只看退出码。**
    `total=0` + `result="Passed"` + 退出码 0 三者同时成立，却什么都没测。

13. **Task 5b 审查的 4 项 Minor，其中 3 项是"仓库文档里的假陈述"，已改（含计划）。**
    审查结论本身是 Spec ✅ / Approved，无 Critical / Important，但这几条被核实为真问题：

    a. **`LogAssert.ignoreFailingMessages` 写在 `[OneTimeSetUp]` 里等于没写。**
       它是**当前 LogScope 实例**的属性（`LogAssert` 的静态属性转发到 `LogScope.Current`，
       见 `com.unity.test-framework@1.1.33/UnityEngine.TestRunner/Assertions/LogScope/LogScope.cs`），
       每个测试各有 scope 且随测试销毁；`[OneTimeSetUp]` 的 scope 在方法返回时就没，
       设置随之失效，而 `[TearDown]` 的复位打在另一个新 scope 上。
       日志指纹：只出现一次 `IgnoreFailingMessages:true`、从未出现 `false`。
       → 计划与代码同步改为**写在 `[UnityTest]` 方法体第一条**，删掉 `[TearDown]` 复位。
       （连带教训：这条日志**无法**区分两种写法，因为 setter 只在值变化时打日志 ——
       别再拿它当验证手段。）

    b. **生成器注释"两行都要，缺任一行动画都跑不起来"是实测证伪的假陈述。**
       实现者自己的 DIAG3 实验（报告 210–217 行，四种组合各跑一遍）显示：
       `entries=0` + `defaultState=Tree_Free_Normal` **同样正确**。
       即决定启动状态的是 `defaultState`，入口转换不参与启动（保留它是为了让图不悬空）。
       → 计划 Step 4 与源码注释都已改写为准确表述，并附上四条实测组合。

    c. **brief Step 7 里写的测试名是旧名**（`根状态机有指向Locomotion的入口转换`），
       Step 6 的代码里已改名为 `根状态机的入口指向Locomotion` → 计划已同步。
       （Stage 已被 319 行版 brief 覆盖，`results-task5b-editmode3.xml` 里新名在 passed 中。）

    d. `ProjectSettings/SceneTemplateSettings.json` 是 Unity 首次跑测试时自己生成的，
       不是手写交付物 —— 记录在案，避免日后被误认为越界改动。

14. Tech Stack 写的 "Unity Test Framework 1.1.31" 是包**声明**的依赖版本；
    `Packages/packages-lock.json` 实际解析为 **1.1.33**（经 `com.unity.feature.development` 1.0.1）。
    已改为 1.1.33 —— 引用 UTF 源码行号时版本必须对得上。
