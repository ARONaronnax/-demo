# Task 4 报告：生成器骨架 —— 参数与六个空子状态机

**状态：DONE**（唯一的测试失败是 brief 明确预期的失败）

Unity 2022.3.62f3c1 / Built-in RP / 旧版 Input Manager / 无 git 操作。

---

## 1. 实现了什么

| 文件 | 内容 |
|---|---|
| `Assets/Script/Editor/HeroAnimatorBuilder.cs`（新建） | `Demo.EditorTools.HeroAnimatorBuilder`：`Build() -> AnimatorController`、`BuildFromMenu()`（`[MenuItem("Tools/角色/生成主角状态机")]`）、常量 `ComboLinkExitTime = 0.55f`、`ActionReturnExitTime = 0.90f`、`OutputPath = AnimatorParams.ControllerPath`；私有 `DeleteExistingAsset` / `EnsureFolder` / `AddParameters` / `CreateGroupStateMachines` |
| `Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs`（新建） | brief Step 1 的 7 个断言，逐字照抄 |

两个文件的代码均**逐字**采用 brief Step 1 / Step 3 的内容，未做任何增删。没有引入 brief 未列出的参数、状态或过渡。

新增文件均已由 Unity 导入并生成 `.meta`。

Step 6（git 提交）按全局约束**整步跳过**。

---

## 2. API 可用性结论（本任务的核心目的）

**全部 API 在 2022.3.62f3c1 上存在且行为与 brief 假设一致。没有发现任何 API 缺口，无需替换实现。**

| API | 结果 |
|---|---|
| `AnimatorController.CreateAnimatorControllerAtPath(string)` | 可用。返回含 1 个名为 `Base Layer` 的 layer 的控制器 |
| `AnimatorController.AddParameter(string, AnimatorControllerParameterType)` | 可用。Float/Bool/Int/Trigger 四种类型均正确落盘（序列化为 `m_Type` 1 / 4 / 3 / 9） |
| `AnimatorStateMachine.AddStateMachine(string, Vector3)` | 可用，是本任务的关键验证点。返回的子 `AnimatorStateMachine` 非空，位置按传入值落盘 |
| `AnimatorStateMachine.name` 赋值 | 可用，重命名生效 |
| `AnimatorLayer.name` 赋值 | 可用 |
| `AssetDatabase.LoadAssetAtPath<AnimatorController>` / `DeleteAsset` / `SaveAssets` / `IsValidFolder` / `CreateFolder` | 均可用 |

补充观察（供后续任务参考）：

- 子状态机在 `.controller` YAML 里以独立的 `--- !u!1107` 文档序列化，父状态机通过 `m_ChildStateMachines[].m_StateMachine: {fileID: ...}` 引用。**根状态机的 `m_Name` 会被写成 `Base Layer`**（代码里显式赋值），与 layer 名一致。
- `AddStateMachine` 返回的对象在 YAML 中的 fileID 是负数（-140053445289074356 等），与 `AnimatorStateMachine` 的实例 ID 语义有关，不影响使用。

---

## 3. 生成资源的程序化结构（Step 5 的代偿验证）

**Step 5 是 GUI 步骤，本任务无法执行**（Unity 全程关闭，且按要求不自行调用 `Unity.exe`）。代偿方式：直接解析 `Build()` 落盘的资产文件。

命令：

```bash
cd "E:/unity/求职demo"
python - <<'EOF'
import re, collections
txt=open('Assets/Animator/Hero_SwordAndShield.controller',encoding='utf-8').read()
blocks=re.split(r'^--- ', txt, flags=re.M)[1:]
objs=[(int(re.match(r'!u!(\d+)',b).group(1)), b) for b in blocks]
ac=[b for c,b in objs if c==91][0]
print(re.findall(r'^  - m_Name: (.+)$', ac, flags=re.M))
EOF
```

实测结果（`Assets/Animator/Hero_SwordAndShield.controller`，8545 字节）：

