# Task 5 报告：Locomotion —— 三个混合树与组内转换

**Status: DONE**

最终测试：`total=29 passed=28 failed=1`，**exit 1**。唯一失败项是预期的
`每个状态都被归入某个子状态机`（其余四组未建），现点名的状态是 **`Attack01`** ——
即第一个未建组（Combat）的首个状态；Locomotion 的 `Idle_Normal` 已可被找到，不再是它。

---

## 1. 本次落地范围

三步：

1. **修复简报缺陷 A**（`blendParameterX` 不存在 → `blendParameter`），由协调方在 `task-5-brief.md`
   上游改好，我按重新生成的简报执行。
2. **实现简报缺陷 B 的方案 A**：给 `AnimationClipLocator` 加"架势根目录回退"（已获用户/协调方授权）。
3. **按简报 Step 1、3、4 实现 Locomotion 组**（Step 6 提交按要求跳过——本项目不使用 git）。

---

## 2. 剪辑解析证据（本任务核心风险点）

### 2.1 磁盘核对

简报里 8 个剪辑名逐个核对 `InPlace/` 与架势根目录：

```
$ cd "E:/unity/求职demo/Assets/RPGTinyHeroWavePBR/Animation/SwordAndShield"
$ for n in Idle_Normal_SwordAndShield Idle_Battle_SwordAndShiled ... ; do
    ip=MISSING; rt=MISSING
    [ -f "InPlace/$n.fbx" ] && ip=ok
    [ -f "$n.fbx" ] && rt=ok
    printf "%-42s InPlace=%-8s stanceRoot=%s\n" "$n" "$ip" "$rt"; done

Idle_Normal_SwordAndShield      InPlace=MISSING  stanceRoot=ok      ← 走回退
Idle_Battle_SwordAndShiled      InPlace=MISSING  stanceRoot=ok      ← 走回退
MoveFWD_Normal_InPlace_SwordAndShield    InPlace=ok  stanceRoot=MISSING
MoveFWD_Battle_InPlace_SwordAndShield    InPlace=ok  stanceRoot=MISSING
SprintFWD_Battle_InPlace_SwordAndShield  InPlace=ok  stanceRoot=MISSING
MoveBWD_Battle_InPlace_SwordAndShield    InPlace=ok  stanceRoot=MISSING
MoveLFT_Battle_InPlace_SwordAndShield    InPlace=ok  stanceRoot=MISSING
MoveRGT_Battle_InPlace_SwordAndShield    InPlace=ok  stanceRoot=MISSING
```

另用 `find` 确认：资源包 7 个架势的 Idle 剪辑**全部**只在架势根目录，
`InPlace/` 与 `RootMotion/` 下**一个都没有**（`ls InPlace/ | grep -i idle` → NONE）。

`Shiled` 拼写保持资源原名未动（`Idle_Battle_SwordAndShiled`）。

### 2.2 运行时证据：没有任何剪辑加载失败

构建器在剪辑为 `null` 时会打 `LogError("有剪辑加载失败")`。全量日志：

```
$ grep -c "有剪辑加载失败" docs/superpowers/sdd/test-task5.log
0
```

**0 条**——简报加载的 8 个剪辑全部解析成功，包括两个走回退的待机。
（若回退没生效，锁定混合树会拿不到中心待机，`锁定混合树_含中心待机与四个方向` 必然红。）

### 2.3 生成产物结构（直接读 controller YAML）

```
blend trees (name, children): [('Locomotion_Free_Battle', 3),
                              ('Locomotion_Free_Normal', 2),
                              ('Locomotion_Locked', 5)]
count trees      = 3
transition 对象数 = 4
Locomotion 子状态 = Idle_Normal, Idle_Battle, Tree_Free_Normal,
                   Tree_Free_Battle, Tree_Locked        （正好 5 个，无重复）
Locomotion defaultState = Tree_Free_Normal
```

阈值 / 坐标 / 条件逐项核对：

| 混合树 | 类型 | 参数 | 子节点 |
| --- | --- | --- | --- |
| `Locomotion_Free_Normal` | Simple1D | `Speed` | 阈值 0 / 0.5 |
| `Locomotion_Free_Battle` | Simple1D | `Speed` | 阈值 0 / 0.5 / 1 |
| `Locomotion_Locked` | SimpleDirectional2D | `MoveDirX`, `MoveDirZ` | 坐标 (0,0) (0,1) (0,-1) (-1,0) (1,0) |

| 转换 | 条件 | exitTime | hasExitTime | duration |
| --- | --- | --- | --- | --- |
| FreeNormal → FreeBattle | `IsBattleStance` **If** | 1 | 1 | 0.15 (fixed) |
| FreeBattle → FreeNormal | `IsBattleStance` **IfNot** | 1 | 1 | 0.15 (fixed) |
| FreeBattle → Locked | `IsLockedOn` **If** | 1 | 1 | 0.15 (fixed) |
| Locked → FreeBattle | `IsLockedOn` **IfNot** | 1 | 1 | 0.15 (fixed) |