| 项 | 实际值 |
|---|---|
| 序列化文档总数 | 8 = 1 个 `AnimatorController` + 7 个 `AnimatorStateMachine`（1 根 + 6 组） |
| 控制器名 | `Hero_SwordAndShield` |
| layer 数 | **1**，名为 `Base Layer`（= `AnimatorParams.LayerName`） |
| **参数数** | **23**，无重名 |
| **子状态机数** | **6**：`Locomotion` / `Combat` / `Movement` / `Reaction` / `Special` / `Death`，顺序与 `AnimatorParams.Groups.All` 一致 |
| 子状态机位置 | x = 300 / 700 / 1100 / 1500 / 1900 / 2300（= `GroupSpacing.x` 400，与 brief 吻合） |
| 每个子状态机的 `m_ChildStates` | 全部为 `[]`（空，符合本任务范围） |
| `AnimatorState`（!u!1102）对象数 | **0**（状态留给 Task 5–7） |

参数清单与类型：

```
Float(1)   Speed, MoveDirX, MoveDirZ
Bool(4)    IsLockedOn, IsBattleStance, IsGrounded, IsDefending, IsDead
Int(3)     HitIndex
Trigger(9) LightAttack, HeavyAttack, Roll, Jump, Hit, DefendHit, Dizzy, Die, GetUp,
           Victory, Dance, LevelUp, Challenging, SenseSomething
```

**视觉确认（Animator 窗口里看到 6 个节点 + Parameters 面板）顺延到 Task 16 的人工验收。**

---

## 4. TDD 证据

### RED（Step 2）

命令：`"E:/unity/求职demo/docs/superpowers/sdd/run-tests" task4`（timeout 600000）

输出：

```
Aborting batchmode due to failure:
Scripts have compiler errors.

✗ 测试没有产生结果文件。日志：docs/superpowers/sdd/test-task4.log
EXIT=2
```

日志中唯一的编译错误（3 次重复，同一条）：

```
Assets\Tests\EditMode\HeroAnimatorBuilderTests.cs(25,21):
  error CS0103: The name 'HeroAnimatorBuilder' does not exist in the current context
```

**与 brief 预期完全一致**：exit 2 + `error CS...`，原因是 `HeroAnimatorBuilder` 尚未实现。

### GREEN-with-expected-failure（Step 4）

命令：`"E:/unity/求职demo/docs/superpowers/sdd/run-tests" task4`（timeout 600000）

输出：

```
total=20  passed=19  failed=1  result=Failed(Child)
结果文件：E:/unity/求职demo/docs/superpowers/sdd/results-task4.xml
EXIT=1
```

**exit 1 + `failed=1`，失败者正是且仅是 `HeroAnimatorBuilderTests.每个状态都被归入某个子状态机`**：

```
状态 Idle_Normal 没有被放进任何子状态机
Expected: True
But was:  False
  at Demo.Tests.HeroAnimatorBuilderTests.每个状态都被归入某个子状态机 ()
     [0x00072] in Assets\Tests\EditMode\HeroAnimatorBuilderTests.cs:96
```

7 个新测试的结果与 brief Step 4 的 `Expected:` 逐条吻合：

| 测试 | brief 预期 | 实测 |
|---|---|---|
| `生成了控制器资源` | PASS | PASS |
| `参数齐全且类型正确` | PASS | PASS |
| `六个子状态机全部存在` | PASS | PASS |
| `每个状态都被归入某个子状态机` | **FAIL** | **FAIL** ✅ 预期 |
| `根状态机上没有游离状态` | PASS | PASS |
| `没有重复状态名` | PASS | PASS |
| `状态名已修正拼写错误` | PASS | PASS |

原有 13 个测试（Task 1–3）全部仍然 PASS，无回归。**该断言未被削弱、删除、注释或跳过，也未添加任何占位状态来让它变绿。**

### 幂等性验证（额外）

由于 `run-tests` 的 `OneTimeSetUp` 每次都会调用 `Build()`，而第二次运行是在**第一次运行已经落盘资产之后**进行的，因此**第二次运行本身就是一次真实的幂等性检验**（第二个标签避免覆盖 Step 4 的证据）：

命令：`"E:/unity/求职demo/docs/superpowers/sdd/run-tests" task4-idem`
结果：`total=20  passed=19  failed=1`，失败者仍是同一个预期断言。

两次 `Build()` 之后重新解析资产：仍然 **1 个控制器 / 1 个 layer / 23 个参数（零重复）/ 6 个子状态机（零重复）/ 0 个游离状态**。幂等性成立。

---

## 5. 变更文件

```
新建  Assets/Script/Editor/HeroAnimatorBuilder.cs          (+ Unity 生成的 .meta)
新建  Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs    (+ Unity 生成的 .meta)
新建  Assets/Animator/Hero_SwordAndShield.controller       (Build() 的产物，+ .meta)
新建  docs/superpowers/sdd/run-tests 产生的 task4 系列日志与结果 XML
未改动  Assets/RPGTinyHeroWavePBR/、Assets/RPGMonsterWave02PBR/
```

资源包完整性：`docs/superpowers/sdd/check-assetpack` → `✓ 资源包与基线一致（2624 个文件）`。

---

## 6. 自查结果

- **完整性**：brief 列出的每个成员都在，签名一致；两个退出时间常量确为 `0.55f` / `0.90f`。
- **命名硬编码**：对两个新文件 grep 全部 `AnimatorParams` 常量字符串值 → **零命中**。新文件里仅有的字符串字面量是：`"Tools/角色/生成主角状态机"`（`MenuItem` 特性要求字面量）、`"Assets/Animator"`（目录路径，brief 原文）、以及一条调试日志——均非 animator 名称。
- **纪律（YAGNI）**：没有多加任何状态、参数或过渡；`AddParameters` 与 `CreateGroupStateMachines` 与 brief 逐字一致。
- **控制台噪声**：两次运行的日志中 `error CS` / `warning CS` 计数均为 **0**；XML 中也无异常或 `NullReferenceException`。
- **测试有效性**：断言直接读生成后的 `AnimatorController` 对象（层名、参数表与类型、子状态机集合、状态归属），是对真实产物的行为验证，不是对实现细节的复述。
- **未使用引用**：`HeroAnimatorBuilder.cs` 的 5 个 `using` 全部被用到（`System.IO`→`Path`，`UnityEditor`→`AssetDatabase`/`MenuItem`/`Selection`，`UnityEditor.Animations`→`AnimatorController`/`AnimatorStateMachine`/`AnimatorControllerParameterType`，`UnityEngine`→`Vector3`/`Debug`，`Demo`→`AnimatorParams`），无冗余。

## 7. 问题与关注点

1. **brief 文案中的参数个数有误（仅文档，非代码）**：Step 5 与 Step 6 的 commit message 都写「22 个参数」，但 `AnimatorParams.AllParameters` 实际有 **23** 个，brief 的 `AddParameters` 代码也确实添加了 **23** 个，落盘结果就是 23 个。测试断言的是「`AllParameters` 里的每个名字都存在」而非固定条数，因此**不影响任何断言**，代码按 brief 逐字实现、未改。仅提醒：Task 16 人工验收时请以 **23** 为准，不要照 Step 5 的「22」去找。
2. **Step 5 未能执行**（GUI 步骤）。已用第 3 节的 YAML 解析代偿；Animator 窗口的视觉确认顺延至 Task 16。
3. **`AnimationClipLocator` 本任务未使用**。brief 的 Interfaces 把它列为 Consumes，但 Step 3 的代码并未引用它。按 brief「Task 4 may not need it yet」处理，未强加依赖（后续任务会用到）。