全部 4 条转换的条件都只引用已注册参数（`m_ConditionEvent` 只有 `IsBattleStance` / `IsLockedOn`）。

---

## 3. 两次缺陷的上游更正

### 缺陷 A —— `BlendTree.blendParameterX` 不存在（已由上游修正）

初版简报在**测试断言**与 `CreateDirectional2DTree` 两处都写了 `blendParameterX`。
本版本 Unity 2022.3.62f3c1 的 `UnityEditor.Animations.BlendTree` 只有
`blendParameter`（1D，或 2D 的 X 轴）与 `blendParameterY`，没有 `blendParameterX`。
初版 Step 1 一跑即 **exit 2 / CS1061**，且 Step 3 消不掉它（Step 3 不改测试），
简报内部自相矛盾——当时按升级约定停下上报。

协调方复核后，把简报的测试断言与 `CreateDirectional2DTree` 都改为 `blendParameter`，
并重新生成了 `task-5-brief.md`；本实现完全按新简报执行，未再自行改动 API。

> 说明：`自由姿态混合树_是一维且用Speed参数` 断言 `blendParameter == Speed`，
> `锁定混合树_是二维且用方向参数` 断言 `blendParameter == MoveDirX`。
> 同一属性同时服务 1D 与 2D-X，两条断言因此在同一份产物上并存成立。

### 缺陷 B —— 两个待机剪辑不在 `InPlace/`（已获授权，按方案 A 修复）

初版简报用 `LoadInPlace` 加载两个待机，但 `InPlace/` 下没有任何 Idle 剪辑；
`LoadClip` 对不存在路径返回 `null`，构建器空值守卫会跳过它 —— 锁定混合树只会有
**4** 个子节点，简报自己的 `锁定混合树_含中心待机与四个方向`（断言 5）必然失败。

协调方独立复核并确认这是**系统性**问题：资源包三种布局并存，位移类只在 `InPlace/`、
动作类（Idle/Defend/DefendHit/Dizzy/GetHit/GetUp/Die）只在架势根、连招类两处各有分工；
且 19 个引用名**没有任何一个同时存在于两处**，故回退不会产生歧义。
用户批准方案 A，`AnimationClipLocator.cs` 纳入本任务授权范围。

---

## 4. 改动清单

### 4.1 `Assets/Script/Editor/AnimationClipLocator.cs`

- 新增 `StanceRootPath(string)`，复用既有 `HeroAnimationRoot` 常量。
- `LoadInPlace` 改为先 `InPlace/` 再回退架势根，并补 `///` 说明三种布局与"名字互斥"这一前提。
- **`LoadRootMotion` 未加同样的回退**（按要求保持最小改动，无调用方需要）。

```csharp
public static AnimationClip LoadInPlace(string clipName) =>
    LoadClip(InPlacePath(clipName)) ?? LoadClip(StanceRootPath(clipName));
```

### 4.2 `Assets/Script/Editor/HeroAnimatorBuilder.cs`

- `Build()` 内 `CreateGroupStateMachines(root)` 之后追加 `GetGroups` + `BuildLocomotion` 调用。
- 新增 `GetGroups`、`BuildLocomotion`、`Create1DTree`、`CreateDirectional2DTree`、`AddTransition`。
- 除 `BuildLocomotion` 加了一段 `///` 说明（沿用本文件既有"中文注释 + `///` 摘要"风格）外，
  其余代码与简报逐字一致。

### 4.3 `Assets/Tests/EditMode/HeroAnimatorBuilderTests.cs`

追加简报 Step 1 的 6 个测试与 `Group` / `FindTree` 辅助方法，逐字照抄（其中
`blendParameter` 两处为上游已修正版本）。

### 4.4 `Assets/Tests/EditMode/AnimationClipLocatorTests.cs`

追加 **3** 个测试（按协调方要求的 2 条 + 1 条路径辅助方法的同型测试）：

| 测试 | 作用 |
| --- | --- |
| `StanceRoot路径_指向架势根目录` | 新公开辅助方法的路径契约；与既有 `InPlace路径_*` / `RootMotion路径_*` 同型 |
| `加载只在架势根目录的动作剪辑_回退后返回非空且名称匹配` | **回退生效**：先断言 `InPlace/` 那份确实不存在（否则测不到回退），再断言 `LoadInPlace` 取到且 `clip.name` 匹配 |
| `不存在的剪辑_回退也不会误命中` | **防吞拼写错误**：`LoadInPlace("Idle_Battle_SwordAndShield")`（正确拼写）仍须为 `null`，证明回退只按精确文件名命中 |

### 4.5 生成产物

`Assets/Animator/Hero_SwordAndShield.controller`（由 `Build()` 幂等重建）。

---

## 5. TDD 证据

### Step 2 RED

初版简报（含 `blendParameterX`）：

```
$ "E:/unity/求职demo/docs/superpowers/sdd/run-tests" task5
Aborting batchmode due to failure:
Scripts have compiler errors.
✗ 测试没有产生结果文件。
EXIT=2
```

上游修正 A 之后重跑 RED（构建器尚未实现，这是干净的 RED）：

```
$ "E:/unity/求职demo/docs/superpowers/sdd/run-tests" task5
total=29  passed=22  failed=7  result=Failed(Child)
EXIT=1
```

7 个失败 = 6 个新增 Locomotion 测试（未实现，红得其所）+ 1 个既有
`每个状态都被归入某个子状态机`；3 个新增 locator 测试**当场通过**（回退已生效）：

```
Locomotion组_含三个混合树
Locomotion组_含两个待机状态
待机与锁定切换由IsLockedOn驱动
每个状态都被归入某个子状态机
自由姿态混合树_是一维且用Speed参数
锁定混合树_含中心待机与四个方向
锁定混合树_是二维且用方向参数
```

### Step 5 最终运行

```
$ cd "E:/unity/求职demo" && "E:/unity/求职demo/docs/superpowers/sdd/run-tests" task5
total=29  passed=28  failed=1  result=Failed(Child)
结果文件：E:/unity/求职demo/docs/superpowers/sdd/results-task5.xml
EXIT=1
```

唯一失败项与报文：

```
NAME: 每个状态都被归入某个子状态机
MSG:  状态 Attack01 没有被放进任何子状态机
      Expected: True
      But was:  False
```

失败者只有 Combat 组首个状态 —— 即"仅失败于尚未构建的组"，符合预期；
`Idle_Normal` 已被 Locomotion 收拢，不再是失败点。**其余 28 项全绿，无第二个失败。**

> 该测试未被削弱/删除/跳过/预填；动画参数、状态、组、混合树名全部来自 `Demo.AnimatorParams`。

---

## 6. 自查

- **完整性**：3 个混合树阈值/坐标/类型/参数逐项对上（见 §2.3）；4 条组内转换条件、
  `exitTime`、`hasExitTime`、`duration`、`hasFixedDuration` 逐项对上；`defaultState` 为 `Tree_Free_Normal`。
- **纪律性（YAGNI）**：Locomotion 组恰好 5 个状态、4 条转换、3 个混合树，未多建任何
  状态/参数/转换/混合树，未触碰其余五组。
- **幂等性**：`Build()` 仍是"先删资源再重建"。跨两次运行的产物**结构指纹完全一致**
  （3 棵混合树 / 子节点 2,3,5 / 4 条转换），未出现翻倍或重复对象。
  注：两次运行的 `.controller` **md5 不同**，原因是 Unity 每次构建会给子资源分配新的
  fileID；字节级比对不是幂等的正确判据，结构比对才是（Task 8 有专门的幂等测试）。
- **硬编码检查**：新增构建器代码里唯一的字符串字面量是**资源剪辑文件名**
  （`Idle_Normal_SwordAndShield`、`Idle_Battle_SwordAndShiled`），不属于"参数/状态/组/混合树名"
  硬编码范畴（与既有约定一致）；所有 Animator 名称均取自 `Demo.AnimatorParams`。
- **`m_ApplyRootMotion`**：该字段位于 **prefab 的 Animator 组件**上，不在 controller 资源里
  （本任务只生成 controller，且 `Assets/Prefabs/` 尚未创建），故本次改动**不可能**把它置 false。
- **资源包只读**：`Assets/RPGTinyHeroWavePBR/`、`Assets/RPGMonsterWave02PBR/` 未写入任何文件。
- **未执行任何 git 命令**（Step 6 按要求跳过）。

---

## 7. 遗留与提示

1. **回退的副作用面**：`LoadInPlace` 现在是"先 InPlace/ 再架势根"。Task 6/7 里 15 个
   只在架势根的动作类剪辑（Attack01..04_SwordAndShiled、Defend*、GetHit01/02、Dizzy、
   Die01/Die01_Stay/Die02、GetUp）因此可直接解析，无需再各自打补丁。
   前提是"同名不同处"永不发生——本次已核对 19 个名字无重叠；后续若新增剪辑名，
   需继续保持这个前提。
2. `BuildLocomotion` 保留了简报签名的 `root` 形参但未使用（简报原样），
   仅为后续组可能需要遍历根级对象预留；如协调方希望清理可一并去掉。
